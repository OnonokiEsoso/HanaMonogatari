using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 客が在庫の単品商品・作成済み花束から商品を選び、購入する処理を担当します。
/// 購入後は満足度・店評価・常連ポイントを処理します。
/// </summary>
public class CustomerPurchaseSystem : MonoBehaviour
{
    public enum SatisfactionLevel
    {
        Okay,
        Good,
        Best
    }

    [Serializable]
    public class PurchaseResult
    {
        public bool purchased;
        public CustomerSystem.VisitingCustomer customer;
        public FlowerData flower;
        public BouquetSystem.BouquetData bouquet;
        public int salePrice;
        public int satisfactionScore;
        public SatisfactionLevel satisfactionLevel;
        public int shopRatingGain;
        public int regularPoints;
        public int regularRequiredPoints;
        public int regularCount;
        public bool becameRegular;
        public int bouquetQualityScore;
        public string message;
    }

    private class Candidate
    {
        public FlowerData flower;
        public BouquetSystem.BouquetData bouquet;
        public int price;
        public float weight;
        public int bouquetQualityScore;
        public bool IsBouquet => bouquet != null;
    }

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private PricingSystem pricingSystem;
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("満足度による店評価")]
    [Min(0)] [SerializeField] private int okayRatingGain = 1;
    [Min(0)] [SerializeField] private int goodRatingGain = 3;
    [Min(0)] [SerializeField] private int bestRatingGain = 5;

    /// <summary>
    /// TryPurchase（トライ・パーチェス）＝購入を試みる。
    /// 条件に合う単品商品または花束があれば1個購入します。
    /// </summary>
    public PurchaseResult TryPurchase(CustomerSystem.VisitingCustomer customer)
    {
        PurchaseResult result = new PurchaseResult
        {
            customer = customer,
            purchased = false,
            message = "購入しませんでした"
        };

        if (customer?.data == null || inventorySystem == null || pricingSystem == null || shopManager == null)
        {
            result.message = "購入処理に必要な参照が不足しています";
            return result;
        }

        List<Candidate> candidates = BuildCandidates(customer);
        if (candidates.Count == 0)
        {
            result.message = $"{customer.data.displayName}：条件に合う商品がありませんでした";
            return result;
        }

        Candidate selected = PickWeighted(candidates);
        if (selected == null)
        {
            result.message = "商品抽選に失敗しました";
            return result;
        }

        int satisfactionScore;
        string purchasedItemText;
        string bouquetQualityText = string.Empty;

        if (selected.IsBouquet)
        {
            if (bouquetSystem == null || !bouquetSystem.RemoveBouquet(selected.bouquet))
            {
                result.message = "花束の販売処理に失敗しました";
                return result;
            }

            satisfactionScore = CalculateBouquetSatisfactionScore(customer, selected.bouquet, selected.price, selected.bouquetQualityScore);
            purchasedItemText = selected.bouquet.bouquetName;
            bouquetQualityText = $"　花束評価：{selected.bouquetQualityScore}/10";
            result.bouquet = selected.bouquet;
            result.bouquetQualityScore = selected.bouquetQualityScore;
        }
        else
        {
            if (selected.flower == null || !inventorySystem.TryRemoveFlower(selected.flower, 1))
            {
                result.message = "在庫が足りませんでした";
                return result;
            }

            satisfactionScore = CalculateSatisfactionScore(customer, selected.flower, selected.price);
            purchasedItemText = $"{selected.flower.flowerName}（{selected.flower.color}）";
            result.flower = selected.flower;
        }

        shopManager.AddMoney(selected.price);

        SatisfactionLevel satisfactionLevel = GetSatisfactionLevel(satisfactionScore);
        int ratingGain = GetRatingGain(satisfactionLevel);
        if (ratingGain > 0)
            shopManager.AddShopRating(ratingGain);

        CustomerSystem.RegularPointResult regularResult = default;
        if (customerSystem != null)
            regularResult = customerSystem.AddRegularPoint(customer.data.customerType);

        result.purchased = true;
        result.salePrice = selected.price;
        result.satisfactionScore = satisfactionScore;
        result.satisfactionLevel = satisfactionLevel;
        result.shopRatingGain = ratingGain;
        result.regularPoints = regularResult.currentPoints;
        result.regularRequiredPoints = regularResult.requiredPoints;
        result.regularCount = regularResult.regularCount;
        result.becameRegular = regularResult.becameRegular;

        string regularText = string.Empty;
        if (customerSystem != null)
        {
            regularText = regularResult.becameRegular
                ? $"　★常連になりました！ 常連{regularResult.regularCount}人"
                : $"　常連P {regularResult.currentPoints}/{regularResult.requiredPoints}";
        }

        result.message = $"{customer.data.displayName}が{purchasedItemText}を{selected.price:N0}円で購入しました　満足度：{GetSatisfactionLabel(satisfactionLevel)}　店評価+{ratingGain}{bouquetQualityText}{regularText}";
        Debug.Log(result.message);
        return result;
    }

    private List<Candidate> BuildCandidates(CustomerSystem.VisitingCustomer customer)
    {
        List<Candidate> candidates = new();
        AddFlowerCandidates(customer, candidates);
        AddBouquetCandidates(customer, candidates);
        return candidates;
    }

    private void AddFlowerCandidates(CustomerSystem.VisitingCustomer customer, List<Candidate> candidates)
    {
        var flowers = inventorySystem.Batches
            .Where(b => b?.flower != null && b.quantity > 0)
            .Select(b => b.flower)
            .Distinct()
            .ToList();

        foreach (FlowerData flower in flowers)
        {
            int rarity = flower.GetRarity(shopManager.CurrentSeason);
            int price = pricingSystem.GetSalePrice(flower);
            if (!MatchesCustomerRange(customer, flower.basePopularity, rarity, price)) continue;

            float favoriteColorWeight = ColorsEqual(flower.color, customer.favoriteColor) ? 1.5f : 1f;
            candidates.Add(new Candidate
            {
                flower = flower,
                price = price,
                weight = CalculatePurchaseWeight(customer, rarity, price, favoriteColorWeight)
            });
        }
    }

    /// <summary>
    /// AddBouquetCandidates（アド・ブーケ・キャンディデーツ）
    /// 花束評価を購入確率へ反映し、良い花束ほど選ばれやすくします。
    /// </summary>
    private void AddBouquetCandidates(CustomerSystem.VisitingCustomer customer, List<Candidate> candidates)
    {
        if (bouquetSystem == null) return;

        foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
        {
            if (bouquet?.components == null || bouquet.components.Count == 0) continue;

            int price = bouquet.salePrice;
            int popularity = GetBouquetAveragePopularity(bouquet);
            int rarity = GetBouquetAverageRarity(bouquet);
            if (!MatchesCustomerRange(customer, popularity, rarity, price)) continue;

            BouquetEvaluator.Evaluation evaluation = BouquetEvaluator.Evaluate(bouquet, shopManager.CurrentSeason);
            float favoriteColorWeight = IsFavoriteColorMain(customer, evaluation.mainColors) ? 1.5f : 1f;

            // 0点=0.75倍、5点=1.125倍、10点=1.5倍。
            // 悪い花束も完全に売れなくはせず、良い花束ほど明確に選ばれやすくする。
            float qualityWeight = 0.75f + evaluation.totalScore * 0.075f;

            candidates.Add(new Candidate
            {
                bouquet = bouquet,
                price = price,
                bouquetQualityScore = evaluation.totalScore,
                weight = CalculatePurchaseWeight(customer, rarity, price, favoriteColorWeight) * qualityWeight
            });
        }
    }

    private static bool MatchesCustomerRange(CustomerSystem.VisitingCustomer customer, int popularity, int rarity, int price)
    {
        if (popularity < customer.data.minPopularity || popularity > customer.data.maxPopularity) return false;
        if (rarity < customer.data.minRarity || rarity > customer.data.maxRarity) return false;
        if (price <= 0 || price > customer.data.budget) return false;
        return true;
    }

    private static float CalculatePurchaseWeight(CustomerSystem.VisitingCustomer customer, int rarity, int price, float favoriteColorWeight)
    {
        float rarityWeight = 1f + rarity * 0.1f;
        float budgetConsumption = price / (float)Mathf.Max(1, customer.data.budget);
        float priceWeight = 1f / (0.25f + budgetConsumption);
        return rarityWeight * priceWeight * favoriteColorWeight;
    }

    private int CalculateSatisfactionScore(CustomerSystem.VisitingCustomer customer, FlowerData flower, int price)
    {
        int rarity = flower.GetRarity(shopManager.CurrentSeason);
        return CalculateCommonSatisfactionScore(customer, flower.basePopularity, rarity, price, ColorsEqual(flower.color, customer.favoriteColor));
    }

    /// <summary>
    /// CalculateBouquetSatisfactionScore（カルキュレート・ブーケ・サティスファクション・スコア）
    /// 共通満足度に花束評価を加点します。5点以上で+1、8点以上で+2です。
    /// </summary>
    private int CalculateBouquetSatisfactionScore(CustomerSystem.VisitingCustomer customer, BouquetSystem.BouquetData bouquet, int price, int qualityScore)
    {
        BouquetEvaluator.Evaluation evaluation = BouquetEvaluator.Evaluate(bouquet, shopManager.CurrentSeason);
        int score = CalculateCommonSatisfactionScore(
            customer,
            GetBouquetAveragePopularity(bouquet),
            GetBouquetAverageRarity(bouquet),
            price,
            IsFavoriteColorMain(customer, evaluation.mainColors));

        if (qualityScore >= 8) score += 2;
        else if (qualityScore >= 5) score += 1;

        return score;
    }

    private static int CalculateCommonSatisfactionScore(CustomerSystem.VisitingCustomer customer, int popularity, int rarity, int price, bool favoriteColorMatches)
    {
        int score = 0;
        if (favoriteColorMatches) score += 2;

        int popularityCenter = Mathf.RoundToInt((customer.data.minPopularity + customer.data.maxPopularity) / 2f);
        score += Mathf.Abs(popularity - popularityCenter) <= 1 ? 2 : 1;

        int rarityCenter = Mathf.RoundToInt((customer.data.minRarity + customer.data.maxRarity) / 2f);
        score += Mathf.Abs(rarity - rarityCenter) <= 1 ? 2 : 1;

        float budgetRatio = price / (float)Mathf.Max(1, customer.data.budget);
        if (budgetRatio <= 0.5f) score += 2;
        else if (budgetRatio <= 0.75f) score += 1;

        return score;
    }

    private static int GetBouquetAveragePopularity(BouquetSystem.BouquetData bouquet)
    {
        int totalQuantity = bouquet.components.Sum(c => c?.flower != null ? Mathf.Max(0, c.quantity) : 0);
        if (totalQuantity <= 0) return 1;
        int weightedTotal = bouquet.components.Sum(c => c?.flower != null ? c.flower.basePopularity * Mathf.Max(0, c.quantity) : 0);
        return Mathf.Clamp(Mathf.RoundToInt(weightedTotal / (float)totalQuantity), 1, 10);
    }

    private int GetBouquetAverageRarity(BouquetSystem.BouquetData bouquet)
    {
        int totalQuantity = bouquet.components.Sum(c => c?.flower != null ? Mathf.Max(0, c.quantity) : 0);
        if (totalQuantity <= 0) return 1;
        int weightedTotal = bouquet.components.Sum(c => c?.flower != null ? c.flower.GetRarity(shopManager.CurrentSeason) * Mathf.Max(0, c.quantity) : 0);
        return Mathf.Clamp(Mathf.RoundToInt(weightedTotal / (float)totalQuantity), 1, 10);
    }

    private static bool IsFavoriteColorMain(CustomerSystem.VisitingCustomer customer, IReadOnlyList<string> mainColors)
    {
        if (customer == null || string.IsNullOrWhiteSpace(customer.favoriteColor) || mainColors == null) return false;
        return mainColors.Any(color => ColorsEqual(color, customer.favoriteColor));
    }

    private static bool ColorsEqual(string a, string b)
    {
        return NormalizeColor(a) == NormalizeColor(b);
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

    private static SatisfactionLevel GetSatisfactionLevel(int score)
    {
        if (score >= 6) return SatisfactionLevel.Best;
        if (score >= 3) return SatisfactionLevel.Good;
        return SatisfactionLevel.Okay;
    }

    private int GetRatingGain(SatisfactionLevel level)
    {
        return level switch
        {
            SatisfactionLevel.Best => bestRatingGain,
            SatisfactionLevel.Good => goodRatingGain,
            _ => okayRatingGain
        };
    }

    private static string GetSatisfactionLabel(SatisfactionLevel level)
    {
        return level switch
        {
            SatisfactionLevel.Best => "最高！",
            SatisfactionLevel.Good => "良い",
            _ => "まあまあ"
        };
    }

    private static Candidate PickWeighted(List<Candidate> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        float total = candidates.Sum(c => Mathf.Max(0.001f, c.weight));
        float roll = UnityEngine.Random.value * total;
        float cursor = 0f;

        foreach (Candidate candidate in candidates)
        {
            cursor += Mathf.Max(0.001f, candidate.weight);
            if (roll <= cursor) return candidate;
        }

        return candidates[^1];
    }
}
