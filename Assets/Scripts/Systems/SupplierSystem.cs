using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 仕入先の1日分の商品ラインナップを生成するシステム。
/// 入荷難易度ごとの枠と、季節重みによる抽選を担当します。
/// </summary>
public class SupplierSystem : MonoBehaviour
{
    [Serializable]
    public class ArrivalItem
    {
        public FlowerData flower;
        public int purchaseLimit;
        public int purchasedQuantity;
        public bool fromRareChanceSlot;
        public int discountPercent;

        public int RemainingQuantity => Mathf.Max(0, purchaseLimit - purchasedQuantity);

        public int UnitPurchasePrice
        {
            get
            {
                if (flower == null) return 0;
                return Mathf.Max(0, Mathf.RoundToInt(flower.purchasePrice * (1f - discountPercent / 100f)));
            }
        }
    }

    private readonly struct ArrivalSlot
    {
        public readonly int minDifficulty;
        public readonly int maxDifficulty;
        public readonly int count;
        public readonly float appearanceChance;
        public readonly bool rareChanceSlot;

        public ArrivalSlot(int minDifficulty, int maxDifficulty, int count, float appearanceChance = 1f, bool rareChanceSlot = false)
        {
            this.minDifficulty = minDifficulty;
            this.maxDifficulty = maxDifficulty;
            this.count = count;
            this.appearanceChance = appearanceChance;
            this.rareChanceSlot = rareChanceSlot;
        }
    }

    [Header("参照")]
    [SerializeField] private FlowerDatabase flowerDatabase;

    [Header("現在の状態")]
    [Range(1, 10)] [SerializeField] private int supplierLevel = 1;
    [SerializeField] private Season currentSeason = Season.Spring;

    [Header("セール（仮初期値）")]
    [Tooltip("各入荷商品がセールになる確率。『極まれ』の仮値として2%。")]
    [Range(0f, 1f)] [SerializeField] private float saleChancePerItem = 0.02f;

    [Header("ラッピング販売")]
    [Range(0f, 1f)] [SerializeField] private float wrappingAppearanceChance = 0.15f;
    [Min(1)] [SerializeField] private int wrappingUnitPrice = 300;
    [SerializeField] private bool wrappingAvailableToday;
    [Min(0)] [SerializeField] private int wrappingPurchaseLimitToday;
    [Min(0)] [SerializeField] private int wrappingPurchasedToday;

    [SerializeField] private List<ArrivalItem> todayArrivals = new();

    public IReadOnlyList<ArrivalItem> TodayArrivals => todayArrivals;
    public int SupplierLevel => supplierLevel;
    public bool WrappingAvailableToday => wrappingAvailableToday;
    public int WrappingUnitPrice => wrappingUnitPrice;
    public int WrappingPurchaseLimitToday => wrappingPurchaseLimitToday;
    public int WrappingRemainingToday => Mathf.Max(0, wrappingPurchaseLimitToday - wrappingPurchasedToday);

    public void SetSupplierLevel(int level) => supplierLevel = Mathf.Clamp(level, 1, 10);
    public void SetSeason(Season season) => currentSeason = season;

    [ContextMenu("本日の入荷を生成")]
    public void GenerateDailyArrivals()
    {
        todayArrivals.Clear();
        RollWrappingOffer();

        if (flowerDatabase == null || flowerDatabase.flowers == null || flowerDatabase.flowers.Count == 0)
        {
            Debug.LogWarning("SupplierSystem: FlowerDatabaseが未設定、または商品がありません。");
            return;
        }

        var alreadySelected = new HashSet<FlowerData>();

        foreach (ArrivalSlot slot in GetSlotsForLevel(supplierLevel))
        {
            if (UnityEngine.Random.value > slot.appearanceChance) continue;

            List<FlowerData> candidates = flowerDatabase.flowers
                .Where(f => f != null
                    && !alreadySelected.Contains(f)
                    && f.arrivalDifficulty >= slot.minDifficulty
                    && f.arrivalDifficulty <= slot.maxDifficulty)
                .ToList();

            for (int i = 0; i < slot.count && candidates.Count > 0; i++)
            {
                FlowerData selected = PickWeightedBySeason(candidates, currentSeason);
                if (selected == null) break;

                candidates.Remove(selected);
                alreadySelected.Add(selected);

                todayArrivals.Add(new ArrivalItem
                {
                    flower = selected,
                    purchaseLimit = GetPurchaseLimit(supplierLevel, selected.arrivalDifficulty),
                    purchasedQuantity = 0,
                    fromRareChanceSlot = slot.rareChanceSlot,
                    discountPercent = RollSaleDiscount()
                });
            }
        }

        Debug.Log($"本日の入荷を生成しました。仕入先Lv.{supplierLevel} / {currentSeason} / {todayArrivals.Count}種類 / ラッピング販売:{(wrappingAvailableToday ? WrappingRemainingToday + "個" : "なし")}");
    }

    /// <summary>
    /// どのLvでも15%でラッピング販売。販売数は現在Lvと同数です。
    /// </summary>
    private void RollWrappingOffer()
    {
        wrappingAvailableToday = UnityEngine.Random.value < wrappingAppearanceChance;
        wrappingPurchaseLimitToday = wrappingAvailableToday ? supplierLevel : 0;
        wrappingPurchasedToday = 0;
    }

    public bool TryPurchaseWrapping(int quantity)
    {
        if (!wrappingAvailableToday || quantity <= 0) return false;
        if (WrappingRemainingToday < quantity) return false;

        wrappingPurchasedToday += quantity;
        return true;
    }

    private static FlowerData PickWeightedBySeason(List<FlowerData> candidates, Season season)
    {
        float totalWeight = 0f;
        foreach (FlowerData flower in candidates)
            totalWeight += flower.GetSeasonArrivalWeight(season);

        if (totalWeight <= 0f) return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        float roll = UnityEngine.Random.value * totalWeight;
        float cursor = 0f;

        foreach (FlowerData flower in candidates)
        {
            cursor += flower.GetSeasonArrivalWeight(season);
            if (roll <= cursor) return flower;
        }

        return candidates[^1];
    }

    private int RollSaleDiscount()
    {
        if (UnityEngine.Random.value > saleChancePerItem) return 0;
        return UnityEngine.Random.Range(1, 6) * 10;
    }

    private static int GetPurchaseLimit(int level, int difficulty)
    {
        int normalLimit = level switch
        {
            1 => 5,
            2 => 6,
            3 => 7,
            4 => 8,
            5 => 9,
            6 => 10,
            7 => 15,
            8 => 20,
            9 => 35,
            10 => 50,
            _ => 5
        };

        if (level == 8 && difficulty >= 9) return 1;
        if ((level == 7 || level == 8) && difficulty >= 6) return 3;
        if ((level == 9 || level == 10) && difficulty >= 9) return 3;

        return normalLimit;
    }

    private static List<ArrivalSlot> GetSlotsForLevel(int level)
    {
        return level switch
        {
            1 => new() { new(1,1,5) },
            2 => new() { new(1,1,3), new(2,2,3) },
            3 => new() { new(1,1,2), new(2,2,3), new(3,3,2) },
            4 => new() { new(1,1,2), new(2,2,3), new(3,3,3) },
            5 => new() { new(1,2,4), new(3,3,3), new(4,4,2) },
            6 => new() { new(1,2,4), new(3,3,3), new(4,4,2), new(5,5,1) },
            7 => new() { new(1,2,4), new(3,3,4), new(4,4,3), new(5,5,1), new(6,8,1,0.5f,true) },
            8 => new() { new(1,2,4), new(3,4,4), new(5,5,3), new(6,6,1), new(7,8,1,0.5f,true), new(9,10,1,0.1f,true) },
            9 => new() { new(1,2,4), new(3,4,3), new(5,6,3), new(7,8,3), new(9,10,1,0.5f,true) },
            10 => new() { new(1,3,4), new(4,6,5), new(7,9,5), new(10,10,1,0.5f,true) },
            _ => new() { new(1,1,5) }
        };
    }
}
