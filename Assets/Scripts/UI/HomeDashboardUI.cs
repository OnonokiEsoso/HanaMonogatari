using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「開店」タブの開店前ホーム画面を管理します。
/// 背景・主人公・レジなど既存の営業画面はそのまま使い、
/// ホーム専用の吹き出しとダッシュボードだけを重ねて表示します。
/// </summary>
public class HomeDashboardUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private SalesVisualController salesVisualController;
    [Tooltip("ホームの『開店する』から確認を挟まず直接営業開始するために設定します。")]
    [SerializeField] private CustomerUI customerUI;

    [Header("ホーム表示")]
    [Tooltip("HomeUIRoot。ホーム専用UI全体の親を設定します。")]
    [SerializeField] private GameObject homeUIRoot;
    [Tooltip("通常営業で使っている既存のSpeechBubble。ホーム表示中だけOFFにします。")]
    [SerializeField] private GameObject standardSpeechBubble;
    [SerializeField] private GameObject homeSpeechBubble;
    [SerializeField] private GameObject homeDashboard;

    [Header("ホーム吹き出し")]
    [Tooltip("HomeSpeechBubble内のCommentTextを設定します。")]
    [SerializeField] private TMP_Text homeMessageText;
    [Tooltip("複製元に残っているPurchaseText。任意。設定するとホーム中は空文字にします。")]
    [SerializeField] private TMP_Text homePurchaseText;
    [Tooltip("複製元に残っているPriceText。任意。設定するとホーム中は空文字にします。")]
    [SerializeField] private TMP_Text homePriceText;

    [Header("今日の情報")]
    [SerializeField] private TMP_Text homeDateText;
    [SerializeField] private TMP_Text homeMoneyText;
    [SerializeField] private TMP_Text homeShopRatingText;
    [SerializeField] private TMP_Text homeTrendText;

    [Header("ホームボタン")]
    [SerializeField] private Button requestButton;
    [SerializeField] private Button furnitureButton;
    [SerializeField] private Button checkoutButton;
    [SerializeField] private Button openShopButton;

    public bool IsHomeVisible => homeUIRoot != null && homeUIRoot.activeSelf;

    private void Awake()
    {
        if (openShopButton != null)
            openShopButton.onClick.AddListener(HandleOpenShopClicked);

        if (requestButton != null)
            requestButton.onClick.AddListener(HandleRequestClicked);

        if (furnitureButton != null)
            furnitureButton.onClick.AddListener(HandleFurnitureClicked);

        if (checkoutButton != null)
            checkoutButton.onClick.AddListener(HandleCheckoutClicked);
    }

    private void OnEnable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (openShopButton != null)
            openShopButton.onClick.RemoveListener(HandleOpenShopClicked);

        if (requestButton != null)
            requestButton.onClick.RemoveListener(HandleRequestClicked);

        if (furnitureButton != null)
            furnitureButton.onClick.RemoveListener(HandleFurnitureClicked);

        if (checkoutButton != null)
            checkoutButton.onClick.RemoveListener(HandleCheckoutClicked);
    }

    /// <summary>
    /// 開店前のホームを表示します。
    /// </summary>
    public void ShowHome()
    {
        if (homeUIRoot != null)
            homeUIRoot.SetActive(true);

        if (homeSpeechBubble != null)
            homeSpeechBubble.SetActive(true);

        if (homeDashboard != null)
            homeDashboard.SetActive(true);

        if (standardSpeechBubble != null)
            standardSpeechBubble.SetActive(false);

        if (homePurchaseText != null)
            homePurchaseText.text = string.Empty;

        if (homePriceText != null)
            homePriceText.text = string.Empty;

        Refresh();
    }

    /// <summary>
    /// 営業中・営業結果ではホーム専用UIを隠します。
    /// </summary>
    public void HideHome()
    {
        if (homeUIRoot != null)
            homeUIRoot.SetActive(false);
    }

    public void Refresh()
    {
        if (shopManager == null) return;

        if (homeDateText != null)
            homeDateText.text = shopManager.DateDisplayText;

        if (homeMoneyText != null)
            homeMoneyText.text = $"所持金：{shopManager.Money:N0}円";

        if (homeShopRatingText != null)
            homeShopRatingText.text = $"店評価：{shopManager.ShopRating:N0} / 10,000";

        string trendMessage = TrendSystem.GetDailySupplierMessage(shopManager);
        if (homeTrendText != null)
            homeTrendText.text = string.IsNullOrWhiteSpace(trendMessage)
                ? "今日のトレンド：特になし"
                : $"今日のトレンド：{trendMessage}";

        if (homeMessageText != null)
        {
            homeMessageText.text = string.IsNullOrWhiteSpace(trendMessage)
                ? "今日も一日がんばろう！ 開店前に準備を確認しておこう。"
                : trendMessage;
        }
    }

    private void HandleOpenShopClicked()
    {
        if (customerUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: CustomerUIが設定されていません。");
            return;
        }

        if (customerUI.IsShopOpen || customerUI.HasFinishedToday)
            return;

        // ホームのボタン自体を最終確認として扱う。
        // 既存SpeechBubbleの『開店する？』確認は挟まず、そのまま営業開始する。
        HideHome();

        if (standardSpeechBubble != null)
            standardSpeechBubble.SetActive(true);

        if (salesVisualController != null)
            salesVisualController.PrepareForBusiness();

        customerUI.OpenShop();
    }

    private static void HandleRequestClicked()
    {
        Debug.Log("依頼画面はver0.0.4で実装予定です。");
    }

    private static void HandleFurnitureClicked()
    {
        Debug.Log("家具画面は今後のアップデートで実装予定です。");
    }

    private static void HandleCheckoutClicked()
    {
        Debug.Log("レジ横商品の管理は現在、在庫画面から行えます。");
    }
}
