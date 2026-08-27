using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 花束作成画面で、1種類の商品と使用本数を表示・変更するUIです。
/// </summary>
public class BouquetIngredientItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text quantityText;

    [Header("ボタン")]
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    private FlowerData flower;
    private int stock;
    private int selectedQuantity;
    private Action<BouquetIngredientItemUI> onChanged;

    public FlowerData Flower => flower;
    public int SelectedQuantity => selectedQuantity;

    private void Awake()
    {
        if (minusButton != null)
            minusButton.onClick.AddListener(Decrease);

        if (plusButton != null)
            plusButton.onClick.AddListener(Increase);
    }

    private void OnDestroy()
    {
        if (minusButton != null)
            minusButton.onClick.RemoveListener(Decrease);

        if (plusButton != null)
            plusButton.onClick.RemoveListener(Increase);
    }

    /// <summary>
    /// Bind（バインド）＝このUIに商品データを結び付ける。
    /// </summary>
    public void Bind(FlowerData flower, int stock, Action<BouquetIngredientItemUI> onChanged)
    {
        this.flower = flower;
        this.stock = Mathf.Max(0, stock);
        this.onChanged = onChanged;
        selectedQuantity = 0;
        Refresh();
    }

    public void RefreshStock(int newStock)
    {
        stock = Mathf.Max(0, newStock);
        selectedQuantity = Mathf.Clamp(selectedQuantity, 0, stock);
        Refresh();
    }

    public void ResetQuantity()
    {
        selectedQuantity = 0;
        Refresh();
    }

    private void Increase()
    {
        if (selectedQuantity >= stock) return;
        selectedQuantity++;
        Refresh();
        onChanged?.Invoke(this);
    }

    private void Decrease()
    {
        if (selectedQuantity <= 0) return;
        selectedQuantity--;
        Refresh();
        onChanged?.Invoke(this);
    }

    private void Refresh()
    {
        if (nameText != null)
            nameText.text = flower != null ? flower.flowerName : "-";

        if (colorText != null)
            colorText.text = flower != null ? flower.color : "-";

        if (stockText != null)
            stockText.text = $"在庫 {stock}";

        if (quantityText != null)
            quantityText.text = selectedQuantity.ToString();

        if (minusButton != null)
            minusButton.interactable = selectedQuantity > 0;

        if (plusButton != null)
            plusButton.interactable = selectedQuantity < stock;
    }
}
