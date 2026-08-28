using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 客が「単品花」か「花束」かを先に決め、魅力で商品を見つけたあと価格を見て購入判断します。
/// 花束は基本1個、単品花は予算内で複数本・複数種類を購入できます。
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
        public int recommendedPrice;
        public bool IsBouquet => bouquet != null;
    }

    private class FlowerPurchaseRecord
    {
        public FlowerData flower;
        public int quantity;
        public int unitPrice;
        public int satisfactionScore;
        public int TotalPrice => unitPrice * quantity;
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

    private const int MaxCandidateChecks = 3;
    private const int MaxFlowerShoppingRounds = 3;

    /// <summary>
    /// TryPurchase（トライ・パーチェス）＝購入を試みる。
    /// 先に商品カテゴリを決め、魅力で候補を選び、その後に価格を見て購入判断します。
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

        bool wantsBouquet = UnityEngine.Random.value < GetBouquetPreferenceChance(customer.data.customerType);
        List<Candidate> preferred = wantsBouquet ? BuildBouquetCandidates(customer) : BuildFlowerCandidates(customer);

        // 希望カテゴリに魅力条件を満たす商品が1つもない時だけ、反対カテゴリも見ます。
        if (preferred.Count == 0)
        {
            wantsBouquet = !wantsBouquet;
            preferred = wantsBouquet ? BuildBouquetCandidates(customer) : BuildFlowerCandidates(customer);
        }

        if (preferred.Count == 0)
        {
            result.message = $"{customer.data.displayName}：好みに合う商品がありませんでした";
            return result;
        }

        if (wantsBouquet)
            TryPurchaseBouquet(customer, preferred, result);
        else
            TryPurchaseFlowers(customer, result);

        return result;
    }

    /// <summary>
    /// 客タイプごとの「今日は花束を見る」基礎確率。
    /// </summary>
    private static float GetBouquetPreferenceChance(CustomerType customerType)
    {
        return customerType switch
        {
            CustomerType.Housewife => 0.20f,
            CustomerType.Student => 0.10f,
            CustomerType.Grandmother => 0.30f,
            CustomerType.Wealthy => 0.70f,
            CustomerType.Child => 0.03f,
            CustomerType.OfficeWorker => 0.90f,
            _ => 0.20f
        };
    }

    private void TryPurchaseBouquet(CustomerSystem.VisitingCustomer customer, List<Candidate> candidates, PurchaseResult result)
    {
        List<Candidate> remaining = new(candidates);

        for (int attempt = 0; attempt < MaxCandidateChecks && remaining.Count > 0; attempt++)
        {
            Candidate selected = PickWeighted(remaining);
            if (selected == null) break;
            remaining.Remove(selected);

            // 値札を見るのは候補を気に入った後。予算オーバーならここで諦めます。
            if (selected.price <= 0 || selected.price > customer.data.budget)
                continue;

            float buyChance = CalculateBouquetPurchaseChance(
                selected.price,
                selected.recommendedPrice,
                selected.bouquetQualityScore);

            if (UnityEngine.Random.value > buyChance)
                continue;

            if (bouquetSystem == null || !bouquetSystem.RemoveBouquet(selected.bouquet))
                continue;

            shopManager.AddMoney(selected.price);

            int satisfactionScore = CalculateBouquetSatisfactionScore(
                customer,
                selected.bouquet,
                selected.price,
                selected.bouquetQualityScore);

            CompleteSuccessfulVisit(
                customer,
                result,
                selected.price,
                satisfactionScore,
                $"{selected.bouquet.bouquetName} ×1",
                selected.bouquet,
                null,
                selected.bouquetQualityScore);
            return;
        }

        result.message = $"{customer.data.displayName}は花束を見ましたが、値段などを考えて購入しませんでした";
    }

    private void TryPurchaseFlowers(CustomerSystem.VisitingCustomer customer, PurchaseResult result)
    {
        int remainingBudget = Mathf.Max(0, customer.data.budget);
        List<FlowerPurchaseRecord> purchases = new();

        for (int round = 0; round < MaxFlowerShoppingRounds && remainingBudget > 0; round++)
        {
            List<Candidate> candidates = BuildFlowerCandidates(customer);
            if (candidates.Count == 0) break;

            bool boughtThisRound = false;

            for (int attempt = 0; attempt < MaxCandidateChecks && candidates.Count > 0; attempt++)
            {
                Candidate selected = PickWeighted(candidates);
                if (selected == null) break;
                candidates.Remove(selected);

                if (selected.flower == null || selected.price <= 0 || selected.price > remainingBudget)
                    continue;

                // 単品花はまだ専用の適正価格を持たないため、今回は「残り予算に対する値段」で最終判断します。
                float buyChance = CalculateFlowerPurchaseChance(selected.price, remainingBudget);
                if (UnityEngine.Random.value > buyChance)
                    continue;

                int desiredQuantity = RollFlowerQuantity();
                int stock = inventorySystem.GetTotalQuantity(selected.flower);
                int affordableQuantity = remainingBudget / selected.price;
                int quantity = Mathf.Min(desiredQuantity, stock, affordableQuantity);
                if (quantity <= 0)
                    continue;

                if (!inventorySystem.TryRemoveFlower(selected.flower, quantity))
                    continue;

                int totalPrice = selected.price * quantity;
                remainingBudget -= totalPrice;
                shopManager.AddMoney(totalPrice);

                purchases.Add(new FlowerPurchaseRecord
                {
                    flower = selected.flower,
                    quantity = quantity,
                    unitPrice = selected.price,
                    satisfactionScore = CalculateSatisfactionScore(customer, selected.flower, selected.price)
                });

                boughtThisRound = true;
                break;
            }

            // 3候補見ても買わなかったら、その時点で買い物を終えます。
            if (!boughtThisRound)
                break;

            // 1回目の購入後は70%、2回目の購入後は40%でさらに店内を見る。3回目で終了。
            if (round == 0 && UnityEngine.Random.value > 0.70f) break;
            if (round == 1 && UnityEngine.Random.value > 0.40f) break;
        }

        if (purchases.Count == 0)
        {
            result.message = $"{customer.data.displayName}は花を見ましたが、値段などを考えて購入しませんでした";
            return;
        }

        int totalSpent = purchases.Sum(p => p.TotalPrice);
        int averageSatisfaction = Mathf.RoundToInt((float)purchases.Average(p => p.satisfactionScore));

        var grouped = purchases
            .GroupBy(p => p.flower)
            .Select(g => new
            {
                flower = g.Key,
                quantity = g.Sum(x => x.quantity)
            })
            .OrderBy(x => x.flower.sortOrder)
            .ToList();

        string itemText = string.Join("、", grouped.Select(x =>
            $"{x.flower.flowerName}（{x.flower.color}）×{x.quantity}"));

        FlowerData singleFlower = grouped.Count == 1 ? grouped[0].flower : null;
        CompleteSuccessfulVisit(customer, result, totalSpent, averageSatisfaction, itemText, null, singleFlower, 0);
    }

    private List<Candidate> BuildFlowerCandidates(CustomerSystem.VisitingCustomer customer)
    {
        List<Candidate> candidates = new();

        var flowers = inventorySystem.Batches
            .Where(b => b?.flower != null && b.quantity > 0)
            .Select(b => b.flower)
            .Distinct()
            .ToList();

        foreach (FlowerData flower in flowers)
        {
            int rarity = flower.GetRarity(shopManager.CurrentSeason);
            int price = pricingSystem.GetSalePrice(flower);
            if (!MatchesAttractivenessRange(customer, flower.basePopularity, rarity)) continue;
            if (price <= 0) continue;

            float favoriteColorWeight = ColorsEqual(flower.color, customer.favoriteColor) ? 1.6f : 1f;
            candidates.Add(new Candidate
            {
                flower = flower,
                price = price,
                weight = CalculateAttractivenessWeight(
                    customer,
                    flower.basePopularity,
                    rarity,
                    favoriteColorWeight,
                    1f)
            });
        }

        return candidates;
    }

    private List<Candidate> BuildBouquetCandidates(CustomerSystem.VisitingCustomer customer)
    {
        List<Candidate> candidates = new();
        if (bouquetSystem == null) return candidates;

        foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
        {
            if (bouquet?.components == null || bouquet.components.Count == 0) continue;
            if (bouquet.salePrice <= 0) continue;

            int popularity = GetBouquetAveragePopularity(bouquet);
            int rarity = GetBouquetAverageRarity(bouquet);
            if (!MatchesAttractivenessRange(customer, popularity, rarity)) continue;

            BouquetEvaluator.Evaluation evaluation = BouquetEvaluator.Evaluate(bouquet, shopManager.CurrentSeason);
            float favoriteColorWeight = IsFavoriteColorMain(customer, evaluation.mainColors) ? 1.6f : 1f;
            float qualityWeight = 0.75f + evaluation.totalScore * 0.075f;

            candidates.Add(new Candidate
            {
                bouquet = bouquet,
                price = bouquet.salePrice,
                bouquetQualityScore = evaluation.totalScore,
                recommendedPrice = bouquetSystem.GetRecommendedPrice(bouquet),
                weight = CalculateAttractivenessWeight(
                    customer,
                    popularity,
                    rarity,
                    favoriteColorWeight,
                    qualityWeight)
            });
        }

        return candidates;
    }

    /// <summary>
    /// 魅力の入口では価格を使いません。
    /// 人気度・珍しさの客との相性、好きな色、花束評価、0.85～1.15倍の個体差で重みを作ります。
    /// </summary>
    private static float CalculateAttractivenessWeight(
        CustomerSystem.VisitingCustomer customer,
        int popularity,
        int rarity,
        float favoriteColorWeight,
        float qualityWeight)
    {
        float popularityCenter = (customer.data.minPopularity + customer.data.maxPopularity) / 2f;
        float rarityCenter = (customer.data.minRarity + customer.data.maxRarity) / 2f;

        float popularityFit = Mathf.Clamp(1.5f - Mathf.Abs(popularity - popularityCenter) * 0.10f, 1f, 1.5f);
        float rarityFit = Mathf.Clamp(1.5f - Mathf.Abs(rarity - rarityCenter) * 0.10f, 1f, 1.5f);
        float personalSway = UnityEngine.Random.Range(0.85f, 1.15f);

        return popularityFit * rarityFit * favoriteColorWeight * qualityWeight * personalSway;
    }

    private static bool MatchesAttractivenessRange(CustomerSystem.VisitingCustomer customer, int popularity, int rarity)
    {
        if (popularity < customer.data.minPopularity || popularity > customer.data.maxPopularity) return false;
        if (rarity < customer.data.minRarity || rarity > customer.data.maxRarity) return false;
        return true;
    }

    /// <summary>
    /// 花束は「適正価格に対して何％の売価か」で最終購入率を決め、花束評価で少し補正します。
    /// </summary>
    private static float CalculateBouquetPurchaseChance(int salePrice, int recommendedPrice, int qualityScore)
    {
        if (salePrice <= 0 || recommendedPrice <= 0) return 0f;

        float ratio = salePrice / (float)recommendedPrice;
        float baseChance;

        if (ratio <= 0.70f) baseChance = 0.98f;
        else if (ratio <= 0.80f) baseChance = Mathf.Lerp(0.98f, 0.95f, Mathf.InverseLerp(0.70f, 0.80f, ratio));
        else if (ratio <= 0.90f) baseChance = Mathf.Lerp(0.95f, 0.92f, Mathf.InverseLerp(0.80f, 0.90f, ratio));
        else if (ratio <= 1.00f) baseChance = Mathf.Lerp(0.92f, 0.88f, Mathf.InverseLerp(0.90f, 1.00f, ratio));
        else if (ratio <= 1.10f) baseChance = Mathf.Lerp(0.88f, 0.78f, Mathf.InverseLerp(1.00f, 1.10f, ratio));
        else if (ratio <= 1.20f) baseChance = Mathf.Lerp(0.78f, 0.65f, Mathf.InverseLerp(1.10f, 1.20f, ratio));
        else if (ratio <= 1.30f) baseChance = Mathf.Lerp(0.65f, 0.48f, Mathf.InverseLerp(1.20f, 1.30f, ratio));
        else if (ratio <= 1.40f) baseChance = Mathf.Lerp(0.48f, 0.32f, Mathf.InverseLerp(1.30f, 1.40f, ratio));
        else if (ratio <= 1.50f) baseChance = Mathf.Lerp(0.32f, 0.20f, Mathf.InverseLerp(1.40f, 1.50f, ratio));
        else if (ratio <= 1.75f) baseChance = Mathf.Lerp(0.20f, 0.08f, Mathf.InverseLerp(1.50f, 1.75f, ratio));
        else if (ratio <= 2.00f) baseChance = Mathf.Lerp(0.08f, 0.02f, Mathf.InverseLerp(1.75f, 2.00f, ratio));
        else baseChance = 0.02f;

        float qualityMultiplier = qualityScore switch
        {
            <= 2 => 0.85f,
            <= 4 => 0.95f,
            <= 6 => 1.00f,
            <= 8 => 1.10f,
            _ => 1.20f
        };

        return Mathf.Clamp01(baseChance * qualityMultiplier);
    }

    /// <summary>
    /// 単品花は専用適正価格をまだ持たないため、残り予算に占める単価の割合で最終判断します。
    /// </summary>
    private static float CalculateFlowerPurchaseChance(int unitPrice, int remainingBudget)
    {
        if (unitPrice <= 0 || remainingBudget <= 0 || unitPrice > remainingBudget) return 0f;

        float ratio = unitPrice / (float)remainingBudget;
        if (ratio <= 0.10f) return 0.98f;
        if (ratio <= 0.25f) return 0.95f;
        if (ratio <= 0.50f) return 0.88f;
        if (ratio <= 0.75f) return 0.75f;
        return 0.60f;
    }

    private static int RollFlowerQuantity()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.45f) return 1;
        if (roll < 0.70f) return 2;
        if (roll < 0.85f) return 3;
        if (roll < 0.95f) return 4;
        return 5;
    }

    private void CompleteSuccessfulVisit(
        CustomerSystem.VisitingCustomer customer,
        PurchaseResult result,
        int totalSalePrice,
        int satisfactionScore,
        string purchasedItemText,
        BouquetSystem.BouquetData bouquet,
        FlowerData flower,
        int bouquetQualityScore)
    {
        SatisfactionLevel satisfactionLevel = GetSatisfactionLevel(satisfactionScore);
        int ratingGain = GetRatingGain(satisfactionLevel);
        if (ratingGain > 0)
            shopManager.AddShopRating(ratingGain);

        CustomerSystem.RegularPointResult regularResult = default;
        if (customerSystem != null)
            regularResult = customerSystem.AddRegularPoint(customer.data.customerType);

        result.purchased = true;
        result.flower = flower;
        result.bouquet = bouquet;
        result.salePrice = totalSalePrice;
        result.satisfactionScore = satisfactionScore;
        result.satisfactionLevel = satisfactionLevel;
        result.shopRatingGain = ratingGain;
        result.regularPoints = regularResult.currentPoints;
        result.regularRequiredPoints = regularResult.requiredPoints;
        result.regularCount = regularResult.regularCount;
        result.becameRegular = regularResult.becameRegular;
        result.bouquetQualityScore = bouquetQualityScore;

        string qualityText = bouquet != null ? $"　花束評価：{bouquetQualityScore}/10" : string.Empty;
        string regularText = string.Empty;
        if (customerSystem != null)
        {
            regularText = regularResult.becameRegular
                ? $"　★常連になりました！ 常連{regularResult.regularCount}人"
                : $"　常連P {regularResult.currentPoints}/{regularResult.requiredPoints}";
        }

        result.message = $"{customer.data.displayName}が{purchasedItemText}を購入　合計{totalSalePrice:N0}円　満足度：{GetSatisfactionLabel(satisfactionLevel)}　店評価+{ratingGain}{qualityText}{regularText}";
        Debug.Log(result.message);
    }

    private int CalculateSatisfactionScore(CustomerSystem.VisitingCustomer customer, FlowerData flower, int price)
    {
        int rarity = flower.GetRarity(shopManager.CurrentSeason);
        return CalculateCommonSatisfactionScore(customer, flower.basePopularity, rarity, price, ColorsEqual(flower.color, customer.favoriteColor));
    }

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
