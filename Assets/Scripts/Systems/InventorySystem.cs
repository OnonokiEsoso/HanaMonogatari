using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// お店が所持している花・植物の在庫を管理します。
/// 同じ商品でも仕入れ日が違うと鮮度が違うため、在庫は「ロット」単位で保持します。
/// </summary>
public class InventorySystem : MonoBehaviour
{
    [Serializable]
    public class InventoryBatch
    {
        public FlowerData flower;
        [Min(0)] public int quantity;
        [Min(0)] public int remainingFreshnessDays;

        public InventoryBatch(FlowerData flower, int quantity, int remainingFreshnessDays)
        {
            this.flower = flower;
            this.quantity = quantity;
            this.remainingFreshnessDays = remainingFreshnessDays;
        }
    }

    [SerializeField] private List<InventoryBatch> batches = new();

    public IReadOnlyList<InventoryBatch> Batches => batches;

    public event Action OnInventoryChanged;

    /// <summary>
    /// 仕入れた商品を在庫へ追加します。
    /// 同じ商品かつ同じ残り鮮度のロットがあればまとめます。
    /// </summary>
    public void AddFlower(FlowerData flower, int quantity)
    {
        if (flower == null || quantity <= 0) return;

        int freshness = Mathf.Max(1, flower.freshnessDays);
        InventoryBatch existing = batches.FirstOrDefault(b =>
            b.flower == flower && b.remainingFreshnessDays == freshness);

        if (existing != null)
        {
            existing.quantity += quantity;
        }
        else
        {
            batches.Add(new InventoryBatch(flower, quantity, freshness));
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 指定商品の現在庫数を、全ロット合計で返します。
    /// </summary>
    public int GetTotalQuantity(FlowerData flower)
    {
        if (flower == null) return 0;
        return batches.Where(b => b.flower == flower).Sum(b => b.quantity);
    }

    /// <summary>
    /// 指定商品の在庫を減らします。
    /// 鮮度の低いロットから先に使用します。
    /// 在庫不足の場合は何も変更せずfalseを返します。
    /// </summary>
    public bool TryRemoveFlower(FlowerData flower, int quantity)
    {
        if (flower == null || quantity <= 0) return false;
        if (GetTotalQuantity(flower) < quantity) return false;

        int remaining = quantity;
        List<InventoryBatch> targets = batches
            .Where(b => b.flower == flower && b.quantity > 0)
            .OrderBy(b => b.remainingFreshnessDays)
            .ToList();

        foreach (InventoryBatch batch in targets)
        {
            int take = Mathf.Min(batch.quantity, remaining);
            batch.quantity -= take;
            remaining -= take;

            if (remaining <= 0) break;
        }

        batches.RemoveAll(b => b.quantity <= 0);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 1日終了時に全在庫の鮮度を1日減らします。
    /// 0日になった商品は自動的に廃棄します。
    /// 戻り値は廃棄された合計個数です。
    /// </summary>
    public int AdvanceFreshnessOneDay()
    {
        int discarded = 0;

        foreach (InventoryBatch batch in batches)
        {
            batch.remainingFreshnessDays--;

            if (batch.remainingFreshnessDays <= 0)
            {
                discarded += batch.quantity;
            }
        }

        batches.RemoveAll(b => b.remainingFreshnessDays <= 0 || b.quantity <= 0);
        OnInventoryChanged?.Invoke();

        if (discarded > 0)
        {
            Debug.Log($"鮮度切れにより{discarded}個の商品を廃棄しました。");
        }

        return discarded;
    }

    [ContextMenu("在庫一覧をログ表示")]
    private void DebugPrintInventory()
    {
        if (batches.Count == 0)
        {
            Debug.Log("在庫は空です。");
            return;
        }

        foreach (InventoryBatch batch in batches)
        {
            if (batch.flower == null) continue;
            Debug.Log($"{batch.flower.flowerName}（{batch.flower.color}） x{batch.quantity} / 鮮度残り{batch.remainingFreshnessDays}日");
        }
    }
}
