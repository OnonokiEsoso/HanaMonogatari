using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 仕入れ画面全体を管理します。
/// 今日の入荷一覧を生成し、購入処理をShopManagerとInventorySystemへつなぎます。
/// </summary>
public class SupplierUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private SupplierSystem supplierSystem;
    [SerializeField] private InventorySystem inventorySystem;
    [Tooltip("仕入先キャラクターの吹き出し表示を担当するControllerを設定します。")]
    [SerializeField] private SupplierCommentController supplierCommentController;

    [Header("一覧表示")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private SupplierItemUI itemPrefab;

    [Header("ヘッダー表示")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text supplierLevelText;
    [SerializeField] private TMP_Text dateText;

    [Header("開始時")]
    [Tooltip("画面開始時に今日の入荷を自動生成します。")]
    [SerializeField] private bool generateArrivalsOnStart = true;

    private readonly List<SupplierItemUI> spawnedItems = new();

    private void OnEnable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged += RefreshHeader;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= RefreshHeader;
    }

    private void Start()
    {
        if (shopManager != null)
            shopManager.SyncSupplierSystem();

        if (generateArrivalsOnStart && supplierSystem != null)
            supplierSystem.GenerateDailyArrivals();

        RefreshAll();
    }

    [ContextMenu("今日の仕入れ画面を更新")]
    public void RegenerateTodayArrivals()
    {
        if (shopManager != null)
            shopManager.SyncSupplierSystem();

        if (supplierSystem != null)
            supplierSystem.GenerateDailyArrivals();

        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshHeader();
        RebuildItemList();
    }

    private void RefreshHeader()
    {
        if (shopManager == null) return;

        if (moneyText != null)
            moneyText.text = $"所持金：{shopManager.Money:N0}円";

        if (supplierLevelText != null)
            supplierLevelText.text = $"仕入先Lv.{shopManager.SupplierLevel}";

        if (dateText != null)
            dateText.text = $"{shopManager.GameYear}年目 {shopManager.CurrentMonth}月{shopManager.CurrentDay}日";
    }

    private void RebuildItemList()
    {
        foreach (SupplierItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (supplierSystem == null || itemContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("SupplierUI: SupplierSystem / ItemContainer / ItemPrefab のどれかが未設定です。");
            return;
        }

        foreach (SupplierSystem.ArrivalItem arrival in supplierSystem.TodayArrivals
                     .Where(a => a != null && a.flower != null)
                     .OrderBy(a => a.flower.sortOrder)
                     .ThenBy(a => a.flower.flowerName)
                     .ThenBy(a => a.flower.color))
        {
            SupplierItemUI item = Instantiate(itemPrefab, itemContainer);
            item.Bind(arrival, TryBuyOne, TryBuyMultiple);
            spawnedItems.Add(item);
        }
    }

    private void TryBuyOne(SupplierSystem.ArrivalItem arrival)
    {
        TryBuyMultiple(arrival, 1);
    }

    /// <summary>
    /// TryBuyMultiple（トライ・バイ・マルチプル）
    /// Multiple＝複数。
    /// 指定数をまとめて購入します。残数を超えず、1回の上限は5本です。
    /// </summary>
    private void TryBuyMultiple(SupplierSystem.ArrivalItem arrival, int requestedQuantity)
    {
        if (arrival == null || arrival.flower == null) return;
        if (shopManager == null || inventorySystem == null) return;
        if (arrival.RemainingQuantity <= 0) return;

        int quantity = Mathf.Clamp(requestedQuantity, 1, 5);
        quantity = Mathf.Min(quantity, arrival.RemainingQuantity);

        if (quantity <= 0) return;

        int totalPrice = arrival.UnitPurchasePrice * quantity;

        if (!shopManager.TryPurchaseFromSupplier(totalPrice))
        {
            Debug.Log($"所持金が足りないため、{arrival.flower.flowerName}を{quantity}個まとめて購入できませんでした。必要額：{totalPrice:N0}円");
            return;
        }

        arrival.purchasedQuantity += quantity;
        inventorySystem.AddFlower(arrival.flower, quantity);

        Debug.Log($"{arrival.flower.flowerName}（{arrival.flower.color}）を{quantity}個仕入れました。合計{totalPrice:N0}円");

        // 購入に成功した花について、仕入先キャラクターが一言しゃべります。
        if (supplierCommentController != null)
            supplierCommentController.ShowFlowerComment(arrival.flower);

        RefreshHeader();

        foreach (SupplierItemUI item in spawnedItems)
        {
            if (item != null)
                item.Refresh();
        }
    }
}
