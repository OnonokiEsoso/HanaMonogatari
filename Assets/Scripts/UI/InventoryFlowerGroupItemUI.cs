using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 同じ花・同じ色の在庫を1行にまとめて表示します。
/// 表面には合計数と最も古い鮮度を表示し、クリックで鮮度別ロットを展開します。
/// </summary>
public class InventoryFlowerGroupItemUI : MonoBehaviour
{
    [Header("ヘッダー")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text freshnessText;
    [SerializeField] private TMP_Text purchasePriceText;

    [Header("鮮度別内訳")]
    [SerializeField] private Transform expandedContainer;
    [SerializeField] private InventoryItemUI lotItemPrefab;
    [Min(0f)] [SerializeField] private float headerToItemsSpacing = 6f;

    private FlowerData flower;
    private List<InventorySystem.InventoryBatch> batches = new();
    private bool isExpanded;
    private LayoutElement rootLayoutElement;
    private RectTransform rootRectTransform;
    private float headerHeight = 100f;
    private readonly List<InventoryItemUI> spawnedLotItems = new();

    private void Awake()
    {
        rootRectTransform = transform as RectTransform;

        rootLayoutElement = GetComponent<LayoutElement>();
        if (rootLayoutElement == null)
            rootLayoutElement = gameObject.AddComponent<LayoutElement>();

        // 展開で親の高さが変わってもヘッダー自身の高さは変えない。
        if (toggleButton != null && toggleButton.transform is RectTransform headerRect && headerRect.rect.height > 0f)
            headerHeight = headerRect.rect.height;

        PositionHeaderAtTop();

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleExpanded);
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleExpanded);
    }

    /// <summary>
    /// Bind（バインド）＝同一商品の鮮度違いロットをこの1行へまとめます。
    /// </summary>
    public void Bind(FlowerData flowerData, IEnumerable<InventorySystem.InventoryBatch> sourceBatches)
    {
        flower = flowerData;
        batches = sourceBatches?
            .Where(b => b != null && b.flower == flowerData && b.quantity > 0)
            .OrderBy(b => b.remainingFreshnessDays)
            .ToList() ?? new List<InventorySystem.InventoryBatch>();

        isExpanded = false;
        Refresh();
    }

    public void Refresh()
    {
        if (flower == null || batches.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        int totalQuantity = batches.Sum(b => b.quantity);
        int oldestFreshness = batches.Min(b => b.remainingFreshnessDays);

        if (nameText != null)
            nameText.text = flower.flowerName;

        if (colorText != null)
            colorText.text = flower.color;

        if (quantityText != null)
            quantityText.text = $"×{totalQuantity}";

        if (freshnessText != null)
            freshnessText.text = $"あと{oldestFreshness}日";

        if (purchasePriceText != null)
            purchasePriceText.text = $"{flower.purchasePrice:N0}円";

        PositionHeaderAtTop();
        PositionExpandedContainerBelowHeader();
        BuildLotItems();
        ApplyExpandedState();
    }

    /// <summary>
    /// ToggleExpanded（トグル・エクスパンデッド）
    /// Toggle＝切り替える、Expanded＝展開状態。
    /// </summary>
    private void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        ApplyExpandedState();
        ForceRebuildParentLayout();
    }

    /// <summary>
    /// PositionHeaderAtTop（ポジション・ヘッダー・アット・トップ）
    /// 展開で親オブジェクトが縦に伸びても、元の花プレハブを一番上に固定します。
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

    /// <summary>
    /// PositionExpandedContainerBelowHeader（ポジション・エクスパンデッド・コンテナ・ビロウ・ヘッダー）
    /// 展開一覧を元の花カードの1個下へ配置します。
    /// </summary>
    private void PositionExpandedContainerBelowHeader()
    {
        if (expandedContainer is not RectTransform rect) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -(headerHeight + headerToItemsSpacing));
        rect.localScale = Vector3.one;
    }

    private void BuildLotItems()
    {
        foreach (InventoryItemUI item in spawnedLotItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedLotItems.Clear();

        if (expandedContainer == null || lotItemPrefab == null)
            return;

        foreach (InventorySystem.InventoryBatch batch in batches)
        {
            InventoryItemUI item = Instantiate(lotItemPrefab, expandedContainer);
            item.transform.localScale = Vector3.one;
            item.BindLotDetail(batch);
            spawnedLotItems.Add(item);
        }

        if (expandedContainer is RectTransform expandedRect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(expandedRect);
        }
    }

    private void ApplyExpandedState()
    {
        if (expandedContainer != null)
            expandedContainer.gameObject.SetActive(isExpanded);

        UpdatePreferredHeight();
        PositionHeaderAtTop();
        PositionExpandedContainerBelowHeader();
    }

    private void UpdatePreferredHeight()
    {
        if (rootLayoutElement == null) return;

        float targetHeight = headerHeight;

        if (isExpanded)
        {
            float itemHeight = GetLotItemHeight();
            float spacing = 0f;
            if (expandedContainer != null && expandedContainer.TryGetComponent(out VerticalLayoutGroup group))
                spacing = group.spacing;

            int count = spawnedLotItems.Count;
            float contentHeight = count > 0
                ? itemHeight * count + spacing * Mathf.Max(0, count - 1)
                : 0f;

            targetHeight = headerHeight + headerToItemsSpacing + contentHeight;
        }

        rootLayoutElement.preferredHeight = targetHeight;
        rootLayoutElement.minHeight = targetHeight;
        rootLayoutElement.flexibleHeight = 0f;

        if (rootRectTransform != null)
            rootRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
    }

    private float GetLotItemHeight()
    {
        if (lotItemPrefab != null && lotItemPrefab.transform is RectTransform rect && rect.rect.height > 0f)
            return rect.rect.height;
        return 100f;
    }

    /// <summary>
    /// ForceRebuildParentLayout（フォース・リビルド・ペアレント・レイアウト）
    /// Force Rebuild＝強制再計算、Parent Layout＝親の一覧レイアウト。
    /// 展開後の高さをScrollViewのContentまで即座に反映し、最下段でも下へスクロールできるようにします。
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
            // この親がInventory ScrollViewのContent。
            // 子のLayoutElementが変わった後にVerticalLayoutGroup / ContentSizeFitterを再計算する。
            LayoutRebuilder.MarkLayoutForRebuild(contentRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();

            // ContentSizeFitterの更新タイミングに依存せず、ScrollRectが正しい下端を認識できるよう
            // Content自身の高さを現在のPreferred Heightへ同期する。
            float preferredHeight = LayoutUtility.GetPreferredHeight(contentRect);
            if (preferredHeight > 0f)
                contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (contentRect.parent is RectTransform viewportRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRect);
        }

        PositionHeaderAtTop();
        PositionExpandedContainerBelowHeader();
        Canvas.ForceUpdateCanvases();
    }
}
