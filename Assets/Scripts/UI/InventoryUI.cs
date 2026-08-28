using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫画面全体を管理します。
/// 通常商品は同一FlowerDataごとにまとめ、花束は専用の展開式InventoryBouquetItemで表示します。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("一覧表示")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private InventoryItemUI itemPrefab;
    [SerializeField] private InventoryFlowerGroupItemUI flowerGroupItemPrefab;
    [SerializeField] private InventoryBouquetItemUI bouquetItemPrefab;

    [Header("ヘッダー表示")]
    [SerializeField] private TMP_Text totalStockText;
    [SerializeField] private TMP_Text emptyMessageText;

    private readonly List<InventoryItemUI> spawnedItems = new();
    private readonly List<InventoryFlowerGroupItemUI> spawnedFlowerGroups = new();
    private readonly List<InventoryBouquetItemUI> spawnedBouquetItems = new();

    private void OnEnable()
    {
        if (inventorySystem != null)
        {
            inventorySystem.OnInventoryChanged -= RefreshAll;
            inventorySystem.OnInventoryChanged += RefreshAll;
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

        if (bouquetSystem != null)
            bouquetSystem.OnBouquetsChanged -= RefreshAll;
    }

    [ContextMenu("在庫画面を更新")]
    public void RefreshAll()
    {
        RefreshHeader();
        RebuildItemList();
    }

    private void RefreshHeader()
    {
        int totalQuantity = 0;

        if (inventorySystem != null)
        {
            foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
            {
                if (batch != null)
                    totalQuantity += Mathf.Max(0, batch.quantity);
            }
        }

        if (bouquetSystem != null)
            totalQuantity += bouquetSystem.Bouquets.Count;

        if (totalStockText != null)
            totalStockText.text = $"在庫：{totalQuantity}個";

        if (emptyMessageText != null)
        {
            bool isEmpty = totalQuantity <= 0;
            emptyMessageText.gameObject.SetActive(isEmpty);
            if (isEmpty) emptyMessageText.text = "在庫はありません";
        }
    }

    private void RebuildItemList()
    {
        foreach (InventoryItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        foreach (InventoryFlowerGroupItemUI item in spawnedFlowerGroups)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedFlowerGroups.Clear();

        foreach (InventoryBouquetItemUI item in spawnedBouquetItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedBouquetItems.Clear();

        if (itemContainer == null)
        {
            Debug.LogWarning("InventoryUI: ItemContainer が未設定です。");
            return;
        }

        if (inventorySystem != null)
        {
            var groups = inventorySystem.Batches
                .Where(b => b != null && b.flower != null && b.quantity > 0)
                .GroupBy(b => b.flower)
                .OrderBy(g => g.Key.sortOrder)
                .ThenBy(g => g.Key.flowerName)
                .ThenBy(g => g.Key.color)
                .ToList();

            foreach (var group in groups)
            {
                if (flowerGroupItemPrefab != null)
                {
                    InventoryFlowerGroupItemUI groupItem = Instantiate(flowerGroupItemPrefab, itemContainer);
                    groupItem.Bind(group.Key, group);
                    spawnedFlowerGroups.Add(groupItem);
                }
                else if (itemPrefab != null)
                {
                    foreach (InventorySystem.InventoryBatch batch in group.OrderBy(b => b.remainingFreshnessDays))
                    {
                        InventoryItemUI item = Instantiate(itemPrefab, itemContainer);
                        item.Bind(batch);
                        spawnedItems.Add(item);
                    }
                }
            }
        }

        if (bouquetSystem != null && bouquetItemPrefab != null)
        {
            foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
            {
                if (bouquet == null) continue;

                InventoryBouquetItemUI item = Instantiate(bouquetItemPrefab, itemContainer);
                item.Bind(bouquet, bouquetSystem, RefreshAll);
                spawnedBouquetItems.Add(item);
            }
        }

        ForceRebuildLayout();
    }

    private void ForceRebuildLayout()
    {
        if (itemContainer is not RectTransform rectTransform) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}
