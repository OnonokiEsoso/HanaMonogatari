using System;
using UnityEngine;

/// <summary>
/// デイリー／マンスリートレンドを日付から決定する静的システムです。
/// デイリーは各日30%で発生し、1か月最大3回。マンスリーはプレイヤーへ明示しない隠し補正です。
/// 日付をシードにしているため、同じ日付の途中で内容が変わりません。
/// </summary>
public static class TrendSystem
{
    public enum DailyTrendType
    {
        None,
        SingleFlowerUp,
        BouquetUp,
        VisitorsUp,
        BudgetUp
    }

    public readonly struct MonthlyTrend
    {
        public readonly string trendColor;
        public readonly bool visitorsUp;
        public readonly bool budgetUp;

        public MonthlyTrend(string trendColor, bool visitorsUp, bool budgetUp)
        {
            this.trendColor = trendColor;
            this.visitorsUp = visitorsUp;
            this.budgetUp = budgetUp;
        }
    }

    public const float DailyEventChance = 0.30f;
    public const int MaxDailyEventsPerMonth = 3;
    public const float CategoryChanceBonus = 0.20f;
    public const float DailyVisitorBonus = 0.30f;
    public const float DailyBudgetBonus = 0.15f;

    public const float MonthlyFavoriteColorWeight = 1.50f;
    public const int MonthlyTrendColorSatisfactionBonus = 1;
    public const float MonthlyVisitorBonus = 0.10f;
    public const float MonthlyBudgetBonus = 0.05f;

    private const float MonthlyExtraTrendChance = 0.20f;

    private static readonly string[] TrendColors =
    {
        "赤", "桃", "白", "黄", "橙", "紫", "青", "緑"
    };

    /// <summary>
    /// その日のデイリートレンドを返します。
    /// 月内を1日目から順に30%抽選し、当選は最大3日までです。
    /// </summary>
    public static DailyTrendType GetDailyTrend(int gameYear, int month, int day)
    {
        day = Mathf.Clamp(day, 1, ShopManager.DaysPerMonth);
        var random = new System.Random(BuildSeed(gameYear, month, 4171));
        int eventCount = 0;

        for (int currentDay = 1; currentDay <= ShopManager.DaysPerMonth; currentDay++)
        {
            DailyTrendType trend = DailyTrendType.None;

            if (eventCount < MaxDailyEventsPerMonth && random.NextDouble() < DailyEventChance)
            {
                eventCount++;
                trend = (DailyTrendType)random.Next(1, 5);
            }

            if (currentDay == day)
                return trend;
        }

        return DailyTrendType.None;
    }

    /// <summary>
    /// 毎月必ず流行色を1色決め、さらに来客+10%と予算+5%を各20%で独立抽選します。
    /// プレイヤーへは直接表示しません。
    /// </summary>
    public static MonthlyTrend GetMonthlyTrend(int gameYear, int month)
    {
        var random = new System.Random(BuildSeed(gameYear, month, 9283));
        string color = TrendColors[random.Next(TrendColors.Length)];
        bool visitorsUp = random.NextDouble() < MonthlyExtraTrendChance;
        bool budgetUp = random.NextDouble() < MonthlyExtraTrendChance;
        return new MonthlyTrend(color, visitorsUp, budgetUp);
    }

    /// <summary>
    /// 今日のトレンドによる来客率補正を「加算用の割合」で返します。
    /// 例：+30%なら0.30。月間+10%とデイリー+30%が同時なら0.40です。
    /// </summary>
    public static float GetVisitorBonusPercent(ShopManager shopManager)
    {
        if (shopManager == null) return 0f;

        MonthlyTrend monthly = GetMonthlyTrend(shopManager.GameYear, shopManager.CurrentMonth);
        DailyTrendType daily = GetDailyTrend(shopManager.GameYear, shopManager.CurrentMonth, shopManager.CurrentDay);

        float bonus = 0f;
        if (monthly.visitorsUp) bonus += MonthlyVisitorBonus;
        if (daily == DailyTrendType.VisitorsUp) bonus += DailyVisitorBonus;
        return bonus;
    }

    public static float GetVisitorMultiplier(ShopManager shopManager)
    {
        return 1f + GetVisitorBonusPercent(shopManager);
    }

    public static float GetBudgetMultiplier(ShopManager shopManager)
    {
        if (shopManager == null) return 1f;

        MonthlyTrend monthly = GetMonthlyTrend(shopManager.GameYear, shopManager.CurrentMonth);
        DailyTrendType daily = GetDailyTrend(shopManager.GameYear, shopManager.CurrentMonth, shopManager.CurrentDay);

        float bonus = 0f;
        if (monthly.budgetUp) bonus += MonthlyBudgetBonus;
        if (daily == DailyTrendType.BudgetUp) bonus += DailyBudgetBonus;

        // 天候の予算補正も同じ「加算してから一度だけ掛ける」ルールへ統合。
        // 雨の日はWeatherSystemから-3%が入ります。
        bonus += WeatherSystem.CurrentBudgetBonusPercent;

        return Mathf.Max(0f, 1f + bonus);
    }

    public static float ApplyBouquetChanceBonus(float bouquetChance, ShopManager shopManager)
    {
        if (shopManager == null) return Mathf.Clamp01(bouquetChance);

        DailyTrendType daily = GetDailyTrend(shopManager.GameYear, shopManager.CurrentMonth, shopManager.CurrentDay);
        if (daily == DailyTrendType.BouquetUp)
            bouquetChance += CategoryChanceBonus;
        else if (daily == DailyTrendType.SingleFlowerUp)
            bouquetChance -= CategoryChanceBonus;

        return Mathf.Clamp01(bouquetChance);
    }

    public static bool IsMonthlyTrendColor(string color, ShopManager shopManager)
    {
        if (shopManager == null || string.IsNullOrWhiteSpace(color)) return false;
        string trendColor = GetMonthlyTrend(shopManager.GameYear, shopManager.CurrentMonth).trendColor;
        return NormalizeColor(color) == NormalizeColor(trendColor);
    }

    public static string GetDailySupplierMessage(ShopManager shopManager)
    {
        if (shopManager == null) return null;

        return GetDailyTrend(shopManager.GameYear, shopManager.CurrentMonth, shopManager.CurrentDay) switch
        {
            DailyTrendType.SingleFlowerUp => "今日は一本ずつ選びたい人が多そうだよ！",
            DailyTrendType.BouquetUp => "今日は花束を探してる人が多そう！",
            DailyTrendType.VisitorsUp => "今日はなんだか人が多くなりそうだね！",
            DailyTrendType.BudgetUp => "今日はちょっと奮発する人が多いかも！",
            _ => null
        };
    }

    private static int BuildSeed(int gameYear, int month, int salt)
    {
        unchecked
        {
            int seed = 17;
            seed = seed * 31 + gameYear;
            seed = seed * 31 + month;
            seed = seed * 31 + salt;
            return seed;
        }
    }

    private static string NormalizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return string.Empty;
        return color.Trim() switch
        {
            "桃" or "桃色" => "ピンク",
            "橙" or "橙色" => "オレンジ",
            "黄色" => "黄",
            "赤色" => "赤",
            "青色" => "青",
            "紫色" => "紫",
            "白色" => "白",
            "緑色" => "緑",
            _ => color.Trim()
        };
    }
}
