using TMPro;
using UnityEngine;

/// <summary>
/// 在庫一覧の1行分を表示します。
/// 通常在庫ロットと、花束の中身表示用の材料カードに使います。
/// </summary>
public class InventoryItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text freshnessText;
    [SerializeField] private TMP_Text purchasePriceText;

    private InventorySystem.InventoryBatch batch;
    private BouquetSystem.BouquetComponent bouquetComponent;
    private bool showPurchasePrice = true;
    private bool showTexts = true;

    public void Bind(InventorySystem.InventoryBatch inventoryBatch)
    {
        batch = inventoryBatch;
        bouquetComponent = null;
        showPurchasePrice = true;
        showTexts = true;
        Refresh();
    }

    /// <summary>
    /// 花束の展開表示用。
    /// 花束材料を通常の花カードと同じ見た目で表示します。
    /// </summary>
    public void Bind(BouquetSystem.BouquetComponent component, bool showPrice)
    {
        Bind(component, showPrice, true);
    }

    /// <summary>
    /// 花束材料カードの表示方法を指定して結び付けます。
    /// showTexts=falseなら、閉じた花束プレビュー用に背景だけ表示します。
    /// </summary>
    public void Bind(BouquetSystem.BouquetComponent component, bool showPrice, bool showTexts)
    {
        bouquetComponent = component;
        batch = null;
        showPurchasePrice = showPrice;
        this.showTexts = showTexts;
        Refresh();
    }

    public void Refresh()
    {
        if (bouquetComponent != null && bouquetComponent.flower != null)
        {
            gameObject.SetActive(true);

            SetTextObjectActive(nameText, showTexts);
            SetTextObjectActive(colorText, showTexts);
            SetTextObjectActive(quantityText, showTexts);
            SetTextObjectActive(freshnessText, showTexts);

            if (nameText != null)
                nameText.text = bouquetComponent.flower.flowerName;

            if (colorText != null)
                colorText.text = bouquetComponent.flower.color;

            if (quantityText != null)
                quantityText.text = $"×{bouquetComponent.quantity}";

            if (freshnessText != null)
                freshnessText.text = $"鮮度 残り{bouquetComponent.remainingFreshnessDays}日";

            if (purchasePriceText != null)
            {
                bool shouldShowPrice = showTexts && showPurchasePrice;
                purchasePriceText.gameObject.SetActive(shouldShowPrice);
                if (shouldShowPrice)
                    purchasePriceText.text = $"仕入 {bouquetComponent.flower.purchasePrice:N0}円";
            }

            return;
        }

        if (batch == null || batch.flower == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        showTexts = true;

        SetTextObjectActive(nameText, true);
        SetTextObjectActive(colorText, true);
        SetTextObjectActive(quantityText, true);
        SetTextObjectActive(freshnessText, true);

        if (nameText != null)
            nameText.text = batch.flower.flowerName;

        if (colorText != null)
            colorText.text = batch.flower.color;

        if (quantityText != null)
            quantityText.text = $"×{batch.quantity}";

        if (freshnessText != null)
            freshnessText.text = $"鮮度 残り{batch.remainingFreshnessDays}日";

        if (purchasePriceText != null)
        {
            purchasePriceText.gameObject.SetActive(true);
            purchasePriceText.text = $"仕入 {batch.flower.purchasePrice:N0}円";
        }
    }

    private static void SetTextObjectActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }
}
