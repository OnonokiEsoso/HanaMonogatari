using System;
using System.Collections.Generic;
using UnityEngine;

public enum VisitPurpose
{
    SelfUse,
    Gift,
    Offering,
    Anniversary
}

public class CustomerSystem : MonoBehaviour
{
    [Serializable]
    public class VisitingCustomer
    {
        public CustomerData data;
        public string favoriteColor;
        public VisitPurpose purpose;
        [Min(0)] public int budget;

        public VisitingCustomer(CustomerData data, string favoriteColor, VisitPurpose purpose, int budget)
        {
            this.data = data;
            this.favoriteColor = favoriteColor;
            this.purpose = purpose;
            this.budget = budget;
        }
    }

    [Serializable]
    public class RegularStatus
    {
        public CustomerType customerType;
        [Min(0)] public int currentPoints;
        [Min(0)] public int regularCount;
    }

    private class ProfileBaseline
    {
        public int budget;
        public int minPopularity;
        public int maxPopularity;
        public int minRarity;
        public int maxRarity;
    }

    public readonly struct RegularPointResult
    {
        public readonly int currentPoints;
        public readonly int requiredPoints;
        public readonly int regularCount;
        public readonly bool becameRegular;

        public RegularPointResult(int currentPoints, int requiredPoints, int regularCount, bool becameRegular)
        {
            this.currentPoints = currentPoints;
            this.requiredPoints = requiredPoints;
            this.regularCount = regularCount;
            this.becameRegular = becameRegular;
        }
    }

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private VisitorModifierSystem visitorModifierSystem;
    [SerializeField] private FurnitureSystem furnitureSystem;

    [Header("客タイプ")]
    [SerializeField] private List<CustomerData> customerProfiles = new();

    [Header("来客数")]
    [Tooltip("ゲーム開始から2日間だけ、倍率計算後に固定人数として加算します。")]
    [SerializeField] private int openingBonusVisitors = 5;

    [Header("店成長による客単価")]
    [Tooltip("店評価が上がるほど客の基礎予算を増やします。最大店評価ではおよそ3.2倍です。")]
    [SerializeField] private bool useShopRatingBudgetGrowth = true;
    [Tooltip("同じ客タイプでも所持予算に少し個人差を付けます。")]
    [Range(0f, 0.5f)] [SerializeField] private float budgetRandomVariance = 0.12f;

    [Header("常連")]
    [Tooltip("常連1人につき、その客タイプの来店抽選重みを何%増やすか。")]
    [Min(0f)] [SerializeField] private float regularSpawnBonusPercent = 5f;
    [SerializeField] private List<RegularStatus> regularStatuses = new();

    [SerializeField] private List<VisitingCustomer> todayCustomers = new();

    private readonly Dictionary<CustomerType, ProfileBaseline> profileBaselines = new();

    public IReadOnlyList<VisitingCustomer> TodayCustomers => todayCustomers;
    public IReadOnlyList<RegularStatus> RegularStatuses => regularStatuses;

    private void Awake()
    {
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        EnsureDefaultProfiles();
        CaptureProfileBaselines();
        ApplyShopRatingProfileGrowth();
        ApplyBaseSpawnWeights();
        EnsureRegularStatuses();
    }

    [ContextMenu("本日の来客を生成")]
    public void GenerateTodayCustomers()
    {
        EnsureDefaultProfiles();
        CaptureProfileBaselines();
        ApplyShopRatingProfileGrowth();
        ApplyBaseSpawnWeights();
        EnsureRegularStatuses();
        todayCustomers.Clear();

        int visitorCount = CalculateTodayVisitorCount();
        float dayBudgetMultiplier = TrendSystem.GetBudgetMultiplier(shopManager);
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();
        if (furnitureSystem != null)
            dayBudgetMultiplier += furnitureSystem.GetBudgetBonusPercentToday();

        float shopBudgetMultiplier = useShopRatingBudgetGrowth ? GetShopRatingBudgetMultiplier() : 1f;

        for (int i = 0; i < visitorCount; i++)
        {
            CustomerData profile = PickCustomerProfile();
            if (profile == null) continue;

            string favoriteColor = PickFavoriteColor();
            VisitPurpose purpose = PickVisitPurpose(profile.customerType);
            float purposeMultiplier = GetPurposeBudgetMultiplier(purpose);
            float individualMultiplier = UnityEngine.Random.Range(
                Mathf.Max(0.5f, 1f - budgetRandomVariance),
                1f + budgetRandomVariance);

            int baseBudget = GetBaselineBudget(profile);
            int effectiveBudget = Mathf.Max(0, Mathf.RoundToInt(
                baseBudget * shopBudgetMultiplier * dayBudgetMultiplier * purposeMultiplier * individualMultiplier));

            todayCustomers.Add(new VisitingCustomer(profile, favoriteColor, purpose, effectiveBudget));
        }

        int rating = shopManager != null ? shopManager.ShopRating : 0;
        Debug.Log(
            $"本日の来客を生成しました。{todayCustomers.Count}人 / 店評価:{rating:N0} / " +
            $"店成長予算×{shopBudgetMultiplier:0.00} / 日補正×{dayBudgetMultiplier:0.00}");
    }

    public int CalculateTodayVisitorCount()
    {
        int rating = shopManager != null ? shopManager.ShopRating : 0;
        int baseVisitors = 2 + Mathf.FloorToInt(rating / 300f);
        float randomMultiplier = UnityEngine.Random.Range(0.8f, 1.2f);
        int openingFlatBonus = shopManager != null && shopManager.GameYear == 1 && shopManager.DayOfYear <= 2
            ? openingBonusVisitors
            : 0;

        if (visitorModifierSystem != null)
            return visitorModifierSystem.CalculateVisitorCount(baseVisitors, randomMultiplier, openingFlatBonus);

        Debug.LogWarning("CustomerSystem: VisitorModifierSystemが設定されていません。依頼・家具等の来客補正は反映されません。");
        float trendMultiplier = 1f + TrendSystem.GetVisitorBonusPercent(shopManager);
        int visitors = Mathf.RoundToInt(baseVisitors * randomMultiplier * trendMultiplier) + openingFlatBonus;
        return Mathf.Max(1, visitors);
    }

    public RegularPointResult AddRegularPoint(CustomerType customerType)
    {
        EnsureDefaultProfiles();
        CaptureProfileBaselines();
        ApplyShopRatingProfileGrowth();
        ApplyBaseSpawnWeights();
        EnsureRegularStatuses();

        CustomerData profile = customerProfiles.Find(p => p != null && p.customerType == customerType);
        RegularStatus status = regularStatuses.Find(s => s != null && s.customerType == customerType);

        if (profile == null || status == null)
            return new RegularPointResult(0, 1, 0, false);

        int required = Mathf.Max(1, profile.regularPointMax);
        status.currentPoints++;

        bool becameRegular = false;
        if (status.currentPoints >= required)
        {
            status.currentPoints -= required;
            status.regularCount++;
            becameRegular = true;
            Debug.Log($"{profile.displayName}の常連が1人増えました！ 現在{status.regularCount}人");
        }

        return new RegularPointResult(status.currentPoints, required, status.regularCount, becameRegular);
    }

    private static VisitPurpose PickVisitPurpose(CustomerType customerType)
    {
        float selfUse;
        float gift;
        float offering;

        switch (customerType)
        {
            case CustomerType.Housewife: selfUse = 50f; gift = 25f; offering = 20f; break;
            case CustomerType.Student: selfUse = 45f; gift = 40f; offering = 5f; break;
            case CustomerType.Grandmother: selfUse = 35f; gift = 20f; offering = 40f; break;
            case CustomerType.Wealthy: selfUse = 15f; gift = 35f; offering = 10f; break;
            case CustomerType.Child: selfUse = 70f; gift = 25f; offering = 5f; break;
            case CustomerType.OfficeWorker: selfUse = 10f; gift = 55f; offering = 10f; break;
            default: selfUse = 45f; gift = 30f; offering = 15f; break;
        }

        float roll = UnityEngine.Random.Range(0f, 100f);
        if (roll < selfUse) return VisitPurpose.SelfUse;
        if (roll < selfUse + gift) return VisitPurpose.Gift;
        if (roll < selfUse + gift + offering) return VisitPurpose.Offering;
        return VisitPurpose.Anniversary;
    }

    private static float GetPurposeBudgetMultiplier(VisitPurpose purpose)
    {
        return purpose switch
        {
            VisitPurpose.Gift => 1.10f,
            VisitPurpose.Offering => 1.05f,
            VisitPurpose.Anniversary => 1.25f,
            _ => 1f
        };
    }

    public static string GetPurposeLabel(VisitPurpose purpose)
    {
        return purpose switch
        {
            VisitPurpose.SelfUse => "自宅用",
            VisitPurpose.Gift => "プレゼント",
            VisitPurpose.Offering => "お供え",
            VisitPurpose.Anniversary => "記念日",
            _ => "その他"
        };
    }

    private CustomerData PickCustomerProfile()
    {
        if (customerProfiles == null || customerProfiles.Count == 0) return null;

        float totalWeight = 0f;
        foreach (CustomerData profile in customerProfiles)
            if (profile != null) totalWeight += GetProfileSpawnWeight(profile);

        float roll = UnityEngine.Random.value * totalWeight;
        float cursor = 0f;

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null) continue;
            cursor += GetProfileSpawnWeight(profile);
            if (roll <= cursor) return profile;
        }

        return customerProfiles[^1];
    }

    private float GetProfileSpawnWeight(CustomerData profile)
    {
        float baseWeight = Mathf.Max(0.01f, profile.spawnWeight);
        RegularStatus status = regularStatuses.Find(s => s != null && s.customerType == profile.customerType);
        int regularCount = status != null ? status.regularCount : 0;
        return baseWeight * (1f + regularCount * (regularSpawnBonusPercent / 100f));
    }

    private void ApplyBaseSpawnWeights()
    {
        if (customerProfiles == null) return;

        float progress = GetShopRatingProgress();

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null) continue;

            float baseWeight = profile.customerType switch
            {
                CustomerType.Housewife => 42.5f,
                CustomerType.Student => 14.1667f,
                CustomerType.Grandmother => 28.3333f,
                CustomerType.Wealthy => 2.5f,
                CustomerType.Child => 2.5f,
                CustomerType.OfficeWorker => 10f,
                _ => 1f
            };

            float growth = profile.customerType switch
            {
                CustomerType.Housewife => Mathf.Lerp(1f, 0.80f, progress),
                CustomerType.Student => Mathf.Lerp(1f, 0.55f, progress),
                CustomerType.Grandmother => Mathf.Lerp(1f, 0.90f, progress),
                CustomerType.Wealthy => Mathf.Lerp(1f, 5.00f, progress),
                CustomerType.Child => Mathf.Lerp(1f, 0.70f, progress),
                CustomerType.OfficeWorker => Mathf.Lerp(1f, 1.80f, progress),
                _ => 1f
            };

            profile.spawnWeight = Mathf.Max(0.01f, baseWeight * growth);
        }
    }

    private void CaptureProfileBaselines()
    {
        if (customerProfiles == null) return;

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null || profileBaselines.ContainsKey(profile.customerType))
                continue;

            profileBaselines[profile.customerType] = new ProfileBaseline
            {
                budget = Mathf.Max(0, profile.budget),
                minPopularity = Mathf.Clamp(profile.minPopularity, 1, 10),
                maxPopularity = Mathf.Clamp(profile.maxPopularity, 1, 10),
                minRarity = Mathf.Clamp(profile.minRarity, 1, 10),
                maxRarity = Mathf.Clamp(profile.maxRarity, 1, 10)
            };
        }
    }

    private void ApplyShopRatingProfileGrowth()
    {
        if (customerProfiles == null) return;

        int rating = shopManager != null ? Mathf.Clamp(shopManager.ShopRating, 0, 10000) : 0;
        GetPreferenceExpansion(rating, out int lowerExpansion, out int upperExpansion);

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null || !profileBaselines.TryGetValue(profile.customerType, out ProfileBaseline baseline))
                continue;

            profile.minPopularity = Mathf.Clamp(baseline.minPopularity - lowerExpansion, 1, 10);
            profile.maxPopularity = Mathf.Clamp(baseline.maxPopularity + upperExpansion, 1, 10);
            profile.minRarity = Mathf.Clamp(baseline.minRarity - lowerExpansion, 1, 10);
            profile.maxRarity = Mathf.Clamp(baseline.maxRarity + upperExpansion, 1, 10);
        }
    }

    private static void GetPreferenceExpansion(int rating, out int lowerExpansion, out int upperExpansion)
    {
        if (rating >= 8000)
        {
            lowerExpansion = 2;
            upperExpansion = 2;
        }
        else if (rating >= 5000)
        {
            lowerExpansion = 1;
            upperExpansion = 2;
        }
        else if (rating >= 2000)
        {
            lowerExpansion = 0;
            upperExpansion = 1;
        }
        else
        {
            lowerExpansion = 0;
            upperExpansion = 0;
        }
    }

    private int GetBaselineBudget(CustomerData profile)
    {
        if (profile == null) return 0;
        return profileBaselines.TryGetValue(profile.customerType, out ProfileBaseline baseline)
            ? Mathf.Max(0, baseline.budget)
            : Mathf.Max(0, profile.budget);
    }

    private float GetShopRatingBudgetMultiplier()
    {
        int rating = shopManager != null ? Mathf.Clamp(shopManager.ShopRating, 0, 10000) : 0;

        if (rating <= 1000) return Mathf.Lerp(1.00f, 1.05f, rating / 1000f);
        if (rating <= 2000) return Mathf.Lerp(1.05f, 1.15f, (rating - 1000) / 1000f);
        if (rating <= 3500) return Mathf.Lerp(1.15f, 1.35f, (rating - 2000) / 1500f);
        if (rating <= 5000) return Mathf.Lerp(1.35f, 1.60f, (rating - 3500) / 1500f);
        if (rating <= 6500) return Mathf.Lerp(1.60f, 1.95f, (rating - 5000) / 1500f);
        if (rating <= 8000) return Mathf.Lerp(1.95f, 2.35f, (rating - 6500) / 1500f);
        if (rating <= 9000) return Mathf.Lerp(2.35f, 2.70f, (rating - 8000) / 1000f);
        return Mathf.Lerp(2.70f, 3.20f, (rating - 9000) / 1000f);
    }

    private float GetShopRatingProgress()
    {
        int rating = shopManager != null ? Mathf.Clamp(shopManager.ShopRating, 0, 10000) : 0;
        return rating / 10000f;
    }

    private string PickFavoriteColor()
    {
        List<string> colors = new();

        if (inventorySystem != null)
        {
            foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
            {
                if (batch?.flower == null) continue;

                foreach (string color in batch.flower.GetColors())
                {
                    if (string.IsNullOrWhiteSpace(color)) continue;
                    if (!colors.Contains(color)) colors.Add(color);
                }
            }
        }

        if (colors.Count == 0)
            colors.AddRange(new[] { "赤", "桃", "白", "黄", "青", "紫", "橙", "緑" });

        float totalWeight = 0f;
        foreach (string color in colors)
            totalWeight += TrendSystem.IsMonthlyTrendColor(color, shopManager) ? TrendSystem.MonthlyFavoriteColorWeight : 1f;

        float roll = UnityEngine.Random.value * totalWeight;
        float cursor = 0f;
        foreach (string color in colors)
        {
            cursor += TrendSystem.IsMonthlyTrendColor(color, shopManager) ? TrendSystem.MonthlyFavoriteColorWeight : 1f;
            if (roll <= cursor) return color;
        }

        return colors[^1];
    }

    private void EnsureRegularStatuses()
    {
        regularStatuses ??= new List<RegularStatus>();

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null) continue;
            if (!regularStatuses.Exists(s => s != null && s.customerType == profile.customerType))
            {
                regularStatuses.Add(new RegularStatus
                {
                    customerType = profile.customerType,
                    currentPoints = 0,
                    regularCount = 0
                });
            }
        }
    }

    private void EnsureDefaultProfiles()
    {
        if (customerProfiles != null && customerProfiles.Count > 0) return;

        customerProfiles = new List<CustomerData>
        {
            new(CustomerType.Housewife, "主婦", 2000, 3, 7, 1, 8, 3, 42.5f),
            new(CustomerType.Student, "学生", 1000, 1, 6, 1, 10, 5, 14.1667f),
            new(CustomerType.Grandmother, "おばあさん", 5000, 5, 10, 3, 10, 5, 28.3333f),
            new(CustomerType.Wealthy, "富豪", 10000, 7, 10, 7, 10, 10, 2.5f),
            new(CustomerType.Child, "ちびっこ", 300, 1, 3, 1, 3, 10, 2.5f),
            new(CustomerType.OfficeWorker, "サラリーマン", 5000, 4, 8, 1, 10, 10, 10f)
        };
    }
}
