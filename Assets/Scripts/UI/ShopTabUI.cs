using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ・在庫・値付け画面のタブ切り替えを管理します。
/// 各タブごとに「選択中の色」「未選択時の色」を個別設定できます。
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

    [Header("仕入れボタンの色")]
    [Tooltip("仕入れタブが選択されているときの背景色")]
    [SerializeField] private Color supplierSelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);

    [Tooltip("仕入れタブが選択されていないときの背景色")]
    [SerializeField] private Color supplierUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("在庫ボタンの色")]
    [Tooltip("在庫タブが選択されているときの背景色")]
    [SerializeField] private Color inventorySelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);

    [Tooltip("在庫タブが選択されていないときの背景色")]
    [SerializeField] private Color inventoryUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("値付けボタンの色")]
    [Tooltip("値付けタブが選択されているときの背景色")]
    [SerializeField] private Color pricingSelectedColor = new Color(0.78f, 0.90f, 0.72f, 1f);

    [Tooltip("値付けタブが選択されていないときの背景色")]
    [SerializeField] private Color pricingUnselectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);

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
    /// UpdateTabColors（アップデート・タブ・カラーズ）
    /// Update＝更新する、Tab Colors＝タブの色。
    /// 現在選択中のタブに応じて、各ボタン固有の色を反映します。
    /// </summary>
    private void UpdateTabColors(ShopTab selectedTab)
    {
        SetButtonColor(
            supplierTabButton,
            selectedTab == ShopTab.Supplier ? supplierSelectedColor : supplierUnselectedColor);

        SetButtonColor(
            inventoryTabButton,
            selectedTab == ShopTab.Inventory ? inventorySelectedColor : inventoryUnselectedColor);

        SetButtonColor(
            pricingTabButton,
            selectedTab == ShopTab.Pricing ? pricingSelectedColor : pricingUnselectedColor);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = color;
    }
}
