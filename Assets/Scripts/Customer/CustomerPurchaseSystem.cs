using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 客が「単品花」か「花束」かを先に決め、魅力で商品を見つけたあと価格を見て購入判断します。
/// 花束は基本1個、単品花は予算内で複数本・複数種類を購入できます。
/// 来店目的によって花束を見る確率も変化します。
/// 好みに合う商品がまったく無い場合、一部の客は人気度・珍しさを下方向にだけ妥協します。
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
        public bool compromised;
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

    [Header("妥協購入")]
    [Tooltip("本命商品が無い時、人気度を最低希望値から何段階まで下げて候補にするか。上方向への妥協はしません。")]
    [Range(1, 9)] [SerializeField] private int compromisePopularityDrop = 2;
    [Tooltip("本命商品が無い時、珍しさを最低希望値から何段階まで下げて候補にするか。上方向への妥協はしません。")]
    [Range(1, 9)] [SerializeField] private int compromiseRarityDrop = 2;
    [Tooltip("妥協候補を実際に買う確率へ掛ける倍率。妥協時は候補確認も1商品だけです。")]
    [Range(0f, 1f)] [SerializeField] private float compromisePurchaseChanceMultiplier = 0.35f;
    [Tooltip("妥協購入時に満足度スコアから引く値。さらに最高評価にはならないよう上限5に固定します。")]
    [Min(0)] [SerializeField] private int compromiseSatisfactionPenalty = 1;

    private const int MaxCandidateChecks = 3;
    private const int MaxFlowerShoppingRounds = 3;
    private const int CompromiseCandidateChecks = 1;
    private const int CompromiseFlowerShoppingRounds = 1;
    private const int CompromiseMaxSatisfactionScore = 5;

    public PurchaseResult TryPurchase(CustomerSystem.VisitingCustomer customer)
    {
        PurchaseResult result = new PurchaseResult
        {
            customer = customer,
            purchased = false,
            compromised = false,
            message = "購入しませんでした"
        };

        if (customer?.data == null || inventorySystem == null || pricingSystem == null || shopManager == null)
        {
            result.message = "購入処理に必要な参照が不足しています";
            return result;
        }

        float bouquetChance = TrendSystem.ApplyBouquetChanceBonus(GetPurposeAdjustedBouquetChance(customer), shopManager);
        bool originallyWantsBouquet = UnityEngine.Random.value < bouquetChance;
        bool wantsBouquet = originallyWantsBouquet;
        bool compromised = false;

        // まずは従来どおり、希望範囲内の商品を優先して探します。
        List<Candidate> preferred = wantsBouquet
            ? BuildBouquetCandidates(customer, false)
            : BuildFlowerCandidates(customer, false);

        if (preferred.Count == 0)
        {
            wantsBouquet = !wantsBouquet;
            preferred = wantsBouquet
                ? BuildBouquetCandidates(customer, false)
                : BuildFlowerCandidates(customer, false);
        }

        // 本命商品が単品・花束のどちらにも無い時だけ妥協を検討します。
        // 富豪とちびっこは妥協しません。
        if (preferred.Count == 0 && CanCompromise(customer))
        {
            wantsBouquet = originallyWantsBouquet;
            preferred = wantsBouquet
                ? BuildBouquetCandidates(customer, true)
                : BuildFlowerCandidates(customer, true);

            if (preferred.Count == 0)
            {
                wantsBouquet = !wantsBouquet;
                preferred = wantsBouquet
                    ? BuildBouquetCandidates(customer, true)
                    : BuildFlowerCandidates(customer, true);
            }

            compromised = preferred.Count > 0;
        }

        if (preferred.Count == 0)
        {
            result.message = $"{customer.data.displayName}（{CustomerSystem.GetPurposeLabel(customer.purpose)}）：好みに合う商品がありませんでした";
            return result;
        }

        if (wantsBouquet)
            TryPurchaseBouquet(customer, preferred, result, compromised);
        else
            TryPurchaseFlowers(customer, result, compromised);

        return result;
    }

    private static bool CanCompromise(CustomerSystem.VisitingCustomer customer)
    {
        if (customer?.data == null) return false;
        return customer.data.customerType != CustomerType.Wealthy
            && customer.data.customerType != CustomerType.Child;
    }

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

    private static float GetPurposeAdjustedBouquetChance(CustomerSystem.VisitingCustomer customer)
    {
        float baseChance = GetBouquetPreferenceChance(customer.data.customerType);

        float adjusted = customer.purpose switch
        {
            VisitPurpose.SelfUse => baseChance * 0.45f,
            VisitPurpose.Gift => Mathf.Lerp(baseChance, 1f, 0.25f),
            VisitPurpose.Offering => baseChance * 0.75f,
            VisitPurpose.Anniversary => Mathf.Lerp(baseChance, 1f, 0.55f),
            _ => baseChance
        };

        return Mathf.Clamp01(adjusted);
    }

    private void TryPurchaseBouquet(
        CustomerSystem.VisitingCustomer customer,
        List<Candidate> candidates,
        PurchaseResult result,
        bool compromised)
    {
        List<Candidate> remaining = new(candidates);
        int maxChecks = compromised ? CompromiseCandidateChecks : MaxCandidateChecks;

        for (int attempt = 0; attempt < maxChecks && remaining.Count > 0; attempt++)
        {
            Candidate selected = PickWeighted(remaining);
            if (selected == null) break;
            remaining.Remove(selected);

            if (selected.price <= 0 || selected.price > customer.budget)
                continue;

            float buyChance = CalculateBouquetPurchaseChance(
                selected.price,
                selected.recommendedPrice,
                selected.bouquetQualityScore);

            if (compromised)
                buyChance *= compromisePurchaseChanceMultiplier;

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
                selected.bouquetQualityScore,
                compromised);
            return;
        }

        result.message = compromised
            ? $"{customer.data.displayName}（{CustomerSystem.GetPurposeLabel(customer.purpose)}）は少し妥協して花束を見ましたが、購入しませんでした"
            : $"{customer.data.displayName}（{CustomerSystem.GetPurposeLabel(customer.purpose)}）は花束を見ましたが、値段などを考えて購入しませんでした";
    }

    private void TryPurchaseFlowers(CustomerSystem.VisitingCustomer customer, PurchaseResult result, bool compromised)
    {
        int remainingBudget = Mathf.Max(0, customer.budget);
        List<FlowerPurchaseRecord> purchases = new();
        int maxRounds = compromised ? CompromiseFlowerShoppingRounds : MaxFlowerShoppingRounds;
        int maxChecks = compromised ? CompromiseCandidateChecks : MaxCandidateChecks;

        for (int round = 0; round < maxRounds && remainingBudget > 0; round++)
        {
            List<Candidate> candidates = BuildFlowerCandidates(customer, compromised);
            if (candidates.Count == 0) break;

            bool boughtThisRound = false;

            for (int attempt = 0; attempt < maxChecks && candidates.Count > 0; attempt++)
            {
                Candidate selected = PickWeighted(candidates);
                if (selected == null) break;
                candidates.Remove(selected);

                if (selected.flower == null || selected.price <= 0 || selected.price > remainingBudget)
                    continue;

                float buyChance = CalculatePricePurchaseChance(selected.price, selected.recommendedPrice);
                if (compromised)
                    buyChance *= compromisePurchaseChanceMultiplier;

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

            if (!boughtThisRound)
                break;

            if (!compromised)
            {
                if (round == 0 && UnityEngine.Random.value > 0.70f) break;
                if (round == 1 && UnityEngine.Random.value > 0.40f) break;
            }
        }

        if (purchases.Count == 0)
        {
            result.message = compromised
                ? $"{customer.data.displayName}（{CustomerSystem.GetPurposeLabel(customer.purpose)}）は少し妥協して花を見ましたが、購入しませんでした"
                : $"{customer.data.displayName}（{CustomerSystem.GetPurposeLabel(customer.purpose)}）は花を見ましたが、値段などを考えて購入しませんでした";
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
        CompleteSuccessfulVisit(customer, result, totalSpent, averageSatisfaction, itemText, null, singleFlower, 0, compromised);
    }

    private List<Candidate> BuildFlowerCandidates(CustomerSystem.VisitingCustomer customer, bool compromise)
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
            bool matches = compromise
                ? MatchesCompromiseRange(customer, flower.basePopularity, rarity)
                : MatchesAttractivenessRange(customer, flower.basePopularity, rarity);

            if (!matches) continue;
            if (price <= 0) continue;

            float favoriteColorWeight = ColorsEqual(flower.color, customer.favoriteColor) ? 1.6f : 1f;
            float weight = CalculateAttractivenessWeight(
                customer,
                flower.basePopularity,
                rarity,
                favoriteColorWeight,
                1f);

            if (compromise)
                weight *= CalculateCompromiseClosenessWeight(customer, flower.basePopularity, rarity);

            candidates.Add(new Candidate
            {
                flower = flower,
                price = price,
                recommendedPrice = pricingSystem.GetRecommendedPrice(flower),
                weight = weight
            });
        }

        return candidates;
    }

    private List<Candidate> BuildBouquetCandidates(CustomerSystem.VisitingCustomer customer, bool compromise)
    {
        List<Candidate> candidates = new();
        if (bouquetSystem == null) return candidates;

        foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
        {
            if (bouquet?.components == null || bouquet.components.Count == 0) continue;
            if (bouquet.salePrice <= 0) continue;

            int popularity = GetBouquetAveragePopularity(bouquet);
            int rarity = GetBouquetAverageRarity(bouquet);
            bool matches = compromise
                ? MatchesCompromiseRange(customer, popularity, rarity)
                : MatchesAttractivenessRange(customer, popularity, rarity);

            if (!matches) continue;

            BouquetEvaluator.Evaluation evaluation = BouquetEvaluator.Evaluate(bouquet, shopManager.CurrentSeason);
            float favoriteColorWeight = IsFavoriteColorMain(customer, evaluation.mainColors) ? 1.6f : 1f;
            float qualityWeight = 0.75f + evaluation.totalScore * 0.075f;
            float weight = CalculateAttractivenessWeight(
                customer,
                popularity,
                rarity,
                favoriteColorWeight,
                qualityWeight);

            if (compromise)
                weight *= CalculateCompromiseClosenessWeight(customer, popularity, rarity);

            candidates.Add(new Candidate
            {
                bouquet = bouquet,
                price = bouquet.salePrice,
                bouquetQualityScore = evaluation.totalScore,
                recommendedPrice = bouquetSystem.GetRecommendedPrice(bouquet),
                weight = weight
            });
        }

        return candidates;
    }

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

    private bool MatchesCompromiseRange(CustomerSystem.VisitingCustomer customer, int popularity, int rarity)
    {
        if (!CanCompromise(customer)) return false;

        // 上側の上限は絶対に広げない。
        if (popularity > customer.data.maxPopularity || rarity > customer.data.maxRarity)
            return false;

        int loweredMinPopularity = Mathf.Max(1, customer.data.minPopularity - compromisePopularityDrop);
        int loweredMinRarity = Mathf.Max(1, customer.data.minRarity - compromiseRarityDrop);

        if (popularity < loweredMinPopularity || rarity < loweredMinRarity)
            return false;

        // 妥協候補は、最低希望値を少なくとも片方で下回っている商品だけ。
        // つまり本命範囲の商品と妥協商品の候補が混ざることはありません。
        return popularity < customer.data.minPopularity || rarity < customer.data.minRarity;
    }

    private static float CalculateCompromiseClosenessWeight(
        CustomerSystem.VisitingCustomer customer,
        int popularity,
        int rarity)
    {
        int popularityShortfall = Mathf.Max(0, customer.data.minPopularity - popularity);
        int rarityShortfall = Mathf.Max(0, customer.data.minRarity - rarity);
        int totalShortfall = popularityShortfall + rarityShortfall;

        // 妥協するなら、希望条件により近い商品ほど選びやすくします。
        return 1f / (1f + totalShortfall * 0.5f);
    }

    private static float CalculatePricePurchaseChance(int salePrice, int recommendedPrice)
    {
        if (salePrice <= 0 || recommendedPrice <= 0) return 0f;

        float ratio = salePrice / (float)recommendedPrice;

        if (ratio <= 0.70f) return 0.98f;
        if (ratio <= 0.80f) return Mathf.Lerp(0.98f, 0.95f, Mathf.InverseLerp(0.70f, 0.80f, ratio));
        if (ratio <= 0.90f) return Mathf.Lerp(0.95f, 0.92f, Mathf.InverseLerp(0.80f, 0.90f, ratio));
        if (ratio <= 1.00f) return Mathf.Lerp(0.92f, 0.88f, Mathf.InverseLerp(0.90f, 1.00f, ratio));
        if (ratio <= 1.10f) return Mathf.Lerp(0.88f, 0.78f, Mathf.InverseLerp(1.00f, 1.10f, ratio));
        if (ratio <= 1.20f) return Mathf.Lerp(0.78f, 0.65f, Mathf.InverseLerp(1.10f, 1.20f, ratio));
        if (ratio <= 1.30f) return Mathf.Lerp(0.65f, 0.48f, Mathf.InverseLerp(1.20f, 1.30f, ratio));
        if (ratio <= 1.40f) return Mathf.Lerp(0.48f, 0.32f, Mathf.InverseLerp(1.30f, 1.40f, ratio));
        if (ratio <= 1.50f) return Mathf.Lerp(0.32f, 0.20f, Mathf.InverseLerp(1.40f, 1.50f, ratio));
        if (ratio <= 1.75f) return Mathf.Lerp(0.20f, 0.08f, Mathf.InverseLerp(1.50f, 1.75f, ratio));
        if (ratio <= 2.00f) return Mathf.Lerp(0.08f, 0.02f, Mathf.InverseLerp(1.75f, 2.00f, ratio));
        return 0.02f;
    }

    private static float CalculateBouquetPurchaseChance(int salePrice, int recommendedPrice, int qualityScore)
    {
        float baseChance = CalculatePricePurchaseChance(salePrice, recommendedPrice);

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
        int bouquetQualityScore,
        bool compromised)
    {
        if (compromised)
        {
            satisfactionScore = Mathf.Max(0, satisfactionScore - compromiseSatisfactionPenalty);
            satisfactionScore = Mathf.Min(satisfactionScore, CompromiseMaxSatisfactionScore);
        }

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
        result.compromised = compromised;

        string qualityText = bouquet != null ? $"　花束評価：{bouquetQualityScore}/10" : string.Empty;
        string regularText = string.Empty;
        if (customerSystem != null)
        {
            regularText = regularResult.becameRegular
                ? $"　★常連になりました！ 常連{regularResult.regularCount}人"
                : $"　常連P {regularResult.currentPoints}/{regularResult.requiredPoints}";
        }

        string purposeText = CustomerSystem.GetPurposeLabel(customer.purpose);
        string compromiseText = compromised ? "　妥協購入" : string.Empty;
        result.message = $"{customer.data.displayName}（{purposeText}）が{purchasedItemText}を購入　合計{totalSalePrice:N0}円　満足度：{GetSatisfactionLabel(satisfactionLevel)}　店評価+{ratingGain}{qualityText}{regularText}{compromiseText}";
        Debug.Log(result.message);
    }

    private int CalculateSatisfactionScore(CustomerSystem.VisitingCustomer customer, FlowerData flower, int price)
    {
        int rarity = flower.GetRarity(shopManager.CurrentSeason);
        int score = CalculateCommonSatisfactionScore(customer, flower.basePopularity, rarity, price, ColorsEqual(flower.color, customer.favoriteColor));
        if (TrendSystem.IsMonthlyTrendColor(flower.color, shopManager))
            score += TrendSystem.MonthlyTrendColorSatisfactionBonus;
        return score;
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

        if (evaluation.mainColors != null && evaluation.mainColors.Any(color => TrendSystem.IsMonthlyTrendColor(color, shopManager)))
            score += TrendSystem.MonthlyTrendColorSatisfactionBonus;

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

        float budgetRatio = price / (float)Mathf.Max(1, customer.budget);
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
