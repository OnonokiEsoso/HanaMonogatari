using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ver0.0.6 の開発・作成を管理します。
/// 開発と作成は同じ1枠を共有し、日付が1日進むたびに残り日数が減ります。
/// 開発済みの自社製品は、お金と時間だけで確実に作成できます。
/// </summary>
public class DevelopmentSystem : MonoBehaviour
{
    public const int DevelopmentUnlockShopRating = 2000;

    public const string NutritionItemId = "nutrition_supplement";
    public const string FertilizerItemId = "fertilizer";
    public const string KarasanItemId = "karasan";
    public const string SodatsuChoItemId = "sodatsu_cho";
    public const string SodatsuTsubuItemId = "sodatsu_tsubu";
    public const string SodatsuEkiItemId = "sodatsu_eki";
    public const string KarasanTsuiItemId = "karasan_tsui";

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;

    [Header("開発定義")]
    [SerializeField] private List<DevelopmentDefinition> definitions = new();

    [Header("進行状態")]
    [SerializeField] private List<DevelopmentProgressState> progressStates = new();
    [SerializeField] private DevelopmentJobState activeJob = new();
    [SerializeField] private string lastCompletionMessage;

    private int observedAbsoluteDay = -1;

    public IReadOnlyList<DevelopmentDefinition> Definitions => definitions;
    public DevelopmentJobState ActiveJob => activeJob;
    public bool HasActiveJob => activeJob != null && activeJob.IsActive;
    public bool IsDevelopmentFeatureUnlocked => shopManager != null && shopManager.ShopRating >= DevelopmentUnlockShopRating;
    public bool IsNewSpeciesDevelopmentUnlocked => IsCompleted(DevelopmentId.KarasanTsui);
    public string LastCompletionMessage => lastCompletionMessage;

    public event Action OnChanged;
    public event Action<string> OnJobCompleted;

    private void Awake()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (checkoutItemSystem == null)
            checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();

        BuildDefaultDefinitions();
        NormalizeProgressStates();
        activeJob ??= new DevelopmentJobState();
        observedAbsoluteDay = GetAbsoluteDay();
    }

    private void OnEnable()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();

        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;
    }

    public DevelopmentDefinition GetDefinition(DevelopmentId id)
    {
        BuildDefaultDefinitions();
        return definitions.FirstOrDefault(d => d != null && d.id == id);
    }

    public bool IsCompleted(DevelopmentId id)
    {
        DevelopmentProgressState state = progressStates?.FirstOrDefault(s => s != null && s.id == id);
        return state != null && state.completed;
    }

    public bool ArePrerequisitesCompleted(DevelopmentDefinition definition)
    {
        if (definition == null)
            return false;

        DevelopmentId[] prerequisites = definition.prerequisiteDevelopments ?? Array.Empty<DevelopmentId>();
        return prerequisites.All(IsCompleted);
    }

    public bool IsDevelopmentVisible(DevelopmentId id)
    {
        DevelopmentDefinition definition = GetDefinition(id);
        if (definition == null || !IsDevelopmentFeatureUnlocked)
            return false;

        // 最初の枯ラサンは開発機能解禁時から表示。
        if (id == DevelopmentId.Karasan)
            return true;

        return ArePrerequisitesCompleted(definition);
    }

    public bool CanStartDevelopment(DevelopmentId id, FlowerData materialFlower = null)
    {
        DevelopmentDefinition definition = GetDefinition(id);
        if (definition == null || HasActiveJob || IsCompleted(id))
            return false;

        if (!IsDevelopmentFeatureUnlocked || shopManager == null || shopManager.ShopRating < definition.requiredShopRating)
            return false;

        if (!ArePrerequisitesCompleted(definition))
            return false;

        if (shopManager.Money < definition.developmentCost)
            return false;

        if (!HasRequiredCheckoutMaterials(definition))
            return false;

        return IsValidMaterialFlower(definition, materialFlower);
    }

    public bool TryStartDevelopment(DevelopmentId id, FlowerData materialFlower = null)
    {
        DevelopmentDefinition definition = GetDefinition(id);
        if (!CanStartDevelopment(id, materialFlower))
        {
            Debug.Log($"開発『{definition?.displayName ?? id.ToString()}』を開始できません。条件・材料・所持金を確認してください。");
            return false;
        }

        if (!shopManager.TrySpendMoney(definition.developmentCost))
            return false;

        ConsumeCheckoutMaterial(definition.requiredCheckoutItemId, definition.requiredCheckoutItemQuantity);
        ConsumeCheckoutMaterial(definition.requiredCheckoutItemId2, definition.requiredCheckoutItemQuantity2);

        if (definition.requiresFlower && materialFlower != null)
            inventorySystem.TryRemoveFlower(materialFlower, 1);

        activeJob.jobType = DevelopmentJobType.Development;
        activeJob.targetId = id;
        activeJob.remainingDays = Mathf.Max(1, definition.developmentDays);
        lastCompletionMessage = string.Empty;

        Debug.Log($"『{definition.displayName}』の開発を開始しました。費用：{definition.developmentCost:N0}円 / {activeJob.remainingDays}日");
        OnChanged?.Invoke();
        return true;
    }

    public bool CanStartProduction(DevelopmentId id)
    {
        DevelopmentDefinition definition = GetDefinition(id);
        if (definition == null || HasActiveJob || !IsCompleted(id) || shopManager == null)
            return false;

        return shopManager.Money >= definition.productionCost;
    }

    public bool TryStartProduction(DevelopmentId id)
    {
        DevelopmentDefinition definition = GetDefinition(id);
        if (!CanStartProduction(id))
        {
            Debug.Log($"『{definition?.displayName ?? id.ToString()}』を作成できません。開発状態・所持金・進行中作業を確認してください。");
            return false;
        }

        if (!shopManager.TrySpendMoney(definition.productionCost))
            return false;

        activeJob.jobType = DevelopmentJobType.Production;
        activeJob.targetId = id;
        activeJob.remainingDays = Mathf.Max(1, definition.productionDays);
        lastCompletionMessage = string.Empty;

        Debug.Log($"『{definition.displayName}』×{definition.productionQuantity}の作成を開始しました。費用：{definition.productionCost:N0}円 / {activeJob.remainingDays}日");
        OnChanged?.Invoke();
        return true;
    }

    public int GetRemainingDays()
    {
        return HasActiveJob ? Mathf.Max(0, activeJob.remainingDays) : 0;
    }

    public bool HasAnyImmediatelyStartableDevelopment()
    {
        if (!IsDevelopmentFeatureUnlocked || HasActiveJob)
            return false;

        foreach (DevelopmentDefinition definition in definitions)
        {
            if (definition == null || IsCompleted(definition.id) || !ArePrerequisitesCompleted(definition))
                continue;

            if (definition.requiresFlower)
            {
                bool hasSuitableFlower = inventorySystem != null && inventorySystem.Batches.Any(batch =>
                    batch?.flower != null && batch.quantity > 0 &&
                    batch.flower.arrivalDifficulty >= definition.minimumFlowerArrivalDifficulty);

                if (!hasSuitableFlower)
                    continue;
            }

            if (shopManager != null && shopManager.Money >= definition.developmentCost && HasRequiredCheckoutMaterials(definition))
                return true;
        }

        return false;
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
            AdvanceJobOneDay();

        OnChanged?.Invoke();
    }

    private void AdvanceJobOneDay()
    {
        if (!HasActiveJob)
            return;

        activeJob.remainingDays = Mathf.Max(0, activeJob.remainingDays - 1);
        if (activeJob.remainingDays > 0)
        {
            DevelopmentDefinition definition = GetDefinition(activeJob.targetId);
            Debug.Log($"{definition?.displayName ?? activeJob.targetId.ToString()}：残り{activeJob.remainingDays}日");
            return;
        }

        CompleteActiveJob();
    }

    private void CompleteActiveJob()
    {
        if (activeJob == null || activeJob.jobType == DevelopmentJobType.None)
            return;

        DevelopmentDefinition definition = GetDefinition(activeJob.targetId);
        if (definition == null)
        {
            activeJob.Clear();
            return;
        }

        if (activeJob.jobType == DevelopmentJobType.Development)
        {
            DevelopmentProgressState state = GetOrCreateProgressState(definition.id);
            state.completed = true;
            lastCompletionMessage = $"『{definition.displayName}』の開発が完了しました！ 作成できるようになりました。";
        }
        else
        {
            if (checkoutItemSystem == null)
                checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();

            checkoutItemSystem?.AddStock(definition.producedCheckoutItemId, definition.productionQuantity, false);
            lastCompletionMessage = $"『{definition.displayName}』×{definition.productionQuantity}の作成が完了しました！";
        }

        activeJob.Clear();
        Debug.Log(lastCompletionMessage);
        OnJobCompleted?.Invoke(lastCompletionMessage);
    }

    private bool HasRequiredCheckoutMaterials(DevelopmentDefinition definition)
    {
        if (definition == null)
            return false;

        if (checkoutItemSystem == null)
            checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();

        if (!HasCheckoutMaterial(definition.requiredCheckoutItemId, definition.requiredCheckoutItemQuantity))
            return false;

        return HasCheckoutMaterial(definition.requiredCheckoutItemId2, definition.requiredCheckoutItemQuantity2);
    }

    private bool HasCheckoutMaterial(string itemId, int quantity)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
            return true;

        return checkoutItemSystem != null && checkoutItemSystem.GetStockQuantity(itemId) >= quantity;
    }

    private void ConsumeCheckoutMaterial(string itemId, int quantity)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId) || checkoutItemSystem == null)
            return;

        checkoutItemSystem.TryConsumeStock(itemId, quantity);
    }

    private bool IsValidMaterialFlower(DevelopmentDefinition definition, FlowerData flower)
    {
        if (definition == null)
            return false;

        if (!definition.requiresFlower)
            return true;

        if (flower == null || inventorySystem == null || inventorySystem.GetTotalQuantity(flower) <= 0)
            return false;

        return flower.arrivalDifficulty >= Mathf.Max(1, definition.minimumFlowerArrivalDifficulty);
    }

    private DevelopmentProgressState GetOrCreateProgressState(DevelopmentId id)
    {
        progressStates ??= new List<DevelopmentProgressState>();
        DevelopmentProgressState state = progressStates.FirstOrDefault(s => s != null && s.id == id);
        if (state != null)
            return state;

        state = new DevelopmentProgressState { id = id, completed = false };
        progressStates.Add(state);
        return state;
    }

    private void NormalizeProgressStates()
    {
        progressStates ??= new List<DevelopmentProgressState>();
        foreach (DevelopmentDefinition definition in definitions)
            if (definition != null)
                GetOrCreateProgressState(definition.id);
    }

    private int GetAbsoluteDay()
    {
        if (shopManager == null)
            return -1;

        return (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
    }

    private void BuildDefaultDefinitions()
    {
        if (definitions != null && definitions.Count == 5)
            return;

        definitions = new List<DevelopmentDefinition>
        {
            new()
            {
                id = DevelopmentId.Karasan,
                displayName = "枯ラサン",
                developmentCost = 20000,
                developmentDays = 7,
                requiredShopRating = DevelopmentUnlockShopRating,
                requiredCheckoutItemId = NutritionItemId,
                requiredCheckoutItemQuantity = 1,
                requiresFlower = true,
                minimumFlowerArrivalDifficulty = 1,
                producedCheckoutItemId = KarasanItemId,
                productionQuantity = 10,
                productionCost = 2000,
                productionDays = 1
            },
            new()
            {
                id = DevelopmentId.SodatsuCho,
                displayName = "そだーつ長",
                developmentCost = 40000,
                developmentDays = 10,
                requiredShopRating = DevelopmentUnlockShopRating,
                prerequisiteDevelopments = new[] { DevelopmentId.Karasan },
                requiredCheckoutItemId = FertilizerItemId,
                requiredCheckoutItemQuantity = 1,
                requiresFlower = true,
                minimumFlowerArrivalDifficulty = 1,
                producedCheckoutItemId = SodatsuChoItemId,
                productionQuantity = 10,
                productionCost = 3000,
                productionDays = 1
            },
            new()
            {
                id = DevelopmentId.SodatsuTsubu,
                displayName = "そだーつ粒",
                developmentCost = 60000,
                developmentDays = 10,
                requiredShopRating = DevelopmentUnlockShopRating,
                prerequisiteDevelopments = new[] { DevelopmentId.SodatsuCho },
                requiredCheckoutItemId = SodatsuChoItemId,
                requiredCheckoutItemQuantity = 1,
                requiresFlower = true,
                minimumFlowerArrivalDifficulty = 5,
                producedCheckoutItemId = SodatsuTsubuItemId,
                productionQuantity = 10,
                productionCost = 4000,
                productionDays = 1
            },
            new()
            {
                id = DevelopmentId.SodatsuEki,
                displayName = "そだーつ液",
                developmentCost = 70000,
                developmentDays = 10,
                requiredShopRating = DevelopmentUnlockShopRating,
                prerequisiteDevelopments = new[] { DevelopmentId.SodatsuCho },
                requiredCheckoutItemId = SodatsuChoItemId,
                requiredCheckoutItemQuantity = 1,
                requiresFlower = true,
                minimumFlowerArrivalDifficulty = 6,
                producedCheckoutItemId = SodatsuEkiItemId,
                productionQuantity = 10,
                productionCost = 4000,
                productionDays = 1
            },
            new()
            {
                id = DevelopmentId.KarasanTsui,
                displayName = "枯ラサンつい",
                developmentCost = 150000,
                developmentDays = 15,
                requiredShopRating = DevelopmentUnlockShopRating,
                prerequisiteDevelopments = new[] { DevelopmentId.SodatsuCho },
                requiredCheckoutItemId = KarasanItemId,
                requiredCheckoutItemQuantity = 1,
                requiredCheckoutItemId2 = SodatsuChoItemId,
                requiredCheckoutItemQuantity2 = 1,
                requiresFlower = true,
                minimumFlowerArrivalDifficulty = 8,
                producedCheckoutItemId = KarasanTsuiItemId,
                productionQuantity = 5,
                productionCost = 8000,
                productionDays = 1
            }
        };
    }

    [ContextMenu("DEBUG: 開発状態をログ表示")]
    private void DebugPrintState()
    {
        string completed = string.Join(", ", definitions.Where(d => d != null && IsCompleted(d.id)).Select(d => d.displayName));
        string job = HasActiveJob
            ? $"{activeJob.jobType} / {GetDefinition(activeJob.targetId)?.displayName} / 残り{activeJob.remainingDays}日"
            : "なし";
        Debug.Log($"開発解禁:{IsDevelopmentFeatureUnlocked} / 完了:[{completed}] / 進行中:{job}");
    }
}
