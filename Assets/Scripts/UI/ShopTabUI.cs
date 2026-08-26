using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ・在庫・値付け画面のタブ切り替えを管理します。
/// ボタンを押すと対応するPanelだけを表示し、選択中タブの色も切り替えます。
/// </summary>
public class ShopTabUI : MonoBehaviour
{
    private enum ShopTab
    {
        Supplier,
        Inventory,
        Pricing
    }

    [Header("画面")]
    [SerializeField] private GameObject supplierPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject pricingPanel;

    [Header("タブボタン")]
    [SerializeField] private Button supplierTabButton;
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button pricingTabButton;

    [Header("タブ色")]
    [Tooltip("現在選択されているタブの背景色")]
    [SerializeField] private Color selectedTabColor = new Color(0.78f, 0.90f, 0.72f, 1f);

    [Tooltip("選択されていないタブの背景色")]
    [SerializeField] private Color unselectedTabColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("開始時")]
    [SerializeField] private bool startWithSupplierTab = true;

    private void Awake()
    {
        if (supplierTabButton != null)
            supplierTabButton.onClick.AddListener(ShowSupplierTab);

        if (inventoryTabButton != null)
            inventoryTabButton.onClick.AddListener(ShowInventoryTab);

        if (pricingTabButton != null)
            pricingTabButton.onClick.AddListener(ShowPricingTab);
    }

    private void Start()
    {
        if (startWithSupplierTab)
            ShowSupplierTab();
        else
            ShowInventoryTab();
    }

    private void OnDestroy()
    {
        if (supplierTabButton != null)
            supplierTabButton.onClick.RemoveListener(ShowSupplierTab);

        if (inventoryTabButton != null)
            inventoryTabButton.onClick.RemoveListener(ShowInventoryTab);

        if (pricingTabButton != null)
            pricingTabButton.onClick.RemoveListener(ShowPricingTab);
    }

    public void ShowSupplierTab()
    {
        SetTab(ShopTab.Supplier);
    }

    public void ShowInventoryTab()
    {
        SetTab(ShopTab.Inventory);
    }

    public void ShowPricingTab()
    {
        SetTab(ShopTab.Pricing);
    }

    private void SetTab(ShopTab selectedTab)
    {
        if (supplierPanel != null)
            supplierPanel.SetActive(selectedTab == ShopTab.Supplier);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(selectedTab == ShopTab.Inventory);

        if (pricingPanel != null)
            pricingPanel.SetActive(selectedTab == ShopTab.Pricing);

        UpdateTabColors(selectedTab);
    }

    /// <summary>
    /// 選択中タブと未選択タブの背景色を更新します。
    /// </summary>
    private void UpdateTabColors(ShopTab selectedTab)
    {
        SetButtonColor(
            supplierTabButton,
            selectedTab == ShopTab.Supplier ? selectedTabColor : unselectedTabColor);

        SetButtonColor(
            inventoryTabButton,
            selectedTab == ShopTab.Inventory ? selectedTabColor : unselectedTabColor);

        SetButtonColor(
            pricingTabButton,
            selectedTab == ShopTab.Pricing ? selectedTabColor : unselectedTabColor);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = color;
    }
}
