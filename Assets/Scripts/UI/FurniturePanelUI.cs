using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面の家具パネルを管理します。
/// 購入済み家具を一覧表示し、家具専用Prefabから設置/撤去を切り替えます。
/// 現在設置中の家具数と、設置家具から発動している効果合計も表示します。
/// パネルの初期表示/非表示はHierarchy側のActive状態で管理します。
/// </summary>
public class FurniturePanelUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private FurnitureSystem furnitureSystem;

    [Header("パネル")]
    [Tooltip("家具パネル全体の親オブジェクト。未設定ならこのGameObjectを使用します。")]
    [SerializeField] private GameObject furniturePanel;
    [SerializeField] private Button closeButton;

    [Header("家具一覧")]
    [Tooltip("FurnitureScrollView / Viewport / FurnitureContent を設定します。")]
    [SerializeField] private Transform furnitureListContent;
    [Tooltip("倉庫のレジ横Prefabを複製して作った FurnitureInventoryItemUI 付き家具Prefabを設定します。")]
    [SerializeField] private FurnitureInventoryItemUI furnitureItemPrefab;

    [Header("集計表示")]
    [Tooltip("設置中家具の現在効果合計を表示します。")]
    [SerializeField] private TMP_Text furnitureEffectSummaryText;
    [Tooltip("設置中数と所持数を表示します。")]
    [SerializeField] private TMP_Text furnitureCountText;

    private readonly List<FurnitureInventoryItemUI> spawnedItems = new();

    public bool IsVisible => GetPanelRoot() != null && GetPanelRoot().activeSelf;

    private void Awake()
    {
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        // ここでパネルを非表示にしない。
        // Hierarchyで非アクティブ開始の場合、初回ShowPanel()のSetActive(true)でAwakeが走り、
        // その中で再びSetActive(false)すると「最初の1回だけ開かない」ため。
    }

    private void OnEnable()
    {
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        if (furnitureSystem != null)
            furnitureSystem.OnChanged += HandleFurnitureChanged;
    }

    private void OnDisable()
    {
        if (furnitureSystem != null)
            furnitureSystem.OnChanged -= HandleFurnitureChanged;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    public void ShowPanel()
    {
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        GameObject root = GetPanelRoot();
        if (root != null)
            root.SetActive(true);

        RefreshAll();
    }

    public void HidePanel()
    {
        GameObject root = GetPanelRoot();
        if (root != null)
            root.SetActive(false);
    }

    public void TogglePanel()
    {
        if (IsVisible)
            HidePanel();
        else
            ShowPanel();
    }

    public void RefreshAll()
    {
        RebuildFurnitureList();
        RefreshSummary();
    }

    private void HandleFurnitureChanged()
    {
        if (IsVisible)
            RefreshAll();
    }

    private void RebuildFurnitureList()
    {
        foreach (FurnitureInventoryItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (furnitureSystem == null || furnitureListContent == null || furnitureItemPrefab == null)
            return;

        foreach (FurnitureData furniture in furnitureSystem.GetOwnedDefinitions()
                     .OrderBy(f => f.purchasePrice)
                     .ThenBy(f => f.displayName))
        {
            FurnitureInventoryItemUI item = Instantiate(furnitureItemPrefab, furnitureListContent);
            item.Bind(furnitureSystem, furniture);
            spawnedItems.Add(item);
        }
    }

    private void RefreshSummary()
    {
        if (furnitureSystem == null)
        {
            if (furnitureCountText != null)
                furnitureCountText.text = "家具：0個";
            if (furnitureEffectSummaryText != null)
                furnitureEffectSummaryText.text = "現在の効果：なし";
            return;
        }

        if (furnitureCountText != null)
            furnitureCountText.text = $"家具：設置中 {furnitureSystem.InstalledCount} / 所持 {furnitureSystem.OwnedCount}";

        if (furnitureEffectSummaryText != null)
        {
            float visitor = furnitureSystem.GetVisitorBonusPercentToday();
            float budget = furnitureSystem.GetBudgetBonusPercentToday();
            float rainFloor = furnitureSystem.GetRainVisitorPenaltyFloorPercent();

            List<string> effects = new();
            if (visitor != 0f)
                effects.Add($"来客率 +{visitor * 100f:0.#}%");
            if (budget != 0f)
                effects.Add($"予算 +{budget * 100f:0.#}%");
            if (rainFloor < 0f)
                effects.Add($"雨の来客率減少ペナルティを {rainFloor * 100f:0.#}% まで軽減");

            furnitureEffectSummaryText.text = effects.Count > 0
                ? "現在の効果：" + string.Join(" / ", effects)
                : "現在の効果：なし";
        }
    }

    private GameObject GetPanelRoot()
    {
        return furniturePanel != null ? furniturePanel : gameObject;
    }
}
