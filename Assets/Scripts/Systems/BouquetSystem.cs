using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// プレイヤーが作成した花束を管理します。
/// 花束は3種類以上、合計100本以内で作成します。
/// </summary>
public class BouquetSystem : MonoBehaviour
{
    [Serializable]
    public class BouquetComponent
    {
        public FlowerData flower;
        [Min(1)] public int quantity = 1;
        [Min(1)] public int remainingFreshnessDays = 1;
    }

    [Serializable]
    public class BouquetData
    {
        public string bouquetName;
        [Min(1)] public int salePrice;
        public List<BouquetComponent> components = new();

        public int TotalQuantity => components?.Sum(c => c != null ? Mathf.Max(0, c.quantity) : 0) ?? 0;
        public int DistinctFlowerCount => components?.Count(c => c?.flower != null && c.quantity > 0) ?? 0;

        public int MaterialCost => components?.Sum(c =>
            c?.flower != null ? c.flower.purchasePrice * Mathf.Max(0, c.quantity) : 0) ?? 0;
    }

    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;

    [Header("作成済み花束")]
    [SerializeField] private List<BouquetData> bouquets = new();

    public IReadOnlyList<BouquetData> Bouquets => bouquets;
    public event Action OnBouquetsChanged;

    /// <summary>
    /// TryCreateBouquet（トライ・クリエイト・ブーケ）
    /// Try＝試す、Create Bouquet＝花束を作る。
    /// 条件と在庫を確認し、成功した場合だけ材料を在庫から減らして花束を登録します。
    /// </summary>
    public bool TryCreateBouquet(string bouquetName, int salePrice, List<BouquetComponent> requestedComponents, out string message)
    {
        message = string.Empty;

        if (inventorySystem == null)
        {
            message = "InventorySystemが設定されていません";
            return false;
        }

        if (salePrice <= 0)
        {
            message = "販売価格は1円以上にしてください";
            return false;
        }

        List<BouquetComponent> components = NormalizeComponents(requestedComponents);

        if (components.Count < 3)
        {
            message = "花束には3種類以上の商品が必要です";
            return false;
        }

        int totalQuantity = components.Sum(c => c.quantity);
        if (totalQuantity > 100)
        {
            message = "花束は合計100本以内で作成してください";
            return false;
        }

        foreach (BouquetComponent component in components)
        {
            if (component.flower == null || !component.flower.canUseInBouquet)
            {
                message = "花束に使用できない商品が含まれています";
                return false;
            }

            int stock = inventorySystem.GetTotalQuantity(component.flower);
            if (stock < component.quantity)
            {
                message = $"{component.flower.flowerName}（{component.flower.color}）の在庫が足りません";
                return false;
            }

            // 解体時に鮮度が新品へ戻るのを防ぐため、作成時点の最も古い鮮度を保持します。
            component.remainingFreshnessDays = Mathf.Max(1, inventorySystem.GetOldestFreshnessDays(component.flower));
        }

        foreach (BouquetComponent component in components)
        {
            if (!inventorySystem.TryRemoveFlower(component.flower, component.quantity))
            {
                message = "花束材料の在庫処理に失敗しました";
                return false;
            }
        }

        BouquetData bouquet = new BouquetData
        {
            bouquetName = string.IsNullOrWhiteSpace(bouquetName) ? $"花束{bouquets.Count + 1}" : bouquetName.Trim(),
            salePrice = salePrice,
            components = components
        };

        bouquets.Add(bouquet);
        OnBouquetsChanged?.Invoke();

        message = $"{bouquet.bouquetName}を作成しました（{totalQuantity}本 / {salePrice:N0}円）";
        Debug.Log(message);
        return true;
    }

    public bool SetSalePrice(BouquetData bouquet, int price)
    {
        if (bouquet == null || price <= 0 || !bouquets.Contains(bouquet)) return false;

        bouquet.salePrice = price;
        OnBouquetsChanged?.Invoke();
        return true;
    }

    public int GetRecommendedPrice(BouquetData bouquet)
    {
        if (bouquet == null) return 0;
        return Mathf.Max(1, bouquet.MaterialCost * 2);
    }

    /// <summary>
    /// TryDisassembleBouquet（トライ・ディスアセンブル・ブーケ）
    /// Disassemble＝解体する。
    /// 花束を解体して、材料を保持していた鮮度のまま在庫へ戻します。
    /// </summary>
    public bool TryDisassembleBouquet(BouquetData bouquet, out string message)
    {
        message = string.Empty;

        if (bouquet == null || !bouquets.Contains(bouquet))
        {
            message = "解体する花束が見つかりません";
            return false;
        }

        if (inventorySystem == null)
        {
            message = "InventorySystemが設定されていません";
            return false;
        }

        foreach (BouquetComponent component in bouquet.components)
        {
            if (component?.flower == null || component.quantity <= 0) continue;

            inventorySystem.AddFlowerWithFreshness(
                component.flower,
                component.quantity,
                Mathf.Max(1, component.remainingFreshnessDays));
        }

        string name = bouquet.bouquetName;
        bouquets.Remove(bouquet);
        OnBouquetsChanged?.Invoke();

        message = $"{name}を解体し、材料を在庫へ戻しました";
        Debug.Log(message);
        return true;
    }

    /// <summary>
    /// 販売済み花束などを一覧から取り除きます。材料は戻しません。
    /// </summary>
    public bool RemoveBouquet(BouquetData bouquet)
    {
        if (bouquet == null) return false;
        bool removed = bouquets.Remove(bouquet);
        if (removed) OnBouquetsChanged?.Invoke();
        return removed;
    }

    private static List<BouquetComponent> NormalizeComponents(List<BouquetComponent> requestedComponents)
    {
        if (requestedComponents == null)
            return new List<BouquetComponent>();

        return requestedComponents
            .Where(c => c?.flower != null && c.quantity > 0)
            .GroupBy(c => c.flower)
            .Select(g => new BouquetComponent
            {
                flower = g.Key,
                quantity = g.Sum(x => Mathf.Max(0, x.quantity)),
                remainingFreshnessDays = 1
            })
            .Where(c => c.quantity > 0)
            .ToList();
    }
}
