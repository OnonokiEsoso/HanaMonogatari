using System.Collections;
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
    [Tooltip("ホームの各ボタンから既存タブへ移動するために設定します。")]
    [SerializeField] private ShopTabUI shopTabUI;
    [Tooltip("ホームの依頼ボタンから依頼パネルを開くために設定します。")]
    [SerializeField] private RequestPanelUI requestPanelUI;
    [Tooltip("ホームの家具ボタンから家具パネルを開くために設定します。")]
    [SerializeField] private FurniturePanelUI furniturePanelUI;
    [Tooltip("依頼の有無を監視し、開店時に依頼条件を判定するために設定します。")]
    [SerializeField] private RequestSystem requestSystem;
    [Tooltip("家具の設置状況と設置上限を監視するために設定します。")]
    [SerializeField] private FurnitureSystem furnitureSystem;

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

    [Header("通常SpeechBubbleの開店表示")]
    [Tooltip("既存SpeechBubble内のPurchaseTextを設定します。開店メッセージはここに表示します。")]
    [SerializeField] private TMP_Text standardPurchaseText;
    [Tooltip("既存SpeechBubble内のPriceText。任意。開店演出中は空文字にします。")]
    [SerializeField] private TMP_Text standardPriceText;
    [Tooltip("既存SpeechBubble内のCommentText。依頼成功時はここに成功セリフを表示します。")]
    [SerializeField] private TMP_Text standardCommentText;

    [Header("今日の情報")]
    [SerializeField] private TMP_Text homeDateText;
    [SerializeField] private TMP_Text homeMoneyText;
    [SerializeField] private TMP_Text homeShopRatingText;
    [Tooltip("旧『今日のトレンド』表示。吹き出しと内容が重複するためホームでは非表示にします。")]
    [SerializeField] private TMP_Text homeTrendText;

    [Header("ホームボタン")]
    [SerializeField] private Button requestButton;
    [Tooltip("依頼が存在する時だけ表示するビックリマーク等のTMPテキスト。")]
    [SerializeField] private TMP_Text requestAlertText;
    [SerializeField] private Button furnitureButton;
    [Tooltip("設置可能な未設置家具があり、かつ設置枠が空いている時だけ表示するビックリマーク等のTMPテキスト。")]
    [SerializeField] private TMP_Text furnitureAlertText;
    [SerializeField] private Button checkoutButton;
    [SerializeField] private Button openShopButton;

    [Header("開店演出")]
    [Tooltip("ホームの『開店する』を押してから、お客が来始めるまで開店メッセージを表示する時間。")]
    [Min(0f)] [SerializeField] private float openingAnnouncementDuration = 1.4f;

    private bool isOpening;

    public bool IsHomeVisible => homeUIRoot != null && homeUIRoot.activeSelf;

    private void Awake()
    {
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        if (openShopButton != null)
            openShopButton.onClick.AddListener(HandleOpenShopClicked);

        if (requestButton != null)
            requestButton.onClick.AddListener(HandleRequestClicked);

        if (furnitureButton != null)
            furnitureButton.onClick.AddListener(HandleFurnitureClicked);

        if (checkoutButton != null)
            checkoutButton.onClick.AddListener(HandleCheckoutClicked);

        RefreshRequestAlert();
        RefreshFurnitureAlert();
    }

    private void OnEnable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged += Refresh;

        if (requestSystem != null)
        {
            requestSystem.OnRequestOffered += HandleRequestStateChanged;
            requestSystem.OnRequestChanged += HandleRequestStateChanged;
            requestSystem.OnRequestResolved += HandleRequestStateChanged;
        }

        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();
        if (furnitureSystem != null)
            furnitureSystem.OnChanged += RefreshFurnitureAlert;

        RefreshRequestAlert();
        RefreshFurnitureAlert();
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= Refresh;

        if (requestSystem != null)
        {
            requestSystem.OnRequestOffered -= HandleRequestStateChanged;
            requestSystem.OnRequestChanged -= HandleRequestStateChanged;
            requestSystem.OnRequestResolved -= HandleRequestStateChanged;
        }

        if (furnitureSystem != null)
            furnitureSystem.OnChanged -= RefreshFurnitureAlert;
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

    public void ShowHome()
    {
        isOpening = false;

        if (homeUIRoot != null)
            homeUIRoot.SetActive(true);

        if (homeSpeechBubble != null)
            homeSpeechBubble.SetActive(true);

        if (homeDashboard != null)
            homeDashboard.SetActive(true);

        if (standardSpeechBubble != null)
            standardSpeechBubble.SetActive(false);

        if (homeTrendText != null)
            homeTrendText.gameObject.SetActive(false);

        if (homePurchaseText != null)
            homePurchaseText.text = string.Empty;

        if (homePriceText != null)
            homePriceText.text = string.Empty;

        if (furniturePanelUI != null)
            furniturePanelUI.HidePanel();

        Refresh();
        RefreshRequestAlert();
        RefreshFurnitureAlert();
    }

    public void HideHome()
    {
        if (furniturePanelUI != null)
            furniturePanelUI.HidePanel();

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

        if (homeMessageText != null && !isOpening)
        {
            homeMessageText.text = string.IsNullOrWhiteSpace(trendMessage)
                ? "今日も一日がんばろう！ 開店前に準備を確認しておこう。"
                : trendMessage;
        }

        RefreshFurnitureAlert();
    }

    private void HandleOpenShopClicked()
    {
        if (customerUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: CustomerUIが設定されていません。");
            return;
        }

        if (customerUI.IsShopOpen || customerUI.HasFinishedToday || isOpening)
            return;

        StartCoroutine(OpenShopRoutine());
    }

    private IEnumerator OpenShopRoutine()
    {
        isOpening = true;

        if (openShopButton != null)
            openShopButton.interactable = false;

        if (furniturePanelUI != null)
            furniturePanelUI.HidePanel();

        // 依頼の成功/失敗確認は「開店する」を押したこの瞬間に固定する。
        if (requestSystem != null)
            requestSystem.ResolveAcceptedRequestAtOpening();

        string requestOpeningMessage = requestSystem != null
            ? requestSystem.LastOpeningRequestMessage
            : string.Empty;

        if (homeDashboard != null)
            homeDashboard.SetActive(false);

        if (homeSpeechBubble != null)
            homeSpeechBubble.SetActive(false);

        if (standardSpeechBubble != null)
            standardSpeechBubble.SetActive(true);

        int month = shopManager != null ? shopManager.CurrentMonth : 0;
        int day = shopManager != null ? shopManager.CurrentDay : 0;

        if (standardPurchaseText != null)
            standardPurchaseText.text = $"～～～　{month}月{day}/{ShopManager.DaysPerMonth}日、開店　～～～";

        if (standardPriceText != null)
            standardPriceText.text = string.Empty;

        if (standardCommentText != null)
            standardCommentText.text = requestOpeningMessage;

        if (openingAnnouncementDuration > 0f)
            yield return new WaitForSeconds(openingAnnouncementDuration);

        if (salesVisualController != null)
            salesVisualController.PrepareForBusiness();

        customerUI.OpenShop();
        HideHome();

        if (openShopButton != null)
            openShopButton.interactable = true;

        isOpening = false;
    }

    private void HandleRequestClicked()
    {
        if (requestPanelUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: RequestPanelUIが設定されていません。");
            return;
        }

        requestPanelUI.ShowPanel();
    }

    private void HandleRequestStateChanged(RequestData request)
    {
        RefreshRequestAlert();
    }

    private void RefreshRequestAlert()
    {
        if (requestAlertText == null)
            return;

        bool shouldShow = requestSystem != null && requestSystem.HasActiveRequest;
        requestAlertText.gameObject.SetActive(shouldShow);
    }

    private void RefreshFurnitureAlert()
    {
        if (furnitureAlertText == null)
            return;

        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        bool hasOpenSlot = furnitureSystem != null && furnitureSystem.InstalledCount < furnitureSystem.MaxInstalledCount;
        bool hasInstallableFurniture = furnitureSystem != null && furnitureSystem.HasInstallableUninstalledFurniture;
        furnitureAlertText.gameObject.SetActive(hasOpenSlot && hasInstallableFurniture);
    }

    private void HandleFurnitureClicked()
    {
        if (furniturePanelUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: FurniturePanelUIが設定されていません。");
            return;
        }

        furniturePanelUI.ShowPanel();
    }

    private void HandleCheckoutClicked()
    {
        if (shopTabUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: ShopTabUIが設定されていません。");
            return;
        }

        shopTabUI.ShowInventoryTab();
    }
}
