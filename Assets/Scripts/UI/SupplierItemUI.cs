using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面の商品1種類分を表示する共通UI。
/// 花とレジ横商品BOXの両方を同じPrefabで表示します。
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
    private bool showNewMarker;

    private Action<SupplierSystem.ArrivalItem> onBuyOneRequested;
    private Action<SupplierSystem.ArrivalItem, int> onBuyMultipleRequested;
    private Action<CheckoutItemSystem.CheckoutItemDefinition> onBuyCheckoutRequested;

    private bool IsCheckoutItem => checkoutItem != null;

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
        showNewMarker = isNew;
        onBuyOneRequested = buyOneCallback;
        onBuyMultipleRequested = buyMultipleCallback;
        onBuyCheckoutRequested = null;
        Refresh();
    }

    /// <summary>
    /// レジ横商品のBOXを、花と同じ仕入れPrefabへ表示します。
    /// 1クリックで1BOXだけ購入するため、まとめ買いボタンは非表示にします。
    /// </summary>
    public void BindCheckout(
        CheckoutItemSystem system,
        CheckoutItemSystem.CheckoutItemDefinition item,
        Action<CheckoutItemSystem.CheckoutItemDefinition> buyCallback,
        bool isNew)
    {
        arrivalItem = null;
        checkoutItemSystem = system;
        checkoutItem = item;
        showNewMarker = isNew;
        onBuyOneRequested = null;
        onBuyMultipleRequested = null;
        onBuyCheckoutRequested = buyCallback;
        Refresh();
    }

    public void Refresh()
    {
        RefreshNewMarker();

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

        Sprite sprite = FlowerSpriteLoader.GetSprite(arrivalItem.flower);
        SetImage(sprite);

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

        // レジ横商品は1BOXが最小単位なので花の「5本購入」は使いません。
        if (buyFiveButton != null)
            buyFiveButton.gameObject.SetActive(false);
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
        if (IsCheckoutItem) return;

        int quantity = GetBulkPurchaseQuantity();
        if (arrivalItem == null || quantity <= 0) return;
        onBuyMultipleRequested?.Invoke(arrivalItem, quantity);
    }
}
