using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// お店全体の進行状態を管理するクラス。
/// 所持金・店評価・日付・累計仕入額・仕入先Lv・月間集計をまとめて保持します。
/// ゲーム内カレンダーは4月スタート、1か月10日、1年120日です。
/// </summary>
public class ShopManager : MonoBehaviour
{
    public const int DaysPerMonth = 10;
    public const int MonthsPerYear = 12;
    public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

    [Header("参照")]
    [SerializeField] private SupplierSystem supplierSystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("お店の状態")]
    [Min(0)] [SerializeField] private int money = 10000;
    [Range(0, 10000)] [SerializeField] private int shopRating = 0;
    [Min(0)] [SerializeField] private int cumulativePurchaseAmount = 0;

    [Header("日付")]
    [Min(1)] [SerializeField] private int gameYear = 1;
    [Tooltip("4月1日を1日目とするゲーム内年間通算日。1年は120日です。")]
    [Range(1, DaysPerYear)] [SerializeField] private int dayOfYear = 1;

    [Header("仕入先")]
    [Range(1, 10)] [SerializeField] private int supplierLevel = 1;
    [Range(1, 10)] [SerializeField] private int pendingSupplierLevel = 1;
    [Tooltip("このゲーム中に一度でも仕入れた商品の識別キー。仕入れ画面のNew!表示判定に使います。")]
    [SerializeField] private List<string> purchasedSupplierProductKeys = new();

    [Header("月間集計")]
    [Min(0)] [SerializeField] private int monthlySales = 0;
    [Min(0)] [SerializeField] private int monthlyPurchaseCost = 0;
    [Min(0)] [SerializeField] private int monthlyVisitors = 0;
    [Min(0)] [SerializeField] private int monthlyBuyers = 0;
    [Min(0)] [SerializeField] private int monthlyShopRatingGain = 0;
    [Min(0)] [SerializeField] private int monthlyMaintenanceCost = 5000;
    [SerializeField] private bool recordedBusinessResultToday = false;

    [Header("ラッピング抽選")]
    [Min(0)] [SerializeField] private int todayFlowerPurchaseCount = 0;
    [SerializeField] private bool gotSupplierWrappingBonusToday = false;
    [SerializeField] private bool resolvedClosingGiftToday = false;

    [Header("クリア状態")]
    [SerializeField] private bool hasCleared = false;

    public int Money => money;
    public int ShopRating => shopRating;
    public int CumulativePurchaseAmount => cumulativePurchaseAmount;
    public int SupplierLevel => supplierLevel;
    public int PendingSupplierLevel => pendingSupplierLevel;
    public bool IsSupplierLevelUpPending => pendingSupplierLevel > supplierLevel;
    public int GameYear => gameYear;
    public int DayOfYear => dayOfYear;
    public bool HasCleared => hasCleared;
    public int TodayFlowerPurchaseCount => todayFlowerPurchaseCount;

    public int CurrentMonth => GetMonthAndDay(dayOfYear).month;
    public int CurrentDay => GetMonthAndDay(dayOfYear).day;
    public Season CurrentSeason => GetSeason(CurrentMonth);
    public string DateDisplayText => $"{gameYear}年目　{CurrentMonth}月 {CurrentDay}/{DaysPerMonth}日";
    public bool IsMonthEnd => CurrentDay == DaysPerMonth;

    public int MonthlySales => monthlySales;
    public int MonthlyPurchaseCost => monthlyPurchaseCost;
    public int MonthlyProfit => monthlySales - monthlyPurchaseCost;
    public int MonthlyVisitors => monthlyVisitors;
    public int MonthlyBuyers => monthlyBuyers;
    public int MonthlyShopRatingGain => monthlyShopRatingGain;
    public int MonthlyMaintenanceCost => monthlyMaintenanceCost;

    public event Action OnStateChanged;
    public event Action<int> OnSupplierPurchaseSpent;
    public event Action<string> OnSupplierProductPurchased;
    public event Action<int> OnSupplierFlowerPurchased;

    private void Awake()
    {
        purchasedSupplierProductKeys ??= new List<string>();
        dayOfYear = Mathf.Clamp(dayOfYear, 1, DaysPerYear);
        pendingSupplierLevel = Mathf.Max(supplierLevel, CalculateEligibleSupplierLevel());
        SyncSupplierSystem();
    }

    public bool HasPurchasedSupplierProduct(string productKey)
    {
        if (string.IsNullOrWhiteSpace(productKey)) return false;
        return purchasedSupplierProductKeys != null && purchasedSupplierProductKeys.Contains(productKey);
    }

    public void RegisterSupplierProductPurchase(string productKey)
    {
        if (string.IsNullOrWhiteSpace(productKey)) return;

        purchasedSupplierProductKeys ??= new List<string>();
        if (!purchasedSupplierProductKeys.Contains(productKey))
            purchasedSupplierProductKeys.Add(productKey);

        // New!判定とは別に、チャレンジ等では同じ商品を別の月に買ったことも知りたいので毎回通知する。
        OnSupplierProductPurchased?.Invoke(productKey);
        NotifyStateChanged();
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("ShopManager: 支払額に負の値は使用できません。");
            return false;
        }

        if (money < amount)
            return false;

        money -= amount;
        NotifyStateChanged();
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        money += amount;
        NotifyStateChanged();
    }

    /// <summary>
    /// 仕入先への支払いと累計仕入額を記録します。
    /// 花・家具・仕入先商品など、この入口を通った購入額はチャレンジ用イベントにも通知します。
    /// </summary>
    public bool TryPurchaseFromSupplier(int totalPrice)
    {
        if (totalPrice <= 0) return false;
        if (money < totalPrice) return false;

        money -= totalPrice;
        cumulativePurchaseAmount += totalPrice;
        monthlyPurchaseCost += totalPrice;

        OnSupplierPurchaseSpent?.Invoke(totalPrice);
        RefreshPendingSupplierLevel();
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// その日の営業結果を月間集計へ1回だけ加算します。
    /// </summary>
    public void RecordDailyBusinessResult(int sales, int visitors, int buyers)
    {
        if (recordedBusinessResultToday) return;

        monthlySales += Mathf.Max(0, sales);
        monthlyVisitors += Mathf.Max(0, visitors);
        monthlyBuyers += Mathf.Max(0, buyers);
        recordedBusinessResultToday = true;
    }

    public int PayMonthlyMaintenance()
    {
        int cost = Mathf.Max(0, monthlyMaintenanceCost);
        money -= cost;
        NotifyStateChanged();
        Debug.Log($"月末の店舗維持費として{cost:N0}円を支払いました。所持金：{money:N0}円");
        return cost;
    }

    public void ResetMonthlyStatistics()
    {
        monthlySales = 0;
        monthlyPurchaseCost = 0;
        monthlyVisitors = 0;
        monthlyBuyers = 0;
        monthlyShopRatingGain = 0;
    }

    public bool RegisterSupplierFlowerPurchase(int quantity)
    {
        if (quantity <= 0) return false;

        OnSupplierFlowerPurchased?.Invoke(quantity);

        bool won = false;
        for (int i = 0; i < quantity; i++)
        {
            todayFlowerPurchaseCount++;

            if (gotSupplierWrappingBonusToday || todayFlowerPurchaseCount <= 10)
                continue;

            float chance = Mathf.Clamp01((todayFlowerPurchaseCount - 10) * 0.01f);
            if (UnityEngine.Random.value < chance)
            {
                gotSupplierWrappingBonusToday = true;
                won = true;
                bouquetSystem?.AddWrapping(1);
                Debug.Log($"仕入れ{todayFlowerPurchaseCount}個目のおまけ抽選に当選！ ラッピングを1個もらいました。");
            }
        }

        return won;
    }

    public bool TryGiveClosingWrappingGift(int purchaserCount)
    {
        if (resolvedClosingGiftToday) return false;
        resolvedClosingGiftToday = true;

        if (purchaserCount <= 0 || bouquetSystem == null)
            return false;

        float chance = Mathf.Clamp01(purchaserCount * 0.01f);
        if (UnityEngine.Random.value >= chance)
            return false;

        bouquetSystem.AddWrapping(1);
        Debug.Log($"閉店後の差し入れ！ 購入者{purchaserCount}人 → {chance * 100f:0.#}%抽選に当選し、ラッピングを1個もらいました。");
        return true;
    }

    public void AddShopRating(int amount)
    {
        if (amount <= 0) return;

        int before = shopRating;
        shopRating = Mathf.Clamp(shopRating + amount, 0, 10000);
        monthlyShopRatingGain += Mathf.Max(0, shopRating - before);

        if (!hasCleared && shopRating >= 10000)
        {
            hasCleared = true;
            Debug.Log("ゲームクリア！ 街で一番人気のお花屋さんになりました！");
        }

        RefreshPendingSupplierLevel();
        NotifyStateChanged();
    }

    public void RemoveShopRating(int amount)
    {
        if (amount <= 0) return;
        shopRating = Mathf.Clamp(shopRating - amount, 0, 10000);
        RefreshPendingSupplierLevel();
        NotifyStateChanged();
    }

    public void ApplyDebugStartupState(
        bool overrideDate,
        int debugYear,
        int debugMonth,
        int debugDay,
        bool overrideMoney,
        int debugMoney,
        bool overrideShopRating,
        int debugShopRating,
        bool overrideSupplierLevel,
        int debugSupplierLevel,
        bool overrideCumulativePurchaseAmount,
        int debugCumulativePurchaseAmount)
    {
        if (overrideDate)
        {
            gameYear = Mathf.Max(1, debugYear);
            int month = Mathf.Clamp(debugMonth, 1, 12);
            int day = Mathf.Clamp(debugDay, 1, DaysPerMonth);
            dayOfYear = GetDayOfYear(month, day);
        }

        if (overrideMoney)
            money = Mathf.Max(0, debugMoney);

        if (overrideShopRating)
        {
            shopRating = Mathf.Clamp(debugShopRating, 0, 10000);
            hasCleared = shopRating >= 10000;
        }

        if (overrideCumulativePurchaseAmount)
            cumulativePurchaseAmount = Mathf.Max(0, debugCumulativePurchaseAmount);

        if (overrideSupplierLevel)
        {
            supplierLevel = Mathf.Clamp(debugSupplierLevel, 1, 10);
            pendingSupplierLevel = supplierLevel;
        }
        else
        {
            pendingSupplierLevel = Mathf.Max(supplierLevel, CalculateEligibleSupplierLevel());
        }

        SyncSupplierSystem();
        NotifyStateChanged();
    }

    [ContextMenu("翌日へ進む")]
    public void AdvanceDay()
    {
        dayOfYear++;

        if (dayOfYear > DaysPerYear)
        {
            dayOfYear = 1;
            gameYear++;
        }

        ApplyPendingSupplierLevel();

        todayFlowerPurchaseCount = 0;
        gotSupplierWrappingBonusToday = false;
        resolvedClosingGiftToday = false;
        recordedBusinessResultToday = false;

        SyncSupplierSystem();
        NotifyStateChanged();

        Debug.Log($"{DateDisplayText} / {CurrentSeason}");
    }

    public void RecalculateSupplierLevel()
    {
        RefreshPendingSupplierLevel();
    }

    private int CalculateEligibleSupplierLevel()
    {
        int newLevel = 1;

        if (shopRating >= 300 && cumulativePurchaseAmount >= 5000) newLevel = 2;
        if (shopRating >= 700 && cumulativePurchaseAmount >= 15000) newLevel = 3;
        if (shopRating >= 1200 && cumulativePurchaseAmount >= 30000) newLevel = 4;
        if (shopRating >= 2000 && cumulativePurchaseAmount >= 60000) newLevel = 5;
        if (shopRating >= 3000 && cumulativePurchaseAmount >= 100000) newLevel = 6;
        if (shopRating >= 4200 && cumulativePurchaseAmount >= 160000) newLevel = 7;
        if (shopRating >= 5500 && cumulativePurchaseAmount >= 250000) newLevel = 8;
        if (shopRating >= 7000 && cumulativePurchaseAmount >= 350000) newLevel = 9;
        if (shopRating >= 8500 && cumulativePurchaseAmount >= 500000) newLevel = 10;

        return newLevel;
    }

    private void RefreshPendingSupplierLevel()
    {
        int eligibleLevel = CalculateEligibleSupplierLevel();
        int newPendingLevel = Mathf.Max(supplierLevel, eligibleLevel);

        if (newPendingLevel != pendingSupplierLevel)
        {
            pendingSupplierLevel = newPendingLevel;

            if (pendingSupplierLevel > supplierLevel)
                Debug.Log($"仕入先Lv.{pendingSupplierLevel}の条件を達成しました。翌日にレベルアップします。");
        }
    }

    private void ApplyPendingSupplierLevel()
    {
        pendingSupplierLevel = Mathf.Max(supplierLevel, CalculateEligibleSupplierLevel());

        if (pendingSupplierLevel > supplierLevel)
        {
            int oldLevel = supplierLevel;
            supplierLevel = pendingSupplierLevel;

            int wrappingReward = 0;
            for (int level = oldLevel + 1; level <= supplierLevel; level++)
                wrappingReward += GetWrappingRewardForLevel(level);

            if (wrappingReward > 0)
                bouquetSystem?.AddWrapping(wrappingReward);

            Debug.Log($"仕入先Lvが{supplierLevel}になりました！ ラッピング報酬：{wrappingReward}個");
        }

        pendingSupplierLevel = supplierLevel;
    }

    private static int GetWrappingRewardForLevel(int level)
    {
        return level switch
        {
            2 => 2,
            3 => 2,
            4 => 3,
            5 => 3,
            6 => 4,
            7 => 4,
            8 => 5,
            9 => 5,
            10 => 10,
            _ => 0
        };
    }

    public void SyncSupplierSystem()
    {
        if (supplierSystem == null) return;
        supplierSystem.SetSupplierLevel(supplierLevel);
        supplierSystem.SetSeason(CurrentSeason);
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    private static Season GetSeason(int month)
    {
        return month switch
        {
            4 or 5 or 6 => Season.Spring,
            7 or 8 or 9 => Season.Summer,
            10 or 11 or 12 => Season.Autumn,
            _ => Season.Winter
        };
    }

    private static int GetDayOfYear(int month, int day)
    {
        int clampedMonth = Mathf.Clamp(month, 1, 12);
        int clampedDay = Mathf.Clamp(day, 1, DaysPerMonth);
        int monthIndexFromApril = (clampedMonth - 4 + 12) % 12;
        return monthIndexFromApril * DaysPerMonth + clampedDay;
    }

    private static (int month, int day) GetMonthAndDay(int day)
    {
        int clamped = Mathf.Clamp(day, 1, DaysPerYear) - 1;
        int monthIndex = clamped / DaysPerMonth;
        int dayInMonth = clamped % DaysPerMonth + 1;

        int month = ((3 + monthIndex) % 12) + 1;
        return (month, dayInMonth);
    }
}
