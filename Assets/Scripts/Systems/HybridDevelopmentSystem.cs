using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 新種開発（交配研究）と、解禁済み交配花の作成を管理します。
/// 開発・作成・新種開発はDevelopmentSystemの共通1枠を共有します。
/// </summary>
public class HybridDevelopmentSystem : MonoBehaviour
{
    public const int DefaultResearchCost = 30000;
    public const int DefaultSuccessDays = 15;
    public const int FailureDays = 1;

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;
    [SerializeField] private DevelopmentSystem developmentSystem;

    [Header("新種開発")]
    [SerializeField] private List<HybridRecipeDefinition> recipes = new();
    [SerializeField] private List<string> unlockedHybridNames = new();
    [SerializeField] private HybridResearchJobState activeJob = new();
    [SerializeField] private HybridProductionJobState activeProductionJob = new();
    [SerializeField] private string lastResultMessage;

    private readonly List<FlowerData> runtimeHybridFlowers = new();
    private int observedAbsoluteDay = -1;

    public IReadOnlyList<HybridRecipeDefinition> Recipes => recipes;
    public IReadOnlyList<string> UnlockedHybridNames => unlockedHybridNames;
    public HybridResearchJobState ActiveJob => activeJob;
    public HybridProductionJobState ActiveProductionJob => activeProductionJob;
    public bool HasActiveJob => activeJob != null && activeJob.active && activeJob.remainingDays > 0;
    public bool HasProductionJob => activeProductionJob != null && activeProductionJob.active && activeProductionJob.remainingDays > 0;
    public bool HasAnyHybridWork => HasActiveJob || HasProductionJob;
    public string LastResultMessage => lastResultMessage;

    public event Action OnChanged;
    public event Action<string> OnResearchCompleted;
    public event Action<string> OnProductionCompleted;

    private void Awake()
    {
        ResolveReferences();
        BuildDefaultRecipes();
        activeJob ??= new HybridResearchJobState();
        activeProductionJob ??= new HybridProductionJobState();
        unlockedHybridNames ??= new List<string>();
        observedAbsoluteDay = GetAbsoluteDay();
        SyncExternalWorkFlag();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;
        SyncExternalWorkFlag();
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;
    }

    public bool IsHybridUnlocked(string hybridName)
    {
        if (string.IsNullOrWhiteSpace(hybridName)) return false;
        return unlockedHybridNames.Any(x => string.Equals(x, hybridName, StringComparison.Ordinal));
    }

    public IEnumerable<HybridRecipeDefinition> GetUnlockedRecipes()
    {
        BuildDefaultRecipes();
        return recipes.Where(r => r != null && IsHybridUnlocked(r.hybridName));
    }

    public HybridRecipeDefinition GetRecipeByHybridName(string hybridName)
    {
        BuildDefaultRecipes();
        return recipes.FirstOrDefault(r => r != null && string.Equals(r.hybridName, hybridName, StringComparison.Ordinal));
    }

    public HybridRecipeDefinition FindRecipe(FlowerData a, FlowerData b)
    {
        BuildDefaultRecipes();
        return recipes.FirstOrDefault(r => r != null && r.Matches(a, b));
    }

    public int GetResearchCost(FlowerData a, FlowerData b) => DefaultResearchCost;

    public int GetResearchDays(FlowerData a, FlowerData b)
    {
        return FindRecipe(a, b) != null ? DefaultSuccessDays : FailureDays;
    }

    public bool CanStartHybrid(FlowerData a, FlowerData b, out string reason)
    {
        ResolveReferences();

        if (developmentSystem == null || !developmentSystem.IsNewSpeciesDevelopmentUnlocked)
        {
            reason = "枯ラサンついの開発が必要です";
            return false;
        }
        if (HasAnyHybridWork || developmentSystem.HasAnyActiveWork)
        {
            reason = "別の作業を進行中です";
            return false;
        }
        if (a == null || b == null)
        {
            reason = "花を2種類選択してください";
            return false;
        }
        if (ReferenceEquals(a, b) || string.Equals(a.flowerName, b.flowerName, StringComparison.Ordinal))
        {
            reason = "同じ花同士は選べません";
            return false;
        }
        if (inventorySystem == null || inventorySystem.GetTotalQuantity(a) < 1 || inventorySystem.GetTotalQuantity(b) < 1)
        {
            reason = "選択した花の在庫が足りません";
            return false;
        }
        if (checkoutItemSystem == null || checkoutItemSystem.GetStockQuantity(DevelopmentSystem.KarasanTsuiItemId) < 1)
        {
            reason = "枯ラサンついが必要です";
            return false;
        }

        HybridRecipeDefinition recipe = FindRecipe(a, b);
        if (recipe != null && IsHybridUnlocked(recipe.hybridName))
        {
            reason = "この新種は開発済みです";
            return false;
        }
        if (shopManager == null || shopManager.Money < DefaultResearchCost)
        {
            reason = "所持金が足りません";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryStartHybrid(FlowerData a, FlowerData b)
    {
        if (!CanStartHybrid(a, b, out string reason))
        {
            Debug.Log($"新種開発を開始できません：{reason}");
            return false;
        }

        HybridRecipeDefinition recipe = FindRecipe(a, b);
        int cost = DefaultResearchCost;
        if (!shopManager.TrySpendMoney(cost)) return false;

        if (!inventorySystem.TryRemoveFlower(a, 1))
        {
            shopManager.AddMoney(cost);
            return false;
        }
        if (!inventorySystem.TryRemoveFlower(b, 1))
        {
            inventorySystem.AddFlower(a, 1);
            shopManager.AddMoney(cost);
            return false;
        }
        if (!checkoutItemSystem.TryConsumeStock(DevelopmentSystem.KarasanTsuiItemId, 1))
        {
            inventorySystem.AddFlower(a, 1);
            inventorySystem.AddFlower(b, 1);
            shopManager.AddMoney(cost);
            return false;
        }

        activeJob.active = true;
        activeJob.parentA = a;
        activeJob.parentB = b;
        activeJob.willSucceed = recipe != null;
        activeJob.resultHybridName = recipe?.hybridName ?? string.Empty;
        activeJob.remainingDays = recipe != null ? DefaultSuccessDays : FailureDays;
        activeJob.paidCost = cost;
        lastResultMessage = string.Empty;

        SyncExternalWorkFlag();
        Debug.Log($"新種開発を開始しました：{a.flowerName} × {b.flowerName} / {activeJob.remainingDays}日 / {cost:N0}円");
        OnChanged?.Invoke();
        return true;
    }

    public bool CanStartHybridProduction(string hybridName, out string reason)
    {
        ResolveReferences();
        HybridRecipeDefinition recipe = GetRecipeByHybridName(hybridName);
        if (recipe == null || !IsHybridUnlocked(hybridName))
        {
            reason = "未開発です";
            return false;
        }
        if (HasAnyHybridWork || developmentSystem == null || developmentSystem.HasAnyActiveWork)
        {
            reason = "別の作業を進行中";
            return false;
        }
        if (shopManager == null || shopManager.Money < recipe.productionCost)
        {
            reason = "所持金不足";
            return false;
        }
        if (FindParentVariant(recipe.parentAName, recipe.parentAQuantity) == null ||
            FindParentVariant(recipe.parentBName, recipe.parentBQuantity) == null)
        {
            reason = "材料不足";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryStartHybridProduction(string hybridName)
    {
        if (!CanStartHybridProduction(hybridName, out string reason))
        {
            Debug.Log($"交配花『{hybridName}』を作成できません：{reason}");
            return false;
        }

        HybridRecipeDefinition recipe = GetRecipeByHybridName(hybridName);
        FlowerData parentA = FindParentVariant(recipe.parentAName, recipe.parentAQuantity);
        FlowerData parentB = FindParentVariant(recipe.parentBName, recipe.parentBQuantity);

        if (!shopManager.TrySpendMoney(recipe.productionCost)) return false;
        if (!inventorySystem.TryRemoveFlower(parentA, recipe.parentAQuantity))
        {
            shopManager.AddMoney(recipe.productionCost);
            return false;
        }
        if (!inventorySystem.TryRemoveFlower(parentB, recipe.parentBQuantity))
        {
            inventorySystem.AddFlower(parentA, recipe.parentAQuantity);
            shopManager.AddMoney(recipe.productionCost);
            return false;
        }

        activeProductionJob.active = true;
        activeProductionJob.hybridName = hybridName;
        activeProductionJob.parentA = parentA;
        activeProductionJob.parentB = parentB;
        activeProductionJob.remainingDays = Mathf.Max(1, recipe.productionDays);
        activeProductionJob.paidCost = recipe.productionCost;
        lastResultMessage = string.Empty;

        SyncExternalWorkFlag();
        Debug.Log($"交配花『{hybridName}』の作成を開始しました。{recipe.productionCost:N0}円 / {recipe.productionDays}日");
        OnChanged?.Invoke();
        return true;
    }

    public int GetRemainingDays() => HasActiveJob ? Mathf.Max(0, activeJob.remainingDays) : 0;
    public int GetProductionRemainingDays() => HasProductionJob ? Mathf.Max(0, activeProductionJob.remainingDays) : 0;

    private FlowerData FindParentVariant(string flowerName, int quantity)
    {
        if (inventorySystem == null || string.IsNullOrWhiteSpace(flowerName)) return null;
        return inventorySystem.Batches
            .Where(b => b != null && b.flower != null && b.quantity > 0)
            .Where(b => string.Equals(b.flower.flowerName, flowerName, StringComparison.Ordinal))
            .GroupBy(b => b.flower)
            .Where(g => g.Sum(x => x.quantity) >= quantity)
            .OrderBy(g => g.Min(x => x.remainingFreshnessDays))
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private void HandleShopStateChanged()
    {
        int currentAbsoluteDay = GetAbsoluteDay();
        if (currentAbsoluteDay < 0) return;
        if (observedAbsoluteDay < 0)
        {
            observedAbsoluteDay = currentAbsoluteDay;
            return;
        }
        if (currentAbsoluteDay == observedAbsoluteDay)
        {
            OnChanged?.Invoke();
            return;
        }

        int elapsedDays = Mathf.Max(1, currentAbsoluteDay - observedAbsoluteDay);
        observedAbsoluteDay = currentAbsoluteDay;
        for (int i = 0; i < elapsedDays; i++) AdvanceOneDay();
        OnChanged?.Invoke();
    }

    private void AdvanceOneDay()
    {
        if (HasActiveJob)
        {
            activeJob.remainingDays = Mathf.Max(0, activeJob.remainingDays - 1);
            if (activeJob.remainingDays <= 0) CompleteResearchJob();
            return;
        }

        if (HasProductionJob)
        {
            activeProductionJob.remainingDays = Mathf.Max(0, activeProductionJob.remainingDays - 1);
            if (activeProductionJob.remainingDays <= 0) CompleteProductionJob();
        }
    }

    private void CompleteResearchJob()
    {
        if (activeJob == null || !activeJob.active) return;

        if (activeJob.willSucceed && !string.IsNullOrWhiteSpace(activeJob.resultHybridName))
        {
            if (!IsHybridUnlocked(activeJob.resultHybridName)) unlockedHybridNames.Add(activeJob.resultHybridName);
            lastResultMessage = $"新種『{activeJob.resultHybridName}』の開発に成功しました！";
        }
        else
        {
            int refund = Mathf.FloorToInt(activeJob.paidCost * (2f / 3f));
            if (refund > 0 && shopManager != null) shopManager.AddMoney(refund);
            lastResultMessage = "この組み合わせは無理っぽかった";
        }

        activeJob.Clear();
        SyncExternalWorkFlag();
        Debug.Log(lastResultMessage);
        OnResearchCompleted?.Invoke(lastResultMessage);
        OnChanged?.Invoke();
    }

    private void CompleteProductionJob()
    {
        if (activeProductionJob == null || !activeProductionJob.active) return;

        HybridRecipeDefinition recipe = GetRecipeByHybridName(activeProductionJob.hybridName);
        if (recipe != null && inventorySystem != null)
        {
            FlowerData flower = GetOrCreateHybridFlower(recipe, activeProductionJob.parentA, activeProductionJob.parentB);
            inventorySystem.AddFlower(flower, recipe.productionQuantity);
            lastResultMessage = $"『{recipe.hybridName}』×{recipe.productionQuantity}の作成が完了しました！";
        }
        else
        {
            lastResultMessage = "交配花の作成に失敗しました。";
        }

        activeProductionJob.Clear();
        SyncExternalWorkFlag();
        Debug.Log(lastResultMessage);
        OnProductionCompleted?.Invoke(lastResultMessage);
        OnChanged?.Invoke();
    }

    private FlowerData GetOrCreateHybridFlower(HybridRecipeDefinition recipe, FlowerData parentA, FlowerData parentB)
    {
        List<string> colors = new();
        if (parentA != null) colors.AddRange(parentA.GetColors());
        if (parentB != null) colors.AddRange(parentB.GetColors());
        colors = colors.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        string colorKey = string.Join("|", colors.OrderBy(x => x));

        FlowerData existing = runtimeHybridFlowers.FirstOrDefault(f =>
            f != null && string.Equals(f.flowerName, recipe.hybridName, StringComparison.Ordinal) &&
            string.Join("|", f.GetColors().OrderBy(x => x)) == colorKey);
        if (existing != null) return existing;

        FlowerData flower = ScriptableObject.CreateInstance<FlowerData>();
        flower.name = $"Runtime_{recipe.hybridName}_{colorKey}";
        flower.flowerName = recipe.hybridName;
        flower.SetColors(colors);
        flower.basePopularity = recipe.basePopularity;
        flower.purchasePrice = 0;
        flower.recommendedSalePrice = recipe.recommendedSalePrice;
        flower.freshnessDays = recipe.freshnessDays;
        flower.springRarity = recipe.springRarity;
        flower.summerRarity = recipe.summerRarity;
        flower.autumnRarity = recipe.autumnRarity;
        flower.winterRarity = recipe.winterRarity;
        flower.productCategory = recipe.productCategory;
        flower.canUseInBouquet = recipe.canUseInBouquet;
        flower.arrivalDifficulty = 1;
        flower.sortOrder = recipe.sortOrder;
        runtimeHybridFlowers.Add(flower);
        return flower;
    }

    private void SyncExternalWorkFlag()
    {
        developmentSystem?.SetExternalJobActive(HasAnyHybridWork);
    }

    private void ResolveReferences()
    {
        if (shopManager == null) shopManager = FindFirstObjectByType<ShopManager>();
        if (inventorySystem == null) inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (checkoutItemSystem == null) checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();
        if (developmentSystem == null) developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
    }

    private int GetAbsoluteDay()
    {
        if (shopManager == null) return -1;
        return (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
    }

    private void BuildDefaultRecipes()
    {
        if (recipes != null && recipes.Count == 25) return;

        recipes = new List<HybridRecipeDefinition>
        {
            R("ガーバラ", "ガーベラ", "バラ", 5,7,4,5,4,5,"切り花",true,84, 3000,2,1200),
            R("アジワリ", "アジサイ", "ヒマワリ", 6,7,6,2,6,9,"切り花",true,85, 3500,2,1500),
            R("スイートモス", "スイートピー", "コスモス", 6,7,7,10,6,7,"切り花",true,86, 4000,2,1600),
            R("パンスライス", "パンジー", "レモンスライス", 6,25,4,7,4,7,"鉢花",false,87, 4500,2,1800),
            R("紫バラ", "黒バラ", "青バラ", 10,3,10,10,10,10,"切り花",true,88, 8000,3,5000),
            R("ユリップ", "ユリ", "チューリップ", 5,8,3,7,8,3,"切り花",true,89, 3500,2,1400),
            R("コスミソウ", "コスモス", "カスミソウ", 5,8,8,6,3,7,"切り花",true,90, 3500,2,1400),
            R("ダリネーション", "ダリア", "カーネーション", 7,10,4,8,5,4,"切り花",true,91, 4500,2,1900),
            R("スイーセンピー", "スイセン", "スイートピー", 6,7,4,10,9,3,"切り花",true,92, 4000,2,1600),
            R("シクラジサイ", "シクラメン", "アジサイ", 7,20,6,7,6,6,"鉢花",false,93, 5000,2,2100),
            R("ヒマセチア", "ヒマワリ", "ポインセチア", 7,20,9,7,7,6,"鉢花",false,94, 5000,2,2200),
            R("サギュリ", "サギソウ", "ユリ", 10,12,7,7,7,8,"切り花",true,95, 7000,3,3800),
            R("トロピカリア", "トロピカルフラワー", "ダリア", 8,8,7,7,7,7,"切り花",true,96, 5500,2,2500),
            R("ジギステラ", "オジギソウ", "モンステラ", 6,25,6,5,6,9,"観葉植物",false,97, 5000,2,2200),
            R("ウツボキリン", "ウツボカズラ", "花麒麟", 8,38,7,7,7,9,"食虫植物",false,98, 6500,3,3000),
            R("月下ユリ", "月下美人", "ユリ", 10,5,7,7,7,10,"希少植物",false,99, 8000,3,4500),
            R("ファイヤーコスモス", "ファイヤーワークスペラルゴニウム", "コスモス", 9,20,9,8,5,10,"鉢花",false,100, 7000,3,3500),
            R("スイートサクラ", "スイートピー", "桜（枝）", 7,7,3,10,10,7,"枝物",true,101, 5000,2,2200),
            R("レモンセチア", "レモンスライス", "ポインセチア", 7,30,8,8,6,7,"鉢花",false,102, 5000,2,2200),
            R("チューラメン", "チューリップ", "シクラメン", 6,18,4,10,9,3,"鉢花",false,103, 4500,2,1900),
            R("ガーネーション", "ガーベラ", "カーネーション", 5,11,3,5,4,4,"切り花",true,104, 3000,2,1300),
            R("カスミユリ", "カスミソウ", "ユリ", 6,10,4,4,4,4,"切り花",true,105, 3500,2,1500),
            R("アジダリア", "アジサイ", "ダリア", 7,7,5,6,5,7,"切り花",true,106, 4500,2,1900),
            R("スイバラ", "バラ", "スイートピー", 6,7,3,7,6,3,"切り花",true,107, 4000,2,1700),
            R("ポインジー", "ポインセチア", "パンジー", 7,25,7,10,5,3,"鉢花",false,108, 5000,2,2200)
        };
    }

    private static HybridRecipeDefinition R(string result, string a, string b,
        int pop, int fresh, int spring, int summer, int autumn, int winter,
        string category, bool bouquet, int sort, int productionCost, int productionDays, int salePrice)
    {
        return new HybridRecipeDefinition
        {
            hybridName = result,
            parentAName = a,
            parentBName = b,
            researchCost = DefaultResearchCost,
            researchDays = DefaultSuccessDays,
            parentAQuantity = 2,
            parentBQuantity = 2,
            productionCost = productionCost,
            productionDays = productionDays,
            productionQuantity = 5,
            basePopularity = pop,
            freshnessDays = fresh,
            springRarity = spring,
            summerRarity = summer,
            autumnRarity = autumn,
            winterRarity = winter,
            productCategory = category,
            canUseInBouquet = bouquet,
            sortOrder = sort,
            recommendedSalePrice = salePrice
        };
    }
}
