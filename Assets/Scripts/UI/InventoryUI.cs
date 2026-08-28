using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫画面全体を管理します。
/// 通常在庫と作成済み花束を同じ一覧へ表示します。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("一覧表示")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private InventoryItemUI itemPrefab;

    [Header("ヘッダー表示")]
    [SerializeField] private TMP_Text totalStockText;
    [SerializeField] private TMP_Text emptyMessageText;

    private readonly List<InventoryItemUI> spawnedItems = new();

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

        if (itemContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("InventoryUI: ItemContainer / ItemPrefab のどちらかが未設定です。");
            return;
        }

        if (inventorySystem != null)
        {
            foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
            {
                if (batch == null || batch.flower == null || batch.quantity <= 0)
                    continue;

                InventoryItemUI item = Instantiate(itemPrefab, itemContainer);
                item.Bind(batch);
                spawnedItems.Add(item);
            }
        }

        if (bouquetSystem != null)
        {
            foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
            {
                if (bouquet == null) continue;

                InventoryItemUI item = Instantiate(itemPrefab, itemContainer);
                item.Bind(bouquet);
                spawnedItems.Add(item);
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
