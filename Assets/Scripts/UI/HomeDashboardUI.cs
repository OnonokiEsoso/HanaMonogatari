using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
    [Tooltip("ホームの依頼ボタンから依頼パネルを開くために設定します。")]
    [SerializeField] private RequestPanelUI requestPanelUI;
    [Tooltip("ホームの家具ボタンから家具パネルを開くために設定します。")]
    [SerializeField] private FurniturePanelUI furniturePanelUI;
    [Tooltip("ホームの開発ボタンから開発パネルを開くために設定します。")]
    [SerializeField] private DevelopmentPanelUI developmentPanelUI;
    [Tooltip("ホームのチャレンジボタンからチャレンジ一覧を開くために設定します。")]
    [SerializeField] private ChallengePanelUI challengePanelUI;
    [Tooltip("チャレンジ達成・報酬受取状態を監視し、ホームの『!!』表示を切り替えます。")]
    [SerializeField] private ChallengeSystem challengeSystem;
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
    [FormerlySerializedAs("checkoutButton")]
    [Tooltip("旧レジ横ボタン。ver0.0.6からホームの『開発』ボタンとして使用します。")]
    [SerializeField] private Button developmentButton;
    [Tooltip("ホームに追加したチャレンジボタン。GameObject名がChallengeButtonなら未設定でも自動取得します。")]
    [SerializeField] private Button challengeButton;
    [Tooltip("ChallengeButton内の『!!』テキスト。達成済み・未受取のチャレンジがある時だけ表示します。")]
    [SerializeField] private TMP_Text challengeAlertText;
    [SerializeField] private Button openShopButton;

    [Header("倍速ボタン")]
    [Tooltip("ホームに追加する倍速ボタン。ONの状態で開店するとその日の通常客を倍速処理します。")]
    [SerializeField] private Button fastForwardButton;
    [Tooltip("任意。倍速ボタン内のTMPテキストを設定すると『倍速：ON/OFF』を自動表示します。")]
    [SerializeField] private TMP_Text fastForwardButtonText;
    [SerializeField] private bool fastForwardEnabled;

    [Header("開店演出")]
    [Tooltip("ホームの『開店する』を押してから、お客が来始めるまで開店メッセージを表示する時間。")]
    [Min(0f)] [SerializeField] private float openingAnnouncementDuration = 1.4f;

    private bool isOpening;

    public bool IsHomeVisible => homeUIRoot != null && homeUIRoot.activeSelf;
    public bool IsFastForwardEnabled => fastForwardEnabled;

    private void Awake()
    {
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        ResolveChallengeReferences();

        if (openShopButton != null)
            openShopButton.onClick.AddListener(HandleOpenShopClicked);

        if (requestButton != null)
            requestButton.onClick.AddListener(HandleRequestClicked);

        if (furnitureButton != null)
            furnitureButton.onClick.AddListener(HandleFurnitureClicked);

        if (developmentButton != null)
            developmentButton.onClick.AddListener(HandleDevelopmentClicked);

        if (challengeButton != null)
            challengeButton.onClick.AddListener(HandleChallengeClicked);

        if (fastForwardButton != null)
            fastForwardButton.onClick.AddListener(HandleFastForwardClicked);

        RefreshFastForwardButton();
        RefreshRequestAlert();
        RefreshFurnitureAlert();
        RefreshChallengeAlert();
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

        ResolveChallengeReferences();
        if (challengeSystem != null)
            challengeSystem.OnChanged += RefreshChallengeAlert;

        RefreshFastForwardButton();
        RefreshRequestAlert();
        RefreshFurnitureAlert();
        RefreshChallengeAlert();
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

        if (challengeSystem != null)
            challengeSystem.OnChanged -= RefreshChallengeAlert;
    }

    private void OnDestroy()
    {
        if (openShopButton != null)
            openShopButton.onClick.RemoveListener(HandleOpenShopClicked);

        if (requestButton != null)
            requestButton.onClick.RemoveListener(HandleRequestClicked);

        if (furnitureButton != null)
            furnitureButton.onClick.RemoveListener(HandleFurnitureClicked);

        if (developmentButton != null)
            developmentButton.onClick.RemoveListener(HandleDevelopmentClicked);

        if (challengeButton != null)
            challengeButton.onClick.RemoveListener(HandleChallengeClicked);

        if (fastForwardButton != null)
            fastForwardButton.onClick.RemoveListener(HandleFastForwardClicked);
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

        if (developmentPanelUI != null)
            developmentPanelUI.HidePanel();

        if (challengePanelUI != null)
            challengePanelUI.HidePanel();

        Refresh();
        RefreshFastForwardButton();
        RefreshRequestAlert();
        RefreshFurnitureAlert();
        RefreshChallengeAlert();
    }

    public void HideHome()
    {
        if (furniturePanelUI != null)
            furniturePanelUI.HidePanel();

        if (developmentPanelUI != null)
            developmentPanelUI.HidePanel();

        if (challengePanelUI != null)
            challengePanelUI.HidePanel();

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

        RefreshFastForwardButton();
        RefreshFurnitureAlert();
        RefreshChallengeAlert();
    }

    private void HandleFastForwardClicked()
    {
        if (isOpening || (customerUI != null && customerUI.IsShopOpen))
            return;

        fastForwardEnabled = !fastForwardEnabled;
        RefreshFastForwardButton();
        Debug.Log($"HomeDashboardUI: 倍速営業 {(fastForwardEnabled ? "ON" : "OFF")}");
    }

    private void RefreshFastForwardButton()
    {
        if (fastForwardButtonText != null)
            fastForwardButtonText.text = fastForwardEnabled ? "倍速：ON" : "倍速：OFF";

        if (fastForwardButton != null)
            fastForwardButton.interactable = !isOpening && (customerUI == null || !customerUI.IsShopOpen);
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
        RefreshFastForwardButton();

        if (openShopButton != null)
            openShopButton.interactable = false;

        if (furniturePanelUI != null)
            furniturePanelUI.HidePanel();

        if (developmentPanelUI != null)
            developmentPanelUI.HidePanel();

        if (challengePanelUI != null)
            challengePanelUI.HidePanel();

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
        {
            standardPurchaseText.text = fastForwardEnabled
                ? $"～～～　{month}月{day}/{ShopManager.DaysPerMonth}日、開店【倍速】　～～～"
                : $"～～～　{month}月{day}/{ShopManager.DaysPerMonth}日、開店　～～～";
        }

        if (standardPriceText != null)
            standardPriceText.text = string.Empty;

        if (standardCommentText != null)
            standardCommentText.text = requestOpeningMessage;

        if (openingAnnouncementDuration > 0f)
            yield return new WaitForSeconds(openingAnnouncementDuration);

        if (salesVisualController != null)
            salesVisualController.PrepareForBusiness();

        // 倍速状態は「開店した瞬間」にその日分として固定する。
        customerUI.SetFastForwardMode(fastForwardEnabled);
        customerUI.OpenShop();
        HideHome();

        if (openShopButton != null)
            openShopButton.interactable = true;

        isOpening = false;
        RefreshFastForwardButton();
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

    private void RefreshChallengeAlert()
    {
        ResolveChallengeReferences();
        if (challengeAlertText == null)
            return;

        // 表示文字はHierarchy側の初期値に依存させない。
        if (challengeAlertText.text != "!!")
            challengeAlertText.text = "!!";

        // 「!!」は新しいチャレンジを見ていない印ではなく、
        // 達成済みでまだ報酬を受け取っていないチャレンジがある時だけ表示する。
        bool hasClaimableReward = false;
        if (challengeSystem != null)
        {
            foreach (ChallengeDefinition challenge in challengeSystem.GetVisibleChallenges())
            {
                if (challenge != null && challengeSystem.IsCompleted(challenge) && !challengeSystem.IsClaimed(challenge))
                {
                    hasClaimableReward = true;
                    break;
                }
            }
        }

        challengeAlertText.gameObject.SetActive(hasClaimableReward);
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

    private void HandleDevelopmentClicked()
    {
        if (developmentPanelUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: DevelopmentPanelUIが設定されていません。");
            return;
        }

        developmentPanelUI.ShowPanel();
    }

    private void HandleChallengeClicked()
    {
        ResolveChallengeReferences();

        if (challengePanelUI == null)
        {
            Debug.LogWarning("HomeDashboardUI: ChallengePanelUIが設定されていません。");
            return;
        }

        challengePanelUI.ShowPanel();
        RefreshChallengeAlert();
    }

    private void ResolveChallengeReferences()
    {
        if (challengePanelUI == null)
            challengePanelUI = FindFirstObjectByType<ChallengePanelUI>(FindObjectsInactive.Include);

        if (challengeSystem == null)
            challengeSystem = FindFirstObjectByType<ChallengeSystem>();

        if (challengeButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button != null && button.gameObject.name == "ChallengeButton")
                {
                    challengeButton = button;
                    break;
                }
            }
        }

        if (challengeAlertText == null && challengeButton != null)
        {
            TMP_Text[] texts = challengeButton.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text == null)
                    continue;

                string value = text.text != null ? text.text.Trim() : string.Empty;
                if (text.gameObject.name == "ChallengeAlertText" ||
                    text.gameObject.name == "UnconfirmedText" ||
                    text.gameObject.name == "AlertText" ||
                    value == "!!" ||
                    value.Contains("未確認"))
                {
                    challengeAlertText = text;
                    challengeAlertText.text = "!!";
                    break;
                }
            }
        }
    }
}
