using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各ゲームシステムの「プレイヤーに知らせたい出来事」を集約します。
/// 営業中や日送り処理の途中ではポップアップを割り込ませず、通知候補を一旦保留します。
/// 翌日の画面へ切り替わったあと DailyResultUI から FlushPendingNotifications を呼び、
/// その日の開始時に未通知項目をまとめて順番に表示します。
/// </summary>
public class GameNotificationBridge : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private NotificationPanelUI notificationPanel;
    [SerializeField] private DevelopmentSystem developmentSystem;
    [SerializeField] private HybridDevelopmentSystem hybridDevelopmentSystem;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private CustomerUI customerUI;

    [Header("来客マイルストーン")]
    [Tooltip("累計来客がこの人数を超えた時に通知候補へ追加します。表示は次の日の開始時です。")]
    [SerializeField] private int[] visitorMilestones = { 100, 500, 1000 };
    [SerializeField] private int cumulativeVisitors;
    [SerializeField] private int nextVisitorMilestoneIndex;

    private readonly Queue<string> deferredMessages = new();

    private int observedSupplierLevel = 1;
    private bool observedClear;
    private bool observedDevelopmentUnlocked;
    private bool initializedShopState;

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
            QueueNotification(
                "店評価10,000達成！",
                "街で一番人気のお花屋さんになったよ！ ゲームクリア！");
        }
        observedClear = shopManager.HasCleared;
    }

    private void HandleBusinessFinished()
    {
        if (customerUI == null)
            return;

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
    /// 保留していた通知を NotificationPanelUI へ渡し、閉じるたびに次の通知を表示します。
    /// パネルがまだ見つからない場合は通知を捨てず、そのまま次回まで保持します。
    /// </summary>
    public void FlushPendingNotifications()
    {
        if (deferredMessages.Count == 0)
            return;

        ResolveReferences();
        if (notificationPanel == null)
        {
            Debug.LogWarning($"GameNotificationBridge: NotificationPanelUIが見つからないため、{deferredMessages.Count}件の通知を保留します。");
            return;
        }

        while (deferredMessages.Count > 0)
            notificationPanel.ShowMessage(deferredMessages.Dequeue());
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
