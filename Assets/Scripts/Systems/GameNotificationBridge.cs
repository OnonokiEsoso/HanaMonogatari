using System;
using UnityEngine;

/// <summary>
/// 各ゲームシステムの「プレイヤーに知らせたい出来事」を NotificationPanelUI へ集約します。
/// 既存のDebug.Logだけで終わっていた完了・解禁・到達系イベントを、ゲーム画面でも通知します。
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
    [Tooltip("累計来客がこの人数を超えた時に通知します。")]
    [SerializeField] private int[] visitorMilestones = { 100, 500, 1000 };
    [SerializeField] private int cumulativeVisitors;
    [SerializeField] private int nextVisitorMilestoneIndex;

    private int observedSupplierLevel = 1;
    private bool observedClear;
    private bool observedDevelopmentUnlocked;
    private bool initializedShopState;

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
        Notify(message);

        // 枯ラサンつい完了時は新種開発が解禁されるため、その変化も明示します。
        if (developmentSystem != null && developmentSystem.IsNewSpeciesDevelopmentUnlocked)
        {
            DevelopmentDefinition definition = developmentSystem.GetDefinition(DevelopmentId.KarasanTsui);
            if (definition != null && message != null && message.Contains(definition.displayName, StringComparison.Ordinal))
            {
                Notify("新種開発が解禁されたよ！", "開発パネルの『交配』から、新しい花を生み出せるようになったよ。");
            }
        }
    }

    private void HandleHybridResearchCompleted(string message)
    {
        Notify(message);

        if (!string.IsNullOrWhiteSpace(message) && message.Contains("成功", StringComparison.Ordinal))
            Notify("交配花を作成できるようになったよ！", "開発パネルの『作成』に、完成した新種が追加されたよ。");
    }

    private void HandleHybridProductionCompleted(string message)
    {
        Notify(message);
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
            Notify(
                $"仕入先Lvが{currentSupplierLevel}になったよ！",
                "仕入れられる商品の幅が広がったよ。仕入れ画面を確認してみよう。");
            observedSupplierLevel = currentSupplierLevel;
        }
        else if (currentSupplierLevel < observedSupplierLevel)
        {
            // デバッグ操作等で戻された場合は観測値だけ同期します。
            observedSupplierLevel = currentSupplierLevel;
        }

        bool developmentUnlocked = shopManager.ShopRating >= DevelopmentSystem.DevelopmentUnlockShopRating;
        if (developmentUnlocked && !observedDevelopmentUnlocked)
        {
            Notify(
                "開発が解禁されたよ！",
                "店評価が2,000に到達したので、ホームの『開発』から新しい商品を研究できるようになったよ。");
        }
        observedDevelopmentUnlocked = developmentUnlocked;

        if (shopManager.HasCleared && !observedClear)
        {
            Notify(
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

            Notify(
                $"累計来客{milestone:N0}人突破！",
                $"これまでに{cumulativeVisitors:N0}人のお客さんが来店してくれたよ。");
            nextVisitorMilestoneIndex++;
        }
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
        if (notificationPanel == null)
            notificationPanel = FindFirstObjectByType<NotificationPanelUI>();
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
        if (hybridDevelopmentSystem == null)
            hybridDevelopmentSystem = FindFirstObjectByType<HybridDevelopmentSystem>();
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (customerUI == null)
            customerUI = FindFirstObjectByType<CustomerUI>();
    }

    private void Notify(string message)
    {
        if (notificationPanel == null)
            notificationPanel = FindFirstObjectByType<NotificationPanelUI>();

        if (notificationPanel != null)
            notificationPanel.ShowMessage(message);
        else if (!string.IsNullOrWhiteSpace(message))
            Debug.LogWarning($"通知パネル未設定：{message}");
    }

    private void Notify(string headline, string detail)
    {
        if (notificationPanel == null)
            notificationPanel = FindFirstObjectByType<NotificationPanelUI>();

        if (notificationPanel != null)
            notificationPanel.ShowMessage(headline, detail);
        else
            Debug.LogWarning($"通知パネル未設定：{headline} / {detail}");
    }
}
