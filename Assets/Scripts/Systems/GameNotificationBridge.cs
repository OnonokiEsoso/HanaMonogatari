using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 各ゲームシステムの「プレイヤーに知らせたい出来事」を集約します。
/// 営業中や日送り処理の途中ではポップアップを割り込ませず、通知候補を一旦保留します。
/// 翌日の画面へ切り替わったあと DailyResultUI から FlushPendingNotifications を呼び、
/// その日の開始時に未通知項目をまとめて順番に表示します。
/// ゲームクリア時は通しプレイ記録も1項目ずつ通知パネルへ流します。
/// </summary>
public class GameNotificationBridge : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private NotificationPanelUI notificationPanel;
    [SerializeField] private DevelopmentSystem developmentSystem;
    [SerializeField] private HybridDevelopmentSystem hybridDevelopmentSystem;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private CustomerUI customerUI;
    [SerializeField] private FurnitureSystem furnitureSystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("来客マイルストーン")]
    [Tooltip("累計来客がこの人数を超えた時に通知候補へ追加します。表示は次の日の開始時です。")]
    [SerializeField] private int[] visitorMilestones = { 100, 500, 1000 };
    [SerializeField] private int cumulativeVisitors;
    [SerializeField] private int nextVisitorMilestoneIndex;

    [Header("通しプレイ記録")]
    [Tooltip("営業終了時に加算される通算売上。クリア時のプレイ記録表示に使用します。")]
    [SerializeField] private int cumulativeSales;
    [Tooltip("失敗が確定した交配研究の通算回数。")]
    [SerializeField] private int hybridFailureCount;
    [Tooltip("交配花の作成が完了した通算回数。")]
    [SerializeField] private int hybridProductionCount;

    private readonly Queue<string> deferredMessages = new();

    private int observedSupplierLevel = 1;
    private bool observedClear;
    private bool observedDevelopmentUnlocked;
    private bool initializedShopState;

    private bool clearSummaryPending;
    private bool clearSummaryQueued;
    private string clearDateText = string.Empty;
    private int clearAbsoluteDay;
    private float clearPlayTimeSeconds;

    public int PendingNotificationCount => deferredMessages.Count;
    public bool HasPendingNotifications => deferredMessages.Count > 0;

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialShopState();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (developmentSystem != null)
            developmentSystem.OnJobCompleted += HandleDevelopmentJobCompleted;

        if (hybridDevelopmentSystem != null)
        {
            hybridDevelopmentSystem.OnResearchCompleted += HandleHybridResearchCompleted;
            hybridDevelopmentSystem.OnProductionCompleted += HandleHybridProductionCompleted;
        }

        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;

        if (customerUI != null)
            customerUI.OnBusinessFinished += HandleBusinessFinished;

        CaptureInitialShopState();
    }

    private void OnDisable()
    {
        if (developmentSystem != null)
            developmentSystem.OnJobCompleted -= HandleDevelopmentJobCompleted;

        if (hybridDevelopmentSystem != null)
        {
            hybridDevelopmentSystem.OnResearchCompleted -= HandleHybridResearchCompleted;
            hybridDevelopmentSystem.OnProductionCompleted -= HandleHybridProductionCompleted;
        }

        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;

        if (customerUI != null)
            customerUI.OnBusinessFinished -= HandleBusinessFinished;
    }

    private void HandleDevelopmentJobCompleted(string message)
    {
        QueueNotification(message);

        // 枯ラサンつい完了時は新種開発も同時に解禁されるので、別通知で明示する。
        if (developmentSystem != null && developmentSystem.IsNewSpeciesDevelopmentUnlocked)
        {
            DevelopmentDefinition definition = developmentSystem.GetDefinition(DevelopmentId.KarasanTsui);
            if (definition != null && message != null && message.Contains(definition.displayName, StringComparison.Ordinal))
            {
                QueueNotification(
                    "新種開発が解禁されたよ！",
                    "開発パネルの『交配』から、新しい花を生み出せるようになったよ。");
            }
        }
    }

    private void HandleHybridResearchCompleted(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && message.Contains("無理っぽかった", StringComparison.Ordinal))
        {
            hybridFailureCount++;
            QueueNotification("この組み合わせはできませんでした");
            return;
        }

        QueueNotification(message);

        if (!string.IsNullOrWhiteSpace(message) && message.Contains("成功", StringComparison.Ordinal))
        {
            QueueNotification(
                "交配花を作成できるようになったよ！",
                "開発パネルの『作成』に、完成した新種が追加されたよ。");
        }
    }

    private void HandleHybridProductionCompleted(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && message.Contains("作成が完了", StringComparison.Ordinal))
            hybridProductionCount++;

        QueueNotification(message);
    }

    private void HandleShopStateChanged()
    {
        if (shopManager == null)
            return;

        if (!initializedShopState)
        {
            CaptureInitialShopState();
            return;
        }

        int currentSupplierLevel = shopManager.SupplierLevel;
        if (currentSupplierLevel > observedSupplierLevel)
        {
            QueueNotification(
                $"仕入先Lvが{currentSupplierLevel}になったよ！",
                "仕入れられる商品の幅が広がったよ。仕入れ画面を確認してみよう。");
            observedSupplierLevel = currentSupplierLevel;
        }
        else if (currentSupplierLevel < observedSupplierLevel)
        {
            // デバッグ操作等で戻された場合は観測値だけ同期する。
            observedSupplierLevel = currentSupplierLevel;
        }

        bool developmentUnlocked = shopManager.ShopRating >= DevelopmentSystem.DevelopmentUnlockShopRating;
        if (developmentUnlocked && !observedDevelopmentUnlocked)
        {
            QueueNotification(
                "開発が解禁されたよ！",
                "店評価が2,000に到達したので、ホームの『開発』から新しい商品を研究できるようになったよ。");
        }
        observedDevelopmentUnlocked = developmentUnlocked;

        if (shopManager.HasCleared && !observedClear)
        {
            clearSummaryPending = true;
            clearSummaryQueued = false;
            clearDateText = shopManager.DateDisplayText;
            clearAbsoluteDay = (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
            clearPlayTimeSeconds = Time.realtimeSinceStartup;

            QueueNotification(
                "店評価10,000達成！",
                "街で一番人気のお花屋さんになったよ！ ゲームクリア！\nこのあと今回のプレイ記録を表示します。");
        }
        observedClear = shopManager.HasCleared;
    }

    private void HandleBusinessFinished()
    {
        if (customerUI == null)
            return;

        cumulativeSales += Mathf.Max(0, customerUI.TotalSales);
        cumulativeVisitors += Mathf.Max(0, customerUI.TotalVisitors);
        CheckVisitorMilestones();
    }

    private void CheckVisitorMilestones()
    {
        if (visitorMilestones == null || visitorMilestones.Length == 0)
            return;

        while (nextVisitorMilestoneIndex < visitorMilestones.Length)
        {
            int milestone = Mathf.Max(1, visitorMilestones[nextVisitorMilestoneIndex]);
            if (cumulativeVisitors < milestone)
                break;

            QueueNotification(
                $"累計来客{milestone:N0}人突破！",
                $"これまでに{cumulativeVisitors:N0}人のお客さんが来店してくれたよ。");
            nextVisitorMilestoneIndex++;
        }
    }

    /// <summary>
    /// 翌日の画面が表示されたタイミングで呼びます。
    /// 保留していた通知を NotificationPanelUI へ渡し、ボタンを押すたびに次の通知を表示します。
    /// ゲームクリア済みなら、通常通知の後ろにプレイ記録を1項目ずつ追加します。
    /// パネルがまだ見つからない場合は通知を捨てず、そのまま次回まで保持します。
    /// </summary>
    public void FlushPendingNotifications()
    {
        ResolveReferences();

        if (clearSummaryPending && !clearSummaryQueued)
            QueueClearSummaryNotifications();

        if (deferredMessages.Count == 0)
            return;

        if (notificationPanel == null)
        {
            Debug.LogWarning($"GameNotificationBridge: NotificationPanelUIが見つからないため、{deferredMessages.Count}件の通知を保留します。");
            return;
        }

        while (deferredMessages.Count > 0)
            notificationPanel.ShowMessage(deferredMessages.Dequeue());
    }

    private void QueueClearSummaryNotifications()
    {
        ResolveReferences();

        int developmentCompletedCount = 0;
        int developmentTotalCount = 0;
        if (developmentSystem != null)
        {
            developmentTotalCount = developmentSystem.Definitions?.Count ?? 0;
            developmentCompletedCount = developmentSystem.Definitions?.Count(definition =>
                definition != null && developmentSystem.IsCompleted(definition.id)) ?? 0;
        }

        int hybridSuccessCount = hybridDevelopmentSystem?.UnlockedHybridNames?.Count ?? 0;
        int hybridTotalCount = hybridDevelopmentSystem?.Recipes?.Count ?? 0;
        int furnitureCount = furnitureSystem != null ? furnitureSystem.OwnedCount : 0;
        int furnitureTotalCount = furnitureSystem?.Definitions?.Count ?? 0;
        int bouquetCount = bouquetSystem != null ? bouquetSystem.TotalCreatedCount : 0;

        int money = shopManager != null ? shopManager.Money : 0;
        int rating = shopManager != null ? shopManager.ShopRating : 0;
        int supplierLevel = shopManager != null ? shopManager.SupplierLevel : 0;

        QueueNotification("クリア日", $"{clearDateText}\n通算{Mathf.Max(1, clearAbsoluteDay):N0}日目");
        QueueNotification("実プレイ時間", FormatPlayTime(clearPlayTimeSeconds));
        QueueNotification("所持金", $"{money:N0}円");
        QueueNotification("店評価", $"{rating:N0} / 10,000");
        QueueNotification("仕入先Lv", $"Lv.{supplierLevel}");
        QueueNotification("累計売上", $"{cumulativeSales:N0}円");
        QueueNotification("累計来客", $"{cumulativeVisitors:N0}人");
        QueueNotification("家具数", furnitureTotalCount > 0 ? $"{furnitureCount} / {furnitureTotalCount}個" : $"{furnitureCount}個");
        QueueNotification("開発完了数", developmentTotalCount > 0 ? $"{developmentCompletedCount} / {developmentTotalCount}" : developmentCompletedCount.ToString());
        QueueNotification("交配成功数", hybridTotalCount > 0 ? $"{hybridSuccessCount} / {hybridTotalCount}" : hybridSuccessCount.ToString());
        QueueNotification("交配失敗数", $"{hybridFailureCount:N0}回");
        QueueNotification("交配花作成数", $"{hybridProductionCount:N0}回");
        QueueNotification("花束作成数", $"{bouquetCount:N0}個");

        clearSummaryQueued = true;
        clearSummaryPending = false;
    }

    private static string FormatPlayTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}時間{minutes}分{secs}秒";
        if (minutes > 0)
            return $"{minutes}分{secs}秒";
        return $"{secs}秒";
    }

    private void CaptureInitialShopState()
    {
        if (shopManager == null)
            return;

        observedSupplierLevel = shopManager.SupplierLevel;
        observedClear = shopManager.HasCleared;
        observedDevelopmentUnlocked = shopManager.ShopRating >= DevelopmentSystem.DevelopmentUnlockShopRating;
        initializedShopState = true;
    }

    private void ResolveReferences()
    {
        // NotificationPanelは通常非表示なので、inactiveも含めて探す。
        if (notificationPanel == null)
            notificationPanel = FindFirstObjectByType<NotificationPanelUI>(FindObjectsInactive.Include);
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
        if (hybridDevelopmentSystem == null)
            hybridDevelopmentSystem = FindFirstObjectByType<HybridDevelopmentSystem>();
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (customerUI == null)
            customerUI = FindFirstObjectByType<CustomerUI>();
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();
        if (bouquetSystem == null)
            bouquetSystem = FindFirstObjectByType<BouquetSystem>();
    }

    private void QueueNotification(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        deferredMessages.Enqueue(message.Trim());
        Debug.Log($"通知予約：{message.Trim()}");
    }

    private void QueueNotification(string headline, string detail)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            QueueNotification(detail);
            return;
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            QueueNotification(headline);
            return;
        }

        QueueNotification($"{headline.Trim()}\n{detail.Trim()}");
    }
}
