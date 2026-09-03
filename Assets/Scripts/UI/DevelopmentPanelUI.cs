using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面から開く「開発」パネルを管理します。
/// ver0.0.6では「開発 / 作成 / 新種開発」の3タブを持ち、
/// まず開発タブの5項目をDevelopmentSystemへ接続します。
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

    [Header("開発カード")]
    [Tooltip("未設定でもDevelopmentTab配下から自動取得します。Hierarchy順に5開発へ割り当てます。")]
    [SerializeField] private DevelopmentItemUI[] developmentItems;

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
        AutoFindDevelopmentItems();
        BindDevelopmentItems();

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);
        if (developmentTabButton != null)
            developmentTabButton.onClick.AddListener(ShowDevelopmentTab);
        if (productionTabButton != null)
            productionTabButton.onClick.AddListener(ShowProductionTab);
        if (hybridTabButton != null)
            hybridTabButton.onClick.AddListener(ShowHybridTab);

        ApplyTabVisibility();

        // 初期表示はHierarchy側のActive状態で管理します。
        // AwakeでHidePanelを呼ぶと、初回クリック時に表示が打ち消されるため行いません。
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

        BindDevelopmentItems();
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
        Refresh();
    }

    public void ShowProductionTab()
    {
        currentTab = PanelTab.Production;
        ApplyTabVisibility();
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

        if (developmentItems == null || developmentItems.Length == 0)
            AutoFindDevelopmentItems();

        BindDevelopmentItems();

        foreach (DevelopmentItemUI item in developmentItems ?? Array.Empty<DevelopmentItemUI>())
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

    private void BindDevelopmentItems()
    {
        if (developmentSystem == null || developmentItems == null)
            return;

        for (int i = 0; i < developmentItems.Length && i < DefaultDevelopmentOrder.Length; i++)
        {
            if (developmentItems[i] != null)
                developmentItems[i].Bind(developmentSystem, DefaultDevelopmentOrder[i]);
        }
    }

    private void AutoFindDevelopmentItems()
    {
        if (developmentTab == null)
            AutoFindTabReferences();

        Transform searchRoot = developmentTab != null ? developmentTab.transform : transform;
        developmentItems = searchRoot
            .GetComponentsInChildren<DevelopmentItemUI>(true)
            .OrderBy(item => item.transform.GetSiblingIndex())
            .ToArray();
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
