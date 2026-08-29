using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ・在庫・値付け・花束・営業画面のタブ切り替えを管理します。
/// 営業中は仕入れ・値付け・花束タブを使用できません。
/// 開店すると、営業演出を配置しているDailyResultPanelへ自動的に切り替えます。
/// </summary>
public class ShopTabUI : MonoBehaviour
{
    private enum ShopTab
    {
        Supplier,
        Inventory,
        Pricing,
        Bouquet,
        Customer,
        Business
    }

    [Header("画面")]
    [SerializeField] private GameObject supplierPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject pricingPanel;
    [SerializeField] private GameObject bouquetPanel;
    [SerializeField] private GameObject customerPanel;
    [Tooltip("営業演出と営業結果を表示するDailyResultPanelを設定します。")]
    [SerializeField] private GameObject dailyResultPanel;

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
    [SerializeField] private bool startWithSupplierTab = true;

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
            customerTabButton.onClick.AddListener(ShowCustomerTab);
    }

    private void Start()
    {
        if (startWithSupplierTab)
            ShowSupplierTab();
        else
            ShowInventoryTab();

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
            customerTabButton.onClick.RemoveListener(ShowCustomerTab);
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

    public void ShowCustomerTab()
    {
        // 営業中に開店タブを押した場合も、営業演出画面へ戻します。
        if (isBusinessOpen)
        {
            SetTab(ShopTab.Business);
            return;
        }

        SetTab(ShopTab.Customer);
    }

    /// <summary>
    /// SetBusinessOpen（セット・ビジネス・オープン）
    /// 開店した瞬間にDailyResultPanelへ切り替え、営業アニメーションを見せます。
    /// 営業終了後も結果確認のため、その画面を維持します。
    /// </summary>
    public void SetBusinessOpen(bool open)
    {
        isBusinessOpen = open;

        if (isBusinessOpen)
            SetTab(ShopTab.Business);

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

        if (customerPanel != null)
            customerPanel.SetActive(selectedTab == ShopTab.Customer);

        if (dailyResultPanel != null)
            dailyResultPanel.SetActive(selectedTab == ShopTab.Business);

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

        // 営業演出中は「開店」タブを選択中として見せます。
        SetButtonColor(customerTabButton,
            selectedTab == ShopTab.Customer || selectedTab == ShopTab.Business
                ? customerSelectedColor
                : customerUnselectedColor);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = color;
    }
}
