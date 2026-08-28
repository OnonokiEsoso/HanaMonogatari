using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫画面で花束1つを表示します。
/// 閉じているときは材料カードを花束ヘッダーの背面に重ねて1枠で表示し、
/// クリックするとヘッダー分の空間を残したまま材料一覧を下へ展開します。
/// </summary>
public class InventoryBouquetItemUI : MonoBehaviour
{
    [Header("花束ヘッダー")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text compositionText;
    [SerializeField] private Button deleteButton;

    [Header("材料表示")]
    [SerializeField] private Transform collapsedPreviewContainer;
    [SerializeField] private Transform expandedContainer;
    [SerializeField] private InventoryItemUI flowerItemPrefab;

    [Header("重なりプレビュー")]
    [Min(1)] [SerializeField] private int maxPreviewItems = 3;
    [SerializeField] private Vector2 previewOffset = new Vector2(12f, -8f);
    [Min(0f)] [SerializeField] private float headerToItemsSpacing = 8f;

    private BouquetSystem.BouquetData bouquet;
    private BouquetSystem bouquetSystem;
    private Action onChanged;
    private bool isExpanded;
    private LayoutElement rootLayoutElement;

    private readonly List<InventoryItemUI> previewItems = new();
    private readonly List<InventoryItemUI> expandedItems = new();

    private void Awake()
    {
        rootLayoutElement = GetComponent<LayoutElement>();
        if (rootLayoutElement == null)
            rootLayoutElement = gameObject.AddComponent<LayoutElement>();

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleExpanded);

        if (deleteButton != null)
            deleteButton.onClick.AddListener(DeleteBouquet);
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleExpanded);

        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(DeleteBouquet);
    }

    public void Bind(BouquetSystem.BouquetData bouquetData, BouquetSystem system, Action onChanged)
    {
        bouquet = bouquetData;
        bouquetSystem = system;
        this.onChanged = onChanged;
        isExpanded = false;
        Refresh();
    }

    public void Refresh()
    {
        if (bouquet == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (nameText != null)
            nameText.text = bouquet.bouquetName;

        if (quantityText != null)
            quantityText.text = "×1";

        if (compositionText != null)
            compositionText.text = $"{bouquet.DistinctFlowerCount}種類 / {bouquet.TotalQuantity}本";

        PositionContainers();
        BuildPreviewItems();
        BuildExpandedItems();
        ApplyExpandedState();
    }

    /// <summary>
    /// ToggleExpanded（トグル・エクスパンデッド）
    /// Toggle＝切り替える、Expanded＝展開状態。
    /// 花束をクリックするたび、材料一覧の開閉を切り替えます。
    /// </summary>
    private void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        ApplyExpandedState();
        ForceRebuildParentLayout();
    }

    private void PositionContainers()
    {
        float headerHeight = GetHeaderHeight();
        PositionContainer(collapsedPreviewContainer, 0f);
        PositionContainer(expandedContainer, -(headerHeight + headerToItemsSpacing));
    }

    private static void PositionContainer(Transform container, float y)
    {
        if (container is not RectTransform rect) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.localScale = Vector3.one;
    }

    private void ApplyExpandedState()
    {
        if (collapsedPreviewContainer != null)
            collapsedPreviewContainer.gameObject.SetActive(!isExpanded);

        if (expandedContainer != null)
            expandedContainer.gameObject.SetActive(isExpanded);

        if (!isExpanded)
        {
            if (collapsedPreviewContainer != null)
                collapsedPreviewContainer.SetAsFirstSibling();

            if (toggleButton != null)
                toggleButton.transform.SetAsLastSibling();
        }

        UpdatePreferredHeight();
    }

    private void BuildPreviewItems()
    {
        ClearItems(previewItems);

        if (bouquet?.components == null || collapsedPreviewContainer == null || flowerItemPrefab == null)
            return;

        int count = Mathf.Min(maxPreviewItems, bouquet.components.Count);
        for (int i = 0; i < count; i++)
        {
            BouquetSystem.BouquetComponent component = bouquet.components[i];
            if (component?.flower == null || component.quantity <= 0) continue;

            InventoryItemUI item = Instantiate(flowerItemPrefab, collapsedPreviewContainer);
            item.transform.localScale = Vector3.one;

            // 閉じた状態では「カードが束になっている見た目」だけ欲しいので、
            // 花名・色・数量・鮮度などの文字はすべて非表示にする。
            item.Bind(component, false, false);

            if (item.transform is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = previewOffset * i;
            }

            item.transform.SetSiblingIndex(0);
            previewItems.Add(item);
        }
    }

    private void BuildExpandedItems()
    {
        ClearItems(expandedItems);

        if (bouquet?.components == null || expandedContainer == null || flowerItemPrefab == null)
            return;

        foreach (BouquetSystem.BouquetComponent component in bouquet.components)
        {
            if (component?.flower == null || component.quantity <= 0) continue;

            InventoryItemUI item = Instantiate(flowerItemPrefab, expandedContainer);
            item.transform.localScale = Vector3.one;
            item.Bind(component, false, true);
            expandedItems.Add(item);
        }
    }

    private void UpdatePreferredHeight()
    {
        if (rootLayoutElement == null) return;

        float headerHeight = GetHeaderHeight();

        if (!isExpanded)
        {
            rootLayoutElement.preferredHeight = headerHeight;
            return;
        }

        float itemHeight = GetFlowerItemHeight();
        int count = expandedItems.Count;
        float spacing = 0f;

        if (expandedContainer != null && expandedContainer.TryGetComponent(out VerticalLayoutGroup group))
            spacing = group.spacing;

        float contentHeight = count > 0
            ? itemHeight * count + spacing * Mathf.Max(0, count - 1)
            : 0f;

        rootLayoutElement.preferredHeight = headerHeight + headerToItemsSpacing + contentHeight;
    }

    private float GetHeaderHeight()
    {
        if (toggleButton != null && toggleButton.transform is RectTransform rect && rect.rect.height > 0f)
            return rect.rect.height;

        return 100f;
    }

    private float GetFlowerItemHeight()
    {
        if (flowerItemPrefab != null && flowerItemPrefab.transform is RectTransform rect && rect.rect.height > 0f)
            return rect.rect.height;

        return 100f;
    }

    private void ForceRebuildParentLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (transform is RectTransform selfRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(selfRect);

        if (transform.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }

    private void DeleteBouquet()
    {
        if (bouquetSystem == null || bouquet == null) return;

        if (bouquetSystem.TryDisassembleBouquet(bouquet, out string message))
        {
            Debug.Log(message);
            onChanged?.Invoke();
        }
    }

    private static void ClearItems(List<InventoryItemUI> items)
    {
        foreach (InventoryItemUI item in items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        items.Clear();
    }
}
