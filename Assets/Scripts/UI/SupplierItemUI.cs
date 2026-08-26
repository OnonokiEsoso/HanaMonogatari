using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面の商品1種類分を表示するUI。
/// 商品名・色・価格・残り数を表示し、購入ボタンをSupplierUIへ通知します。
/// </summary>
public class SupplierItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private TMP_Text saleText;

    [Header("操作")]
    [SerializeField] private Button buyButton;

    private SupplierSystem.ArrivalItem arrivalItem;
    private Action<SupplierSystem.ArrivalItem> onBuyRequested;

    private void Awake()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(HandleBuyClicked);
        }
    }

    /// <summary>
    /// Bind（バインド）= UIと実際の商品データを結び付けます。
    /// </summary>
    public void Bind(SupplierSystem.ArrivalItem item, Action<SupplierSystem.ArrivalItem> buyCallback)
    {
        arrivalItem = item;
        onBuyRequested = buyCallback;
        Refresh();
    }

    /// <summary>
    /// 現在の商品状態をUI表示へ反映します。
    /// </summary>
    public void Refresh()
    {
        bool valid = arrivalItem != null && arrivalItem.flower != null;

        if (!valid)
        {
            if (nameText != null) nameText.text = "商品なし";
            if (colorText != null) colorText.text = string.Empty;
            if (priceText != null) priceText.text = string.Empty;
            if (remainingText != null) remainingText.text = string.Empty;
            if (saleText != null) saleText.text = string.Empty;
            if (buyButton != null) buyButton.interactable = false;
            return;
        }

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

        if (buyButton != null)
        {
            buyButton.interactable = arrivalItem.RemainingQuantity > 0;
        }
    }

    private void HandleBuyClicked()
    {
        if (arrivalItem == null || arrivalItem.RemainingQuantity <= 0) return;
        onBuyRequested?.Invoke(arrivalItem);
    }
}
