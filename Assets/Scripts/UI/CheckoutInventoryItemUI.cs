using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 倉庫画面のレジ横商品、またはホーム家具画面の家具を表示し、設置/撤去を切り替えます。
/// 同じPrefabを両方で流用できます。
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
    private FurnitureSystem furnitureSystem;
    private FurnitureData furnitureDefinition;

    private bool IsFurnitureMode => furnitureDefinition != null;

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
        furnitureSystem = null;
        furnitureDefinition = null;
        Refresh();
    }

    public void BindFurniture(FurnitureSystem system, FurnitureData furniture)
    {
        checkoutItemSystem = null;
        definition = null;
        furnitureSystem = system;
        furnitureDefinition = furniture;
        Refresh();
    }

    public void Refresh()
    {
        if (IsFurnitureMode)
        {
            RefreshFurniture();
            return;
        }

        RefreshCheckoutItem();
    }

    private void RefreshCheckoutItem()
    {
        if (checkoutItemSystem == null || definition == null) return;

        int quantity = checkoutItemSystem.GetStockQuantity(definition.id);
        bool installed = checkoutItemSystem.GetInstalledDefinitions().Contains(definition);
        int salePrice = definition.GetSalePrice(FindCurrentMonth());

        if (nameText != null)
            nameText.text = definition.displayName;

        if (stockText != null)
            stockText.text = $"{quantity}個";

        if (priceText != null)
            priceText.text = $"{salePrice:N0}円";

        if (installedStateText != null)
            installedStateText.text = installed ? "設置中" : "未設置";

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

    private void RefreshFurniture()
    {
        if (furnitureSystem == null || furnitureDefinition == null)
            return;

        bool installed = furnitureSystem.IsInstalled(furnitureDefinition.id);

        if (nameText != null)
            nameText.text = furnitureDefinition.displayName;

        // レジ横Prefabの在庫数欄は、家具では所有状態の補助表示に流用します。
        if (stockText != null)
            stockText.text = "所持中";

        // レジ横Prefabの価格欄は、家具では現在発動する効果の簡易表示に流用します。
        if (priceText != null)
            priceText.text = BuildFurnitureEffectText(furnitureDefinition);

        if (installedStateText != null)
            installedStateText.text = installed ? "設置中" : "未設置";

        if (installButtonText != null)
            installButtonText.text = installed ? "撤去" : "設置";

        if (installButton != null)
            installButton.interactable = true;

        if (itemImage != null)
        {
            Sprite sprite = furnitureSystem.LoadSprite(furnitureDefinition);
            itemImage.sprite = sprite;
            itemImage.enabled = sprite != null;
        }
    }

    private void ToggleInstalled()
    {
        if (IsFurnitureMode)
        {
            if (furnitureSystem == null || furnitureDefinition == null)
                return;

            if (furnitureSystem.IsInstalled(furnitureDefinition.id))
                furnitureSystem.Uninstall(furnitureDefinition.id);
            else
                furnitureSystem.TryInstall(furnitureDefinition.id);

            Refresh();
            return;
        }

        if (checkoutItemSystem == null || definition == null) return;

        bool installed = checkoutItemSystem.GetInstalledDefinitions().Contains(definition);
        if (installed)
            checkoutItemSystem.Uninstall(definition.id);
        else
            checkoutItemSystem.TryInstall(definition.id);

        Refresh();
    }

    private static string BuildFurnitureEffectText(FurnitureData item)
    {
        if (item == null)
            return string.Empty;

        string text = string.Empty;
        Append(ref text, item.visitorBonusPercent, "来客率");
        Append(ref text, item.budgetBonusPercent, "予算");

        if (item.summerVisitorBonusPercent != 0f)
            AppendRaw(ref text, $"夏 来客率+{item.summerVisitorBonusPercent * 100f:0.#}%");
        if (item.rainyVisitorBonusPercent != 0f)
            AppendRaw(ref text, $"雨 来客率+{item.rainyVisitorBonusPercent * 100f:0.#}%");
        if (item.rainyBudgetBonusPercent != 0f)
            AppendRaw(ref text, $"雨 予算+{item.rainyBudgetBonusPercent * 100f:0.#}%");
        if (item.rainyVisitorPenaltyFloorPercent < 0f)
            AppendRaw(ref text, $"雨ペナルティ{item.rainyVisitorPenaltyFloorPercent * 100f:0.#}%まで軽減");

        return string.IsNullOrWhiteSpace(text) ? "効果なし" : text;
    }

    private static void Append(ref string text, float value, string label)
    {
        if (value == 0f) return;
        AppendRaw(ref text, $"{label}+{value * 100f:0.#}%");
    }

    private static void AppendRaw(ref string text, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!string.IsNullOrEmpty(text)) text += " / ";
        text += value;
    }

    private int FindCurrentMonth()
    {
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        return shopManager != null ? shopManager.CurrentMonth : 1;
    }
}
