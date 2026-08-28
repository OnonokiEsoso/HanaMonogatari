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

    public void Bind(InventorySystem.InventoryBatch inventoryBatch)
    {
        batch = inventoryBatch;
        bouquetComponent = null;
        showPurchasePrice = true;
        Refresh();
    }

    /// <summary>
    /// 花束の展開表示用。
    /// 花束材料を通常の花カードと同じ見た目で表示します。
    /// </summary>
    public void Bind(BouquetSystem.BouquetComponent component, bool showPrice)
    {
        bouquetComponent = component;
        batch = null;
        showPurchasePrice = showPrice;
        Refresh();
    }

    public void Refresh()
    {
        if (bouquetComponent != null && bouquetComponent.flower != null)
        {
            gameObject.SetActive(true);

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
                purchasePriceText.gameObject.SetActive(showPurchasePrice);
                if (showPurchasePrice)
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
}
