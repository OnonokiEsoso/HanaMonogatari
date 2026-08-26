using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面と在庫画面のタブ切り替えを管理します。
/// ボタンを押すと対応するPanelだけを表示します。
/// </summary>
public class ShopTabUI : MonoBehaviour
{
    [Header("画面")]
    [SerializeField] private GameObject supplierPanel;
    [SerializeField] private GameObject inventoryPanel;

    [Header("タブボタン")]
    [SerializeField] private Button supplierTabButton;
    [SerializeField] private Button inventoryTabButton;

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
    }
}
