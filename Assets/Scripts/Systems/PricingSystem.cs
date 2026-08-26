using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 商品ごとの販売価格を管理します。
/// 値付けがまだ行われていない商品には、仕入価格の2倍を仮のおすすめ価格として使用します。
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

    /// <summary>
    /// GetRecommendedPrice（ゲット・レコメンデッド・プライス）
    /// Get＝取得する、Recommended＝おすすめ、Price＝価格。
    /// 現時点では仕入価格の2倍をおすすめ価格として返します。
    /// </summary>
    public int GetRecommendedPrice(FlowerData flower)
    {
        if (flower == null) return 0;
        return Mathf.Max(1, flower.purchasePrice * 2);
    }

    /// <summary>
    /// 商品の現在の販売価格を返します。
    /// 未設定ならおすすめ価格を返します。
    /// </summary>
    public int GetSalePrice(FlowerData flower)
    {
        if (flower == null) return 0;

        PriceEntry entry = prices.FirstOrDefault(p => p.flower == flower);
        return entry != null ? entry.salePrice : GetRecommendedPrice(flower);
    }

    /// <summary>
    /// SetSalePrice（セット・セール・プライス）
    /// Set＝設定する、Sale Price＝販売価格。
    /// 指定商品の販売価格を保存します。
    /// </summary>
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
        Debug.Log($"{flower.flowerName}（{flower.color}）の販売価格を{salePrice:N0}円に設定しました。");
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
