using UnityEngine;

/// <summary>
/// 1年目4月1日だけ動作する、最初の日の補助ヘルプです。
/// 一定時間同じタブに留まった時や、花を仕入れた後に次の行動が止まっている時だけ
/// 既存のプレイヤー通知を使って操作案内を表示します。
/// </summary>
public class FirstDayHelpSystem : MonoBehaviour
{
    private const float TabHelpDelaySeconds = 90f;
    private const float FlowerHelpDelaySeconds = 90f;
    private const float FlowerSecondHelpDelaySeconds = 120f;

    private const string TabHelpShownKey = "FirstDayHelp_TabBarShown";
    private const string FlowerHelp90ShownKey = "FirstDayHelp_Flower90Shown";
    private const string FlowerHelp120ShownKey = "FirstDayHelp_Flower120Shown";

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ShopTabUI shopTabUI;
    [SerializeField] private HomeDashboardUI homeDashboardUI;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private NotificationPanelUI notificationPanelUI;

    private bool isTracking;
    private int observedTabIndex = -1;
    private float sameTabElapsedSeconds;
    private bool flowerTimerStarted;
    private float flowerOwnedElapsedSeconds;

    public static void ResetTutorialState()
    {
        PlayerPrefs.DeleteKey(TabHelpShownKey);
        PlayerPrefs.DeleteKey(FlowerHelp90ShownKey);
        PlayerPrefs.DeleteKey(FlowerHelp120ShownKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// BeginTracking（ビギン・トラッキング）
    /// Begin＝開始、Tracking＝状態を追跡する。
    /// タイトル画面を閉じて実際にゲームへ入った瞬間から、初日ヘルプの時間計測を始めます。
    /// </summary>
    public void BeginTracking()
    {
        ResolveReferences();

        isTracking = true;
        sameTabElapsedSeconds = 0f;
        flowerOwnedElapsedSeconds = 0f;
        flowerTimerStarted = HasAnyFlower();
        observedTabIndex = shopTabUI != null ? shopTabUI.CurrentTabIndex : -1;
    }

    private void Update()
    {
        if (!isTracking)
            return;

        ResolveReferences();

        if (!IsFirstDay())
        {
            isTracking = false;
            return;
        }

        UpdateTabHelp();
        UpdateFlowerHelp();
    }

    private void UpdateTabHelp()
    {
        if (PlayerPrefs.GetInt(TabHelpShownKey, 0) != 0 || shopTabUI == null)
            return;

        int currentTabIndex = shopTabUI.CurrentTabIndex;
        if (currentTabIndex != observedTabIndex)
        {
            observedTabIndex = currentTabIndex;
            sameTabElapsedSeconds = 0f;
            return;
        }

        sameTabElapsedSeconds += Time.unscaledDeltaTime;
        if (sameTabElapsedSeconds < TabHelpDelaySeconds)
            return;

        ShowNotification(
            "上のタブバーから色々な画面に移動できます",
            "仕入れ・在庫・値付け・花束・ホームなど、上のタブバーから好きな画面へ移動できるよ。");

        MarkShown(TabHelpShownKey);
    }

    private void UpdateFlowerHelp()
    {
        if (!flowerTimerStarted)
        {
            if (!HasAnyFlower())
                return;

            flowerTimerStarted = true;
            flowerOwnedElapsedSeconds = 0f;
        }

        flowerOwnedElapsedSeconds += Time.unscaledDeltaTime;

        if (PlayerPrefs.GetInt(FlowerHelp90ShownKey, 0) == 0 &&
            flowerOwnedElapsedSeconds >= FlowerHelpDelaySeconds)
        {
            if (IsHomeVisible())
            {
                ShowOpenShopHelp();
            }
            else
            {
                ShowNotification(
                    "ホームに戻ってみよう",
                    "上のタブバーの『ホーム』を押すとホームに戻れます。営業を始める時はホームへ戻ってね。");
            }

            MarkShown(FlowerHelp90ShownKey);
        }

        if (PlayerPrefs.GetInt(FlowerHelp120ShownKey, 0) == 0 &&
            flowerOwnedElapsedSeconds >= FlowerSecondHelpDelaySeconds &&
            IsHomeVisible())
        {
            ShowOpenShopHelp();
            MarkShown(FlowerHelp120ShownKey);
        }
    }

    private void ShowOpenShopHelp()
    {
        ShowNotification(
            "お店を開店してみよう",
            "画面中央付近の青い『開店する』ボタンを押すと開店します。準備ができたら押してみよう。");
    }

    private bool HasAnyFlower()
    {
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();

        if (inventorySystem == null)
            return false;

        foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
        {
            if (batch != null && batch.flower != null && batch.quantity > 0)
                return true;
        }

        return false;
    }

    private bool IsHomeVisible()
    {
        return homeDashboardUI != null && homeDashboardUI.IsHomeVisible;
    }

    private bool IsFirstDay()
    {
        return shopManager != null &&
               shopManager.GameYear == 1 &&
               shopManager.CurrentMonth == 4 &&
               shopManager.CurrentDay == 1;
    }

    private void ShowNotification(string headline, string detail)
    {
        if (notificationPanelUI == null)
            notificationPanelUI = FindFirstObjectByType<NotificationPanelUI>(FindObjectsInactive.Include);

        if (notificationPanelUI != null)
        {
            notificationPanelUI.ShowMessage(headline, detail);
            return;
        }

        Debug.LogWarning($"FirstDayHelpSystem: NotificationPanelUIが見つからないため通知を表示できませんでした。{headline}");
    }

    private static void MarkShown(string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (shopTabUI == null)
            shopTabUI = FindFirstObjectByType<ShopTabUI>(FindObjectsInactive.Include);
        if (homeDashboardUI == null)
            homeDashboardUI = FindFirstObjectByType<HomeDashboardUI>(FindObjectsInactive.Include);
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (notificationPanelUI == null)
            notificationPanelUI = FindFirstObjectByType<NotificationPanelUI>(FindObjectsInactive.Include);
    }
}
