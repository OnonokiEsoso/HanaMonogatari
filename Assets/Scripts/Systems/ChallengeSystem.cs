using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ChallengeScope
{
    Daily,
    Monthly,
    Lifetime
}

public enum ChallengeType
{
    SupplierSpend,
    FlowerPurchaseQuantity,
    DistinctFlowerPurchases,
    MonthlySales,
    MonthlyBuyers,
    MonthlyVisitors,
    OwnFurniture,
    TotalBouquetsCreated,
    TotalVisitors,
    ShopRating,
    SupplierLevel,
    HybridUnlockedCount
}

public enum ChallengeRewardType
{
    Money,
    ShopRating,
    Wrapping
}

[Serializable]
public class ChallengeDefinition
{
    public string id;
    public ChallengeScope scope;
    public string chainId;
    [Min(0)] public int chainStage;
    public string title;
    [TextArea] public string description;
    public ChallengeType type;
    [Min(1)] public int targetValue = 1;
    public ChallengeRewardType rewardType;
    [Min(1)] public int rewardAmount = 1;
}

/// <summary>
/// チャレンジ全体を管理します。
/// ・デイリー：毎日1件
/// ・月間：毎月3件（Inspectorで2～3件に変更可能）
/// ・通算：複数の段階式チャレンジを常時表示
/// 同じ系列の通算チャレンジは、前段階の報酬を受け取ると次段階が表示されます。
/// </summary>
public class ChallengeSystem : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private FurnitureSystem furnitureSystem;
    [SerializeField] private BouquetSystem bouquetSystem;
    [SerializeField] private CustomerUI customerUI;
    [SerializeField] private HybridDevelopmentSystem hybridDevelopmentSystem;

    [Header("表示数")]
    [Range(2, 3)] [SerializeField] private int monthlyChallengeCount = 3;

    [Header("チャレンジ定義")]
    [SerializeField] private List<ChallengeDefinition> challenges = new();

    [Header("現在選出中")]
    [SerializeField] private List<string> selectedDailyChallengeIds = new();
    [SerializeField] private List<string> selectedMonthlyChallengeIds = new();

    [Header("受取済み")]
    [SerializeField] private List<string> claimedDailyChallengeIds = new();
    [SerializeField] private List<string> claimedMonthlyChallengeIds = new();
    [SerializeField] private List<string> claimedLifetimeChallengeIds = new();

    [Header("当日集計")]
    [Min(0)] [SerializeField] private int dailySupplierSpend;
    [Min(0)] [SerializeField] private int dailyFlowerPurchaseQuantity;
    [SerializeField] private List<string> dailyFlowerProductKeys = new();

    [Header("当月集計")]
    [Min(0)] [SerializeField] private int monthlyTrackedSupplierSpend;
    [Min(0)] [SerializeField] private int monthlyFlowerPurchaseQuantity;
    [SerializeField] private List<string> monthlyFlowerProductKeys = new();

    [Header("通算集計")]
    [Min(0)] [SerializeField] private int totalVisitors;

    [Header("日付監視")]
    [SerializeField] private int observedYear = -1;
    [SerializeField] private int observedMonth = -1;
    [SerializeField] private int observedDayOfYear = -1;

    public IReadOnlyList<ChallengeDefinition> Challenges => challenges;
    public event Action OnChanged;

    private void Awake()
    {
        ResolveReferences();
        EnsureDefaultChallenges();
        EnsureLists();
        InitializeDateState();
        EnsureSelections();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (shopManager != null)
        {
            shopManager.OnStateChanged += HandleStateChanged;
            shopManager.OnSupplierPurchaseSpent += HandleSupplierPurchaseSpent;
            shopManager.OnSupplierProductPurchased += HandleSupplierProductPurchased;
            shopManager.OnSupplierFlowerPurchased += HandleSupplierFlowerPurchased;
        }

        if (furnitureSystem != null)
            furnitureSystem.OnChanged += HandleStateChanged;
        if (bouquetSystem != null)
            bouquetSystem.OnBouquetsChanged += HandleStateChanged;
        if (customerUI != null)
            customerUI.OnBusinessFinished += HandleBusinessFinished;
        if (hybridDevelopmentSystem != null)
            hybridDevelopmentSystem.OnChanged += HandleStateChanged;

        SyncDateIfNeeded();
        EnsureSelections();
    }

    private void OnDisable()
    {
        if (shopManager != null)
        {
            shopManager.OnStateChanged -= HandleStateChanged;
            shopManager.OnSupplierPurchaseSpent -= HandleSupplierPurchaseSpent;
            shopManager.OnSupplierProductPurchased -= HandleSupplierProductPurchased;
            shopManager.OnSupplierFlowerPurchased -= HandleSupplierFlowerPurchased;
        }

        if (furnitureSystem != null)
            furnitureSystem.OnChanged -= HandleStateChanged;
        if (bouquetSystem != null)
            bouquetSystem.OnBouquetsChanged -= HandleStateChanged;
        if (customerUI != null)
            customerUI.OnBusinessFinished -= HandleBusinessFinished;
        if (hybridDevelopmentSystem != null)
            hybridDevelopmentSystem.OnChanged -= HandleStateChanged;
    }

    public IEnumerable<ChallengeDefinition> GetVisibleChallenges()
    {
        EnsureDefaultChallenges();
        EnsureSelections();

        IEnumerable<ChallengeDefinition> daily = challenges
            .Where(c => c != null && c.scope == ChallengeScope.Daily && selectedDailyChallengeIds.Contains(c.id));

        IEnumerable<ChallengeDefinition> monthly = challenges
            .Where(c => c != null && c.scope == ChallengeScope.Monthly && selectedMonthlyChallengeIds.Contains(c.id));

        // 通算は各系列につき「まだ受取済みではない最も低い段階」だけを表示する。
        IEnumerable<ChallengeDefinition> lifetime = challenges
            .Where(c => c != null && c.scope == ChallengeScope.Lifetime)
            .GroupBy(c => string.IsNullOrWhiteSpace(c.chainId) ? c.id : c.chainId)
            .Select(group => group.OrderBy(c => c.chainStage)
                .FirstOrDefault(c => !claimedLifetimeChallengeIds.Contains(c.id)))
            .Where(c => c != null);

        return daily.Concat(monthly).Concat(lifetime);
    }

    public string GetScopeLabel(ChallengeDefinition challenge)
    {
        if (challenge == null) return string.Empty;
        return challenge.scope switch
        {
            ChallengeScope.Daily => "デイリー",
            ChallengeScope.Monthly => "月間",
            ChallengeScope.Lifetime => "通算",
            _ => string.Empty
        };
    }

    public int GetCurrentValue(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return 0;

        ResolveReferences();

        return challenge.type switch
        {
            ChallengeType.SupplierSpend => challenge.scope == ChallengeScope.Daily
                ? dailySupplierSpend
                : monthlyTrackedSupplierSpend,
            ChallengeType.FlowerPurchaseQuantity => challenge.scope == ChallengeScope.Daily
                ? dailyFlowerPurchaseQuantity
                : monthlyFlowerPurchaseQuantity,
            ChallengeType.DistinctFlowerPurchases => challenge.scope == ChallengeScope.Daily
                ? dailyFlowerProductKeys.Count
                : monthlyFlowerProductKeys.Count,
            ChallengeType.MonthlySales => shopManager != null ? Mathf.Max(0, shopManager.MonthlySales) : 0,
            ChallengeType.MonthlyBuyers => shopManager != null ? Mathf.Max(0, shopManager.MonthlyBuyers) : 0,
            ChallengeType.MonthlyVisitors => shopManager != null ? Mathf.Max(0, shopManager.MonthlyVisitors) : 0,
            ChallengeType.OwnFurniture => furnitureSystem != null ? Mathf.Max(0, furnitureSystem.OwnedCount) : 0,
            ChallengeType.TotalBouquetsCreated => bouquetSystem != null ? Mathf.Max(0, bouquetSystem.TotalCreatedCount) : 0,
            ChallengeType.TotalVisitors => Mathf.Max(0, totalVisitors),
            ChallengeType.ShopRating => shopManager != null ? Mathf.Max(0, shopManager.ShopRating) : 0,
            ChallengeType.SupplierLevel => shopManager != null ? Mathf.Max(1, shopManager.SupplierLevel) : 1,
            ChallengeType.HybridUnlockedCount => hybridDevelopmentSystem != null
                ? hybridDevelopmentSystem.UnlockedHybridNames.Count
                : 0,
            _ => 0
        };
    }

    public bool IsCompleted(ChallengeDefinition challenge)
    {
        return challenge != null && GetCurrentValue(challenge) >= Mathf.Max(1, challenge.targetValue);
    }

    public bool IsClaimed(ChallengeDefinition challenge)
    {
        if (challenge == null || string.IsNullOrWhiteSpace(challenge.id))
            return false;

        return GetClaimedList(challenge.scope).Contains(challenge.id);
    }

    public bool TryClaim(ChallengeDefinition challenge)
    {
        if (challenge == null || IsClaimed(challenge) || !IsCompleted(challenge))
            return false;

        ResolveReferences();

        switch (challenge.rewardType)
        {
            case ChallengeRewardType.Money:
                shopManager?.AddMoney(challenge.rewardAmount);
                break;
            case ChallengeRewardType.ShopRating:
                shopManager?.AddShopRating(challenge.rewardAmount);
                break;
            case ChallengeRewardType.Wrapping:
                bouquetSystem?.AddWrapping(challenge.rewardAmount);
                break;
        }

        List<string> claimed = GetClaimedList(challenge.scope);
        if (!claimed.Contains(challenge.id))
            claimed.Add(challenge.id);

        Debug.Log($"チャレンジ達成報酬を受け取りました：{challenge.title} / {GetRewardText(challenge)}");
        OnChanged?.Invoke();
        return true;
    }

    public string GetProgressText(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return string.Empty;

        int current = GetCurrentValue(challenge);
        int target = Mathf.Max(1, challenge.targetValue);
        int shown = Mathf.Min(current, target);

        return challenge.type switch
        {
            ChallengeType.SupplierSpend or ChallengeType.MonthlySales => $"{shown:N0} / {target:N0}円",
            ChallengeType.MonthlyBuyers or ChallengeType.MonthlyVisitors or ChallengeType.TotalVisitors => $"{shown:N0} / {target:N0}人",
            ChallengeType.OwnFurniture or ChallengeType.TotalBouquetsCreated or ChallengeType.HybridUnlockedCount => $"{shown:N0} / {target:N0}個",
            ChallengeType.FlowerPurchaseQuantity => $"{shown:N0} / {target:N0}本",
            ChallengeType.DistinctFlowerPurchases => $"{shown:N0} / {target:N0}種類",
            ChallengeType.ShopRating => $"{shown:N0} / {target:N0}",
            ChallengeType.SupplierLevel => $"Lv.{shown} / Lv.{target}",
            _ => $"{shown:N0} / {target:N0}"
        };
    }

    public string GetRewardText(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return string.Empty;

        return challenge.rewardType switch
        {
            ChallengeRewardType.Money => $"報酬：{challenge.rewardAmount:N0}円",
            ChallengeRewardType.ShopRating => $"報酬：店評価 +{challenge.rewardAmount:N0}",
            ChallengeRewardType.Wrapping => $"報酬：ラッピング ×{challenge.rewardAmount:N0}",
            _ => string.Empty
        };
    }

    private void HandleSupplierPurchaseSpent(int amount)
    {
        if (amount <= 0) return;
        SyncDateIfNeeded();
        dailySupplierSpend += amount;
        monthlyTrackedSupplierSpend += amount;
        OnChanged?.Invoke();
    }

    private void HandleSupplierProductPurchased(string productKey)
    {
        if (string.IsNullOrWhiteSpace(productKey) || !productKey.StartsWith("flower:", StringComparison.Ordinal))
            return;

        SyncDateIfNeeded();
        if (!dailyFlowerProductKeys.Contains(productKey))
            dailyFlowerProductKeys.Add(productKey);
        if (!monthlyFlowerProductKeys.Contains(productKey))
            monthlyFlowerProductKeys.Add(productKey);
        OnChanged?.Invoke();
    }

    private void HandleSupplierFlowerPurchased(int quantity)
    {
        if (quantity <= 0) return;
        SyncDateIfNeeded();
        dailyFlowerPurchaseQuantity += quantity;
        monthlyFlowerPurchaseQuantity += quantity;
        OnChanged?.Invoke();
    }

    private void HandleBusinessFinished()
    {
        if (customerUI == null)
            return;

        totalVisitors += Mathf.Max(0, customerUI.TotalVisitors);
        OnChanged?.Invoke();
    }

    private void HandleStateChanged()
    {
        SyncDateIfNeeded();
        OnChanged?.Invoke();
    }

    private void InitializeDateState()
    {
        if (shopManager == null)
            return;

        if (observedYear < 0) observedYear = shopManager.GameYear;
        if (observedMonth < 0) observedMonth = shopManager.CurrentMonth;
        if (observedDayOfYear < 0) observedDayOfYear = shopManager.DayOfYear;
    }

    private void SyncDateIfNeeded()
    {
        if (shopManager == null)
            return;

        EnsureLists();
        int year = shopManager.GameYear;
        int month = shopManager.CurrentMonth;
        int day = shopManager.DayOfYear;

        bool dayChanged = observedYear >= 0 && (year != observedYear || day != observedDayOfYear);
        bool monthChanged = observedYear >= 0 && observedMonth >= 0 && (year != observedYear || month != observedMonth);

        if (dayChanged)
        {
            dailySupplierSpend = 0;
            dailyFlowerPurchaseQuantity = 0;
            dailyFlowerProductKeys.Clear();
            claimedDailyChallengeIds.Clear();
            selectedDailyChallengeIds.Clear();
        }

        if (monthChanged)
        {
            monthlyTrackedSupplierSpend = 0;
            monthlyFlowerPurchaseQuantity = 0;
            monthlyFlowerProductKeys.Clear();
            claimedMonthlyChallengeIds.Clear();
            selectedMonthlyChallengeIds.Clear();
        }

        observedYear = year;
        observedMonth = month;
        observedDayOfYear = day;

        EnsureSelections();
    }

    private void EnsureSelections()
    {
        EnsureLists();
        EnsureDefaultChallenges();

        if (shopManager == null)
            return;

        if (selectedDailyChallengeIds.Count == 0)
        {
            List<ChallengeDefinition> pool = challenges.Where(c => c.scope == ChallengeScope.Daily).ToList();
            PickDeterministic(pool, 1, GetDailySeed(), selectedDailyChallengeIds);
        }

        int desiredMonthlyCount = Mathf.Clamp(monthlyChallengeCount, 2, 3);
        if (selectedMonthlyChallengeIds.Count == 0)
        {
            List<ChallengeDefinition> pool = challenges.Where(c => c.scope == ChallengeScope.Monthly).ToList();
            PickDeterministic(pool, desiredMonthlyCount, GetMonthlySeed(), selectedMonthlyChallengeIds);
        }
    }

    private static void PickDeterministic(List<ChallengeDefinition> pool, int count, int seed, List<string> destination)
    {
        destination.Clear();
        if (pool == null || pool.Count == 0 || count <= 0)
            return;

        System.Random random = new(seed);
        List<ChallengeDefinition> remaining = new(pool);
        int actualCount = Mathf.Min(count, remaining.Count);

        for (int i = 0; i < actualCount; i++)
        {
            int index = random.Next(remaining.Count);
            destination.Add(remaining[index].id);
            remaining.RemoveAt(index);
        }
    }

    private int GetDailySeed()
    {
        return shopManager != null
            ? shopManager.GameYear * 1000 + shopManager.DayOfYear * 17 + 7
            : 7;
    }

    private int GetMonthlySeed()
    {
        return shopManager != null
            ? shopManager.GameYear * 100 + shopManager.CurrentMonth * 31 + 11
            : 11;
    }

    private List<string> GetClaimedList(ChallengeScope scope)
    {
        EnsureLists();
        return scope switch
        {
            ChallengeScope.Daily => claimedDailyChallengeIds,
            ChallengeScope.Monthly => claimedMonthlyChallengeIds,
            _ => claimedLifetimeChallengeIds
        };
    }

    private void EnsureLists()
    {
        challenges ??= new List<ChallengeDefinition>();
        selectedDailyChallengeIds ??= new List<string>();
        selectedMonthlyChallengeIds ??= new List<string>();
        claimedDailyChallengeIds ??= new List<string>();
        claimedMonthlyChallengeIds ??= new List<string>();
        claimedLifetimeChallengeIds ??= new List<string>();
        dailyFlowerProductKeys ??= new List<string>();
        monthlyFlowerProductKeys ??= new List<string>();
    }

    private void EnsureDefaultChallenges()
    {
        // 旧ver0.0.7初期版の3件だけがInspectorに残っている場合も、新構成へ置き換える。
        if (challenges != null && challenges.Any(c => c != null && c.id == "daily_spend_500") &&
            challenges.Any(c => c != null && c.id == "lifetime_furniture_1"))
            return;

        challenges = new List<ChallengeDefinition>
        {
            // ---------- デイリー：毎日この中から1件 ----------
            C("daily_spend_500", ChallengeScope.Daily, "", 0, "今日の仕入れ", "今日、仕入れで500円使おう。", ChallengeType.SupplierSpend, 500, ChallengeRewardType.ShopRating, 50),
            C("daily_buy_flowers_5", ChallengeScope.Daily, "", 0, "今日の仕入れ本数", "今日、花を5本仕入れよう。", ChallengeType.FlowerPurchaseQuantity, 5, ChallengeRewardType.ShopRating, 40),
            C("daily_buy_types_2", ChallengeScope.Daily, "", 0, "いろいろ仕入れよう", "今日、2種類の花を仕入れよう。", ChallengeType.DistinctFlowerPurchases, 2, ChallengeRewardType.ShopRating, 45),
            C("daily_spend_1000", ChallengeScope.Daily, "", 0, "ちょっと大きな仕入れ", "今日、仕入れで1,000円使おう。", ChallengeType.SupplierSpend, 1000, ChallengeRewardType.ShopRating, 70),
            C("daily_buy_flowers_8", ChallengeScope.Daily, "", 0, "品揃えを増やそう", "今日、花を8本仕入れよう。", ChallengeType.FlowerPurchaseQuantity, 8, ChallengeRewardType.ShopRating, 60),

            // ---------- 月間：毎月この中から2～3件 ----------
            C("monthly_types_10", ChallengeScope.Monthly, "", 0, "今月はいろんな花を", "今月、10種類の花を仕入れよう。", ChallengeType.DistinctFlowerPurchases, 10, ChallengeRewardType.Money, 3000),
            C("monthly_sales_20000", ChallengeScope.Monthly, "", 0, "月商20,000円", "今月の売上を20,000円以上にしよう。", ChallengeType.MonthlySales, 20000, ChallengeRewardType.Money, 3000),
            C("monthly_buyers_25", ChallengeScope.Monthly, "", 0, "25人に届けよう", "今月25人のお客さんに商品を買ってもらおう。", ChallengeType.MonthlyBuyers, 25, ChallengeRewardType.ShopRating, 120),
            C("monthly_visitors_40", ChallengeScope.Monthly, "", 0, "にぎわうお店", "今月40人のお客さんに来店してもらおう。", ChallengeType.MonthlyVisitors, 40, ChallengeRewardType.Money, 2500),
            C("monthly_flowers_30", ChallengeScope.Monthly, "", 0, "仕入れ強化月間", "今月、花を合計30本仕入れよう。", ChallengeType.FlowerPurchaseQuantity, 30, ChallengeRewardType.Money, 2500),
            C("monthly_spend_10000", ChallengeScope.Monthly, "", 0, "今月の仕入れ投資", "今月、仕入れで10,000円使おう。", ChallengeType.SupplierSpend, 10000, ChallengeRewardType.ShopRating, 150),

            // ---------- 通算：家具系列 ----------
            C("lifetime_furniture_1", ChallengeScope.Lifetime, "furniture", 1, "はじめての家具", "家具を1個買おう。", ChallengeType.OwnFurniture, 1, ChallengeRewardType.Money, 1500),
            C("lifetime_furniture_2", ChallengeScope.Lifetime, "furniture", 2, "家具が増えてきた", "家具を2個買おう。", ChallengeType.OwnFurniture, 2, ChallengeRewardType.Money, 2500),
            C("lifetime_furniture_4", ChallengeScope.Lifetime, "furniture", 3, "居心地のいい店", "家具を4個買おう。", ChallengeType.OwnFurniture, 4, ChallengeRewardType.ShopRating, 150),
            C("lifetime_furniture_7", ChallengeScope.Lifetime, "furniture", 4, "こだわりの店内", "家具を7個買おう。", ChallengeType.OwnFurniture, 7, ChallengeRewardType.ShopRating, 300),
            C("lifetime_furniture_10", ChallengeScope.Lifetime, "furniture", 5, "家具コレクター", "家具を10個買おう。", ChallengeType.OwnFurniture, 10, ChallengeRewardType.Money, 10000),

            // ---------- 通算：花束系列 ----------
            C("lifetime_bouquet_1", ChallengeScope.Lifetime, "bouquet", 1, "はじめての花束", "花束を1個作ろう。", ChallengeType.TotalBouquetsCreated, 1, ChallengeRewardType.Wrapping, 2),
            C("lifetime_bouquet_3", ChallengeScope.Lifetime, "bouquet", 2, "花束づくりに慣れよう", "花束を3個作ろう。", ChallengeType.TotalBouquetsCreated, 3, ChallengeRewardType.Money, 2000),
            C("lifetime_bouquet_10", ChallengeScope.Lifetime, "bouquet", 3, "花束職人", "花束を10個作ろう。", ChallengeType.TotalBouquetsCreated, 10, ChallengeRewardType.ShopRating, 200),
            C("lifetime_bouquet_25", ChallengeScope.Lifetime, "bouquet", 4, "ブーケの名店", "花束を25個作ろう。", ChallengeType.TotalBouquetsCreated, 25, ChallengeRewardType.Money, 8000),

            // ---------- 通算：来客系列 ----------
            C("lifetime_visitors_50", ChallengeScope.Lifetime, "visitors", 1, "50人のお客さん", "累計50人に来店してもらおう。", ChallengeType.TotalVisitors, 50, ChallengeRewardType.ShopRating, 100),
            C("lifetime_visitors_100", ChallengeScope.Lifetime, "visitors", 2, "100人突破", "累計100人に来店してもらおう。", ChallengeType.TotalVisitors, 100, ChallengeRewardType.ShopRating, 150),
            C("lifetime_visitors_250", ChallengeScope.Lifetime, "visitors", 3, "町で評判のお店", "累計250人に来店してもらおう。", ChallengeType.TotalVisitors, 250, ChallengeRewardType.Money, 5000),
            C("lifetime_visitors_500", ChallengeScope.Lifetime, "visitors", 4, "500人突破", "累計500人に来店してもらおう。", ChallengeType.TotalVisitors, 500, ChallengeRewardType.ShopRating, 400),
            C("lifetime_visitors_1000", ChallengeScope.Lifetime, "visitors", 5, "千客万来", "累計1,000人に来店してもらおう。", ChallengeType.TotalVisitors, 1000, ChallengeRewardType.Money, 15000),

            // ---------- 通算：店評価系列 ----------
            C("lifetime_rating_500", ChallengeScope.Lifetime, "rating", 1, "評判の芽", "店評価500に到達しよう。", ChallengeType.ShopRating, 500, ChallengeRewardType.Money, 2000),
            C("lifetime_rating_2000", ChallengeScope.Lifetime, "rating", 2, "開発への一歩", "店評価2,000に到達しよう。", ChallengeType.ShopRating, 2000, ChallengeRewardType.Money, 5000),
            C("lifetime_rating_5000", ChallengeScope.Lifetime, "rating", 3, "人気店", "店評価5,000に到達しよう。", ChallengeType.ShopRating, 5000, ChallengeRewardType.Wrapping, 5),
            C("lifetime_rating_7500", ChallengeScope.Lifetime, "rating", 4, "有名店", "店評価7,500に到達しよう。", ChallengeType.ShopRating, 7500, ChallengeRewardType.Money, 10000),
            C("lifetime_rating_10000", ChallengeScope.Lifetime, "rating", 5, "一番人気のお花屋さん", "店評価10,000に到達しよう。", ChallengeType.ShopRating, 10000, ChallengeRewardType.Money, 20000),

            // ---------- 通算：仕入先系列 ----------
            C("lifetime_supplier_2", ChallengeScope.Lifetime, "supplier", 1, "仕入先Lv.2", "仕入先Lv.2にしよう。", ChallengeType.SupplierLevel, 2, ChallengeRewardType.Wrapping, 1),
            C("lifetime_supplier_4", ChallengeScope.Lifetime, "supplier", 2, "仕入先Lv.4", "仕入先Lv.4にしよう。", ChallengeType.SupplierLevel, 4, ChallengeRewardType.Money, 3000),
            C("lifetime_supplier_6", ChallengeScope.Lifetime, "supplier", 3, "仕入先Lv.6", "仕入先Lv.6にしよう。", ChallengeType.SupplierLevel, 6, ChallengeRewardType.ShopRating, 200),
            C("lifetime_supplier_8", ChallengeScope.Lifetime, "supplier", 4, "仕入先Lv.8", "仕入先Lv.8にしよう。", ChallengeType.SupplierLevel, 8, ChallengeRewardType.Money, 10000),
            C("lifetime_supplier_10", ChallengeScope.Lifetime, "supplier", 5, "最高の仕入先", "仕入先Lv.10にしよう。", ChallengeType.SupplierLevel, 10, ChallengeRewardType.ShopRating, 500),

            // ---------- 通算：交配系列 ----------
            C("lifetime_hybrid_1", ChallengeScope.Lifetime, "hybrid", 1, "はじめての新種", "交配で新種を1種類開発しよう。", ChallengeType.HybridUnlockedCount, 1, ChallengeRewardType.Money, 5000),
            C("lifetime_hybrid_3", ChallengeScope.Lifetime, "hybrid", 2, "交配研究者", "交配で新種を3種類開発しよう。", ChallengeType.HybridUnlockedCount, 3, ChallengeRewardType.ShopRating, 200),
            C("lifetime_hybrid_5", ChallengeScope.Lifetime, "hybrid", 3, "新種が増えてきた", "交配で新種を5種類開発しよう。", ChallengeType.HybridUnlockedCount, 5, ChallengeRewardType.Money, 10000),
            C("lifetime_hybrid_10", ChallengeScope.Lifetime, "hybrid", 4, "交配の達人", "交配で新種を10種類開発しよう。", ChallengeType.HybridUnlockedCount, 10, ChallengeRewardType.ShopRating, 500),
            C("lifetime_hybrid_25", ChallengeScope.Lifetime, "hybrid", 5, "新種コンプリート", "交配で新種を25種類すべて開発しよう。", ChallengeType.HybridUnlockedCount, 25, ChallengeRewardType.Money, 25000)
        };
    }

    private static ChallengeDefinition C(
        string id, ChallengeScope scope, string chainId, int stage, string title, string description,
        ChallengeType type, int target, ChallengeRewardType rewardType, int rewardAmount)
    {
        return new ChallengeDefinition
        {
            id = id,
            scope = scope,
            chainId = chainId,
            chainStage = stage,
            title = title,
            description = description,
            type = type,
            targetValue = target,
            rewardType = rewardType,
            rewardAmount = rewardAmount
        };
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();
        if (bouquetSystem == null)
            bouquetSystem = FindFirstObjectByType<BouquetSystem>();
        if (customerUI == null)
            customerUI = FindFirstObjectByType<CustomerUI>();
        if (hybridDevelopmentSystem == null)
            hybridDevelopmentSystem = FindFirstObjectByType<HybridDevelopmentSystem>();
    }
}
