using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HybridTab のUIを管理します。
/// A/Bの花選択、所持花一覧の自動生成、画像/名前反映、交配開始条件判定まで担当します。
/// </summary>
public class HybridTabUI : MonoBehaviour
{
    private enum SelectionSide
    {
        A,
        B
    }

    [Header("参照")]
    [SerializeField] private HybridDevelopmentSystem hybridDevelopmentSystem;
    [SerializeField] private DevelopmentSystem developmentSystem;
    [SerializeField] private InventorySystem inventorySystem;

    [Header("親花A")]
    [SerializeField] private Image flowerAImage;
    [SerializeField] private TMP_Text flowerANameText;
    [SerializeField] private Button selectFlowerAButton;

    [Header("親花B")]
    [SerializeField] private Image flowerBImage;
    [SerializeField] private TMP_Text flowerBNameText;
    [SerializeField] private Button selectFlowerBButton;

    [Header("交配情報")]
    [Tooltip("必要素材と現在の状態をまとめて表示します。")]
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text daysText;
    [Tooltip("旧UI互換用。RequirementTextへ統合したため、見つかった場合は非表示にします。")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button startHybridButton;

    [Header("花選択パネル")]
    [SerializeField] private GameObject flowerSelectPanel;
    [SerializeField] private Transform flowerSelectContent;
    [SerializeField] private FlowerSelectItemUI flowerSelectItemPrefab;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text selectionTitleText;

    private FlowerData selectedFlowerA;
    private FlowerData selectedFlowerB;
    private SelectionSide currentSelectionSide;

    private void Awake()
    {
        ResolveReferences();
        AutoFindReferences();

        if (selectFlowerAButton != null)
            selectFlowerAButton.onClick.AddListener(HandleSelectA);
        if (selectFlowerBButton != null)
            selectFlowerBButton.onClick.AddListener(HandleSelectB);
        if (startHybridButton != null)
            startHybridButton.onClick.AddListener(HandleStartHybrid);
        if (backButton != null)
            backButton.onClick.AddListener(HideFlowerSelection);

        if (flowerSelectPanel != null)
            flowerSelectPanel.SetActive(false);

        if (stateText != null)
            stateText.gameObject.SetActive(false);

        Refresh();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (hybridDevelopmentSystem != null)
        {
            hybridDevelopmentSystem.OnChanged += Refresh;
            hybridDevelopmentSystem.OnResearchCompleted += HandleResearchCompleted;
        }

        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged += HandleInventoryChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (hybridDevelopmentSystem != null)
        {
            hybridDevelopmentSystem.OnChanged -= Refresh;
            hybridDevelopmentSystem.OnResearchCompleted -= HandleResearchCompleted;
        }

        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void OnDestroy()
    {
        if (selectFlowerAButton != null)
            selectFlowerAButton.onClick.RemoveListener(HandleSelectA);
        if (selectFlowerBButton != null)
            selectFlowerBButton.onClick.RemoveListener(HandleSelectB);
        if (startHybridButton != null)
            startHybridButton.onClick.RemoveListener(HandleStartHybrid);
        if (backButton != null)
            backButton.onClick.RemoveListener(HideFlowerSelection);
    }

    public void Refresh()
    {
        ResolveReferences();
        RefreshSelectedFlowerDisplay();

        bool active = hybridDevelopmentSystem != null && hybridDevelopmentSystem.HasActiveJob;
        bool canStart = false;
        string reason = "新種開発システムが見つかりません";
        if (hybridDevelopmentSystem != null)
            canStart = hybridDevelopmentSystem.CanStartHybrid(selectedFlowerA, selectedFlowerB, out reason);

        if (requirementText != null)
        {
            string aName = selectedFlowerA != null ? selectedFlowerA.flowerName : "花A";
            string bName = selectedFlowerB != null ? selectedFlowerB.flowerName : "花B";

            string status;
            if (active)
                status = $"新種開発中　残り{hybridDevelopmentSystem.GetRemainingDays()}日";
            else if (!string.IsNullOrWhiteSpace(hybridDevelopmentSystem?.LastResultMessage))
                status = hybridDevelopmentSystem.LastResultMessage;
            else
                status = canStart ? "交配可能" : reason;

            requirementText.text =
                $"必要：\n{aName} ×1\n{bName} ×1\n枯ラサンつい ×1\n\n{status}";
        }

        if (costText != null)
            costText.text = $"研究費：{HybridDevelopmentSystem.DefaultResearchCost:N0}円";

        if (daysText != null)
            daysText.text = active
                ? $"期間：{hybridDevelopmentSystem.GetRemainingDays()}日"
                : "期間未定";

        if (stateText != null && stateText.gameObject.activeSelf)
            stateText.gameObject.SetActive(false);

        if (startHybridButton != null)
            startHybridButton.interactable = canStart;

        bool canChangeSelection = !active && developmentSystem != null && !developmentSystem.HasAnyActiveWork;
        if (selectFlowerAButton != null)
            selectFlowerAButton.interactable = canChangeSelection;
        if (selectFlowerBButton != null)
            selectFlowerBButton.interactable = canChangeSelection;
    }

    private void HandleSelectA()
    {
        ShowFlowerSelection(SelectionSide.A);
    }

    private void HandleSelectB()
    {
        ShowFlowerSelection(SelectionSide.B);
    }

    private void ShowFlowerSelection(SelectionSide side)
    {
        currentSelectionSide = side;
        if (selectionTitleText != null)
            selectionTitleText.text = side == SelectionSide.A ? "花Aを選択" : "花Bを選択";

        PopulateFlowerSelection();
        if (flowerSelectPanel != null)
            flowerSelectPanel.SetActive(true);
    }

    public void HideFlowerSelection()
    {
        if (flowerSelectPanel != null)
            flowerSelectPanel.SetActive(false);
    }

    private void PopulateFlowerSelection()
    {
        AutoFindReferences();

        if (flowerSelectContent == null || flowerSelectItemPrefab == null || inventorySystem == null)
        {
            Debug.LogWarning("HybridTabUI: FlowerSelectContent / FlowerSelectItemPrefab / InventorySystem のいずれかが未設定です。");
            return;
        }

        foreach (DevelopmentItemUI stray in flowerSelectContent.GetComponentsInChildren<DevelopmentItemUI>(true))
        {
            if (stray != null)
                Destroy(stray.gameObject);
        }

        foreach (FlowerSelectItemUI existing in flowerSelectContent.GetComponentsInChildren<FlowerSelectItemUI>(true))
        {
            if (existing != null)
                Destroy(existing.gameObject);
        }

        var flowers = inventorySystem.Batches
            .Where(batch => batch != null && batch.flower != null && batch.quantity > 0)
            .GroupBy(batch => batch.flower)
            .Select(group => new
            {
                Flower = group.Key,
                Quantity = group.Sum(x => x.quantity)
            })
            .OrderBy(x => x.Flower.sortOrder)
            .ThenBy(x => x.Flower.flowerName)
            .ThenBy(x => x.Flower.GetColorDisplayText())
            .ToList();

        FlowerData otherSide = currentSelectionSide == SelectionSide.A ? selectedFlowerB : selectedFlowerA;

        foreach (var entry in flowers)
        {
            bool sameAsOther = otherSide != null &&
                (ReferenceEquals(entry.Flower, otherSide) ||
                 string.Equals(entry.Flower.flowerName, otherSide.flowerName, StringComparison.Ordinal));

            FlowerSelectItemUI item = Instantiate(flowerSelectItemPrefab, flowerSelectContent);
            item.gameObject.name = $"FlowerSelectItem_{entry.Flower.flowerName}_{entry.Flower.GetColorDisplayText()}";
            item.gameObject.SetActive(true);
            item.Bind(entry.Flower, entry.Quantity, !sameAsOther, HandleFlowerSelected);
        }
    }

    private void HandleFlowerSelected(FlowerData flower)
    {
        if (flower == null)
            return;

        if (currentSelectionSide == SelectionSide.A)
            selectedFlowerA = flower;
        else
            selectedFlowerB = flower;

        HideFlowerSelection();
        Refresh();
    }

    private void HandleStartHybrid()
    {
        if (hybridDevelopmentSystem == null)
            return;

        if (hybridDevelopmentSystem.TryStartHybrid(selectedFlowerA, selectedFlowerB))
            Refresh();
    }

    private void HandleResearchCompleted(string message)
    {
        Refresh();
    }

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    private void RefreshSelectedFlowerDisplay()
    {
        SetFlowerDisplay(flowerAImage, flowerANameText, selectedFlowerA, "未選択");
        SetFlowerDisplay(flowerBImage, flowerBNameText, selectedFlowerB, "未選択");
    }

    private static void SetFlowerDisplay(Image image, TMP_Text nameText, FlowerData flower, string emptyText)
    {
        if (nameText != null)
            nameText.text = flower != null ? flower.flowerName : emptyText;

        if (image == null)
            return;

        Sprite sprite = FlowerSpriteLoader.GetSprite(flower);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.enabled = sprite != null;
    }

    private void ResolveReferences()
    {
        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (hybridDevelopmentSystem == null)
            hybridDevelopmentSystem = FindFirstObjectByType<HybridDevelopmentSystem>();

        if (hybridDevelopmentSystem == null && developmentSystem != null)
            hybridDevelopmentSystem = developmentSystem.gameObject.AddComponent<HybridDevelopmentSystem>();
    }

    private void AutoFindReferences()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        Image[] images = GetComponentsInChildren<Image>(true);
        Button[] buttons = GetComponentsInChildren<Button>(true);

        if (flowerAImage == null)
            flowerAImage = images.FirstOrDefault(x => x.gameObject.name == "FlowerAImage");
        if (flowerBImage == null)
            flowerBImage = images.FirstOrDefault(x => x.gameObject.name == "FlowerBImage");
        if (flowerANameText == null)
            flowerANameText = texts.FirstOrDefault(x => x.gameObject.name == "FlowerANameText");
        if (flowerBNameText == null)
            flowerBNameText = texts.FirstOrDefault(x => x.gameObject.name == "FlowerBNameText");
        if (requirementText == null)
            requirementText = texts.FirstOrDefault(x => x.gameObject.name == "RequirementText");
        if (costText == null)
            costText = texts.FirstOrDefault(x => x.gameObject.name == "CostText");
        if (daysText == null)
            daysText = texts.FirstOrDefault(x => x.gameObject.name == "DaysText");
        if (stateText == null)
            stateText = texts.FirstOrDefault(x => x.gameObject.name == "StateText");
        if (selectionTitleText == null)
            selectionTitleText = texts.FirstOrDefault(x => x.gameObject.name == "TitleText" && x.transform.IsChildOf(flowerSelectPanel != null ? flowerSelectPanel.transform : transform));

        if (selectFlowerAButton == null)
            selectFlowerAButton = buttons.FirstOrDefault(x => x.gameObject.name == "SelectFlowerAButton");
        if (selectFlowerBButton == null)
            selectFlowerBButton = buttons.FirstOrDefault(x => x.gameObject.name == "SelectFlowerBButton");
        if (startHybridButton == null)
            startHybridButton = buttons.FirstOrDefault(x => x.gameObject.name == "StartHybridButton");
        if (backButton == null)
            backButton = buttons.FirstOrDefault(x => x.gameObject.name == "BackButton");

        if (flowerSelectPanel == null)
            flowerSelectPanel = transforms.FirstOrDefault(x => x.gameObject.name == "FlowerSelectPanel")?.gameObject;

        if (flowerSelectPanel != null &&
            (flowerSelectContent == null || !flowerSelectContent.IsChildOf(flowerSelectPanel.transform)))
        {
            flowerSelectContent = flowerSelectPanel.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.gameObject.name == "Content");
        }
    }
}
