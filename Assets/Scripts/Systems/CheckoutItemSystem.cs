using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// レジ横商品の仕入れ・在庫・設置・追加購入をまとめて管理します。
/// 最大3種類まで設置でき、営業時は花/花束購入後の残り予算で最大1個だけ追加購入されます。
/// </summary>
public class CheckoutItemSystem : MonoBehaviour
{
    public enum PurchaseCondition
    {
        FlowerOnly,
        FlowerOrBouquet
    }

    [Serializable]
    public class CheckoutItemDefinition
    {
        public string id;
        public string displayName;
        public int boxQuantity;
        public int boxPurchasePrice;
        public int regularSalePrice;
        public int arrivalDifficulty;
        [Range(0f, 1f)] public float purchaseChance;
        public PurchaseCondition purchaseCondition;
        public int targetMonth;
        public int offSeasonSalePrice = 100;
        [Range(0f, 1f)] public float offSeasonPurchaseChance = 0.03f;
        public string resourceSpriteName;

        public bool IsSeasonal => targetMonth >= 1 && targetMonth <= 12;

        public int GetSalePrice(int currentMonth)
        {
            if (IsSeasonal && currentMonth != targetMonth)
                return Mathf.Max(0, offSeasonSalePrice);
            return Mathf.Max(0, regularSalePrice);
        }

        public float GetPurchaseChance(int currentMonth)
        {
            if (IsSeasonal && currentMonth != targetMonth)
                return Mathf.Clamp01(offSeasonPurchaseChance);
            return Mathf.Clamp01(purchaseChance);
        }
    }

    [Serializable]
    public class CheckoutItemStock
    {
        public string itemId;
        [Min(0)] public int quantity;
        public bool installed;
    }

    public readonly struct AddonSaleResult
    {
        public readonly bool purchased;
        public readonly string itemName;
        public readonly int price;

        public AddonSaleResult(bool purchased, string itemName, int price)
        {
            this.purchased = purchased;
            this.itemName = itemName;
            this.price = price;
        }
    }

    public const int MaxInstalledItems = 3;

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;

    [Header("入荷")]
    [Tooltip("花・ラッピングとは別枠で、1日0～1種類のレジ横商品が出る確率。仮初期値35%。")]
    [Range(0f, 1f)] [SerializeField] private float dailyOfferChance = 0.35f;

    [Header("デバッグ")]
    [Tooltip("ONの間だけ、1年目4月1日にキープパワーを必ず入荷させます。通常運用へ戻す時はOFFにしてください。")]
    [SerializeField] private bool forceKeepPowerOnFirstDay = true;
    [Tooltip("ONの間だけ、購入条件と残り予算を満たす花購入客はキープパワーを100%購入します。通常運用へ戻す時はOFFにしてください。")]
    [SerializeField] private bool forceKeepPowerPurchaseChance = true;

    [Header("状態")]
    [SerializeField] private List<CheckoutItemStock> stocks = new();
    [SerializeField] private string todayOfferItemId;
    [SerializeField] private bool todayOfferPurchased;

    private readonly List<CheckoutItemDefinition> catalog = new();

    public IReadOnlyList<CheckoutItemDefinition> Catalog => catalog;
    public IReadOnlyList<CheckoutItemStock> Stocks => stocks;
    public CheckoutItemDefinition TodayOffer => GetDefinition(todayOfferItemId);
    public bool HasTodayOffer => TodayOffer != null && !todayOfferPurchased;

    public event Action OnChanged;

    private void Awake()
    {
        BuildDefaultCatalog();
        NormalizeStocks();
    }

    public void GenerateDailyOffer()
    {
        todayOfferItemId = null;
        todayOfferPurchased = false;

        // デバッグ用：1年目4月1日だけ、入荷確率と仕入先Lvを無視してキープパワーを確定入荷。
        // Inspectorの Force Keep Power On First Day をOFFにすれば即座に通常仕様へ戻せます。
        if (forceKeepPowerOnFirstDay && shopManager != null && shopManager.GameYear == 1 && shopManager.DayOfYear == 1)
        {
            todayOfferItemId = "keep_power";
            OnChanged?.Invoke();
            return;
        }

        if (shopManager == null || UnityEngine.Random.value > dailyOfferChance)
        {
            OnChanged?.Invoke();
            return;
        }

        int level = shopManager.SupplierLevel;
        List<CheckoutItemDefinition> candidates = catalog
            .Where(i => i != null && i.arrivalDifficulty >= 3 && i.arrivalDifficulty <= level)
            .ToList();

        if (candidates.Count > 0)
            todayOfferItemId = candidates[UnityEngine.Random.Range(0, candidates.Count)].id;

        OnChanged?.Invoke();
    }

    public bool TryBuyTodayOffer()
    {
        CheckoutItemDefinition item = TodayOffer;
        if (item == null || todayOfferPurchased || shopManager == null) return false;

        // 仕入先から買う商品なので、月間仕入れ額と累計仕入額にも含めます。
        if (!shopManager.TryPurchaseFromSupplier(item.boxPurchasePrice)) return false;

        todayOfferPurchased = true;
        AddStock(item.id, item.boxQuantity, autoInstall: true);
        OnChanged?.Invoke();
        return true;
    }

    public void AddStock(string itemId, int quantity, bool autoInstall)
    {
        if (quantity <= 0 || GetDefinition(itemId) == null) return;

        CheckoutItemStock stock = GetOrCreateStock(itemId);
        stock.quantity += quantity;

        if (autoInstall && !stock.installed && InstalledCount < MaxInstalledItems)
            stock.installed = true;

        OnChanged?.Invoke();
    }

    public bool TryInstall(string itemId)
    {
        CheckoutItemStock stock = GetStock(itemId);
        if (stock == null || stock.quantity <= 0) return false;
        if (stock.installed) return true;
        if (InstalledCount >= MaxInstalledItems) return false;

        stock.installed = true;
        OnChanged?.Invoke();
        return true;
    }

    public void Uninstall(string itemId)
    {
        CheckoutItemStock stock = GetStock(itemId);
        if (stock == null || !stock.installed) return;
        stock.installed = false;
        OnChanged?.Invoke();
    }

    public int InstalledCount => stocks.Count(s => s != null && s.installed && s.quantity > 0);

    public IReadOnlyList<CheckoutItemDefinition> GetInstalledDefinitions()
    {
        return stocks
            .Where(s => s != null && s.installed && s.quantity > 0)
            .Select(s => GetDefinition(s.itemId))
            .Where(d => d != null)
            .Take(MaxInstalledItems)
            .ToList();
    }

    public int GetStockQuantity(string itemId)
    {
        CheckoutItemStock stock = GetStock(itemId);
        return stock != null ? Mathf.Max(0, stock.quantity) : 0;
    }

    /// <summary>
    /// 設置中の商品をランダム順に見て、条件・残り予算を満たすものだけ低確率で判定します。
    /// 1人につき購入は最大1個です。
    /// </summary>
    public AddonSaleResult TrySellAddon(bool boughtBouquet, int remainingBudget)
    {
        if (shopManager == null || remainingBudget <= 0)
            return default;

        List<CheckoutItemDefinition> candidates = GetInstalledDefinitions()
            .Where(item => item != null)
            .Where(item => !boughtBouquet || item.purchaseCondition == PurchaseCondition.FlowerOrBouquet)
            .Where(item => item.GetSalePrice(shopManager.CurrentMonth) <= remainingBudget)
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        foreach (CheckoutItemDefinition item in candidates)
        {
            float purchaseChance = forceKeepPowerPurchaseChance && item.id == "keep_power"
                ? 1f
                : item.GetPurchaseChance(shopManager.CurrentMonth);

            if (UnityEngine.Random.value > purchaseChance)
                continue;

            CheckoutItemStock stock = GetStock(item.id);
            if (stock == null || stock.quantity <= 0)
                continue;

            int price = item.GetSalePrice(shopManager.CurrentMonth);
            stock.quantity--;
            shopManager.AddMoney(price);

            if (stock.quantity <= 0)
            {
                stock.quantity = 0;
                stock.installed = false;
            }

            OnChanged?.Invoke();
            return new AddonSaleResult(true, item.displayName, price);
        }

        return default;
    }

    public CheckoutItemDefinition GetDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        return catalog.FirstOrDefault(i => i.id == itemId);
    }

    public Sprite LoadSprite(CheckoutItemDefinition item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.resourceSpriteName)) return null;
        return Resources.Load<Sprite>($"CheckoutItems/{item.resourceSpriteName}");
    }

    private CheckoutItemStock GetStock(string itemId)
    {
        return stocks.FirstOrDefault(s => s != null && s.itemId == itemId);
    }

    private CheckoutItemStock GetOrCreateStock(string itemId)
    {
        CheckoutItemStock stock = GetStock(itemId);
        if (stock != null) return stock;

        stock = new CheckoutItemStock { itemId = itemId, quantity = 0, installed = false };
        stocks.Add(stock);
        return stock;
    }

    private void NormalizeStocks()
    {
        stocks ??= new List<CheckoutItemStock>();
        foreach (CheckoutItemDefinition item in catalog)
            GetOrCreateStock(item.id);

        int installed = 0;
        foreach (CheckoutItemStock stock in stocks)
        {
            if (stock == null || stock.quantity <= 0)
            {
                if (stock != null) stock.installed = false;
                continue;
            }

            if (stock.installed)
            {
                installed++;
                if (installed > MaxInstalledItems)
                    stock.installed = false;
            }
        }
    }

    private void BuildDefaultCatalog()
    {
        catalog.Clear();
        catalog.Add(new CheckoutItemDefinition
        {
            id = "keep_power", displayName = "キープパワー", boxQuantity = 100, boxPurchasePrice = 2000,
            regularSalePrice = 50, arrivalDifficulty = 3, purchaseChance = 0.10f,
            purchaseCondition = PurchaseCondition.FlowerOnly, resourceSpriteName = "checkout_keep_power"
        });
        catalog.Add(new CheckoutItemDefinition
        {
            id = "mini_flower_base", displayName = "ミニフラワーベース", boxQuantity = 5, boxPurchasePrice = 1000,
            regularSalePrice = 500, arrivalDifficulty = 5, purchaseChance = 0.06f,
            purchaseCondition = PurchaseCondition.FlowerOnly, resourceSpriteName = "checkout_mini_flower_base"
        });
        catalog.Add(new CheckoutItemDefinition
        {
            id = "iinioi", displayName = "Iinioi", boxQuantity = 5, boxPurchasePrice = 1500,
            regularSalePrice = 1500, arrivalDifficulty = 7, purchaseChance = 0.03f,
            purchaseCondition = PurchaseCondition.FlowerOrBouquet, resourceSpriteName = "checkout_iinioi"
        });

        AddSeasonal("mini_kadomatsu", "ミニ門松", 1, "checkout_mini_kadomatsu");
        AddSeasonal("tsukimi_dango", "お月見団子フィギュア", 9, "checkout_tsukimi_dango");
        AddSeasonal("mini_pumpkin", "ミニカボチャ", 10, "checkout_mini_pumpkin");
        AddSeasonal("mini_tree", "ミニツリー", 12, "checkout_mini_tree");
    }

    private void AddSeasonal(string id, string name, int month, string spriteName)
    {
        catalog.Add(new CheckoutItemDefinition
        {
            id = id,
            displayName = name,
            boxQuantity = 10,
            boxPurchasePrice = 1500,
            regularSalePrice = 300,
            arrivalDifficulty = 6,
            purchaseChance = 0.08f,
            purchaseCondition = PurchaseCondition.FlowerOrBouquet,
            targetMonth = month,
            offSeasonSalePrice = 100,
            offSeasonPurchaseChance = 0.03f,
            resourceSpriteName = spriteName
        });
    }
}
