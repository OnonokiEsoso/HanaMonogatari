using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫画面全体を管理します。
/// InventorySystemのロット一覧をUIへ展開し、在庫変化時に自動更新します。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;

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

        // タブが非表示中に仕入れが行われても、
        // 在庫タブを開いた瞬間に最新状態を必ず表示する。
        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventorySystem != null)
        {
            inventorySystem.OnInventoryChanged -= RefreshAll;
        }
    }

    /// <summary>
    /// Refresh（リフレッシュ）＝更新する。
    /// 在庫数表示と一覧を現在のInventorySystemに合わせて更新します。
    /// </summary>
    [ContextMenu("在庫画面を更新")]
    public void RefreshAll()
    {
        RefreshHeader();
        RebuildItemList();
    }

    private void RefreshHeader()
    {
        if (inventorySystem == null)
        {
            if (totalStockText != null) totalStockText.text = "在庫：0個";
            if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(true);
            return;
        }

        int totalQuantity = 0;
        foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
        {
            if (batch != null)
                totalQuantity += Mathf.Max(0, batch.quantity);
        }

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

        if (inventorySystem == null || itemContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("InventoryUI: InventorySystem / ItemContainer / ItemPrefab のどれかが未設定です。");
            return;
        }

        foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
        {
            if (batch == null || batch.flower == null || batch.quantity <= 0)
                continue;

            InventoryItemUI item = Instantiate(itemPrefab, itemContainer);
            item.Bind(batch);
            spawnedItems.Add(item);
        }

        ForceRebuildLayout();
    }

    /// <summary>
    /// ForceRebuildLayout（フォース・リビルド・レイアウト）
    /// Force＝強制、Rebuild＝作り直す、Layout＝配置。
    /// 商品カード追加後にVertical Layout Groupへ再計算を命令します。
    /// </summary>
    private void ForceRebuildLayout()
    {
        if (itemContainer is not RectTransform rectTransform) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}
