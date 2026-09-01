using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 仕入れ・在庫・値付け・花束・開店画面のタブ切り替えを管理します。
/// 開店前の「開店」タブはホーム画面として使い、HomeDashboardUIを表示します。
/// </summary>
public class ShopTabUI : MonoBehaviour
{
    private enum ShopTab
    {
        Supplier,
        Inventory,
        Pricing,
        Bouquet,
        Business
    }

    [Header("画面")]
    [SerializeField] private GameObject supplierPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject pricingPanel;
    [SerializeField] private GameObject bouquetPanel;
    [Tooltip("ホーム・営業演出・営業結果を表示するDailyResultPanelを設定します。")]
    [SerializeField] private GameObject dailyResultPanel;

    [Header("営業・ホーム")]
    [Tooltip("常駐GameObjectに置いたCustomerUIを設定します。")]
    [SerializeField] private CustomerUI customerUI;
    [Tooltip("DailyResultPanel内のSalesVisualControllerを設定します。")]
    [SerializeField] private SalesVisualController salesVisualController;
    [Tooltip("DailyResultPanel内のホームUIを管理するHomeDashboardUIを設定します。")]
    [SerializeField] private HomeDashboardUI homeDashboardUI;

    [Header("タブボタン")]
    [SerializeField] private Button supplierTabButton;
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button pricingTabButton;
    [SerializeField] private Button bouquetTabButton;
    [SerializeField] private Button customerTabButton;

    [Header("仕入れボタンの色")]
    [SerializeField] private Color supplierSelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);
    [SerializeField] private Color supplierUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("在庫ボタンの色")]
    [SerializeField] private Color inventorySelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);
    [SerializeField] private Color inventoryUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("値付けボタンの色")]
    [SerializeField] private Color pricingSelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);
    [SerializeField] private Color pricingUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("花束ボタンの色")]
    [SerializeField] private Color bouquetSelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);
    [SerializeField] private Color bouquetUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("営業ボタンの色")]
    [SerializeField] private Color customerSelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);
    [SerializeField] private Color customerUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("開始時")]
    [Tooltip("ONならゲーム開始時に開店タブのホーム画面を表示します。")]
    [FormerlySerializedAs("startWithSupplierTab")]
    [SerializeField] private bool startWithHome = true;

    private bool isBusinessOpen;
    private ShopTab currentTab;

    public bool IsBusinessOpen => isBusinessOpen;

    private void Awake()
    {
        if (supplierTabButton != null)
            supplierTabButton.onClick.AddListener(ShowSupplierTab);

        if (inventoryTabButton != null)
            inventoryTabButton.onClick.AddListener(ShowInventoryTab);

        if (pricingTabButton != null)
            pricingTabButton.onClick.AddListener(ShowPricingTab);

        if (bouquetTabButton != null)
            bouquetTabButton.onClick.AddListener(ShowBouquetTab);

        if (customerTabButton != null)
            customerTabButton.onClick.AddListener(HandleBusinessButton);
    }

    private void Start()
    {
        if (startWithHome)
            ShowBusinessHome();
        else
            ShowSupplierTab();

        RefreshTabInteractable();
    }

    private void OnDestroy()
    {
        if (supplierTabButton != null)
            supplierTabButton.onClick.RemoveListener(ShowSupplierTab);

        if (inventoryTabButton != null)
            inventoryTabButton.onClick.RemoveListener(ShowInventoryTab);

        if (pricingTabButton != null)
            pricingTabButton.onClick.RemoveListener(ShowPricingTab);

        if (bouquetTabButton != null)
            bouquetTabButton.onClick.RemoveListener(ShowBouquetTab);

        if (customerTabButton != null)
            customerTabButton.onClick.RemoveListener(HandleBusinessButton);
    }

    public void ShowSupplierTab()
    {
        if (isBusinessOpen) return;
        SetTab(ShopTab.Supplier);
    }

    public void ShowInventoryTab()
    {
        SetTab(ShopTab.Inventory);
    }

    public void ShowPricingTab()
    {
        if (isBusinessOpen) return;
        SetTab(ShopTab.Pricing);
    }

    public void ShowBouquetTab()
    {
        if (isBusinessOpen) return;
        SetTab(ShopTab.Bouquet);
    }

    /// <summary>
    /// 開店前なら「開店」タブをホームとして表示します。
    /// 営業中・営業終了後は既存の営業画面をそのまま表示します。
    /// </summary>
    public void ShowBusinessHome()
    {
        SetTab(ShopTab.Business);

        if (customerUI != null && !customerUI.IsShopOpen && !customerUI.HasFinishedToday)
            homeDashboardUI?.ShowHome();
    }

    private void HandleBusinessButton()
    {
        if (customerUI == null)
        {
            Debug.LogWarning("ShopTabUI: CustomerUIが設定されていません。");
            return;
        }

        SetTab(ShopTab.Business);

        if (!customerUI.IsShopOpen && !customerUI.HasFinishedToday)
            homeDashboardUI?.ShowHome();
    }

    /// <summary>
    /// CustomerUIから営業状態を受け取ります。
    /// 開店時はDailyResultPanelを維持し、ホームを隠して編集系タブをロックします。
    /// </summary>
    public void SetBusinessOpen(bool open)
    {
        isBusinessOpen = open;

        if (isBusinessOpen)
        {
            homeDashboardUI?.HideHome();
            SetTab(ShopTab.Business);

            if (salesVisualController != null)
                salesVisualController.PrepareForBusiness();
        }

        RefreshTabInteractable();
    }

    private void SetTab(ShopTab selectedTab)
    {
        currentTab = selectedTab;

        if (supplierPanel != null)
            supplierPanel.SetActive(selectedTab == ShopTab.Supplier);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(selectedTab == ShopTab.Inventory);

        if (pricingPanel != null)
            pricingPanel.SetActive(selectedTab == ShopTab.Pricing);

        if (bouquetPanel != null)
            bouquetPanel.SetActive(selectedTab == ShopTab.Bouquet);

        if (dailyResultPanel != null)
            dailyResultPanel.SetActive(selectedTab == ShopTab.Business);

        // ホーム以外のタブへ移動した時は、ホーム専用UIだけ隠します。
        if (selectedTab != ShopTab.Business)
            homeDashboardUI?.HideHome();

        UpdateTabColors(selectedTab);
    }

    private void RefreshTabInteractable()
    {
        if (supplierTabButton != null)
            supplierTabButton.interactable = !isBusinessOpen;

        if (pricingTabButton != null)
            pricingTabButton.interactable = !isBusinessOpen;

        if (bouquetTabButton != null)
            bouquetTabButton.interactable = !isBusinessOpen;

        if (inventoryTabButton != null)
            inventoryTabButton.interactable = true;

        if (customerTabButton != null)
            customerTabButton.interactable = true;
    }

    private void UpdateTabColors(ShopTab selectedTab)
    {
        SetButtonColor(supplierTabButton,
            selectedTab == ShopTab.Supplier ? supplierSelectedColor : supplierUnselectedColor);

        SetButtonColor(inventoryTabButton,
            selectedTab == ShopTab.Inventory ? inventorySelectedColor : inventoryUnselectedColor);

        SetButtonColor(pricingTabButton,
            selectedTab == ShopTab.Pricing ? pricingSelectedColor : pricingUnselectedColor);

        SetButtonColor(bouquetTabButton,
            selectedTab == ShopTab.Bouquet ? bouquetSelectedColor : bouquetUnselectedColor);

        SetButtonColor(customerTabButton,
            selectedTab == ShopTab.Business ? customerSelectedColor : customerUnselectedColor);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = color;
    }
}
