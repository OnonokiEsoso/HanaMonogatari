using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面と在庫画面のタブ切り替えを管理します。
/// ボタンを押すと対応するPanelだけを表示し、選択中タブの色も切り替えます。
/// </summary>
public class ShopTabUI : MonoBehaviour
{
    [Header("画面")]
    [SerializeField] private GameObject supplierPanel;
    [SerializeField] private GameObject inventoryPanel;

    [Header("タブボタン")]
    [SerializeField] private Button supplierTabButton;
    [SerializeField] private Button inventoryTabButton;

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
    }

    public void ShowSupplierTab()
    {
        SetPanels(supplier: true);
    }

    public void ShowInventoryTab()
    {
        SetPanels(supplier: false);
    }

    private void SetPanels(bool supplier)
    {
        if (supplierPanel != null)
            supplierPanel.SetActive(supplier);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(!supplier);

        UpdateTabColors(supplier);
    }

    /// <summary>
    /// 選択中タブと未選択タブの背景色を更新します。
    /// </summary>
    private void UpdateTabColors(bool supplierSelected)
    {
        SetButtonColor(supplierTabButton, supplierSelected ? selectedTabColor : unselectedTabColor);
        SetButtonColor(inventoryTabButton, supplierSelected ? unselectedTabColor : selectedTabColor);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = color;
    }
}
