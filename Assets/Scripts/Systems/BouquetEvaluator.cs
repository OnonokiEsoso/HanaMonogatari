using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 花束の構成を0～10点で評価します。
/// 色のまとまり・人気構成・珍しさ構成・主役・全体バランスを分けて採点します。
/// </summary>
public static class BouquetEvaluator
{
    public readonly struct Evaluation
    {
        public readonly int totalScore;
        public readonly int colorScore;
        public readonly int popularityScore;
        public readonly int rarityScore;
        public readonly int centerpieceScore;
        public readonly int balanceScore;
        public readonly IReadOnlyList<string> mainColors;

        public Evaluation(int total, int color, int popularity, int rarity, int centerpiece, int balance, IReadOnlyList<string> mainColors)
        {
            totalScore = total;
            colorScore = color;
            popularityScore = popularity;
            rarityScore = rarity;
            centerpieceScore = centerpiece;
            balanceScore = balance;
            this.mainColors = mainColors;
        }
    }

    public static Evaluation Evaluate(BouquetSystem.BouquetData bouquet, Season season)
    {
        if (bouquet?.components == null || bouquet.components.Count == 0)
            return new Evaluation(0, 0, 0, 0, 0, 0, Array.Empty<string>());

        List<BouquetSystem.BouquetComponent> components = bouquet.components
            .Where(c => c?.flower != null && c.quantity > 0)
            .ToList();

        int total = components.Sum(c => c.quantity);
        if (total <= 0)
            return new Evaluation(0, 0, 0, 0, 0, 0, Array.Empty<string>());

        List<string> mainColors = GetMainColors(components, total);
        int color = EvaluateColorHarmony(components, total, mainColors);       // 0-3
        int popularity = EvaluateDistribution(components, c => c.flower.basePopularity); // 0-2
        int rarity = EvaluateDistribution(components, c => c.flower.GetRarity(season));   // 0-2
        int centerpiece = EvaluateCenterpiece(components, season, total);      // 0-2
        int balance = EvaluateOverallBalance(components, total);                // 0-1

        int score = Mathf.Clamp(color + popularity + rarity + centerpiece + balance, 0, 10);
        return new Evaluation(score, color, popularity, rarity, centerpiece, balance, mainColors);
    }

    /// <summary>
    /// GetMainColors（ゲット・メイン・カラーズ）＝メイン色を取得する。
    /// 全体の1/3以上を占める色をすべてメイン色として扱います。
    /// </summary>
    public static List<string> GetMainColors(BouquetSystem.BouquetData bouquet)
    {
        if (bouquet?.components == null) return new List<string>();
        int total = bouquet.components.Where(c => c?.flower != null).Sum(c => Mathf.Max(0, c.quantity));
        return total > 0 ? GetMainColors(bouquet.components.Where(c => c?.flower != null).ToList(), total) : new List<string>();
    }

    private static List<string> GetMainColors(List<BouquetSystem.BouquetComponent> components, int total)
    {
        return components
            .GroupBy(c => NormalizeColor(c.flower.color))
            .Select(g => new { Color = g.Key, Quantity = g.Sum(c => c.quantity) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Color) && x.Quantity / (float)total >= 1f / 3f)
            .OrderByDescending(x => x.Quantity)
            .Select(x => x.Color)
            .ToList();
    }

    private static int EvaluateColorHarmony(List<BouquetSystem.BouquetComponent> components, int total, List<string> mainColors)
    {
        var colorQuantities = components
            .GroupBy(c => NormalizeColor(c.flower.color))
            .Select(g => new { Color = g.Key, Quantity = g.Sum(c => c.quantity) })
            .OrderByDescending(x => x.Quantity)
            .ToList();

        if (colorQuantities.Count == 1) return 3;
        if (mainColors.Count == 0) return 0; // メイン色不在の散らかったミックス

        string primaryFamily = GetColorFamily(mainColors[0]);
        int compatibleQuantity = colorQuantities
            .Where(x => AreColorFamiliesCompatible(primaryFamily, GetColorFamily(x.Color)))
            .Sum(x => x.Quantity);
        float compatibleRatio = compatibleQuantity / (float)total;

        if (mainColors.Count <= 2 && compatibleRatio >= 0.8f) return 3;
        if (compatibleRatio >= 0.65f) return 2;
        return 1;
    }

    /// <summary>
    /// 人気度・珍しさが「まとまった山」または「山が2つ」の構成を評価します。
    /// 極端に散らばるほど点を下げます。
    /// </summary>
    private static int EvaluateDistribution(List<BouquetSystem.BouquetComponent> components, Func<BouquetSystem.BouquetComponent, int> selector)
    {
        var weighted = components.Select(c => new { Value = selector(c), Weight = c.quantity }).ToList();
        int totalWeight = weighted.Sum(x => x.Weight);
        if (totalWeight <= 0) return 0;

        float mean = weighted.Sum(x => x.Value * x.Weight) / (float)totalWeight;
        float variance = weighted.Sum(x => x.Weight * Mathf.Pow(x.Value - mean, 2f)) / totalWeight;
        float sd = Mathf.Sqrt(variance);

        if (sd <= 1.25f) return 2;

        // 二峰型：値を低群・高群に分けた時、それぞれの群内がまとまっていれば高評価。
        var values = weighted.Select(x => x.Value).Distinct().OrderBy(v => v).ToList();
        if (values.Count >= 2)
        {
            for (int split = values.First(); split < values.Last(); split++)
            {
                var low = weighted.Where(x => x.Value <= split).ToList();
                var high = weighted.Where(x => x.Value > split).ToList();
                if (low.Count == 0 || high.Count == 0) continue;

                int lowWeight = low.Sum(x => x.Weight);
                int highWeight = high.Sum(x => x.Weight);
                if (lowWeight < totalWeight * 0.2f || highWeight < totalWeight * 0.2f) continue;

                float lowMean = low.Sum(x => x.Value * x.Weight) / (float)lowWeight;
                float highMean = high.Sum(x => x.Value * x.Weight) / (float)highWeight;
                float lowSd = Mathf.Sqrt(low.Sum(x => x.Weight * Mathf.Pow(x.Value - lowMean, 2f)) / lowWeight);
                float highSd = Mathf.Sqrt(high.Sum(x => x.Weight * Mathf.Pow(x.Value - highMean, 2f)) / highWeight);

                if (lowSd <= 1f && highSd <= 1f && Mathf.Abs(highMean - lowMean) >= 2f)
                    return 2;
            }
        }

        return sd <= 2.5f ? 1 : 0;
    }

    /// <summary>
    /// 1～2本だけ入った、周囲より人気または珍しさが高い花を「主役」として評価します。
    /// </summary>
    private static int EvaluateCenterpiece(List<BouquetSystem.BouquetComponent> components, Season season, int total)
    {
        foreach (BouquetSystem.BouquetComponent candidate in components)
        {
            if (candidate.quantity < 1 || candidate.quantity > 2 || total <= candidate.quantity) continue;

            int otherQty = total - candidate.quantity;
            float otherPopularity = components.Where(c => c != candidate).Sum(c => c.flower.basePopularity * c.quantity) / (float)otherQty;
            float otherRarity = components.Where(c => c != candidate).Sum(c => c.flower.GetRarity(season) * c.quantity) / (float)otherQty;

            int popularityLead = candidate.flower.basePopularity - Mathf.RoundToInt(otherPopularity);
            int rarityLead = candidate.flower.GetRarity(season) - Mathf.RoundToInt(otherRarity);
            int lead = Mathf.Max(popularityLead, rarityLead);

            if (lead >= 3) return 2;
            if (lead >= 2) return 1;
        }

        return 0;
    }

    private static int EvaluateOverallBalance(List<BouquetSystem.BouquetComponent> components, int total)
    {
        if (components.Count < 3 || total <= 0) return 0;
        int maxQuantity = components.Max(c => c.quantity);
        return maxQuantity / (float)total <= 0.7f ? 1 : 0;
    }

    private static string NormalizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return string.Empty;
        string c = color.Trim();
        return c switch
        {
            "桃" => "ピンク",
            "桃色" => "ピンク",
            "橙" => "オレンジ",
            "橙色" => "オレンジ",
            "黄色" => "黄",
            "赤色" => "赤",
            "青色" => "青",
            "紫色" => "紫",
            "白色" => "白",
            "緑色" => "緑",
            _ => c
        };
    }

    private static string GetColorFamily(string color)
    {
        color = NormalizeColor(color);
        return color switch
        {
            "赤" or "ピンク" or "オレンジ" => "暖色",
            "青" or "紫" => "寒色",
            "黄" or "緑" => "自然色",
            "白" or "黒" or "クリーム" => "中性色",
            _ => color
        };
    }

    private static bool AreColorFamiliesCompatible(string a, string b)
    {
        if (a == b) return true;
        if (a == "中性色" || b == "中性色") return true;
        if ((a == "暖色" && b == "自然色") || (a == "自然色" && b == "暖色")) return true;
        if ((a == "寒色" && b == "自然色") || (a == "自然色" && b == "寒色")) return true;
        return false;
    }
}
