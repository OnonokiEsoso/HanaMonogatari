using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 商品ごとの販売価格を管理します。
/// 通常花は仕入価格の2倍、作成品はFlowerDataのおすすめ販売価格を初期値として使用できます。
/// </summary>
public class PricingSystem : MonoBehaviour
{
    [Serializable]
    public class PriceEntry
    {
        public FlowerData flower;
        [Min(1)] public int salePrice;
    }

    [SerializeField] private List<PriceEntry> prices = new();

    public IReadOnlyList<PriceEntry> Prices => prices;

    public event Action OnPricingChanged;

    public int GetRecommendedPrice(FlowerData flower)
    {
        if (flower == null) return 0;

        if (flower.recommendedSalePrice > 0)
            return flower.recommendedSalePrice;

        return Mathf.Max(1, flower.purchasePrice * 2);
    }

    public int GetSalePrice(FlowerData flower)
    {
        if (flower == null) return 0;

        PriceEntry entry = prices.FirstOrDefault(p => p.flower == flower);
        return entry != null ? entry.salePrice : GetRecommendedPrice(flower);
    }

    public bool SetSalePrice(FlowerData flower, int salePrice)
    {
        if (flower == null || salePrice <= 0) return false;

        PriceEntry entry = prices.FirstOrDefault(p => p.flower == flower);
        if (entry == null)
        {
            entry = new PriceEntry
            {
                flower = flower,
                salePrice = salePrice
            };
            prices.Add(entry);
        }
        else
        {
            entry.salePrice = salePrice;
        }

        OnPricingChanged?.Invoke();
        Debug.Log($"{flower.flowerName}（{flower.GetColorDisplayText()}）の販売価格を{salePrice:N0}円に設定しました。");
        return true;
    }

    public bool HasCustomPrice(FlowerData flower)
    {
        if (flower == null) return false;
        return prices.Any(p => p.flower == flower);
    }

    public void ResetToRecommendedPrice(FlowerData flower)
    {
        if (flower == null) return;

        PriceEntry entry = prices.FirstOrDefault(p => p.flower == flower);
        if (entry != null)
        {
            prices.Remove(entry);
            OnPricingChanged?.Invoke();
        }
    }
}
