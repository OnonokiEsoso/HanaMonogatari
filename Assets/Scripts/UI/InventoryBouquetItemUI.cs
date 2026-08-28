using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫画面で花束1つを表示します。
/// 閉じているときは材料カードを花束ヘッダーの背面に重ねて1枠で表示し、
/// クリックするとInventoryFlowerGroupItemプレハブで材料一覧を下へ展開します。
/// 展開時の高さ計算・ScrollView Content更新は通常花グループと同じ方式を使います。
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
    [SerializeField] private InventoryItemUI previewFlowerItemPrefab;
    [SerializeField] private InventoryFlowerGroupItemUI expandedFlowerGroupItemPrefab;

    [Header("重なりプレビュー")]
    [Min(1)] [SerializeField] private int maxPreviewItems = 3;
    [SerializeField] private Vector2 previewOffset = new Vector2(12f, -8f);
    [Min(0f)] [SerializeField] private float headerToItemsSpacing = 8f;

    private BouquetSystem.BouquetData bouquet;
    private BouquetSystem bouquetSystem;
    private Action onChanged;
    private bool isExpanded;
    private LayoutElement rootLayoutElement;
    private RectTransform rootRectTransform;
    private float headerHeight = 100f;

    private readonly List<InventoryItemUI> previewItems = new();
    private readonly List<InventoryFlowerGroupItemUI> expandedItems = new();

    private void Awake()
    {
        rootRectTransform = transform as RectTransform;

        rootLayoutElement = GetComponent<LayoutElement>();
        if (rootLayoutElement == null)
            rootLayoutElement = gameObject.AddComponent<LayoutElement>();

        // 通常花グループと同じく、展開で親が伸びてもヘッダー自身の高さは固定する。
        if (toggleButton != null && toggleButton.transform is RectTransform headerRect && headerRect.rect.height > 0f)
            headerHeight = headerRect.rect.height;

        PositionHeaderAtTop();

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

        PositionHeaderAtTop();
        PositionContainers();
        BuildPreviewItems();
        BuildExpandedItems();
        ApplyExpandedState();
    }

    /// <summary>
    /// ToggleExpanded（トグル・エクスパンデッド）
    /// 通常花グループと同じ流れで、開閉 → 高さ更新 → ScrollView Content再計算を行います。
    /// </summary>
    private void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        ApplyExpandedState();
        ForceRebuildParentLayout();
    }

    /// <summary>
    /// PositionHeaderAtTop（ポジション・ヘッダー・アット・トップ）
    /// 親の高さが展開分だけ伸びても、花束ヘッダーを先頭1行に固定します。
    /// </summary>
    private void PositionHeaderAtTop()
    {
        if (toggleButton == null || toggleButton.transform is not RectTransform rect) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerHeight);
        rect.localScale = Vector3.one;
    }

    private void PositionContainers()
    {
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

        // 描画・クリック順を維持する。
        if (collapsedPreviewContainer != null)
            collapsedPreviewContainer.SetAsFirstSibling();

        if (toggleButton != null)
            toggleButton.transform.SetAsLastSibling();

        if (deleteButton != null)
            deleteButton.transform.SetAsLastSibling();

        UpdatePreferredHeight();
        PositionHeaderAtTop();
        PositionContainers();
    }

    private void BuildPreviewItems()
    {
        ClearPreviewItems();

        if (bouquet?.components == null || collapsedPreviewContainer == null || previewFlowerItemPrefab == null)
            return;

        int count = Mathf.Min(maxPreviewItems, bouquet.components.Count);
        for (int i = 0; i < count; i++)
        {
            BouquetSystem.BouquetComponent component = bouquet.components[i];
            if (component?.flower == null || component.quantity <= 0) continue;

            InventoryItemUI item = Instantiate(previewFlowerItemPrefab, collapsedPreviewContainer);
            item.transform.localScale = Vector3.one;
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
        ClearExpandedItems();

        if (bouquet?.components == null || expandedContainer == null || expandedFlowerGroupItemPrefab == null)
            return;

        foreach (BouquetSystem.BouquetComponent component in bouquet.components)
        {
            if (component?.flower == null || component.quantity <= 0) continue;

            InventoryFlowerGroupItemUI item = Instantiate(expandedFlowerGroupItemPrefab, expandedContainer);
            item.transform.localScale = Vector3.one;
            item.BindBouquetComponent(component);
            expandedItems.Add(item);
        }

        if (expandedContainer is RectTransform expandedRect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(expandedRect);
        }
    }

    /// <summary>
    /// UpdatePreferredHeight（アップデート・プリファード・ハイト）
    /// 通常花グループと同じく、LayoutElementとRectTransform本体の両方へ展開後の高さを反映します。
    /// </summary>
    private void UpdatePreferredHeight()
    {
        if (rootLayoutElement == null) return;

        float targetHeight = headerHeight;

        if (isExpanded)
        {
            float itemHeight = GetExpandedItemHeight();
            float spacing = 0f;

            if (expandedContainer != null && expandedContainer.TryGetComponent(out VerticalLayoutGroup group))
                spacing = group.spacing;

            int count = expandedItems.Count;
            float contentHeight = count > 0
                ? itemHeight * count + spacing * Mathf.Max(0, count - 1)
                : 0f;

            targetHeight = headerHeight + headerToItemsSpacing + contentHeight;
        }

        rootLayoutElement.preferredHeight = targetHeight;
        rootLayoutElement.minHeight = targetHeight;
        rootLayoutElement.flexibleHeight = 0f;

        // ここが通常花と揃える重要部分。
        // LayoutElementだけでなく実RectTransformも伸ばし、ScrollRectが下端を認識できるようにする。
        if (rootRectTransform != null)
            rootRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
    }

    private float GetExpandedItemHeight()
    {
        if (expandedFlowerGroupItemPrefab != null)
        {
            LayoutElement layout = expandedFlowerGroupItemPrefab.GetComponent<LayoutElement>();
            if (layout != null && layout.preferredHeight > 0f)
                return layout.preferredHeight;

            if (expandedFlowerGroupItemPrefab.transform is RectTransform rect && rect.rect.height > 0f)
                return rect.rect.height;
        }

        return headerHeight;
    }

    /// <summary>
    /// ForceRebuildParentLayout（フォース・リビルド・ペアレント・レイアウト）
    /// 通常花グループと同じ方法で、展開後の高さをScrollViewのContentまで伝えます。
    /// </summary>
    private void ForceRebuildParentLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (expandedContainer is RectTransform expandedRect && expandedContainer.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(expandedRect);

        if (rootRectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRectTransform);

        if (transform.parent is RectTransform contentRect)
        {
            LayoutRebuilder.MarkLayoutForRebuild(contentRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();

            float preferredHeight = LayoutUtility.GetPreferredHeight(contentRect);
            if (preferredHeight > 0f)
                contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (contentRect.parent is RectTransform viewportRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRect);
        }

        PositionHeaderAtTop();
        PositionContainers();
        Canvas.ForceUpdateCanvases();
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

    private void ClearPreviewItems()
    {
        foreach (InventoryItemUI item in previewItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        previewItems.Clear();
    }

    private void ClearExpandedItems()
    {
        foreach (InventoryFlowerGroupItemUI item in expandedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        expandedItems.Clear();
    }
}
