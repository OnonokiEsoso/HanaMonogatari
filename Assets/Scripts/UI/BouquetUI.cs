using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 花束作成画面全体を管理します。
/// 在庫から花束に使える商品を一覧表示し、使用本数・名前・販売価格を指定して花束を作成します。
/// 花束1個の作成にはラッピングを1個使用します。
/// 作成済み花束の確認・削除は在庫画面で行います。
/// </summary>
public class BouquetUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("材料一覧")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private BouquetIngredientItemUI itemPrefab;

    [Header("入力")]
    [SerializeField] private TMP_InputField bouquetNameInput;
    [SerializeField] private TMP_InputField salePriceInput;

    [Header("表示")]
    [SerializeField] private TMP_Text totalQuantityText;
    [SerializeField] private TMP_Text distinctCountText;
    [SerializeField] private TMP_Text currentRecommendedPriceText;
    [SerializeField] private TMP_Text wrappingCountText;
    [SerializeField] private TMP_Text resultText;

    [Header("操作")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button resetButton;

    private readonly List<BouquetIngredientItemUI> spawnedItems = new();

    private void Awake()
    {
        if (createButton != null)
            createButton.onClick.AddListener(CreateBouquet);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetSelection);
    }

    private void OnEnable()
    {
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged += RefreshAll;

        if (bouquetSystem != null)
            bouquetSystem.OnWrappingChanged += RefreshSummary;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= RefreshAll;

        if (bouquetSystem != null)
            bouquetSystem.OnWrappingChanged -= RefreshSummary;
    }

    private void OnDestroy()
    {
        if (createButton != null)
            createButton.onClick.RemoveListener(CreateBouquet);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetSelection);
    }

    [ContextMenu("花束画面を更新")]
    public void RefreshAll()
    {
        RebuildIngredientList();
        RefreshSummary();
    }

    private void RebuildIngredientList()
    {
        foreach (BouquetIngredientItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (inventorySystem == null || itemContainer == null || itemPrefab == null)
            return;

        var stocks = inventorySystem.Batches
            .Where(b => b != null && b.flower != null && b.quantity > 0 && b.flower.canUseInBouquet)
            .GroupBy(b => b.flower)
            .Select(g => new
            {
                flower = g.Key,
                quantity = g.Sum(b => b.quantity)
            })
            .OrderBy(x => x.flower.sortOrder)
            .ThenBy(x => x.flower.flowerName)
            .ThenBy(x => x.flower.color)
            .ToList();

        foreach (var stock in stocks)
        {
            BouquetIngredientItemUI item = Instantiate(itemPrefab, itemContainer);
            item.Bind(stock.flower, stock.quantity, OnIngredientChanged);
            spawnedItems.Add(item);
        }

        if (spawnedItems.Count == 0 && resultText != null)
            resultText.text = "花束に使える在庫がありません";
    }

    private void OnIngredientChanged(BouquetIngredientItemUI item)
    {
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        int total = spawnedItems.Sum(i => i != null ? i.SelectedQuantity : 0);
        int distinct = spawnedItems.Count(i => i != null && i.SelectedQuantity > 0);
        int materialCost = spawnedItems.Sum(i =>
            i != null && i.Flower != null
                ? i.Flower.purchasePrice * Mathf.Max(0, i.SelectedQuantity)
                : 0);
        int recommendedPrice = BouquetSystem.CalculateRecommendedPrice(materialCost, total);

        if (totalQuantityText != null)
            totalQuantityText.text = $"合計：{total}/{BouquetSystem.MaximumBouquetQuantity}本";

        if (distinctCountText != null)
            distinctCountText.text = $"種類：{distinct}/3以上";

        if (currentRecommendedPriceText != null)
            currentRecommendedPriceText.text = $"現在の適正価格：{recommendedPrice:N0}円";

        if (wrappingCountText != null)
        {
            int wrappingCount = bouquetSystem != null ? bouquetSystem.WrappingCount : 0;
            wrappingCountText.text = $"ラッピング：{wrappingCount}個";
        }

        if (createButton != null)
        {
            createButton.interactable =
                bouquetSystem != null &&
                bouquetSystem.CanCreateWithWrapping &&
                total >= BouquetSystem.MinimumBouquetQuantity &&
                total <= BouquetSystem.MaximumBouquetQuantity &&
                distinct >= 3;
        }
    }

    public void CreateBouquet()
    {
        if (bouquetSystem == null)
        {
            if (resultText != null)
                resultText.text = "BouquetSystemが設定されていません";
            return;
        }

        if (!bouquetSystem.CanCreateWithWrapping)
        {
            if (resultText != null)
                resultText.text = "ラッピングが足りません";
            return;
        }

        if (!int.TryParse(salePriceInput != null ? salePriceInput.text : string.Empty, out int salePrice))
        {
            if (resultText != null)
                resultText.text = "販売価格を数字で入力してください";
            return;
        }

        List<BouquetSystem.BouquetComponent> components = spawnedItems
            .Where(i => i != null && i.Flower != null && i.SelectedQuantity > 0)
            .OrderBy(i => i.Flower.sortOrder)
            .Select(i => new BouquetSystem.BouquetComponent
            {
                flower = i.Flower,
                quantity = i.SelectedQuantity
            })
            .ToList();

        string bouquetName = bouquetNameInput != null ? bouquetNameInput.text : string.Empty;

        bool success = bouquetSystem.TryCreateBouquet(
            bouquetName,
            salePrice,
            components,
            out string message);

        if (resultText != null)
            resultText.text = message;

        if (success)
        {
            if (bouquetNameInput != null)
                bouquetNameInput.text = string.Empty;

            if (salePriceInput != null)
                salePriceInput.text = string.Empty;

            RefreshAll();
        }
    }

    public void ResetSelection()
    {
        foreach (BouquetIngredientItemUI item in spawnedItems)
        {
            if (item != null)
                item.ResetQuantity();
        }

        if (resultText != null)
            resultText.text = string.Empty;

        RefreshSummary();
    }
}
