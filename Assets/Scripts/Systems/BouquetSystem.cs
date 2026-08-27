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
    }

    [Serializable]
    public class BouquetData
    {
        public string bouquetName;
        [Min(1)] public int salePrice;
        public List<BouquetComponent> components = new();

        public int TotalQuantity => components?.Sum(c => c != null ? Mathf.Max(0, c.quantity) : 0) ?? 0;
        public int DistinctFlowerCount => components?.Count(c => c?.flower != null && c.quantity > 0) ?? 0;
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
        }

        // 全材料の在庫確認が終わってから減らすため、途中失敗で在庫だけ減ることを防ぎます。
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

    /// <summary>
    /// RemoveBouquet（リムーブ・ブーケ）＝花束を一覧から取り除く。
    /// 現段階では材料を在庫へ戻さず、販売処理用の削除として使用します。
    /// 花束の解体処理は別途追加します。
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
                quantity = g.Sum(x => Mathf.Max(0, x.quantity))
            })
            .Where(c => c.quantity > 0)
            .ToList();
    }
}
