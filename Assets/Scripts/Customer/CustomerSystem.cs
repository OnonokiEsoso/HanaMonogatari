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

    [Header("客タイプ")]
    [SerializeField] private List<CustomerData> customerProfiles = new();

    [Header("来客数")]
    [Tooltip("ゲーム開始から2日間だけ、倍率計算後に固定人数として加算します。")]
    [SerializeField] private int openingBonusVisitors = 5;

    [Header("常連")]
    [Tooltip("常連1人につき、その客タイプの来店抽選重みを何%増やすか。")]
    [Min(0f)] [SerializeField] private float regularSpawnBonusPercent = 5f;
    [SerializeField] private List<RegularStatus> regularStatuses = new();

    [SerializeField] private List<VisitingCustomer> todayCustomers = new();

    public IReadOnlyList<VisitingCustomer> TodayCustomers => todayCustomers;
    public IReadOnlyList<RegularStatus> RegularStatuses => regularStatuses;

    private void Awake()
    {
        EnsureDefaultProfiles();
        ApplyBaseSpawnWeights();
        EnsureRegularStatuses();
    }

    [ContextMenu("本日の来客を生成")]
    public void GenerateTodayCustomers()
    {
        EnsureDefaultProfiles();
        ApplyBaseSpawnWeights();
        EnsureRegularStatuses();
        todayCustomers.Clear();

        int visitorCount = CalculateTodayVisitorCount();
        float budgetMultiplier = TrendSystem.GetBudgetMultiplier(shopManager);

        for (int i = 0; i < visitorCount; i++)
        {
            CustomerData profile = PickCustomerProfile();
            if (profile == null) continue;

            string favoriteColor = PickFavoriteColor();
            VisitPurpose purpose = PickVisitPurpose(profile.customerType);
            int effectiveBudget = Mathf.Max(0, Mathf.RoundToInt(profile.budget * budgetMultiplier));
            todayCustomers.Add(new VisitingCustomer(profile, favoriteColor, purpose, effectiveBudget));
        }

        Debug.Log($"本日の来客を生成しました。{todayCustomers.Count}人");
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

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null) continue;
            profile.spawnWeight = profile.customerType switch
            {
                CustomerType.Housewife => 42.5f,
                CustomerType.Student => 14.1667f,
                CustomerType.Grandmother => 28.3333f,
                CustomerType.Wealthy => 2.5f,
                CustomerType.Child => 2.5f,
                CustomerType.OfficeWorker => 10f,
                _ => 1f
            };
        }
    }

    private string PickFavoriteColor()
    {
        List<string> colors = new();

        if (inventorySystem != null)
        {
            foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
            {
                if (batch?.flower == null || string.IsNullOrWhiteSpace(batch.flower.color)) continue;
                if (!colors.Contains(batch.flower.color)) colors.Add(batch.flower.color);
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
