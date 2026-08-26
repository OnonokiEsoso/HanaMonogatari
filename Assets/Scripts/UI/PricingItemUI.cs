using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 値付け画面の商品1種類分を表示・編集します。
/// </summary>
public class PricingItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text purchasePriceText;

    [Header("入力")]
    [SerializeField] private TMP_InputField salePriceInput;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button recommendedButton;

    private FlowerData flower;
    private int totalStock;
    private PricingSystem pricingSystem;
    private Action<FlowerData, int> onApply;

    private void Awake()
    {
        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyPrice);

        if (recommendedButton != null)
            recommendedButton.onClick.AddListener(UseRecommendedPrice);
    }

    private void OnDestroy()
    {
        if (applyButton != null)
            applyButton.onClick.RemoveListener(ApplyPrice);

        if (recommendedButton != null)
            recommendedButton.onClick.RemoveListener(UseRecommendedPrice);
    }

    /// <summary>
    /// Bind（バインド）＝結び付ける。
    /// このUIカードと、対象の商品・在庫数・PricingSystemを結び付けます。
    /// </summary>
    public void Bind(
        FlowerData flower,
        int totalStock,
        PricingSystem pricingSystem,
        Action<FlowerData, int> onApply)
    {
        this.flower = flower;
        this.totalStock = totalStock;
        this.pricingSystem = pricingSystem;
        this.onApply = onApply;

        Refresh();
    }

    public void Refresh()
    {
        if (flower == null || pricingSystem == null) return;

        int recommendedPrice = pricingSystem.GetRecommendedPrice(flower);
        int currentPrice = pricingSystem.GetSalePrice(flower);

        if (nameText != null)
            nameText.text = flower.flowerName;

        if (colorText != null)
            colorText.text = flower.color;

        if (stockText != null)
            stockText.text = $"×{totalStock}";

        if (purchasePriceText != null)
            purchasePriceText.text = $"仕入:{flower.purchasePrice:N0}円";

        if (salePriceInput != null)
        {
            salePriceInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            // おすすめ価格はInputField内の薄いPlaceholderとして表示する。
            if (salePriceInput.placeholder is TMP_Text placeholderText)
            {
                placeholderText.text = $"おすすめ {recommendedPrice:N0}円";
            }

            // まだ価格を設定していない場合は空欄にして、Placeholderを見せる。
            // 既に設定済みなら現在価格を表示する。
            salePriceInput.text = pricingSystem.HasCustomPrice(flower)
                ? currentPrice.ToString()
                : string.Empty;
        }
    }

    private void ApplyPrice()
    {
        if (flower == null || salePriceInput == null) return;

        if (!int.TryParse(salePriceInput.text, out int price) || price <= 0)
        {
            Debug.LogWarning("販売価格には1円以上の整数を入力してください。");
            return;
        }

        onApply?.Invoke(flower, price);
    }

    private void UseRecommendedPrice()
    {
        if (flower == null || pricingSystem == null || salePriceInput == null) return;

        salePriceInput.text = pricingSystem.GetRecommendedPrice(flower).ToString();
        ApplyPrice();
    }
}
