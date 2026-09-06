using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DevelopmentPanel の「開発・作成・交配」3タブの選択状態を色で分かりやすくします。
/// DevelopmentPanelUI のタブ切替処理そのものには触れず、ボタンの見た目だけを担当します。
/// </summary>
public class DevelopmentTabColorUI : MonoBehaviour
{
    private enum TabType
    {
        Development,
        Production,
        Hybrid
    }

    [Header("タブボタン")]
    [SerializeField] private Button developmentTabButton;
    [SerializeField] private Button productionTabButton;
    [SerializeField] private Button hybridTabButton;

    [Header("背景色")]
    [Tooltip("現在選択中のタブの色。")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.82f, 0.48f, 1f);
    [Tooltip("選択されていない2つのタブの色。")]
    [SerializeField] private Color unselectedColor = new Color(0.78f, 0.78f, 0.78f, 1f);

    [Header("文字色（任意）")]
    [SerializeField] private bool changeTextColor = true;
    [SerializeField] private Color selectedTextColor = Color.black;
    [SerializeField] private Color unselectedTextColor = new Color(0.28f, 0.28f, 0.28f, 1f);

    [Header("初期選択")]
    [Tooltip("DevelopmentPanelUI の初期タブは開発なので、通常は Development のままでOKです。")]
    [SerializeField] private TabType initialTab = TabType.Development;

    private TabType currentTab;

    private void Awake()
    {
        AutoFindButtons();

        if (developmentTabButton != null)
            developmentTabButton.onClick.AddListener(SelectDevelopmentTab);
        if (productionTabButton != null)
            productionTabButton.onClick.AddListener(SelectProductionTab);
        if (hybridTabButton != null)
            hybridTabButton.onClick.AddListener(SelectHybridTab);

        currentTab = initialTab;
        ApplyColors();
    }

    private void OnEnable()
    {
        ApplyColors();
    }

    private void OnDestroy()
    {
        if (developmentTabButton != null)
            developmentTabButton.onClick.RemoveListener(SelectDevelopmentTab);
        if (productionTabButton != null)
            productionTabButton.onClick.RemoveListener(SelectProductionTab);
        if (hybridTabButton != null)
            hybridTabButton.onClick.RemoveListener(SelectHybridTab);
    }

    public void SelectDevelopmentTab()
    {
        currentTab = TabType.Development;
        ApplyColors();
    }

    public void SelectProductionTab()
    {
        currentTab = TabType.Production;
        ApplyColors();
    }

    public void SelectHybridTab()
    {
        currentTab = TabType.Hybrid;
        ApplyColors();
    }

    private void ApplyColors()
    {
        ApplyButtonStyle(developmentTabButton, currentTab == TabType.Development);
        ApplyButtonStyle(productionTabButton, currentTab == TabType.Production);
        ApplyButtonStyle(hybridTabButton, currentTab == TabType.Hybrid);
    }

    private void ApplyButtonStyle(Button button, bool selected)
    {
        if (button == null) return;

        Graphic target = button.targetGraphic;
        if (target != null)
            target.color = selected ? selectedColor : unselectedColor;

        if (!changeTextColor) return;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
            if (text != null)
                text.color = selected ? selectedTextColor : unselectedTextColor;
    }

    private void AutoFindButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        if (developmentTabButton == null)
            developmentTabButton = buttons.FirstOrDefault(b => b != null && b.gameObject.name == "DevelopmentTabButton");
        if (productionTabButton == null)
            productionTabButton = buttons.FirstOrDefault(b => b != null && b.gameObject.name == "ProductionTabButton");
        if (hybridTabButton == null)
            hybridTabButton = buttons.FirstOrDefault(b => b != null && b.gameObject.name == "HybridTabButton");
    }
}
