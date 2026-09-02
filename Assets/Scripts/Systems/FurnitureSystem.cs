using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 家具の定義・購入済み状態・設置状態・効果を管理します。
/// 購入した家具はホームの家具画面から設置/撤去でき、設置中の家具だけ効果を発揮します。
/// 来客率補正はVisitorModifierSystemへ登録し、予算補正はCustomerSystemから参照します。
/// </summary>
public class FurnitureSystem : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private VisitorModifierSystem visitorModifierSystem;

    [Header("家具定義")]
    [SerializeField] private List<FurnitureData> furnitureDefinitions = new();

    [Header("購入済み家具")]
    [SerializeField] private List<FurnitureId> ownedFurniture = new();

    [Header("設置中の家具")]
    [SerializeField] private List<FurnitureId> installedFurniture = new();

    [Header("天候連携")]
    [Tooltip("天候システム実装前の仮フラグ。将来は天候側からSetRainyTodayを呼びます。")]
    [SerializeField] private bool isRainyToday;

    public IReadOnlyList<FurnitureData> Definitions => furnitureDefinitions;
    public IReadOnlyList<FurnitureId> OwnedFurniture => ownedFurniture;
    public IReadOnlyList<FurnitureId> InstalledFurniture => installedFurniture;
    public bool IsRainyToday => isRainyToday;
    public int OwnedCount => ownedFurniture?.Distinct().Count() ?? 0;
    public int InstalledCount => installedFurniture?.Distinct().Count() ?? 0;

    public event Action OnChanged;

    private void Awake()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (visitorModifierSystem == null)
            visitorModifierSystem = FindFirstObjectByType<VisitorModifierSystem>();

        EnsureDefinitions();
        ownedFurniture ??= new List<FurnitureId>();
        installedFurniture ??= new List<FurnitureId>();
        SanitizeInstalledFurniture();
    }

    private void OnEnable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged += RefreshEffects;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= RefreshEffects;
    }

    private void Start()
    {
        RefreshEffects();
    }

    public FurnitureData GetDefinition(FurnitureId id)
    {
        EnsureDefinitions();
        return furnitureDefinitions.FirstOrDefault(x => x != null && x.id == id);
    }

    public bool IsOwned(FurnitureId id)
    {
        return ownedFurniture != null && ownedFurniture.Contains(id);
    }

    public bool IsInstalled(FurnitureId id)
    {
        return installedFurniture != null && installedFurniture.Contains(id);
    }

    public IEnumerable<FurnitureData> GetOwnedDefinitions()
    {
        EnsureDefinitions();
        return furnitureDefinitions.Where(f => f != null && IsOwned(f.id));
    }

    public IEnumerable<FurnitureData> GetInstalledDefinitions()
    {
        EnsureDefinitions();
        return furnitureDefinitions.Where(f => f != null && IsInstalled(f.id));
    }

    public bool TryPurchase(FurnitureData furniture)
    {
        if (furniture == null || shopManager == null)
            return false;

        if (IsOwned(furniture.id))
        {
            Debug.Log($"{furniture.displayName}はすでに購入済みです。");
            return false;
        }

        if (!shopManager.TryPurchaseFromSupplier(furniture.purchasePrice))
        {
            Debug.Log($"所持金が足りないため、{furniture.displayName}を購入できませんでした。必要額：{furniture.purchasePrice:N0}円");
            return false;
        }

        ownedFurniture ??= new List<FurnitureId>();
        installedFurniture ??= new List<FurnitureId>();
        ownedFurniture.Add(furniture.id);

        // 購入直後は従来仕様との互換性を保つため自動で設置します。
        // ホームの家具画面からいつでも撤去できます。
        if (!installedFurniture.Contains(furniture.id))
            installedFurniture.Add(furniture.id);

        shopManager.RegisterSupplierProductPurchase(GetProductKey(furniture));

        RefreshEffects();
        OnChanged?.Invoke();
        Debug.Log($"家具『{furniture.displayName}』を{furniture.purchasePrice:N0}円で購入し、設置しました。");
        return true;
    }

    public bool TryInstall(FurnitureId id)
    {
        if (!IsOwned(id))
            return false;

        installedFurniture ??= new List<FurnitureId>();
        if (installedFurniture.Contains(id))
            return true;

        installedFurniture.Add(id);
        RefreshEffects();
        OnChanged?.Invoke();

        FurnitureData furniture = GetDefinition(id);
        Debug.Log($"家具『{furniture?.displayName ?? id.ToString()}』を設置しました。");
        return true;
    }

    public bool Uninstall(FurnitureId id)
    {
        if (installedFurniture == null)
            return false;

        bool removed = installedFurniture.Remove(id);
        if (!removed)
            return false;

        RefreshEffects();
        OnChanged?.Invoke();

        FurnitureData furniture = GetDefinition(id);
        Debug.Log($"家具『{furniture?.displayName ?? id.ToString()}』を撤去しました。");
        return true;
    }

    public float GetBudgetBonusPercentToday()
    {
        EnsureDefinitions();

        float total = 0f;

        foreach (FurnitureData furniture in furnitureDefinitions)
        {
            if (furniture == null || !IsInstalled(furniture.id))
                continue;

            total += furniture.budgetBonusPercent;

            if (isRainyToday)
                total += furniture.rainyBudgetBonusPercent;
        }

        return Mathf.Max(0f, total);
    }

    public float GetVisitorBonusPercentToday()
    {
        EnsureDefinitions();

        bool summer = shopManager != null && shopManager.CurrentSeason == Season.Summer;
        float total = 0f;

        foreach (FurnitureData furniture in furnitureDefinitions)
        {
            if (furniture == null || !IsInstalled(furniture.id))
                continue;

            total += furniture.visitorBonusPercent;

            if (summer)
                total += furniture.summerVisitorBonusPercent;

            if (isRainyToday)
                total += furniture.rainyVisitorBonusPercent;
        }

        return total;
    }

    /// <summary>
    /// 雨による来客率減少ペナルティをどこまで軽減できるか返します。
    /// 例：-50%の雨ペナルティに対し、傘立て設置中なら最低-30%まで軽減できます。
    /// 0は家具による下限指定なしです。
    /// </summary>
    public float GetRainVisitorPenaltyFloorPercent()
    {
        EnsureDefinitions();

        float bestFloor = 0f;
        foreach (FurnitureData furniture in furnitureDefinitions)
        {
            if (furniture == null || !IsInstalled(furniture.id))
                continue;

            if (furniture.rainyVisitorPenaltyFloorPercent < 0f)
                bestFloor = Mathf.Max(bestFloor == 0f ? -1f : bestFloor, furniture.rainyVisitorPenaltyFloorPercent);
        }

        return bestFloor == -1f ? 0f : bestFloor;
    }

    public void SetRainyToday(bool rainy)
    {
        if (isRainyToday == rainy)
            return;

        isRainyToday = rainy;
        RefreshEffects();
        OnChanged?.Invoke();
    }

    public Sprite LoadSprite(FurnitureData furniture)
    {
        if (furniture == null || string.IsNullOrWhiteSpace(furniture.spriteResourcePath))
            return null;

        return Resources.Load<Sprite>(furniture.spriteResourcePath);
    }

    public static string GetProductKey(FurnitureData furniture)
    {
        return furniture != null ? $"furniture:{furniture.id}" : string.Empty;
    }

    [ContextMenu("DEBUG: 家具効果をログ表示")]
    private void DebugPrintFurnitureEffects()
    {
        Debug.Log(
            $"家具効果 / 所持{OwnedCount}個 / 設置{InstalledCount}個 / " +
            $"来客率+{GetVisitorBonusPercentToday() * 100f:0.#}% / " +
            $"予算+{GetBudgetBonusPercentToday() * 100f:0.#}% / " +
            $"雨ペナルティ下限{GetRainVisitorPenaltyFloorPercent() * 100f:0.#}%");
    }

    private void RefreshEffects()
    {
        SanitizeInstalledFurniture();

        if (visitorModifierSystem == null)
            visitorModifierSystem = FindFirstObjectByType<VisitorModifierSystem>();

        if (visitorModifierSystem != null)
        {
            visitorModifierSystem.RegisterOrUpdateModifier(
                "furniture.total",
                GetVisitorBonusPercentToday(),
                0,
                "家具");
        }
    }

    private void SanitizeInstalledFurniture()
    {
        ownedFurniture ??= new List<FurnitureId>();
        installedFurniture ??= new List<FurnitureId>();

        installedFurniture = installedFurniture
            .Where(id => ownedFurniture.Contains(id))
            .Distinct()
            .ToList();
    }

    private void EnsureDefinitions()
    {
        if (furnitureDefinitions != null && furnitureDefinitions.Count == 12)
            return;

        furnitureDefinitions = new List<FurnitureData>
        {
            Create(FurnitureId.WelcomeMat, "ウェルカムマット", 5000, "Furniture/Furniture_WelcomeMat", 0.03f),
            Create(FurnitureId.UmbrellaStand, "傘立て", 10000, "Furniture/Furniture_UmbrellaStand", 0.01f, rainPenaltyFloor: -0.30f),
            Create(FurnitureId.UmbrellaBagMachine, "傘ビニール袋機", 60000, "Furniture/Furniture_UmbrellaBagMachine", 0.01f, rainyVisitor: 0.05f, rainyBudget: 0.08f),
            Create(FurnitureId.Sanitizer, "消毒液機", 50000, "Furniture/Furniture_Sanitizer", 0.03f, 0.03f),
            Create(FurnitureId.InsectKiller, "電子捕虫器", 150000, "Furniture/Furniture_InsectKiller", 0.10f, 0.05f, summerVisitor: 0.10f),
            Create(FurnitureId.OpenCloseSign, "OPEN/CLOSEプレート", 3000, "Furniture/Furniture_OpenCloseSign", 0.01f),
            Create(FurnitureId.LightA, "照明A", 100000, "Furniture/Furniture_LightA", 0.03f, 0.05f),
            Create(FurnitureId.LightB, "照明B", 100000, "Furniture/Furniture_LightB", 0.03f, 0.05f),
            Create(FurnitureId.LightC, "照明C", 100000, "Furniture/Furniture_LightC", 0.03f, 0.05f),
            Create(FurnitureId.PendulumClock, "振り子時計", 50000, "Furniture/Furniture_PendulumClock", budget: 0.06f),
            Create(FurnitureId.NewtonsCradle, "ニュートンの揺りかご", 7500, "Furniture/Furniture_NewtonsCradle", 0.01f, 0.01f),
            Create(FurnitureId.DrinkingBird, "水飲み鳥", 5000, "Furniture/Furniture_DrinkingBird", summerVisitor: 0.02f)
        };
    }

    private static FurnitureData Create(
        FurnitureId id,
        string displayName,
        int price,
        string spritePath,
        float visitor = 0f,
        float budget = 0f,
        float summerVisitor = 0f,
        float rainyVisitor = 0f,
        float rainyBudget = 0f,
        float rainPenaltyFloor = 0f)
    {
        return new FurnitureData
        {
            id = id,
            displayName = displayName,
            purchasePrice = price,
            spriteResourcePath = spritePath,
            visitorBonusPercent = visitor,
            budgetBonusPercent = budget,
            summerVisitorBonusPercent = summerVisitor,
            rainyVisitorBonusPercent = rainyVisitor,
            rainyBudgetBonusPercent = rainyBudget,
            rainyVisitorPenaltyFloorPercent = rainPenaltyFloor
        };
    }
}
