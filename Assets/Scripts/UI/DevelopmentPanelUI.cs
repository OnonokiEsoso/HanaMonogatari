using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面から開く「開発」パネルを管理します。
/// ver0.0.6では「開発 / 作成 / 新種開発」の3タブを持ち、
/// 開発・作成の両タブで同じDevelopmentItemプレハブを自動生成して使います。
/// </summary>
public class DevelopmentPanelUI : MonoBehaviour
{
    private enum PanelTab
    {
        Development,
        Production,
        Hybrid
    }

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

    [Header("表示")]
    [Tooltip("開発パネル全体のルート。未設定ならこのGameObjectを使用します。")]
    [SerializeField] private GameObject panelRoot;

    [Header("タブ")]
    [SerializeField] private Button developmentTabButton;
    [SerializeField] private Button productionTabButton;
    [SerializeField] private Button hybridTabButton;
    [SerializeField] private GameObject developmentTab;
    [SerializeField] private GameObject productionTab;
    [SerializeField] private GameObject hybridTab;

    [Header("共通カードプレハブ")]
    [Tooltip("開発・作成の両方で使うDevelopmentItemプレハブ。")]
    [SerializeField] private DevelopmentItemUI developmentItemPrefab;

    [Header("開発カード生成")]
    [Tooltip("DevelopmentTab側のScrollView/Viewport/Content。未設定ならDevelopmentTab配下の『Content』を自動取得します。")]
    [SerializeField] private Transform developmentContent;
    [SerializeField] private DevelopmentItemUI[] developmentItems;

    [Header("作成カード生成")]
    [Tooltip("ProductionTab側のScrollView/Viewport/Content。未設定ならProductionTab配下の『Content』を自動取得します。")]
    [SerializeField] private Transform productionContent;
    [SerializeField] private DevelopmentItemUI[] productionItems;

    [Header("ボタン")]
    [Tooltip("パネルを閉じるボタン。任意。")]
    [SerializeField] private Button closeButton;

    private PanelTab currentTab = PanelTab.Development;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();

        AutoFindTabReferences();
        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        BindAllItems();

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);
        if (developmentTabButton != null)
            developmentTabButton.onClick.AddListener(ShowDevelopmentTab);
        if (productionTabButton != null)
            productionTabButton.onClick.AddListener(ShowProductionTab);
        if (hybridTabButton != null)
            hybridTabButton.onClick.AddListener(ShowHybridTab);

        ApplyTabVisibility();
    }

    private void OnEnable()
    {
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();

        if (developmentSystem != null)
        {
            developmentSystem.OnChanged += Refresh;
            developmentSystem.OnJobCompleted += HandleJobCompleted;
        }

        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
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
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
        if (developmentTabButton != null)
            developmentTabButton.onClick.RemoveListener(ShowDevelopmentTab);
        if (productionTabButton != null)
            productionTabButton.onClick.RemoveListener(ShowProductionTab);
        if (hybridTabButton != null)
            hybridTabButton.onClick.RemoveListener(ShowHybridTab);
    }

    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        Refresh();
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
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
        BindProductionItems();
        Refresh();
    }

    public void ShowHybridTab()
    {
        currentTab = PanelTab.Hybrid;
        ApplyTabVisibility();
    }

    public void Refresh()
    {
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();

        AutoFindContents();
        EnsureDevelopmentItems();
        EnsureProductionItems();
        BindAllItems();

        foreach (DevelopmentItemUI item in developmentItems ?? Array.Empty<DevelopmentItemUI>())
            item?.Refresh();

        foreach (DevelopmentItemUI item in productionItems ?? Array.Empty<DevelopmentItemUI>())
            item?.Refresh();
    }

    private void HandleJobCompleted(string message)
    {
        Refresh();
    }

    private void ApplyTabVisibility()
    {
        if (developmentTab != null)
            developmentTab.SetActive(currentTab == PanelTab.Development);
        if (productionTab != null)
            productionTab.SetActive(currentTab == PanelTab.Production);
        if (hybridTab != null)
            hybridTab.SetActive(currentTab == PanelTab.Hybrid);
    }

    private void BindAllItems()
    {
        BindDevelopmentItems();
        BindProductionItems();
    }

    private void BindDevelopmentItems()
    {
        if (developmentSystem == null || developmentItems == null)
            return;

        for (int i = 0; i < developmentItems.Length && i < DefaultDevelopmentOrder.Length; i++)
        {
            if (developmentItems[i] != null)
                developmentItems[i].Bind(developmentSystem, DefaultDevelopmentOrder[i], DevelopmentItemUI.DisplayMode.Development);
        }
    }

    private void BindProductionItems()
    {
        if (developmentSystem == null || productionItems == null)
            return;

        for (int i = 0; i < productionItems.Length && i < DefaultDevelopmentOrder.Length; i++)
        {
            if (productionItems[i] != null)
                productionItems[i].Bind(developmentSystem, DefaultDevelopmentOrder[i], DevelopmentItemUI.DisplayMode.Production);
        }
    }

    private void EnsureDevelopmentItems()
    {
        developmentItems = EnsureItemsForContent(
            developmentContent,
            developmentItems,
            DevelopmentItemUI.DisplayMode.Development,
            "DevelopmentItem");
    }

    private void EnsureProductionItems()
    {
        productionItems = EnsureItemsForContent(
            productionContent,
            productionItems,
            DevelopmentItemUI.DisplayMode.Production,
            "ProductionItem");
    }

    private DevelopmentItemUI[] EnsureItemsForContent(
        Transform content,
        DevelopmentItemUI[] currentItems,
        DevelopmentItemUI.DisplayMode mode,
        string objectNamePrefix)
    {
        DevelopmentItemUI[] found = FindItems(content);
        int currentCount = found.Length;

        if (currentCount >= DefaultDevelopmentOrder.Length)
            return found.Take(DefaultDevelopmentOrder.Length).ToArray();

        if (content == null)
            return found;

        DevelopmentItemUI template = developmentItemPrefab;
        if (template == null && currentCount > 0)
            template = found[0];

        if (template == null)
        {
            Debug.LogWarning("DevelopmentPanelUI: Development Item Prefab が未設定です。同じプレハブを開発・作成で共通利用します。");
            return found;
        }

        for (int i = currentCount; i < DefaultDevelopmentOrder.Length; i++)
        {
            DevelopmentItemUI created = Instantiate(template, content);
            created.gameObject.name = $"{objectNamePrefix}_{DefaultDevelopmentOrder[i]}";
            created.gameObject.SetActive(true);
            created.Bind(developmentSystem, DefaultDevelopmentOrder[i], mode);
        }

        return FindItems(content).Take(DefaultDevelopmentOrder.Length).ToArray();
    }

    private DevelopmentItemUI[] FindItems(Transform content)
    {
        if (content == null)
            return Array.Empty<DevelopmentItemUI>();

        return content
            .GetComponentsInChildren<DevelopmentItemUI>(true)
            .Where(item => item != null)
            .OrderBy(item => item.transform.GetSiblingIndex())
            .ToArray();
    }

    private void AutoFindContents()
    {
        if (developmentTab == null || productionTab == null)
            AutoFindTabReferences();

        if (developmentContent == null && developmentTab != null)
            developmentContent = FindNamedContent(developmentTab.transform);

        if (productionContent == null && productionTab != null)
            productionContent = FindNamedContent(productionTab.transform);
    }

    private Transform FindNamedContent(Transform root)
    {
        if (root == null)
            return null;

        return root
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t != null && t.gameObject.name == "Content");
    }

    private void AutoFindTabReferences()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        GameObject[] objects = GetComponentsInChildren<Transform>(true)
            .Select(t => t.gameObject)
            .ToArray();

        if (developmentTabButton == null)
            developmentTabButton = buttons.FirstOrDefault(b => b.gameObject.name == "DevelopmentTabButton");
        if (productionTabButton == null)
            productionTabButton = buttons.FirstOrDefault(b => b.gameObject.name == "ProductionTabButton");
        if (hybridTabButton == null)
            hybridTabButton = buttons.FirstOrDefault(b => b.gameObject.name == "HybridTabButton");
        if (closeButton == null)
            closeButton = buttons.FirstOrDefault(b => b.gameObject.name == "CloseButton");

        if (developmentTab == null)
            developmentTab = objects.FirstOrDefault(o => o.name == "DevelopmentTab");
        if (productionTab == null)
            productionTab = objects.FirstOrDefault(o => o.name == "ProductionTab");
        if (hybridTab == null)
            hybridTab = objects.FirstOrDefault(o => o.name == "HybridTab");
    }
}
