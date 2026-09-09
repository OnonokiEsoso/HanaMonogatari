using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 値付け画面の商品1種類分を表示・編集します。
/// 通常商品と作成済み花束の両方を扱えます。
/// </summary>
public class PricingItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private Image flowerImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text purchasePriceText;

    [Header("入力")]
    [SerializeField] private TMP_InputField salePriceInput;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button recommendedButton;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button pasteButton;

    [Header("決定ボタン表示")]
    [Tooltip("入力値がまだ反映されていない時の決定ボタン色です。")]
    [SerializeField] private Color pendingApplyColor = new Color(0.44f, 0.68f, 0.86f, 1f);

    private static int? copiedSalePrice;
    private static event Action ClipboardChanged;

    private FlowerData flower;
    private BouquetSystem.BouquetData bouquet;
    private int totalStock;
    private PricingSystem pricingSystem;
    private BouquetSystem bouquetSystem;
    private Action<FlowerData, int> onFlowerApply;
    private Action<BouquetSystem.BouquetData, int> onBouquetApply;
    private SEManager seManager;
    private ColorBlock defaultApplyButtonColors;
    private string appliedPriceText = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClipboard()
    {
        copiedSalePrice = null;
        ClipboardChanged = null;
    }

    private void Awake()
    {
        seManager = FindFirstObjectByType<SEManager>();

        if (applyButton != null)
        {
            defaultApplyButtonColors = applyButton.colors;
            applyButton.onClick.AddListener(ApplyPrice);
        }

        if (recommendedButton != null)
            recommendedButton.onClick.AddListener(UseRecommendedPrice);

        if (copyButton != null)
            copyButton.onClick.AddListener(CopyPrice);

        if (pasteButton != null)
            pasteButton.onClick.AddListener(PastePrice);

        if (salePriceInput != null)
            salePriceInput.onValueChanged.AddListener(HandleSalePriceInputChanged);

        ClipboardChanged += RefreshPasteButton;
        RefreshPasteButton();
        RefreshApplyButtonColor();
    }

    private void OnDestroy()
    {
        if (applyButton != null)
            applyButton.onClick.RemoveListener(ApplyPrice);

        if (recommendedButton != null)
            recommendedButton.onClick.RemoveListener(UseRecommendedPrice);

        if (copyButton != null)
            copyButton.onClick.RemoveListener(CopyPrice);

        if (pasteButton != null)
            pasteButton.onClick.RemoveListener(PastePrice);

        if (salePriceInput != null)
            salePriceInput.onValueChanged.RemoveListener(HandleSalePriceInputChanged);

        ClipboardChanged -= RefreshPasteButton;
    }

    public void Bind(
        FlowerData flower,
        int totalStock,
        PricingSystem pricingSystem,
        Action<FlowerData, int> onApply)
    {
        this.flower = flower;
        bouquet = null;
        this.totalStock = totalStock;
        this.pricingSystem = pricingSystem;
        bouquetSystem = null;
        onFlowerApply = onApply;
        onBouquetApply = null;
        Refresh();
    }

    public void Bind(
        BouquetSystem.BouquetData bouquet,
        BouquetSystem bouquetSystem,
        Action<BouquetSystem.BouquetData, int> onApply)
    {
        flower = null;
        this.bouquet = bouquet;
        totalStock = 1;
        pricingSystem = null;
        this.bouquetSystem = bouquetSystem;
        onFlowerApply = null;
        onBouquetApply = onApply;
        Refresh();
    }

    public void Refresh()
    {
        RefreshPasteButton();

        if (bouquet != null && bouquetSystem != null)
        {
            RefreshFlowerImage(null);

            int recommendedPrice = bouquetSystem.GetRecommendedPrice(bouquet);

            if (nameText != null)
                nameText.text = bouquet.bouquetName;

            if (colorText != null)
                colorText.text = "花束";

            if (stockText != null)
                stockText.text = "×1";

            if (purchasePriceText != null)
                purchasePriceText.text = $"原価:{bouquet.MaterialCost:N0}円";

            if (salePriceInput != null)
            {
                salePriceInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                if (salePriceInput.placeholder is TMP_Text placeholderText)
                    placeholderText.text = $"おすすめ {recommendedPrice:N0}円";

                salePriceInput.text = bouquet.salePrice.ToString();
                appliedPriceText = salePriceInput.text;
            }

            RefreshApplyButtonColor();
            return;
        }

        if (flower == null || pricingSystem == null)
        {
            RefreshFlowerImage(null);
            appliedPriceText = string.Empty;
            RefreshApplyButtonColor();
            return;
        }

        RefreshFlowerImage(flower);

        int flowerRecommendedPrice = pricingSystem.GetRecommendedPrice(flower);
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

            if (salePriceInput.placeholder is TMP_Text placeholderText)
                placeholderText.text = $"おすすめ {flowerRecommendedPrice:N0}円";

            salePriceInput.text = pricingSystem.HasCustomPrice(flower)
                ? currentPrice.ToString()
                : string.Empty;
            appliedPriceText = salePriceInput.text;
        }

        RefreshApplyButtonColor();
    }

    private void RefreshFlowerImage(FlowerData targetFlower)
    {
        if (flowerImage == null) return;

        Sprite sprite = FlowerSpriteLoader.GetSprite(targetFlower);
        flowerImage.sprite = sprite;
        flowerImage.preserveAspect = true;
        flowerImage.raycastTarget = false;
        flowerImage.gameObject.SetActive(sprite != null);
    }

    private void ApplyPrice()
    {
        if (salePriceInput == null) return;

        if (!int.TryParse(salePriceInput.text, out int price) || price <= 0)
        {
            ResolveSEManager();
            seManager?.PlayError();
            Debug.LogWarning("販売価格には1円以上の整数を入力してください。");
            return;
        }

        if (bouquet != null)
            onBouquetApply?.Invoke(bouquet, price);
        else if (flower != null)
            onFlowerApply?.Invoke(flower, price);

        appliedPriceText = price.ToString();
        RefreshApplyButtonColor();

        ResolveSEManager();
        seManager?.PlayPriceChange();
    }

    private void UseRecommendedPrice()
    {
        if (salePriceInput == null) return;

        int recommendedPrice;

        if (bouquet != null && bouquetSystem != null)
            recommendedPrice = bouquetSystem.GetRecommendedPrice(bouquet);
        else if (flower != null && pricingSystem != null)
            recommendedPrice = pricingSystem.GetRecommendedPrice(flower);
        else
            return;

        salePriceInput.text = recommendedPrice.ToString();
        ApplyPrice();
    }

    private void CopyPrice()
    {
        if (salePriceInput == null || !int.TryParse(salePriceInput.text, out int price) || price <= 0)
        {
            ResolveSEManager();
            seManager?.PlayError();
            Debug.LogWarning("コピーする販売価格には1円以上の整数を設定してください。");
            return;
        }

        copiedSalePrice = price;
        ClipboardChanged?.Invoke();
    }

    private void PastePrice()
    {
        if (salePriceInput == null || !copiedSalePrice.HasValue)
            return;

        salePriceInput.text = copiedSalePrice.Value.ToString();
        ApplyPrice();
    }

    private void RefreshPasteButton()
    {
        if (pasteButton != null)
            pasteButton.interactable = copiedSalePrice.HasValue;
    }

    private void HandleSalePriceInputChanged(string value)
    {
        RefreshApplyButtonColor();
    }

    private void RefreshApplyButtonColor()
    {
        if (applyButton == null)
            return;

        bool hasPendingChange = salePriceInput != null && salePriceInput.text != appliedPriceText;
        if (!hasPendingChange)
        {
            applyButton.colors = defaultApplyButtonColors;
            return;
        }

        ColorBlock colors = defaultApplyButtonColors;
        colors.normalColor = pendingApplyColor;
        colors.highlightedColor = Color.Lerp(pendingApplyColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(pendingApplyColor, Color.black, 0.12f);
        colors.selectedColor = pendingApplyColor;
        applyButton.colors = colors;
    }

    private void ResolveSEManager()
    {
        if (seManager == null)
            seManager = FindFirstObjectByType<SEManager>();
    }
}
