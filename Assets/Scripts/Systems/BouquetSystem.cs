using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// プレイヤーが作成した花束を管理します。
/// 通常の花束は3種類以上、合計3～25本で作成します。
/// 花束1個の作成にはラッピングを1個使用します。
/// 解体時はラッピングが戻り、販売時は戻りません。
/// 26本以上は将来の花束予約イベント専用とし、通常作成では扱いません。
/// </summary>
public class BouquetSystem : MonoBehaviour
{
    public const int MinimumBouquetQuantity = 3;
    public const int MaximumBouquetQuantity = 25;
    public const int WrappingCostPerBouquet = 1;

    [Serializable]
    public class BouquetFreshnessLot
    {
        [Min(1)] public int quantity = 1;
        [Min(1)] public int remainingFreshnessDays = 1;
    }

    [Serializable]
    public class BouquetComponent
    {
        public FlowerData flower;
        [Min(1)] public int quantity = 1;
        [Min(1)] public int remainingFreshnessDays = 1;
        public List<BouquetFreshnessLot> freshnessLots = new();

        public int OldestRemainingFreshnessDays => freshnessLots != null && freshnessLots.Count > 0
            ? freshnessLots.Min(l => l.remainingFreshnessDays)
            : remainingFreshnessDays;
    }

    [Serializable]
    public class BouquetData
    {
        public string bouquetName;
        [Min(1)] public int salePrice;
        public List<BouquetComponent> components = new();

        public int TotalQuantity => components?.Sum(c => c != null ? Mathf.Max(0, c.quantity) : 0) ?? 0;
        public int DistinctFlowerCount => components?.Count(c => c?.flower != null && c.quantity > 0) ?? 0;
        public int OldestRemainingFreshnessDays => components != null && components.Count > 0
            ? components.Where(c => c != null).Select(c => c.OldestRemainingFreshnessDays).DefaultIfEmpty(0).Min()
            : 0;

        public int MaterialCost => components?.Sum(c =>
            c?.flower != null ? c.flower.purchasePrice * Mathf.Max(0, c.quantity) : 0) ?? 0;
    }

    [Header("参照")]
    [SerializeField] private InventorySystem inventorySystem;

    [Header("ラッピング")]
    [Min(0)] [SerializeField] private int wrappingCount = 5;

    [Header("作成済み花束")]
    [SerializeField] private List<BouquetData> bouquets = new();

    public IReadOnlyList<BouquetData> Bouquets => bouquets;
    public int WrappingCount => wrappingCount;
    public bool CanCreateWithWrapping => wrappingCount >= WrappingCostPerBouquet;

    public event Action OnBouquetsChanged;
    public event Action OnWrappingChanged;

    /// <summary>
    /// AddWrapping（アド・ラッピング）
    /// レベルアップ報酬・仕入れ販売・おまけ・差し入れ等からラッピングを増やすための共通入口です。
    /// </summary>
    public void AddWrapping(int amount)
    {
        if (amount <= 0) return;
        wrappingCount += amount;
        OnWrappingChanged?.Invoke();
        Debug.Log($"ラッピングを{amount}個入手しました。所持数：{wrappingCount}");
    }

    public bool TryCreateBouquet(string bouquetName, int salePrice, List<BouquetComponent> requestedComponents, out string message)
    {
        message = string.Empty;

        if (inventorySystem == null)
        {
            message = "InventorySystemが設定されていません";
            return false;
        }

        if (!CanCreateWithWrapping)
        {
            message = "ラッピングが足りません";
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
        if (totalQuantity < MinimumBouquetQuantity || totalQuantity > MaximumBouquetQuantity)
        {
            message = $"通常の花束は合計{MinimumBouquetQuantity}～{MaximumBouquetQuantity}本で作成してください";
            return false;
        }

        foreach (BouquetComponent component in components)
        {
            if (component.flower == null || !component.flower.canUseInBouquet)
            {
                message = "花束に使用できない商品が含まれています";
                return false;
            }

            if (inventorySystem.GetTotalQuantity(component.flower) < component.quantity)
            {
                message = $"{component.flower.flowerName}（{component.flower.color}）の在庫が足りません";
                return false;
            }
        }

        foreach (BouquetComponent component in components)
        {
            List<InventorySystem.InventoryBatch> taken = inventorySystem.TakeFlowerLots(component.flower, component.quantity);
            if (taken.Sum(x => x.quantity) != component.quantity)
            {
                message = "花束材料の在庫処理に失敗しました";
                return false;
            }

            component.freshnessLots = taken
                .Select(x => new BouquetFreshnessLot
                {
                    quantity = x.quantity,
                    remainingFreshnessDays = x.remainingFreshnessDays
                })
                .ToList();
            component.remainingFreshnessDays = component.OldestRemainingFreshnessDays;
        }

        wrappingCount -= WrappingCostPerBouquet;

        BouquetData bouquet = new BouquetData
        {
            bouquetName = string.IsNullOrWhiteSpace(bouquetName) ? $"花束{bouquets.Count + 1}" : bouquetName.Trim(),
            salePrice = salePrice,
            components = components
        };

        bouquets.Add(bouquet);
        OnWrappingChanged?.Invoke();
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
        return CalculateRecommendedPrice(bouquet.MaterialCost, bouquet.TotalQuantity);
    }

    public static int CalculateRecommendedPrice(int materialCost, int totalQuantity)
    {
        if (materialCost <= 0 || totalQuantity <= 0) return 0;

        float multiplier = totalQuantity switch
        {
            <= 5 => 4.5f,
            <= 10 => 4.0f,
            <= 20 => 3.5f,
            _ => 3.25f
        };

        float rawPrice = materialCost * multiplier;
        int roundedTo50 = Mathf.FloorToInt((rawPrice + 25f) / 50f) * 50;
        return Mathf.Max(50, roundedTo50);
    }

    public int AdvanceFreshnessOneDay()
    {
        if (inventorySystem == null) return 0;

        int autoDisassembled = 0;
        List<BouquetData> expiredBouquets = new();

        foreach (BouquetData bouquet in bouquets)
        {
            bool hasExpiredLot = false;

            foreach (BouquetComponent component in bouquet.components)
            {
                EnsureFreshnessLots(component);

                foreach (BouquetFreshnessLot lot in component.freshnessLots)
                {
                    lot.remainingFreshnessDays--;
                    if (lot.remainingFreshnessDays <= 0)
                        hasExpiredLot = true;
                }

                component.remainingFreshnessDays = component.OldestRemainingFreshnessDays;
            }

            if (hasExpiredLot)
                expiredBouquets.Add(bouquet);
        }

        foreach (BouquetData bouquet in expiredBouquets)
        {
            ReturnAliveMaterials(bouquet);
            bouquets.Remove(bouquet);
            wrappingCount += WrappingCostPerBouquet;
            autoDisassembled++;
            Debug.Log($"{bouquet.bouquetName}は材料の鮮度切れにより自動解体されました。ラッピングが戻りました。");
        }

        if (autoDisassembled > 0)
            OnWrappingChanged?.Invoke();

        OnBouquetsChanged?.Invoke();
        return autoDisassembled;
    }

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

        ReturnAliveMaterials(bouquet);

        string name = bouquet.bouquetName;
        bouquets.Remove(bouquet);
        wrappingCount += WrappingCostPerBouquet;
        OnWrappingChanged?.Invoke();
        OnBouquetsChanged?.Invoke();

        message = $"{name}を解体し、材料とラッピングを戻しました";
        Debug.Log(message);
        return true;
    }

    public bool RemoveBouquet(BouquetData bouquet)
    {
        if (bouquet == null) return false;
        bool removed = bouquets.Remove(bouquet);
        if (removed) OnBouquetsChanged?.Invoke();
        return removed;
    }

    private void ReturnAliveMaterials(BouquetData bouquet)
    {
        if (bouquet?.components == null || inventorySystem == null) return;

        foreach (BouquetComponent component in bouquet.components)
        {
            if (component?.flower == null) continue;
            EnsureFreshnessLots(component);

            foreach (BouquetFreshnessLot lot in component.freshnessLots)
            {
                if (lot.quantity <= 0 || lot.remainingFreshnessDays <= 0) continue;
                inventorySystem.AddFlowerWithFreshness(component.flower, lot.quantity, lot.remainingFreshnessDays);
            }
        }
    }

    private static void EnsureFreshnessLots(BouquetComponent component)
    {
        if (component == null) return;
        component.freshnessLots ??= new List<BouquetFreshnessLot>();

        if (component.freshnessLots.Count == 0 && component.quantity > 0)
        {
            component.freshnessLots.Add(new BouquetFreshnessLot
            {
                quantity = component.quantity,
                remainingFreshnessDays = Mathf.Max(1, component.remainingFreshnessDays)
            });
        }
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
                remainingFreshnessDays = 1,
                freshnessLots = new List<BouquetFreshnessLot>()
            })
            .Where(c => c.quantity > 0)
            .ToList();
    }
}
