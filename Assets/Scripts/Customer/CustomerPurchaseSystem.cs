using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 客が在庫から商品を選び、購入する処理を担当します。
/// </summary>
public class CustomerPurchaseSystem : MonoBehaviour
{
    [Serializable]
    public class PurchaseResult
    {
        public bool purchased;
        public CustomerSystem.VisitingCustomer customer;
        public FlowerData flower;
        public int salePrice;
        public string message;
    }

    private class Candidate
    {
        public FlowerData flower;
        public int price;
        public float weight;
    }

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private PricingSystem pricingSystem;

    /// <summary>
    /// TryPurchase（トライ・パーチェス）＝購入を試みる。
    /// 条件に合う商品があれば1個購入します。
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

        if (!inventorySystem.TryRemoveFlower(selected.flower, 1))
        {
            result.message = "在庫が足りませんでした";
            return result;
        }

        shopManager.AddMoney(selected.price);

        result.purchased = true;
        result.flower = selected.flower;
        result.salePrice = selected.price;
        result.message = $"{customer.data.displayName}が{selected.flower.flowerName}（{selected.flower.color}）を{selected.price:N0}円で購入しました";

        Debug.Log(result.message);
        return result;
    }

    private List<Candidate> BuildCandidates(CustomerSystem.VisitingCustomer customer)
    {
        var flowers = inventorySystem.Batches
            .Where(b => b?.flower != null && b.quantity > 0)
            .Select(b => b.flower)
            .Distinct()
            .ToList();

        List<Candidate> candidates = new();

        foreach (FlowerData flower in flowers)
        {
            int rarity = flower.GetRarity(shopManager.CurrentSeason);
            int price = pricingSystem.GetSalePrice(flower);

            if (flower.basePopularity < customer.data.minPopularity || flower.basePopularity > customer.data.maxPopularity)
                continue;

            if (rarity < customer.data.minRarity || rarity > customer.data.maxRarity)
                continue;

            if (price <= 0 || price > customer.data.budget)
                continue;

            float rarityWeight = 1f + rarity * 0.1f;
            float budgetConsumption = price / (float)Mathf.Max(1, customer.data.budget);
            float priceWeight = 1f / (0.25f + budgetConsumption);
            float favoriteColorWeight = flower.color == customer.favoriteColor ? 1.5f : 1f;

            candidates.Add(new Candidate
            {
                flower = flower,
                price = price,
                weight = rarityWeight * priceWeight * favoriteColorWeight
            });
        }

        return candidates;
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
