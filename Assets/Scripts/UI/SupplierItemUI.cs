using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面の商品1種類分を表示する共通UI。
/// 花・レジ横商品BOX・家具を同じPrefabで表示します。
/// </summary>
public class SupplierItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private Image flowerImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private TMP_Text saleText;
    [SerializeField] private TMP_Text newText;

    [Header("操作")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button buyFiveButton;
    [SerializeField] private TMP_Text buyFiveButtonText;

    private SupplierSystem.ArrivalItem arrivalItem;
    private CheckoutItemSystem checkoutItemSystem;
    private CheckoutItemSystem.CheckoutItemDefinition checkoutItem;
    private FurnitureSystem furnitureSystem;
    private FurnitureData furnitureItem;
    private bool showNewMarker;

    private Action<SupplierSystem.ArrivalItem> onBuyOneRequested;
    private Action<SupplierSystem.ArrivalItem, int> onBuyMultipleRequested;
    private Action<CheckoutItemSystem.CheckoutItemDefinition> onBuyCheckoutRequested;
    private Action<FurnitureData> onBuyFurnitureRequested;

    private bool IsCheckoutItem => checkoutItem != null;
    private bool IsFurnitureItem => furnitureItem != null;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(HandleBuyClicked);

        if (buyFiveButton != null)
            buyFiveButton.onClick.AddListener(HandleBuyFiveClicked);
    }

    private void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);

        if (buyFiveButton != null)
            buyFiveButton.onClick.RemoveListener(HandleBuyFiveClicked);
    }

    public void Bind(
        SupplierSystem.ArrivalItem item,
        Action<SupplierSystem.ArrivalItem> buyOneCallback,
        Action<SupplierSystem.ArrivalItem, int> buyMultipleCallback,
        bool isNew)
    {
        arrivalItem = item;
        checkoutItemSystem = null;
        checkoutItem = null;
        furnitureSystem = null;
        furnitureItem = null;
        showNewMarker = isNew;
        onBuyOneRequested = buyOneCallback;
        onBuyMultipleRequested = buyMultipleCallback;
        onBuyCheckoutRequested = null;
        onBuyFurnitureRequested = null;
        Refresh();
    }

    public void BindCheckout(
        CheckoutItemSystem system,
        CheckoutItemSystem.CheckoutItemDefinition item,
        Action<CheckoutItemSystem.CheckoutItemDefinition> buyCallback,
        bool isNew)
    {
        arrivalItem = null;
        checkoutItemSystem = system;
        checkoutItem = item;
        furnitureSystem = null;
        furnitureItem = null;
        showNewMarker = isNew;
        onBuyOneRequested = null;
        onBuyMultipleRequested = null;
        onBuyCheckoutRequested = buyCallback;
        onBuyFurnitureRequested = null;
        Refresh();
    }

    public void BindFurniture(
        FurnitureSystem system,
        FurnitureData item,
        Action<FurnitureData> buyCallback,
        bool isNew)
    {
        arrivalItem = null;
        checkoutItemSystem = null;
        checkoutItem = null;
        furnitureSystem = system;
        furnitureItem = item;
        showNewMarker = isNew;
        onBuyOneRequested = null;
        onBuyMultipleRequested = null;
        onBuyCheckoutRequested = null;
        onBuyFurnitureRequested = buyCallback;
        Refresh();
    }

    public void Refresh()
    {
        RefreshNewMarker();

        if (IsFurnitureItem)
        {
            RefreshFurnitureItem();
            return;
        }

        if (IsCheckoutItem)
        {
            RefreshCheckoutItem();
            return;
        }

        RefreshFlowerItem();
    }

    public void SetNewMarker(bool isNew)
    {
        showNewMarker = isNew;
        RefreshNewMarker();
    }

    private void RefreshNewMarker()
    {
        if (newText == null) return;
        newText.text = "New!";
        newText.gameObject.SetActive(showNewMarker);
    }

    private void RefreshFlowerItem()
    {
        bool valid = arrivalItem != null && arrivalItem.flower != null;

        if (!valid)
        {
            ClearDisplay();
            return;
        }

        SetImage(FlowerSpriteLoader.GetSprite(arrivalItem.flower));

        if (nameText != null) nameText.text = arrivalItem.flower.flowerName;
        if (colorText != null) colorText.text = $"色：{arrivalItem.flower.color}";
        if (priceText != null) priceText.text = $"{arrivalItem.UnitPurchasePrice:N0}円";
        if (remainingText != null) remainingText.text = $"残り {arrivalItem.RemainingQuantity}";

        if (saleText != null)
            saleText.text = arrivalItem.discountPercent > 0
                ? $"SALE {arrivalItem.discountPercent}%OFF"
                : string.Empty;

        bool hasStock = arrivalItem.RemainingQuantity > 0;
        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = hasStock;
        }

        int bulkQuantity = GetBulkPurchaseQuantity();
        if (buyFiveButton != null)
        {
            buyFiveButton.gameObject.SetActive(true);
            buyFiveButton.interactable = bulkQuantity > 0;
        }

        if (buyFiveButtonText != null)
            buyFiveButtonText.text = bulkQuantity > 0 ? $"{bulkQuantity}本購入" : "5本購入";
    }

    private void RefreshCheckoutItem()
    {
        bool available = checkoutItemSystem != null
            && checkoutItem != null
            && checkoutItemSystem.HasTodayOffer
            && checkoutItemSystem.TodayOffer == checkoutItem;

        SetImage(checkoutItemSystem != null ? checkoutItemSystem.LoadSprite(checkoutItem) : null);

        if (nameText != null) nameText.text = checkoutItem.displayName;
        if (colorText != null) colorText.text = "レジ横商品";
        if (priceText != null) priceText.text = $"{checkoutItem.boxPurchasePrice:N0}円";
        if (remainingText != null) remainingText.text = $"{checkoutItem.boxQuantity}個";
        if (saleText != null) saleText.text = string.Empty;

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = available;
        }

        if (buyFiveButton != null)
            buyFiveButton.gameObject.SetActive(false);
    }

    private void RefreshFurnitureItem()
    {
        bool valid = furnitureSystem != null && furnitureItem != null;
        if (!valid)
        {
            ClearDisplay();
            return;
        }

        bool owned = furnitureSystem.IsOwned(furnitureItem.id);
        SetImage(furnitureSystem.LoadSprite(furnitureItem));

        if (nameText != null) nameText.text = furnitureItem.displayName;
        if (colorText != null) colorText.text = "家具";
        if (priceText != null) priceText.text = $"{furnitureItem.purchasePrice:N0}円";
        if (remainingText != null) remainingText.text = owned ? "購入済み" : "1点限り";
        if (saleText != null) saleText.text = BuildFurnitureEffectText(furnitureItem);

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = !owned;
        }

        if (buyFiveButton != null)
            buyFiveButton.gameObject.SetActive(false);
    }

    private static string BuildFurnitureEffectText(FurnitureData item)
    {
        if (item == null) return string.Empty;

        string text = string.Empty;
        AppendEffect(ref text, item.visitorBonusPercent, "来客率");
        AppendEffect(ref text, item.budgetBonusPercent, "予算");

        if (item.summerVisitorBonusPercent != 0f)
            AppendRaw(ref text, $"夏 来客率+{item.summerVisitorBonusPercent * 100f:0.#}%");

        if (item.rainyVisitorBonusPercent != 0f)
            AppendRaw(ref text, $"雨 来客率+{item.rainyVisitorBonusPercent * 100f:0.#}%");

        if (item.rainyBudgetBonusPercent != 0f)
            AppendRaw(ref text, $"雨 予算+{item.rainyBudgetBonusPercent * 100f:0.#}%");

        if (item.rainyVisitorPenaltyFloorPercent < 0f)
            AppendRaw(ref text, $"雨ペナルティ{item.rainyVisitorPenaltyFloorPercent * 100f:0.#}%まで軽減");

        return text;
    }

    private static void AppendEffect(ref string text, float value, string label)
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

    private void ClearDisplay()
    {
        SetImage(null);
        if (nameText != null) nameText.text = "商品なし";
        if (colorText != null) colorText.text = string.Empty;
        if (priceText != null) priceText.text = string.Empty;
        if (remainingText != null) remainingText.text = string.Empty;
        if (saleText != null) saleText.text = string.Empty;
        if (newText != null) newText.gameObject.SetActive(false);
        if (buyButton != null) buyButton.interactable = false;
        if (buyFiveButton != null) buyFiveButton.interactable = false;
    }

    private void SetImage(Sprite sprite)
    {
        if (flowerImage == null) return;
        flowerImage.sprite = sprite;
        flowerImage.enabled = sprite != null;
        flowerImage.preserveAspect = true;
        flowerImage.raycastTarget = false;
    }

    private int GetBulkPurchaseQuantity()
    {
        if (arrivalItem == null) return 0;
        return Mathf.Clamp(arrivalItem.RemainingQuantity, 0, 5);
    }

    private void HandleBuyClicked()
    {
        if (IsFurnitureItem)
        {
            if (furnitureSystem == null || furnitureItem == null || furnitureSystem.IsOwned(furnitureItem.id)) return;
            onBuyFurnitureRequested?.Invoke(furnitureItem);
            return;
        }

        if (IsCheckoutItem)
        {
            if (checkoutItemSystem == null || checkoutItem == null || !checkoutItemSystem.HasTodayOffer) return;
            onBuyCheckoutRequested?.Invoke(checkoutItem);
            return;
        }

        if (arrivalItem == null || arrivalItem.RemainingQuantity <= 0) return;
        onBuyOneRequested?.Invoke(arrivalItem);
    }

    private void HandleBuyFiveClicked()
    {
        if (IsCheckoutItem || IsFurnitureItem) return;

        int quantity = GetBulkPurchaseQuantity();
        if (arrivalItem == null || quantity <= 0) return;
        onBuyMultipleRequested?.Invoke(arrivalItem, quantity);
    }
}
