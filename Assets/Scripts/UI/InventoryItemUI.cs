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
    private bool lotDetailMode;

    public void Bind(InventorySystem.InventoryBatch inventoryBatch)
    {
        batch = inventoryBatch;
        bouquetComponent = null;
        showPurchasePrice = true;
        showTexts = true;
        lotDetailMode = false;
        Refresh();
    }

    /// <summary>
    /// BindLotDetail（バインド・ロット・ディテール）
    /// 鮮度別の内訳行として「あと○日 ×○」だけを強調表示します。
    /// </summary>
    public void BindLotDetail(InventorySystem.InventoryBatch inventoryBatch)
    {
        batch = inventoryBatch;
        bouquetComponent = null;
        showPurchasePrice = false;
        showTexts = true;
        lotDetailMode = true;
        Refresh();
    }

    public void Bind(BouquetSystem.BouquetComponent component, bool showPrice)
    {
        Bind(component, showPrice, true);
    }

    public void Bind(BouquetSystem.BouquetComponent component, bool showPrice, bool showTexts)
    {
        bouquetComponent = component;
        batch = null;
        showPurchasePrice = showPrice;
        this.showTexts = showTexts;
        lotDetailMode = false;
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
                freshnessText.text = $"あと{bouquetComponent.OldestRemainingFreshnessDays}日";

            if (purchasePriceText != null)
            {
                bool shouldShowPrice = showTexts && showPurchasePrice;
                purchasePriceText.gameObject.SetActive(shouldShowPrice);
                if (shouldShowPrice)
                    purchasePriceText.text = $"{bouquetComponent.flower.purchasePrice:N0}円";
            }

            return;
        }

        if (batch == null || batch.flower == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (lotDetailMode)
        {
            SetTextObjectActive(nameText, false);
            SetTextObjectActive(colorText, false);
            SetTextObjectActive(quantityText, true);
            SetTextObjectActive(freshnessText, true);
            SetTextObjectActive(purchasePriceText, false);

            if (freshnessText != null)
                freshnessText.text = $"あと{batch.remainingFreshnessDays}日";

            if (quantityText != null)
                quantityText.text = $"×{batch.quantity}";

            return;
        }

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
            freshnessText.text = $"あと{batch.remainingFreshnessDays}日";

        if (purchasePriceText != null)
        {
            purchasePriceText.gameObject.SetActive(true);
            purchasePriceText.text = $"{batch.flower.purchasePrice:N0}円";
        }
    }

    private static void SetTextObjectActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }
}
