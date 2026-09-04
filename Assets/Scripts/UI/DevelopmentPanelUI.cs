using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面から開く「開発」パネルを管理します。
/// ver0.0.6では「開発 / 作成 / 新種開発」の3タブを持ち、
/// 開発タブではDevelopmentItemプレハブを5件生成してDevelopmentSystemへ接続します。
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

    [Header("開発カード生成")]
    [Tooltip("DevelopmentItemプレハブを設定します。DevelopmentTab内にカードが無い場合、このプレハブから5枚自動生成します。")]
    [SerializeField] private DevelopmentItemUI developmentItemPrefab;
    [Tooltip("ScrollView/Viewport/Contentを設定します。未設定ならDevelopmentTab配下の『Content』を自動取得します。")]
    [SerializeField] private Transform developmentContent;

    [Header("開発カード")]
    [Tooltip("通常は自動生成・自動取得されるため手動設定不要です。")]
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
        AutoFindDevelopmentContent();
        EnsureDevelopmentItems();
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

        AutoFindDevelopmentContent();
        EnsureDevelopmentItems();
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

        AutoFindDevelopmentContent();
        EnsureDevelopmentItems();
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
        AutoFindDevelopmentContent();
        EnsureDevelopmentItems();
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

        AutoFindDevelopmentContent();
        EnsureDevelopmentItems();
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

    /// <summary>
    /// Content配下に開発カードが無い場合は、プレハブから5枚生成します。
    /// すでに1～4枚置かれている場合は、その先頭カードをテンプレートとして不足分を複製します。
    /// </summary>
    private void EnsureDevelopmentItems()
    {
        AutoFindDevelopmentItems();

        int currentCount = developmentItems?.Length ?? 0;
        if (currentCount >= DefaultDevelopmentOrder.Length)
            return;

        if (developmentContent == null)
        {
            Debug.LogWarning("DevelopmentPanelUI: Development Content が見つかりません。ScrollView/Viewport/Content を設定してください。");
            return;
        }

        DevelopmentItemUI template = developmentItemPrefab;
        if (template == null && currentCount > 0)
            template = developmentItems[0];

        if (template == null)
        {
            Debug.LogWarning("DevelopmentPanelUI: Development Item Prefab が未設定です。DevelopmentItemプレハブをInspectorへ設定してください。");
            return;
        }

        for (int i = currentCount; i < DefaultDevelopmentOrder.Length; i++)
        {
            DevelopmentItemUI created = Instantiate(template, developmentContent);
            created.gameObject.name = $"DevelopmentItem_{DefaultDevelopmentOrder[i]}";
            created.gameObject.SetActive(true);
        }

        AutoFindDevelopmentItems();
    }

    private void AutoFindDevelopmentItems()
    {
        Transform searchRoot = developmentContent != null
            ? developmentContent
            : developmentTab != null ? developmentTab.transform : transform;

        developmentItems = searchRoot
            .GetComponentsInChildren<DevelopmentItemUI>(true)
            .Where(item => item != null)
            .OrderBy(item => item.transform.GetSiblingIndex())
            .Take(DefaultDevelopmentOrder.Length)
            .ToArray();
    }

    private void AutoFindDevelopmentContent()
    {
        if (developmentContent != null)
            return;

        if (developmentTab == null)
            AutoFindTabReferences();

        Transform[] transforms = (developmentTab != null ? developmentTab.transform : transform)
            .GetComponentsInChildren<Transform>(true);

        developmentContent = transforms.FirstOrDefault(t => t != null && t.gameObject.name == "Content");
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
