using System.Collections.Generic;
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
        {
            shopManager.OnStateChanged += RefreshHeader;
        }
    }

    private void OnDisable()
    {
        if (shopManager != null)
        {
            shopManager.OnStateChanged -= RefreshHeader;
        }
    }

    private void Start()
    {
        if (shopManager != null)
        {
            shopManager.SyncSupplierSystem();
        }

        if (generateArrivalsOnStart && supplierSystem != null)
        {
            supplierSystem.GenerateDailyArrivals();
        }

        RefreshAll();
    }

    /// <summary>
    /// 今日の入荷を作り直し、一覧を再表示します。
    /// デバッグ用や日付更新後の呼び出しにも使えます。
    /// </summary>
    [ContextMenu("今日の仕入れ画面を更新")]
    public void RegenerateTodayArrivals()
    {
        if (shopManager != null)
        {
            shopManager.SyncSupplierSystem();
        }

        if (supplierSystem != null)
        {
            supplierSystem.GenerateDailyArrivals();
        }

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

        foreach (SupplierSystem.ArrivalItem arrival in supplierSystem.TodayArrivals)
        {
            SupplierItemUI item = Instantiate(itemPrefab, itemContainer);
            item.Bind(arrival, TryBuyOne);
            spawnedItems.Add(item);
        }
    }

    /// <summary>
    /// 商品を1個購入します。
    /// 代金支払い → 入荷残数減少 → 在庫追加 の順に処理します。
    /// </summary>
    private void TryBuyOne(SupplierSystem.ArrivalItem arrival)
    {
        if (arrival == null || arrival.flower == null) return;
        if (shopManager == null || inventorySystem == null) return;
        if (arrival.RemainingQuantity <= 0) return;

        int price = arrival.UnitPurchasePrice;

        if (!shopManager.TryPurchaseFromSupplier(price))
        {
            Debug.Log($"所持金が足りないため、{arrival.flower.flowerName}を購入できませんでした。");
            return;
        }

        arrival.purchasedQuantity++;
        inventorySystem.AddFlower(arrival.flower, 1);

        Debug.Log($"{arrival.flower.flowerName}（{arrival.flower.color}）を1個仕入れました。{price}円");

        RefreshHeader();

        foreach (SupplierItemUI item in spawnedItems)
        {
            if (item != null) item.Refresh();
        }
    }
}
