using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仕入れ画面全体を管理します。
/// 今日の花・ラッピング・レジ横商品の入荷を生成し、購入処理を各システムへつなぎます。
/// 花とレジ横商品は同じItemContainer・同じSupplierItemUI Prefabに表示します。
/// </summary>
public class SupplierUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private SupplierSystem supplierSystem;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private BouquetSystem bouquetSystem;
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;
    [Tooltip("仕入先キャラクターの吹き出し表示を担当するControllerを設定します。")]
    [SerializeField] private SupplierCommentController supplierCommentController;

    [Header("一覧表示")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private SupplierItemUI itemPrefab;

    [Header("ヘッダー表示")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text supplierLevelText;
    [SerializeField] private TMP_Text dateText;

    [Header("ラッピング販売UI（任意）")]
    [Tooltip("ラッピングが販売される日にだけ表示する親オブジェクト。")]
    [SerializeField] private GameObject wrappingOfferRoot;
    [SerializeField] private TMP_Text wrappingOfferText;
    [SerializeField] private Button wrappingBuyButton;

    [Header("開始時")]
    [Tooltip("画面開始時に今日の入荷を自動生成します。")]
    [SerializeField] private bool generateArrivalsOnStart = true;

    private readonly List<SupplierItemUI> spawnedItems = new();

    private void Awake()
    {
        if (wrappingBuyButton != null)
            wrappingBuyButton.onClick.AddListener(TryBuyWrapping);
    }

    private void OnEnable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged += RefreshHeader;

        if (checkoutItemSystem != null)
            checkoutItemSystem.OnChanged += RefreshAll;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= RefreshHeader;

        if (checkoutItemSystem != null)
            checkoutItemSystem.OnChanged -= RefreshAll;
    }

    private void OnDestroy()
    {
        if (wrappingBuyButton != null)
            wrappingBuyButton.onClick.RemoveListener(TryBuyWrapping);
    }

    private void Start()
    {
        if (shopManager != null)
            shopManager.SyncSupplierSystem();

        if (generateArrivalsOnStart && supplierSystem != null)
            supplierSystem.GenerateDailyArrivals();

        if (generateArrivalsOnStart && checkoutItemSystem != null)
            checkoutItemSystem.GenerateDailyOffer();

        if (supplierCommentController != null)
            supplierCommentController.ShowDefaultMessage(shopManager);

        RefreshAll();
    }

    [ContextMenu("今日の仕入れ画面を更新")]
    public void RegenerateTodayArrivals()
    {
        if (shopManager != null)
            shopManager.SyncSupplierSystem();

        if (supplierSystem != null)
            supplierSystem.GenerateDailyArrivals();

        if (checkoutItemSystem != null)
            checkoutItemSystem.GenerateDailyOffer();

        if (supplierCommentController != null)
            supplierCommentController.ShowDefaultMessage(shopManager);

        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshHeader();
        RebuildItemList();
        RefreshWrappingOffer();
    }

    private void RefreshHeader()
    {
        if (shopManager == null) return;

        if (moneyText != null)
            moneyText.text = $"所持金：{shopManager.Money:N0}円";

        if (supplierLevelText != null)
            supplierLevelText.text = $"仕入先Lv.{shopManager.SupplierLevel}";

        if (dateText != null)
            dateText.text = shopManager.DateDisplayText;
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
            string productKey = GetFlowerProductKey(arrival.flower);
            bool isNew = shopManager == null || !shopManager.HasPurchasedSupplierProduct(productKey);

            SupplierItemUI item = Instantiate(itemPrefab, itemContainer);
            item.Bind(arrival, TryBuyOne, TryBuyMultiple, isNew);
            spawnedItems.Add(item);
        }

        // レジ横商品も花と同じ一覧・同じPrefabに1商品として追加します。
        if (checkoutItemSystem != null && checkoutItemSystem.HasTodayOffer)
        {
            CheckoutItemSystem.CheckoutItemDefinition offer = checkoutItemSystem.TodayOffer;
            if (offer != null)
            {
                string productKey = GetCheckoutProductKey(offer);
                bool isNew = shopManager == null || !shopManager.HasPurchasedSupplierProduct(productKey);

                SupplierItemUI item = Instantiate(itemPrefab, itemContainer);
                item.BindCheckout(checkoutItemSystem, offer, TryBuyCheckoutOffer, isNew);
                spawnedItems.Add(item);
            }
        }
    }

    private void TryBuyOne(SupplierSystem.ArrivalItem arrival)
    {
        TryBuyMultiple(arrival, 1);
    }

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
        shopManager.RegisterSupplierProductPurchase(GetFlowerProductKey(arrival.flower));
        bool gotWrappingBonus = shopManager.RegisterSupplierFlowerPurchase(quantity);

        Debug.Log($"{arrival.flower.flowerName}（{arrival.flower.color}）を{quantity}個仕入れました。合計{totalPrice:N0}円");
        if (gotWrappingBonus)
            Debug.Log("仕入先からラッピングのおまけをもらいました！");

        if (supplierCommentController != null)
            supplierCommentController.ShowFlowerComment(arrival.flower);

        RefreshHeader();
        RebuildItemList();
    }

    private void TryBuyCheckoutOffer(CheckoutItemSystem.CheckoutItemDefinition item)
    {
        if (checkoutItemSystem == null || item == null) return;

        if (!checkoutItemSystem.TryBuyTodayOffer())
        {
            Debug.Log($"{item.displayName}のBOXを購入できませんでした。必要額：{item.boxPurchasePrice:N0}円");
            return;
        }

        if (shopManager != null)
            shopManager.RegisterSupplierProductPurchase(GetCheckoutProductKey(item));

        Debug.Log($"{item.displayName} ×{item.boxQuantity}を{item.boxPurchasePrice:N0}円で仕入れました。");
        RefreshHeader();
        RebuildItemList();
    }

    private static string GetFlowerProductKey(FlowerData flower)
    {
        return flower != null ? $"flower:{flower.name}" : string.Empty;
    }

    private static string GetCheckoutProductKey(CheckoutItemSystem.CheckoutItemDefinition item)
    {
        return item != null ? $"checkout:{item.id}" : string.Empty;
    }

    private void TryBuyWrapping()
    {
        if (supplierSystem == null || shopManager == null || bouquetSystem == null) return;
        if (!supplierSystem.WrappingAvailableToday || supplierSystem.WrappingRemainingToday <= 0) return;

        int price = supplierSystem.WrappingUnitPrice;
        if (!shopManager.TrySpendMoney(price))
        {
            Debug.Log($"ラッピングを購入する所持金が足りません。必要額：{price:N0}円");
            return;
        }

        if (!supplierSystem.TryPurchaseWrapping(1))
        {
            shopManager.AddMoney(price);
            return;
        }

        bouquetSystem.AddWrapping(1);
        Debug.Log($"ラッピングを1個購入しました。{price:N0}円");
        RefreshHeader();
        RefreshWrappingOffer();
    }

    private void RefreshWrappingOffer()
    {
        if (supplierSystem == null)
        {
            if (wrappingOfferRoot != null)
                wrappingOfferRoot.SetActive(false);
            return;
        }

        bool visible = supplierSystem.WrappingAvailableToday && supplierSystem.WrappingRemainingToday > 0;

        if (wrappingOfferRoot != null)
            wrappingOfferRoot.SetActive(visible);

        if (wrappingOfferText != null)
            wrappingOfferText.text = visible
                ? $"ラッピング　{supplierSystem.WrappingUnitPrice:N0}円　残り{supplierSystem.WrappingRemainingToday}個"
                : string.Empty;

        if (wrappingBuyButton != null)
            wrappingBuyButton.interactable = visible && shopManager != null && shopManager.Money >= supplierSystem.WrappingUnitPrice;
    }
}
