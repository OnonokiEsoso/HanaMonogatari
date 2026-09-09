using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 開発パネル内の「開発 / 作成 / 交配」タブに、
/// 今すぐ開始できる作業がある時だけ「!!」を表示します。
/// Scene / Prefab の変更を不要にするため、DevelopmentPanelUIへ実行時に自動追加します。
/// </summary>
public class DevelopmentTabAlertUI : MonoBehaviour
{
    [SerializeField] private DevelopmentSystem developmentSystem;
    [SerializeField] private HybridDevelopmentSystem hybridDevelopmentSystem;
    [SerializeField] private InventorySystem inventorySystem;

    [SerializeField] private Button developmentTabButton;
    [SerializeField] private Button productionTabButton;
    [SerializeField] private Button hybridTabButton;

    [SerializeField] private TMP_Text developmentAlertText;
    [SerializeField] private TMP_Text productionAlertText;
    [SerializeField] private TMP_Text hybridAlertText;

    private float nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAutomatically()
    {
        DevelopmentPanelUI panel = FindFirstObjectByType<DevelopmentPanelUI>(FindObjectsInactive.Include);
        if (panel == null || panel.GetComponent<DevelopmentTabAlertUI>() != null)
            return;

        panel.gameObject.AddComponent<DevelopmentTabAlertUI>();
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshAlerts();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (developmentSystem != null)
            developmentSystem.OnChanged += RefreshAlerts;
        if (hybridDevelopmentSystem != null)
            hybridDevelopmentSystem.OnChanged += RefreshAlerts;
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged += RefreshAlerts;

        RefreshAlerts();
    }

    private void OnDisable()
    {
        if (developmentSystem != null)
            developmentSystem.OnChanged -= RefreshAlerts;
        if (hybridDevelopmentSystem != null)
            hybridDevelopmentSystem.OnChanged -= RefreshAlerts;
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= RefreshAlerts;
    }

    private void Update()
    {
        // 所持金や店評価など、専用イベントを直接購読していない状態変化も拾うための軽い保険。
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + 0.25f;
        RefreshAlerts();
    }

    private void RefreshAlerts()
    {
        ResolveReferences();

        SetAlert(developmentAlertText, HasImmediatelyStartableDevelopment());
        SetAlert(productionAlertText, HasImmediatelyStartableProduction());
        SetAlert(hybridAlertText, HasImmediatelyStartableHybridResearch());
    }

    private bool HasImmediatelyStartableDevelopment()
    {
        if (developmentSystem == null || developmentSystem.HasAnyActiveWork || !developmentSystem.IsDevelopmentFeatureUnlocked)
            return false;

        foreach (DevelopmentDefinition definition in developmentSystem.Definitions)
        {
            if (definition == null || developmentSystem.IsCompleted(definition.id))
                continue;

            FlowerData materialFlower = FindSuitableMaterialFlower(definition);
            if (developmentSystem.CanStartDevelopment(definition.id, materialFlower))
                return true;
        }

        return false;
    }

    private bool HasImmediatelyStartableProduction()
    {
        if (developmentSystem == null || developmentSystem.HasAnyActiveWork)
            return false;

        foreach (DevelopmentDefinition definition in developmentSystem.Definitions)
        {
            if (definition != null && developmentSystem.CanStartProduction(definition.id))
                return true;
        }

        if (hybridDevelopmentSystem == null)
            return false;

        foreach (HybridRecipeDefinition recipe in hybridDevelopmentSystem.GetUnlockedRecipes())
        {
            if (recipe != null && hybridDevelopmentSystem.CanStartHybridProduction(recipe.hybridName, out _))
                return true;
        }

        return false;
    }

    private bool HasImmediatelyStartableHybridResearch()
    {
        if (developmentSystem == null || hybridDevelopmentSystem == null || inventorySystem == null)
            return false;
        if (!developmentSystem.IsNewSpeciesDevelopmentUnlocked || developmentSystem.HasAnyActiveWork)
            return false;

        List<FlowerData> flowers = inventorySystem.Batches
            .Where(batch => batch != null && batch.flower != null && batch.quantity > 0)
            .Select(batch => batch.flower)
            .Distinct()
            .ToList();

        for (int i = 0; i < flowers.Count; i++)
        {
            for (int j = i + 1; j < flowers.Count; j++)
            {
                if (hybridDevelopmentSystem.CanStartHybrid(flowers[i], flowers[j], out _))
                    return true;
            }
        }

        return false;
    }

    private FlowerData FindSuitableMaterialFlower(DevelopmentDefinition definition)
    {
        if (definition == null || !definition.requiresFlower)
            return null;
        if (inventorySystem == null)
            return null;

        return inventorySystem.Batches
            .Where(batch => batch != null && batch.flower != null && batch.quantity > 0)
            .Where(batch => batch.flower.arrivalDifficulty >= Mathf.Max(1, definition.minimumFlowerArrivalDifficulty))
            .OrderBy(batch => batch.remainingFreshnessDays)
            .ThenBy(batch => batch.flower.arrivalDifficulty)
            .Select(batch => batch.flower)
            .FirstOrDefault();
    }

    private static void SetAlert(TMP_Text text, bool visible)
    {
        if (text == null)
            return;

        if (text.text != "!!")
            text.text = "!!";

        text.gameObject.SetActive(visible);
    }

    private void ResolveReferences()
    {
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
        if (hybridDevelopmentSystem == null)
            hybridDevelopmentSystem = FindFirstObjectByType<HybridDevelopmentSystem>();
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();

        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (developmentTabButton == null)
            developmentTabButton = buttons.FirstOrDefault(button => button != null && button.gameObject.name == "DevelopmentTabButton");
        if (productionTabButton == null)
            productionTabButton = buttons.FirstOrDefault(button => button != null && button.gameObject.name == "ProductionTabButton");
        if (hybridTabButton == null)
            hybridTabButton = buttons.FirstOrDefault(button => button != null && button.gameObject.name == "HybridTabButton");

        if (developmentAlertText == null)
            developmentAlertText = FindAlertText(developmentTabButton, "DevelopmentTabAlertText");
        if (productionAlertText == null)
            productionAlertText = FindAlertText(productionTabButton, "ProductionTabAlertText");
        if (hybridAlertText == null)
            hybridAlertText = FindAlertText(hybridTabButton, "HybridTabAlertText");
    }

    private static TMP_Text FindAlertText(Button button, string preferredName)
    {
        if (button == null)
            return null;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text named = texts.FirstOrDefault(text => text != null && text.gameObject.name == preferredName);
        if (named != null)
            return named;

        return texts.FirstOrDefault(text =>
            text != null &&
            (text.gameObject.name == "AlertText" ||
             text.gameObject.name == "TabAlertText" ||
             string.Equals(text.text?.Trim(), "!!", StringComparison.Ordinal)));
    }
}
