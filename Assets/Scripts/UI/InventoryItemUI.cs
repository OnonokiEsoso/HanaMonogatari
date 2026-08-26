using TMPro;
using UnityEngine;

/// <summary>
/// 在庫一覧の1行分を表示します。
/// 1つの在庫ロット（商品・数量・残り鮮度）をUIへ反映します。
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

    /// <summary>
    /// Bind（バインド）＝結び付ける。
    /// このUI行と在庫ロットを結び付けて表示します。
    /// </summary>
    public void Bind(InventorySystem.InventoryBatch inventoryBatch)
    {
        batch = inventoryBatch;
        Refresh();
    }

    /// <summary>
    /// 現在の在庫ロット情報を画面へ再反映します。
    /// </summary>
    public void Refresh()
    {
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
