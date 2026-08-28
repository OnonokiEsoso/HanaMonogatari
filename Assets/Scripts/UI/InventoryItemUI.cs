using TMPro;
using UnityEngine;

/// <summary>
/// 在庫一覧の1行分を表示します。
/// 通常商品と作成済み花束の両方を表示できます。
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
    private BouquetSystem.BouquetData bouquet;

    public void Bind(InventorySystem.InventoryBatch inventoryBatch)
    {
        batch = inventoryBatch;
        bouquet = null;
        Refresh();
    }

    public void Bind(BouquetSystem.BouquetData bouquetData)
    {
        bouquet = bouquetData;
        batch = null;
        Refresh();
    }

    public void Refresh()
    {
        if (bouquet != null)
        {
            gameObject.SetActive(true);

            if (nameText != null)
                nameText.text = bouquet.bouquetName;

            if (colorText != null)
                colorText.text = "花束";

            if (quantityText != null)
                quantityText.text = "×1";

            if (freshnessText != null)
                freshnessText.text = $"構成 {bouquet.DistinctFlowerCount}種類 / {bouquet.TotalQuantity}本";

            if (purchasePriceText != null)
                purchasePriceText.text = $"原価 {bouquet.MaterialCost:N0}円";

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
            purchasePriceText.text = $"仕入 {batch.flower.purchasePrice:N0}円";
    }
}
