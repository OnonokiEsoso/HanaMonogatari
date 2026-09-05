using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 新種開発（交配研究）を管理します。
/// 親花2種 + 枯ラサンつい + 研究費を消費し、定義済みレシピなら15日、失敗組み合わせなら1日で結果が出ます。
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
    [SerializeField] private string lastResultMessage;

    private int observedAbsoluteDay = -1;

    public IReadOnlyList<HybridRecipeDefinition> Recipes => recipes;
    public IReadOnlyList<string> UnlockedHybridNames => unlockedHybridNames;
    public HybridResearchJobState ActiveJob => activeJob;
    public bool HasActiveJob => activeJob != null && activeJob.active && activeJob.remainingDays > 0;
    public string LastResultMessage => lastResultMessage;

    public event Action OnChanged;
    public event Action<string> OnResearchCompleted;

    private void Awake()
    {
        ResolveReferences();
        BuildDefaultRecipes();
        activeJob ??= new HybridResearchJobState();
        unlockedHybridNames ??= new List<string>();
        observedAbsoluteDay = GetAbsoluteDay();
        developmentSystem?.SetExternalJobActive(HasActiveJob);
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;

        developmentSystem?.SetExternalJobActive(HasActiveJob);
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;
    }

    public bool IsHybridUnlocked(string hybridName)
    {
        if (string.IsNullOrWhiteSpace(hybridName))
            return false;
        return unlockedHybridNames.Any(x => string.Equals(x, hybridName, StringComparison.Ordinal));
    }

    public HybridRecipeDefinition FindRecipe(FlowerData a, FlowerData b)
    {
        BuildDefaultRecipes();
        return recipes.FirstOrDefault(r => r != null && r.Matches(a, b));
    }

    public int GetResearchCost(FlowerData a, FlowerData b)
    {
        HybridRecipeDefinition recipe = FindRecipe(a, b);
        return recipe != null ? Mathf.Max(0, recipe.researchCost) : DefaultResearchCost;
    }

    public int GetResearchDays(FlowerData a, FlowerData b)
    {
        HybridRecipeDefinition recipe = FindRecipe(a, b);
        return recipe != null ? Mathf.Max(1, recipe.researchDays) : FailureDays;
    }

    public bool CanStartHybrid(FlowerData a, FlowerData b, out string reason)
    {
        ResolveReferences();

        if (developmentSystem == null || !developmentSystem.IsNewSpeciesDevelopmentUnlocked)
        {
            reason = "枯ラサンついの開発が必要です";
            return false;
        }

        if (HasActiveJob || developmentSystem.HasAnyActiveWork)
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

        int cost = GetResearchCost(a, b);
        if (shopManager == null || shopManager.Money < cost)
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
        int cost = GetResearchCost(a, b);
        if (!shopManager.TrySpendMoney(cost))
            return false;

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
        activeJob.remainingDays = recipe != null ? Mathf.Max(1, recipe.researchDays) : FailureDays;
        activeJob.paidCost = cost;
        lastResultMessage = string.Empty;

        developmentSystem.SetExternalJobActive(true);
        Debug.Log($"新種開発を開始しました：{a.flowerName} × {b.flowerName} / {activeJob.remainingDays}日 / {cost:N0}円");
        OnChanged?.Invoke();
        return true;
    }

    public int GetRemainingDays()
    {
        return HasActiveJob ? Mathf.Max(0, activeJob.remainingDays) : 0;
    }

    private void HandleShopStateChanged()
    {
        int currentAbsoluteDay = GetAbsoluteDay();
        if (currentAbsoluteDay < 0)
            return;

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

        for (int i = 0; i < elapsedDays; i++)
            AdvanceOneDay();

        OnChanged?.Invoke();
    }

    private void AdvanceOneDay()
    {
        if (!HasActiveJob)
            return;

        activeJob.remainingDays = Mathf.Max(0, activeJob.remainingDays - 1);
        if (activeJob.remainingDays > 0)
            return;

        CompleteActiveJob();
    }

    private void CompleteActiveJob()
    {
        if (activeJob == null || !activeJob.active)
            return;

        if (activeJob.willSucceed && !string.IsNullOrWhiteSpace(activeJob.resultHybridName))
        {
            if (!IsHybridUnlocked(activeJob.resultHybridName))
                unlockedHybridNames.Add(activeJob.resultHybridName);

            lastResultMessage = $"新種『{activeJob.resultHybridName}』の開発に成功しました！";
        }
        else
        {
            int refund = Mathf.FloorToInt(activeJob.paidCost * (2f / 3f));
            if (refund > 0 && shopManager != null)
                shopManager.AddMoney(refund);

            lastResultMessage = "この組み合わせは無理っぽかった";
        }

        activeJob.Clear();
        developmentSystem?.SetExternalJobActive(false);
        Debug.Log(lastResultMessage);
        OnResearchCompleted?.Invoke(lastResultMessage);
        OnChanged?.Invoke();
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (checkoutItemSystem == null)
            checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
    }

    private int GetAbsoluteDay()
    {
        if (shopManager == null)
            return -1;
        return (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
    }

    private void BuildDefaultRecipes()
    {
        if (recipes != null && recipes.Count == 25)
            return;

        recipes = new List<HybridRecipeDefinition>
        {
            R("ガーバラ", "ガーベラ", "バラ"),
            R("アジワリ", "アジサイ", "ヒマワリ"),
            R("スイートモス", "スイートピー", "コスモス"),
            R("パンスライス", "パンジー", "レモンスライス"),
            R("紫バラ", "黒バラ", "青バラ", 50000, 15),
            R("ユリップ", "ユリ", "チューリップ"),
            R("コスミソウ", "コスモス", "カスミソウ"),
            R("ダリネーション", "ダリア", "カーネーション"),
            R("スイーセンピー", "スイセン", "スイートピー"),
            R("シクラジサイ", "シクラメン", "アジサイ"),
            R("ヒマセチア", "ヒマワリ", "ポインセチア"),
            R("サギュリ", "サギソウ", "ユリ", 45000, 15),
            R("トロピカリア", "トロピカルフラワー", "ダリア"),
            R("ジギステラ", "オジギソウ", "モンステラ"),
            R("ウツボキリン", "ウツボカズラ", "花麒麟", 40000, 15),
            R("月下ユリ", "月下美人", "ユリ", 50000, 15),
            R("ファイヤーコスモス", "ファイヤーワークスペラルゴニウム", "コスモス", 45000, 15),
            R("スイートサクラ", "スイートピー", "桜（枝）"),
            R("レモンセチア", "レモンスライス", "ポインセチア"),
            R("チューラメン", "チューリップ", "シクラメン"),
            R("ガーネーション", "ガーベラ", "カーネーション"),
            R("カスミユリ", "カスミソウ", "ユリ"),
            R("アジダリア", "アジサイ", "ダリア"),
            R("スイバラ", "バラ", "スイートピー"),
            R("ポインジー", "ポインセチア", "パンジー")
        };
    }

    private static HybridRecipeDefinition R(string result, string a, string b, int cost = DefaultResearchCost, int days = DefaultSuccessDays)
    {
        return new HybridRecipeDefinition
        {
            hybridName = result,
            parentAName = a,
            parentBName = b,
            researchCost = cost,
            researchDays = days
        };
    }
}
