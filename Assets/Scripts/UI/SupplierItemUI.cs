using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面の商品1種類分を表示するUI。
/// 商品名・色・価格・残り数・花画像を表示し、1本購入／最大5本購入をSupplierUIへ通知します。
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

    [Header("操作")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button buyFiveButton;
    [SerializeField] private TMP_Text buyFiveButtonText;

    private SupplierSystem.ArrivalItem arrivalItem;
    private Action<SupplierSystem.ArrivalItem> onBuyOneRequested;
    private Action<SupplierSystem.ArrivalItem, int> onBuyMultipleRequested;

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

    /// <summary>
    /// Bind（バインド）= UIと実際の商品データを結び付けます。
    /// </summary>
    public void Bind(
        SupplierSystem.ArrivalItem item,
        Action<SupplierSystem.ArrivalItem> buyOneCallback,
        Action<SupplierSystem.ArrivalItem, int> buyMultipleCallback)
    {
        arrivalItem = item;
        onBuyOneRequested = buyOneCallback;
        onBuyMultipleRequested = buyMultipleCallback;
        Refresh();
    }

    /// <summary>
    /// 現在の商品状態をUI表示へ反映します。
    /// 花画像は FlowerData の花名＋色から自動取得します。
    /// まとめ買いボタンは残数に応じて「5本購入」～「1本購入」へ自動変更します。
    /// </summary>
    public void Refresh()
    {
        bool valid = arrivalItem != null && arrivalItem.flower != null;

        if (!valid)
        {
            if (flowerImage != null)
            {
                flowerImage.sprite = null;
                flowerImage.enabled = false;
            }

            if (nameText != null) nameText.text = "商品なし";
            if (colorText != null) colorText.text = string.Empty;
            if (priceText != null) priceText.text = string.Empty;
            if (remainingText != null) remainingText.text = string.Empty;
            if (saleText != null) saleText.text = string.Empty;
            if (buyButton != null) buyButton.interactable = false;
            if (buyFiveButton != null) buyFiveButton.interactable = false;
            if (buyFiveButtonText != null) buyFiveButtonText.text = "5本購入";
            return;
        }

        RefreshFlowerImage();

        if (nameText != null) nameText.text = arrivalItem.flower.flowerName;
        if (colorText != null) colorText.text = $"色：{arrivalItem.flower.color}";
        if (priceText != null) priceText.text = $"{arrivalItem.UnitPurchasePrice:N0}円";
        if (remainingText != null) remainingText.text = $"残り {arrivalItem.RemainingQuantity}";

        if (saleText != null)
        {
            saleText.text = arrivalItem.discountPercent > 0
                ? $"SALE {arrivalItem.discountPercent}%OFF"
                : string.Empty;
        }

        bool hasStock = arrivalItem.RemainingQuantity > 0;

        if (buyButton != null)
            buyButton.interactable = hasStock;

        int bulkQuantity = GetBulkPurchaseQuantity();

        if (buyFiveButton != null)
            buyFiveButton.interactable = bulkQuantity > 0;

        if (buyFiveButtonText != null)
            buyFiveButtonText.text = bulkQuantity > 0 ? $"{bulkQuantity}本購入" : "5本購入";
    }

    private void RefreshFlowerImage()
    {
        if (flowerImage == null) return;

        Sprite sprite = FlowerSpriteLoader.GetSprite(arrivalItem.flower);
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
        if (arrivalItem == null || arrivalItem.RemainingQuantity <= 0) return;
        onBuyOneRequested?.Invoke(arrivalItem);
    }

    /// <summary>
    /// HandleBuyFiveClicked（ハンドル・バイ・ファイブ・クリックド）
    /// 残数5本以上なら5本、4本以下なら残っている本数をまとめて購入します。
    /// </summary>
    private void HandleBuyFiveClicked()
    {
        int quantity = GetBulkPurchaseQuantity();
        if (arrivalItem == null || quantity <= 0) return;
        onBuyMultipleRequested?.Invoke(arrivalItem, quantity);
    }
}
