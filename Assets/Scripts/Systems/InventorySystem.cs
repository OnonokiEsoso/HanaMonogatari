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

    public void AddFlower(FlowerData flower, int quantity)
    {
        if (flower == null || quantity <= 0) return;
        AddFlowerWithFreshness(flower, quantity, Mathf.Max(1, flower.freshnessDays));
    }

    /// <summary>
    /// AddFlowerWithFreshness（アド・フラワー・ウィズ・フレッシュネス）
    /// 指定した残り鮮度のまま商品を在庫へ戻します。
    /// </summary>
    public void AddFlowerWithFreshness(FlowerData flower, int quantity, int remainingFreshnessDays)
    {
        if (flower == null || quantity <= 0) return;

        int freshness = Mathf.Max(1, remainingFreshnessDays);
        InventoryBatch existing = batches.FirstOrDefault(b =>
            b.flower == flower && b.remainingFreshnessDays == freshness);

        if (existing != null)
            existing.quantity += quantity;
        else
            batches.Add(new InventoryBatch(flower, quantity, freshness));

        OnInventoryChanged?.Invoke();
    }

    public int GetTotalQuantity(FlowerData flower)
    {
        if (flower == null) return 0;
        return batches.Where(b => b.flower == flower).Sum(b => b.quantity);
    }

    public int GetOldestFreshnessDays(FlowerData flower)
    {
        if (flower == null) return 0;

        return batches
            .Where(b => b.flower == flower && b.quantity > 0)
            .Select(b => b.remainingFreshnessDays)
            .DefaultIfEmpty(0)
            .Min();
    }

    /// <summary>
    /// TakeFlowerLots（テイク・フラワー・ロッツ）
    /// Take＝取り出す、Lots＝鮮度別ロット。
    /// 鮮度の古い順に必要数を取り出し、「何日残りを何個使ったか」を返します。
    /// 在庫不足なら何も変更せず空リストを返します。
    /// </summary>
    public List<InventoryBatch> TakeFlowerLots(FlowerData flower, int quantity)
    {
        List<InventoryBatch> taken = new();
        if (flower == null || quantity <= 0) return taken;
        if (GetTotalQuantity(flower) < quantity) return taken;

        int remaining = quantity;
        List<InventoryBatch> targets = batches
            .Where(b => b.flower == flower && b.quantity > 0)
            .OrderBy(b => b.remainingFreshnessDays)
            .ToList();

        foreach (InventoryBatch batch in targets)
        {
            int take = Mathf.Min(batch.quantity, remaining);
            if (take <= 0) continue;

            taken.Add(new InventoryBatch(batch.flower, take, batch.remainingFreshnessDays));
            batch.quantity -= take;
            remaining -= take;

            if (remaining <= 0) break;
        }

        batches.RemoveAll(b => b.quantity <= 0);
        OnInventoryChanged?.Invoke();
        return taken;
    }

    /// <summary>
    /// 指定商品の在庫を減らします。鮮度の低いロットから先に使用します。
    /// </summary>
    public bool TryRemoveFlower(FlowerData flower, int quantity)
    {
        if (flower == null || quantity <= 0) return false;
        if (GetTotalQuantity(flower) < quantity) return false;

        List<InventoryBatch> taken = TakeFlowerLots(flower, quantity);
        return taken.Sum(x => x.quantity) == quantity;
    }

    /// <summary>
    /// 1日終了時に全在庫の鮮度を1日減らし、0日になった商品を廃棄します。
    /// </summary>
    public int AdvanceFreshnessOneDay()
    {
        int discarded = 0;

        foreach (InventoryBatch batch in batches)
        {
            batch.remainingFreshnessDays--;

            if (batch.remainingFreshnessDays <= 0)
                discarded += batch.quantity;
        }

        batches.RemoveAll(b => b.remainingFreshnessDays <= 0 || b.quantity <= 0);
        OnInventoryChanged?.Invoke();

        if (discarded > 0)
            Debug.Log($"鮮度切れにより{discarded}個の商品を廃棄しました。");

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
