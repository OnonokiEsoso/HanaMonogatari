using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VisitPurpose（ビジット・パーパス）＝来店目的。
/// 客が何のために花屋へ来たかを表します。
/// </summary>
public enum VisitPurpose
{
    SelfUse,
    Gift,
    Offering,
    Anniversary
}

/// <summary>
/// 1日の来客数・客タイプ抽選・来店目的・常連化の進行を担当します。
/// </summary>
public class CustomerSystem : MonoBehaviour
{
    [Serializable]
    public class VisitingCustomer
    {
        public CustomerData data;
        public string favoriteColor;
        public VisitPurpose purpose;

        public VisitingCustomer(CustomerData data, string favoriteColor, VisitPurpose purpose)
        {
            this.data = data;
            this.favoriteColor = favoriteColor;
            this.purpose = purpose;
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

    [Header("客タイプ")]
    [SerializeField] private List<CustomerData> customerProfiles = new();

    [Header("来客数")]
    [Tooltip("開店直後の来客ボーナス。ゲーム開始から2日間だけ加算します。")]
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

    /// <summary>
    /// GenerateTodayCustomers（ジェネレート・トゥデイ・カスタマーズ）
    /// Generate＝生成する。今日来る客をまとめて生成します。
    /// </summary>
    [ContextMenu("本日の来客を生成")]
    public void GenerateTodayCustomers()
    {
        EnsureDefaultProfiles();
        ApplyBaseSpawnWeights();
        EnsureRegularStatuses();
        todayCustomers.Clear();

        int visitorCount = CalculateTodayVisitorCount();

        for (int i = 0; i < visitorCount; i++)
        {
            CustomerData profile = PickCustomerProfile();
            if (profile == null) continue;

            string favoriteColor = PickFavoriteColor();
            VisitPurpose purpose = PickVisitPurpose(profile.customerType);
            todayCustomers.Add(new VisitingCustomer(profile, favoriteColor, purpose));
        }

        Debug.Log($"本日の来客を生成しました。{todayCustomers.Count}人");
    }

    public int CalculateTodayVisitorCount()
    {
        int rating = shopManager != null ? shopManager.ShopRating : 0;
        int baseVisitors = 2 + Mathf.FloorToInt(rating / 300f);

        float randomMultiplier = UnityEngine.Random.Range(0.8f, 1.2f);
        int visitors = Mathf.Max(1, Mathf.RoundToInt(baseVisitors * randomMultiplier));

        // 開店ボーナスは1年目の最初の2日間だけ。
        if (shopManager != null && shopManager.GameYear == 1 && shopManager.DayOfYear <= 2)
            visitors += openingBonusVisitors;

        return visitors;
    }

    /// <summary>
    /// AddRegularPoint（アド・レギュラー・ポイント）
    /// Add＝加える、Regular Point＝常連ポイント。
    /// 購入した客タイプへ1ポイント加え、上限に達すると常連人数を1人増やします。
    /// </summary>
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

    /// <summary>
    /// 客タイプごとに自然な来店目的を重み付きで抽選します。
    /// 数値は合計100になる割合です。
    /// </summary>
    private static VisitPurpose PickVisitPurpose(CustomerType customerType)
    {
        float selfUse;
        float gift;
        float offering;

        switch (customerType)
        {
            case CustomerType.Housewife:
                selfUse = 50f; gift = 25f; offering = 20f;
                break;
            case CustomerType.Student:
                selfUse = 45f; gift = 40f; offering = 5f;
                break;
            case CustomerType.Grandmother:
                selfUse = 35f; gift = 20f; offering = 40f;
                break;
            case CustomerType.Wealthy:
                selfUse = 15f; gift = 35f; offering = 10f;
                break;
            case CustomerType.Child:
                selfUse = 70f; gift = 25f; offering = 5f;
                break;
            case CustomerType.OfficeWorker:
                selfUse = 10f; gift = 55f; offering = 10f;
                break;
            default:
                selfUse = 45f; gift = 30f; offering = 15f;
                break;
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
        {
            if (profile != null)
                totalWeight += GetProfileSpawnWeight(profile);
        }

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
        float multiplier = 1f + regularCount * (regularSpawnBonusPercent / 100f);
        return baseWeight * multiplier;
    }

    /// <summary>
    /// 常連補正が0人の時の基礎来店率。
    /// 主婦42.5%、学生約14.17%、おばあさん約28.33%、サラリーマン10%、
    /// ちびっこ2.5%、富豪2.5%。
    /// おばあさん：学生：主婦 = 2：1：3。
    /// </summary>
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
        if (inventorySystem != null)
        {
            List<string> colors = new();
            foreach (InventorySystem.InventoryBatch batch in inventorySystem.Batches)
            {
                if (batch?.flower == null || string.IsNullOrWhiteSpace(batch.flower.color)) continue;
                if (!colors.Contains(batch.flower.color)) colors.Add(batch.flower.color);
            }

            if (colors.Count > 0)
                return colors[UnityEngine.Random.Range(0, colors.Count)];
        }

        string[] fallbackColors = { "赤", "桃", "白", "黄", "青", "紫", "橙", "緑" };
        return fallbackColors[UnityEngine.Random.Range(0, fallbackColors.Length)];
    }

    private void EnsureRegularStatuses()
    {
        if (regularStatuses == null)
            regularStatuses = new List<RegularStatus>();

        foreach (CustomerData profile in customerProfiles)
        {
            if (profile == null) continue;
            bool exists = regularStatuses.Exists(s => s != null && s.customerType == profile.customerType);
            if (!exists)
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
