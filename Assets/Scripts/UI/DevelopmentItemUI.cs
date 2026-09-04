using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 開発パネル内のカード1枚を担当します。
/// 同じDevelopmentItemプレハブを「開発」と「作成」の両方で流用します。
/// NameText / StateText / RequirementText / CostText / DaysText / DevelopmentButton という
/// 子オブジェクト名なら、Inspector未設定でも自動で参照を探します。
/// </summary>
public class DevelopmentItemUI : MonoBehaviour
{
    public enum DisplayMode
    {
        Development,
        Production
    }

    [Header("対象")]
    [SerializeField] private DevelopmentId developmentId;
    [SerializeField] private DisplayMode displayMode = DisplayMode.Development;

    [Header("表示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text daysText;
    [SerializeField] private Button developmentButton;

    private DevelopmentSystem developmentSystem;
    private InventorySystem inventorySystem;
    private ShopManager shopManager;
    private CheckoutItemSystem checkoutItemSystem;

    public DevelopmentId DevelopmentId => developmentId;
    public DisplayMode Mode => displayMode;

    private void Awake()
    {
        AutoFindReferences();

        if (developmentButton != null)
            developmentButton.onClick.AddListener(HandleActionClicked);
    }

    private void OnDestroy()
    {
        if (developmentButton != null)
            developmentButton.onClick.RemoveListener(HandleActionClicked);
    }

    public void Bind(DevelopmentSystem system, DevelopmentId id, DisplayMode mode = DisplayMode.Development)
    {
        developmentSystem = system;
        developmentId = id;
        displayMode = mode;

        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (checkoutItemSystem == null)
            checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();

        Refresh();
    }

    public void Refresh()
    {
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
        if (developmentSystem == null)
            return;

        DevelopmentDefinition definition = developmentSystem.GetDefinition(developmentId);
        if (definition == null)
            return;

        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (checkoutItemSystem == null)
            checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();

        if (displayMode == DisplayMode.Production)
            RefreshProduction(definition);
        else
            RefreshDevelopment(definition);
    }

    private void RefreshDevelopment(DevelopmentDefinition definition)
    {
        bool featureUnlocked = developmentSystem.IsDevelopmentFeatureUnlocked;
        bool prerequisitesDone = developmentSystem.ArePrerequisitesCompleted(definition);
        bool completed = developmentSystem.IsCompleted(developmentId);
        bool isThisJob = developmentSystem.HasActiveJob &&
                         developmentSystem.ActiveJob.jobType == DevelopmentJobType.Development &&
                         developmentSystem.ActiveJob.targetId == developmentId;

        if (!featureUnlocked)
        {
            SetLockedDisplay($"店評価 {DevelopmentSystem.DevelopmentUnlockShopRating:N0} で開発機能が解禁します");
            return;
        }

        if (!prerequisitesDone)
        {
            SetLockedDisplay("前提となる開発が必要です");
            return;
        }

        if (nameText != null)
            nameText.text = definition.displayName;

        SetRequirementVisible(!completed);
        if (!completed && requirementText != null)
            requirementText.text = BuildRequirementText(definition);

        if (costText != null)
            costText.text = $"{definition.developmentCost:N0}円";

        if (daysText != null)
            daysText.text = $"{definition.developmentDays}日";

        if (completed)
        {
            if (stateText != null)
                stateText.text = "開発済み";
            SetButton("開発済み", false);
            return;
        }

        if (isThisJob)
        {
            int remaining = developmentSystem.GetRemainingDays();
            if (stateText != null)
                stateText.text = $"開発中　残り{remaining}日";
            SetButton("開発中", false);
            return;
        }

        if (developmentSystem.HasActiveJob)
        {
            if (stateText != null)
                stateText.text = "別の作業を進行中";
            SetButton("開発開始", false);
            return;
        }

        FlowerData materialFlower = FindSuitableMaterialFlower(definition);
        bool canStart = developmentSystem.CanStartDevelopment(developmentId, materialFlower);

        if (stateText != null)
            stateText.text = canStart ? "開発可能" : GetUnavailableReason(definition, materialFlower);

        SetButton("開発開始", canStart);
    }

    private void RefreshProduction(DevelopmentDefinition definition)
    {
        bool completed = developmentSystem.IsCompleted(developmentId);
        bool isThisJob = developmentSystem.HasActiveJob &&
                         developmentSystem.ActiveJob.jobType == DevelopmentJobType.Production &&
                         developmentSystem.ActiveJob.targetId == developmentId;

        if (!completed)
        {
            if (nameText != null) nameText.text = "？？？";
            if (stateText != null) stateText.text = "未開発";
            SetRequirementVisible(false);
            if (costText != null) costText.text = "---";
            if (daysText != null) daysText.text = "---";
            SetButton("作成不可", false);
            return;
        }

        if (nameText != null)
            nameText.text = definition.displayName;

        SetRequirementVisible(true);
        if (requirementText != null)
            requirementText.text = $"完成数：{definition.productionQuantity}個";

        if (costText != null)
            costText.text = $"{definition.productionCost:N0}円";

        if (daysText != null)
            daysText.text = $"{definition.productionDays}日";

        if (isThisJob)
        {
            int remaining = developmentSystem.GetRemainingDays();
            if (stateText != null)
                stateText.text = $"作成中　残り{remaining}日";
            SetButton("作成中", false);
            return;
        }

        if (developmentSystem.HasActiveJob)
        {
            if (stateText != null)
                stateText.text = "別の作業を進行中";
            SetButton("作成開始", false);
            return;
        }

        bool canStart = developmentSystem.CanStartProduction(developmentId);
        if (stateText != null)
        {
            if (canStart)
                stateText.text = "作成可能";
            else if (shopManager != null && shopManager.Money < definition.productionCost)
                stateText.text = "所持金不足";
            else
                stateText.text = "作成不可";
        }

        SetButton("作成開始", canStart);
    }

    private void HandleActionClicked()
    {
        if (developmentSystem == null)
            return;

        if (displayMode == DisplayMode.Production)
        {
            if (developmentSystem.TryStartProduction(developmentId))
                Refresh();
            return;
        }

        DevelopmentDefinition definition = developmentSystem.GetDefinition(developmentId);
        if (definition == null)
            return;

        FlowerData materialFlower = FindSuitableMaterialFlower(definition);
        if (developmentSystem.TryStartDevelopment(developmentId, materialFlower))
            Refresh();
    }

    private FlowerData FindSuitableMaterialFlower(DevelopmentDefinition definition)
    {
        if (definition == null || !definition.requiresFlower)
            return null;

        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (inventorySystem == null)
            return null;

        return inventorySystem.Batches
            .Where(b => b != null && b.flower != null && b.quantity > 0)
            .Where(b => b.flower.arrivalDifficulty >= Mathf.Max(1, definition.minimumFlowerArrivalDifficulty))
            .OrderBy(b => b.remainingFreshnessDays)
            .ThenBy(b => b.flower.arrivalDifficulty)
            .Select(b => b.flower)
            .FirstOrDefault();
    }

    private string BuildRequirementText(DevelopmentDefinition definition)
    {
        string text = "必要：";
        bool hasAny = false;

        AppendCheckoutRequirement(ref text, ref hasAny, definition.requiredCheckoutItemId, definition.requiredCheckoutItemQuantity);
        AppendCheckoutRequirement(ref text, ref hasAny, definition.requiredCheckoutItemId2, definition.requiredCheckoutItemQuantity2);

        if (definition.requiresFlower)
        {
            if (hasAny) text += "\n";
            text += definition.minimumFlowerArrivalDifficulty <= 1
                ? "任意の花 ×1"
                : $"入荷難易度{definition.minimumFlowerArrivalDifficulty}以上の花 ×1";

            FlowerData selected = FindSuitableMaterialFlower(definition);
            if (selected != null)
                text += $"\n（使用予定：{selected.flowerName} / {selected.GetColorDisplayText()}）";
            hasAny = true;
        }

        return hasAny ? text : "必要：なし";
    }

    private void AppendCheckoutRequirement(ref string text, ref bool hasAny, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            return;

        if (hasAny) text += "\n";
        text += $"{GetCheckoutItemDisplayName(itemId)} ×{quantity}";
        hasAny = true;
    }

    private string GetCheckoutItemDisplayName(string itemId)
    {
        CheckoutItemSystem.CheckoutItemDefinition item = checkoutItemSystem != null
            ? checkoutItemSystem.GetDefinition(itemId)
            : null;
        if (item != null && !string.IsNullOrWhiteSpace(item.displayName))
            return item.displayName;

        return itemId switch
        {
            DevelopmentSystem.NutritionItemId => "栄養剤",
            DevelopmentSystem.FertilizerItemId => "肥料",
            DevelopmentSystem.KarasanItemId => "枯ラサン",
            DevelopmentSystem.SodatsuChoItemId => "そだーつ長",
            DevelopmentSystem.SodatsuTsubuItemId => "そだーつ粒",
            DevelopmentSystem.SodatsuEkiItemId => "そだーつ液",
            DevelopmentSystem.KarasanTsuiItemId => "枯ラサンつい",
            _ => itemId
        };
    }

    private string GetUnavailableReason(DevelopmentDefinition definition, FlowerData materialFlower)
    {
        if (shopManager != null && shopManager.Money < definition.developmentCost)
            return "所持金不足";

        if (!HasCheckoutMaterial(definition.requiredCheckoutItemId, definition.requiredCheckoutItemQuantity) ||
            !HasCheckoutMaterial(definition.requiredCheckoutItemId2, definition.requiredCheckoutItemQuantity2))
            return "材料不足";

        if (definition.requiresFlower && materialFlower == null)
            return definition.minimumFlowerArrivalDifficulty <= 1
                ? "使用できる花がありません"
                : $"入荷難易度{definition.minimumFlowerArrivalDifficulty}以上の花がありません";

        return "条件不足";
    }

    private bool HasCheckoutMaterial(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            return true;
        return checkoutItemSystem != null && checkoutItemSystem.GetStockQuantity(itemId) >= quantity;
    }

    private void SetLockedDisplay(string reason)
    {
        if (nameText != null) nameText.text = "？？？";
        if (stateText != null) stateText.text = reason;
        SetRequirementVisible(true);
        if (requirementText != null) requirementText.text = "？？？";
        if (costText != null) costText.text = "---";
        if (daysText != null) daysText.text = "---";
        SetButton("未解禁", false);
    }

    private void SetRequirementVisible(bool visible)
    {
        if (requirementText != null)
            requirementText.gameObject.SetActive(visible);
    }

    private void SetButton(string label, bool interactable)
    {
        if (developmentButton == null)
            return;

        developmentButton.interactable = interactable;
        TMP_Text buttonText = developmentButton.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
            buttonText.text = label;
    }

    private void AutoFindReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (nameText == null) nameText = texts.FirstOrDefault(t => t.gameObject.name == "NameText");
        if (stateText == null) stateText = texts.FirstOrDefault(t => t.gameObject.name == "StateText");
        if (requirementText == null) requirementText = texts.FirstOrDefault(t => t.gameObject.name == "RequirementText");
        if (costText == null) costText = texts.FirstOrDefault(t => t.gameObject.name == "CostText");
        if (daysText == null) daysText = texts.FirstOrDefault(t => t.gameObject.name == "DaysText");

        if (developmentButton == null)
        {
            developmentButton = GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.gameObject.name == "DevelopmentButton");
        }
    }
}
