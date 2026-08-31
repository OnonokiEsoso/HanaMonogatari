using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 倉庫画面でレジ横商品の在庫・価格・設置状態を表示し、設置/撤去を切り替えます。
/// </summary>
public class CheckoutInventoryItemUI : MonoBehaviour
{
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text installedStateText;
    [SerializeField] private Button installButton;
    [SerializeField] private TMP_Text installButtonText;

    private CheckoutItemSystem checkoutItemSystem;
    private CheckoutItemSystem.CheckoutItemDefinition definition;

    private void Awake()
    {
        if (installButton != null)
            installButton.onClick.AddListener(ToggleInstalled);
    }

    private void OnDestroy()
    {
        if (installButton != null)
            installButton.onClick.RemoveListener(ToggleInstalled);
    }

    public void Bind(CheckoutItemSystem system, CheckoutItemSystem.CheckoutItemDefinition item)
    {
        checkoutItemSystem = system;
        definition = item;
        Refresh();
    }

    public void Refresh()
    {
        if (checkoutItemSystem == null || definition == null) return;

        int quantity = checkoutItemSystem.GetStockQuantity(definition.id);
        bool installed = checkoutItemSystem.GetInstalledDefinitions().Contains(definition);
        int salePrice = definition.GetSalePrice(FindCurrentMonth());

        if (nameText != null)
            nameText.text = definition.displayName;

        if (stockText != null)
            stockText.text = $"在庫：{quantity}個";

        if (priceText != null)
            priceText.text = $"販売価格：{salePrice:N0}円";

        if (installedStateText != null)
            installedStateText.text = installed ? "レジ横：設置中" : "レジ横：未設置";

        if (installButtonText != null)
            installButtonText.text = installed ? "撤去" : "設置";

        if (installButton != null)
            installButton.interactable = quantity > 0 && (installed || checkoutItemSystem.InstalledCount < CheckoutItemSystem.MaxInstalledItems);

        if (itemImage != null)
        {
            Sprite sprite = checkoutItemSystem.LoadSprite(definition);
            itemImage.sprite = sprite;
            itemImage.enabled = sprite != null;
        }
    }

    private void ToggleInstalled()
    {
        if (checkoutItemSystem == null || definition == null) return;

        bool installed = checkoutItemSystem.GetInstalledDefinitions().Contains(definition);
        if (installed)
            checkoutItemSystem.Uninstall(definition.id);
        else
            checkoutItemSystem.TryInstall(definition.id);

        Refresh();
    }

    private int FindCurrentMonth()
    {
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        return shopManager != null ? shopManager.CurrentMonth : 1;
    }
}
