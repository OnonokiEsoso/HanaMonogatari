using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫にある通常商品と作成済み花束の販売価格を設定する画面です。
/// </summary>
public class PricingUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private PricingSystem pricingSystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("一覧表示")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private PricingItemUI itemPrefab;

    [Header("表示")]
    [SerializeField] private TMP_Text emptyMessageText;

    private readonly List<PricingItemUI> spawnedItems = new();

    private void OnEnable()
    {
        if (inventorySystem != null)
        {
            inventorySystem.OnInventoryChanged -= RefreshAll;
            inventorySystem.OnInventoryChanged += RefreshAll;
        }

        if (pricingSystem != null)
        {
            pricingSystem.OnPricingChanged -= RefreshAll;
            pricingSystem.OnPricingChanged += RefreshAll;
        }

        if (bouquetSystem != null)
        {
            bouquetSystem.OnBouquetsChanged -= RefreshAll;
            bouquetSystem.OnBouquetsChanged += RefreshAll;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= RefreshAll;

        if (pricingSystem != null)
            pricingSystem.OnPricingChanged -= RefreshAll;

        if (bouquetSystem != null)
            bouquetSystem.OnBouquetsChanged -= RefreshAll;
    }

    [ContextMenu("値付け画面を更新")]
    public void RefreshAll()
    {
        RebuildItemList();
    }

    private void RebuildItemList()
    {
        foreach (PricingItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (itemContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("PricingUI: ItemContainer / ItemPrefab のどちらかが未設定です。");
            return;
        }

        var stocks = inventorySystem != null
            ? inventorySystem.Batches
                .Where(b => b != null && b.flower != null && b.quantity > 0)
                .GroupBy(b => b.flower)
                .Select(g => new
                {
                    flower = g.Key,
                    quantity = g.Sum(b => b.quantity)
                })
                .OrderBy(x => x.flower.sortOrder)
                .ThenBy(x => x.flower.flowerName)
                .ThenBy(x => x.flower.color)
                .ToList()
            : null;

        if (stocks != null && pricingSystem != null)
        {
            foreach (var stock in stocks)
            {
                PricingItemUI item = Instantiate(itemPrefab, itemContainer);
                item.Bind(stock.flower, stock.quantity, pricingSystem, ApplyFlowerPrice);
                spawnedItems.Add(item);
            }
        }

        if (bouquetSystem != null)
        {
            foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
            {
                if (bouquet == null) continue;

                PricingItemUI item = Instantiate(itemPrefab, itemContainer);
                item.Bind(bouquet, bouquetSystem, ApplyBouquetPrice);
                spawnedItems.Add(item);
            }
        }

        bool isEmpty = spawnedItems.Count == 0;
        if (emptyMessageText != null)
        {
            emptyMessageText.gameObject.SetActive(isEmpty);
            if (isEmpty)
                emptyMessageText.text = "値付けできる在庫がありません";
        }

        ForceRebuildLayout();
    }

    private void ApplyFlowerPrice(FlowerData flower, int price)
    {
        if (pricingSystem == null) return;
        pricingSystem.SetSalePrice(flower, price);
    }

    private void ApplyBouquetPrice(BouquetSystem.BouquetData bouquet, int price)
    {
        if (bouquetSystem == null) return;
        bouquetSystem.SetSalePrice(bouquet, price);
    }

    private void ForceRebuildLayout()
    {
        if (itemContainer is not RectTransform rectTransform) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}
