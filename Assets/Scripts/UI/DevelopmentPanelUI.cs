using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面から開く「開発」パネルを管理します。
/// 開発・作成では同じDevelopmentItemプレハブを使い、作成タブには解禁済み交配花も追加します。
/// </summary>
public class DevelopmentPanelUI : MonoBehaviour
{
    private enum PanelTab { Development, Production, Hybrid }

    private static readonly DevelopmentId[] DefaultDevelopmentOrder =
    {
        DevelopmentId.Karasan,
        DevelopmentId.SodatsuCho,
        DevelopmentId.SodatsuTsubu,
        DevelopmentId.SodatsuEki,
        DevelopmentId.KarasanTsui
    };

    [Header("参照")]
    [SerializeField] private DevelopmentSystem developmentSystem;
    [SerializeField] private HybridDevelopmentSystem hybridDevelopmentSystem;

    [Header("表示")]
    [SerializeField] private GameObject panelRoot;

    [Header("タブ")]
    [SerializeField] private Button developmentTabButton;
    [SerializeField] private Button productionTabButton;
    [SerializeField] private Button hybridTabButton;
    [SerializeField] private GameObject developmentTab;
    [SerializeField] private GameObject productionTab;
    [SerializeField] private GameObject hybridTab;

    [Header("共通カードプレハブ")]
    [SerializeField] private DevelopmentItemUI developmentItemPrefab;

    [Header("開発カード生成")]
    [SerializeField] private Transform developmentContent;
    [SerializeField] private DevelopmentItemUI[] developmentItems;

    [Header("作成カード生成")]
    [SerializeField] private Transform productionContent;
    [SerializeField] private DevelopmentItemUI[] productionItems;

    [Header("ボタン")]
    [SerializeField] private Button closeButton;

    private PanelTab currentTab = PanelTab.Development;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        ResolveSystems();
        AutoFindTabReferences();
        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        EnsureHybridProductionItems();
        BindAllItems();

        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
        if (developmentTabButton != null) developmentTabButton.onClick.AddListener(ShowDevelopmentTab);
        if (productionTabButton != null) productionTabButton.onClick.AddListener(ShowProductionTab);
        if (hybridTabButton != null) hybridTabButton.onClick.AddListener(ShowHybridTab);
        ApplyTabVisibility();
    }

    private void OnEnable()
    {
        ResolveSystems();
        if (developmentSystem != null)
        {
            developmentSystem.OnChanged += Refresh;
            developmentSystem.OnJobCompleted += HandleJobCompleted;
        }
        if (hybridDevelopmentSystem != null)
        {
            hybridDevelopmentSystem.OnChanged += Refresh;
            hybridDevelopmentSystem.OnResearchCompleted += HandleJobCompleted;
            hybridDevelopmentSystem.OnProductionCompleted += HandleJobCompleted;
        }

        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        EnsureHybridProductionItems();
        BindAllItems();
        Refresh();
    }

    private void OnDisable()
    {
        if (developmentSystem != null)
        {
            developmentSystem.OnChanged -= Refresh;
            developmentSystem.OnJobCompleted -= HandleJobCompleted;
        }
        if (hybridDevelopmentSystem != null)
        {
            hybridDevelopmentSystem.OnChanged -= Refresh;
            hybridDevelopmentSystem.OnResearchCompleted -= HandleJobCompleted;
            hybridDevelopmentSystem.OnProductionCompleted -= HandleJobCompleted;
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(HidePanel);
        if (developmentTabButton != null) developmentTabButton.onClick.RemoveListener(ShowDevelopmentTab);
        if (productionTabButton != null) productionTabButton.onClick.RemoveListener(ShowProductionTab);
        if (hybridTabButton != null) hybridTabButton.onClick.RemoveListener(ShowHybridTab);
    }

    public void ShowPanel()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        EnsureHybridProductionItems();
        Refresh();
    }

    public void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void ShowDevelopmentTab()
    {
        currentTab = PanelTab.Development;
        ApplyTabVisibility();
        EnsureDevelopmentItems();
        Refresh();
    }

    public void ShowProductionTab()
    {
        currentTab = PanelTab.Production;
        ApplyTabVisibility();
        AutoFindContents();
        EnsureProductionItems();
        EnsureHybridProductionItems();
        BindAllItems();
        Refresh();
    }

    public void ShowHybridTab()
    {
        currentTab = PanelTab.Hybrid;
        ApplyTabVisibility();
    }

    public void Refresh()
    {
        ResolveSystems();
        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        EnsureHybridProductionItems();
        BindAllItems();

        foreach (DevelopmentItemUI item in developmentItems ?? Array.Empty<DevelopmentItemUI>()) item?.Refresh();
        foreach (DevelopmentItemUI item in productionItems ?? Array.Empty<DevelopmentItemUI>()) item?.Refresh();

        if (productionContent != null)
        {
            foreach (DevelopmentItemUI item in productionContent.GetComponentsInChildren<DevelopmentItemUI>(true)
                         .Where(x => x != null && x.Mode == DevelopmentItemUI.DisplayMode.HybridProduction))
                item.Refresh();
        }
    }

    private void HandleJobCompleted(string message) => Refresh();

    private void ApplyTabVisibility()
    {
        if (developmentTab != null) developmentTab.SetActive(currentTab == PanelTab.Development);
        if (productionTab != null) productionTab.SetActive(currentTab == PanelTab.Production);
        if (hybridTab != null) hybridTab.SetActive(currentTab == PanelTab.Hybrid);
    }

    private void BindAllItems()
    {
        BindDevelopmentItems();
        BindProductionItems();
        BindHybridProductionItems();
    }

    private void BindDevelopmentItems()
    {
        if (developmentSystem == null || developmentItems == null) return;
        for (int i = 0; i < developmentItems.Length && i < DefaultDevelopmentOrder.Length; i++)
            developmentItems[i]?.Bind(developmentSystem, DefaultDevelopmentOrder[i], DevelopmentItemUI.DisplayMode.Development);
    }

    private void BindProductionItems()
    {
        if (developmentSystem == null || productionItems == null) return;
        for (int i = 0; i < productionItems.Length && i < DefaultDevelopmentOrder.Length; i++)
            productionItems[i]?.Bind(developmentSystem, DefaultDevelopmentOrder[i], DevelopmentItemUI.DisplayMode.Production);
    }

    private void BindHybridProductionItems()
    {
        if (productionContent == null || hybridDevelopmentSystem == null) return;
        foreach (HybridRecipeDefinition recipe in hybridDevelopmentSystem.GetUnlockedRecipes())
        {
            DevelopmentItemUI item = productionContent.GetComponentsInChildren<DevelopmentItemUI>(true)
                .FirstOrDefault(x => x != null && x.gameObject.name == $"HybridProductionItem_{recipe.hybridName}");
            item?.BindHybrid(developmentSystem, hybridDevelopmentSystem, recipe.hybridName);
        }
    }

    private void EnsureDevelopmentItems()
    {
        developmentItems = EnsureItemsForContent(developmentContent, DevelopmentItemUI.DisplayMode.Development, "DevelopmentItem");
    }

    private void EnsureProductionItems()
    {
        productionItems = EnsureItemsForContent(productionContent, DevelopmentItemUI.DisplayMode.Production, "ProductionItem");
    }

    private DevelopmentItemUI[] EnsureItemsForContent(Transform content, DevelopmentItemUI.DisplayMode mode, string prefix)
    {
        if (content == null) return Array.Empty<DevelopmentItemUI>();

        DevelopmentItemUI[] found = content.GetComponentsInChildren<DevelopmentItemUI>(true)
            .Where(item => item != null && item.Mode != DevelopmentItemUI.DisplayMode.HybridProduction)
            .OrderBy(item => item.transform.GetSiblingIndex())
            .Take(DefaultDevelopmentOrder.Length)
            .ToArray();

        DevelopmentItemUI template = developmentItemPrefab != null ? developmentItemPrefab : found.FirstOrDefault();
        if (template == null) return found;

        for (int i = found.Length; i < DefaultDevelopmentOrder.Length; i++)
        {
            DevelopmentItemUI created = Instantiate(template, content);
            created.gameObject.name = $"{prefix}_{DefaultDevelopmentOrder[i]}";
            created.gameObject.SetActive(true);
            created.Bind(developmentSystem, DefaultDevelopmentOrder[i], mode);
        }

        return content.GetComponentsInChildren<DevelopmentItemUI>(true)
            .Where(item => item != null && item.Mode != DevelopmentItemUI.DisplayMode.HybridProduction)
            .OrderBy(item => item.transform.GetSiblingIndex())
            .Take(DefaultDevelopmentOrder.Length)
            .ToArray();
    }

    private void EnsureHybridProductionItems()
    {
        if (productionContent == null || developmentItemPrefab == null || hybridDevelopmentSystem == null) return;

        foreach (HybridRecipeDefinition recipe in hybridDevelopmentSystem.GetUnlockedRecipes())
        {
            string objectName = $"HybridProductionItem_{recipe.hybridName}";
            DevelopmentItemUI existing = productionContent.GetComponentsInChildren<DevelopmentItemUI>(true)
                .FirstOrDefault(x => x != null && x.gameObject.name == objectName);
            if (existing != null) continue;

            DevelopmentItemUI created = Instantiate(developmentItemPrefab, productionContent);
            created.gameObject.name = objectName;
            created.gameObject.SetActive(true);
            created.BindHybrid(developmentSystem, hybridDevelopmentSystem, recipe.hybridName);
        }
    }

    private void AutoFindContents()
    {
        if (developmentTab == null || productionTab == null) AutoFindTabReferences();
        if (developmentContent == null || (developmentTab != null && !developmentContent.IsChildOf(developmentTab.transform)))
            developmentContent = FindNamedContent(developmentTab?.transform);
        if (productionContent == null || (productionTab != null && !productionContent.IsChildOf(productionTab.transform)))
            productionContent = FindNamedContent(productionTab?.transform);
    }

    private Transform FindNamedContent(Transform root)
    {
        return root?.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != null && t.gameObject.name == "Content");
    }

    private void AutoFindTabReferences()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        GameObject[] objects = GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).ToArray();

        if (developmentTabButton == null) developmentTabButton = buttons.FirstOrDefault(b => b.gameObject.name == "DevelopmentTabButton");
        if (productionTabButton == null) productionTabButton = buttons.FirstOrDefault(b => b.gameObject.name == "ProductionTabButton");
        if (hybridTabButton == null) hybridTabButton = buttons.FirstOrDefault(b => b.gameObject.name == "HybridTabButton");
        if (closeButton == null) closeButton = buttons.FirstOrDefault(b => b.gameObject.name == "CloseButton");
        if (developmentTab == null) developmentTab = objects.FirstOrDefault(o => o.name == "DevelopmentTab");
        if (productionTab == null) productionTab = objects.FirstOrDefault(o => o.name == "ProductionTab");
        if (hybridTab == null) hybridTab = objects.FirstOrDefault(o => o.name == "HybridTab");
    }

    private void ResolveSystems()
    {
        if (developmentSystem == null) developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
        if (hybridDevelopmentSystem == null) hybridDevelopmentSystem = FindFirstObjectByType<HybridDevelopmentSystem>();
        if (hybridDevelopmentSystem == null && developmentSystem != null)
            hybridDevelopmentSystem = developmentSystem.gameObject.AddComponent<HybridDevelopmentSystem>();
    }
}
