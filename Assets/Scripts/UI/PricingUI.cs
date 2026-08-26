using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 在庫にある商品を種類ごとにまとめて、販売価格を設定する画面です。
/// </summary>
public class PricingUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private PricingSystem pricingSystem;

    [Header("一覧表示")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private PricingItemUI itemPrefab;

    [Header("表示")]
    [SerializeField] private TMP_Text emptyMessageText;

    private readonly List<PricingItemUI> spawnedItems = new();

    private void OnEnable()
    {
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged += RefreshAll;

        if (pricingSystem != null)
            pricingSystem.OnPricingChanged += RefreshAll;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= RefreshAll;

        if (pricingSystem != null)
            pricingSystem.OnPricingChanged -= RefreshAll;
    }

    /// <summary>
    /// RefreshAll（リフレッシュ・オール）＝全部更新する。
    /// 現在庫と現在の販売価格に合わせて一覧を作り直します。
    /// </summary>
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

        if (inventorySystem == null || pricingSystem == null || itemContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("PricingUI: InventorySystem / PricingSystem / ItemContainer / ItemPrefab のどれかが未設定です。");
            return;
        }

        var stocks = inventorySystem.Batches
            .Where(b => b != null && b.flower != null && b.quantity > 0)
            .GroupBy(b => b.flower)
            .Select(g => new
            {
                flower = g.Key,
                quantity = g.Sum(b => b.quantity)
            })
            .OrderBy(x => x.flower.flowerName)
            .ThenBy(x => x.flower.color)
            .ToList();

        bool isEmpty = stocks.Count == 0;
        if (emptyMessageText != null)
        {
            emptyMessageText.gameObject.SetActive(isEmpty);
            if (isEmpty)
                emptyMessageText.text = "値付けできる在庫がありません";
        }

        foreach (var stock in stocks)
        {
            PricingItemUI item = Instantiate(itemPrefab, itemContainer);
            item.Bind(stock.flower, stock.quantity, pricingSystem, ApplyPrice);
            spawnedItems.Add(item);
        }
    }

    private void ApplyPrice(FlowerData flower, int price)
    {
        if (pricingSystem == null) return;
        pricingSystem.SetSalePrice(flower, price);
    }
}
