using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 同じ花・同じ色の在庫を1行にまとめて表示します。
/// 通常時はクリックで鮮度別ロットを展開し、展開子には同じInventoryFlowerGroupItemプレハブを使います。
/// </summary>
public class InventoryFlowerGroupItemUI : MonoBehaviour
{
    [Header("ヘッダー")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Image flowerImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text freshnessText;
    [SerializeField] private TMP_Text purchasePriceText;

    [Header("鮮度別内訳")]
    [SerializeField] private Transform expandedContainer;
    [SerializeField] private InventoryFlowerGroupItemUI lotGroupItemPrefab;
    [Min(0f)] [SerializeField] private float headerToItemsSpacing = 6f;

    private FlowerData flower;
    private List<InventorySystem.InventoryBatch> batches = new();
    private bool isExpanded;
    private bool isDetailMode;
    private LayoutElement rootLayoutElement;
    private RectTransform rootRectTransform;
    private float headerHeight = 100f;
    private readonly List<InventoryFlowerGroupItemUI> spawnedLotItems = new();

    private void Awake()
    {
        rootRectTransform = transform as RectTransform;

        rootLayoutElement = GetComponent<LayoutElement>();
        if (rootLayoutElement == null)
            rootLayoutElement = gameObject.AddComponent<LayoutElement>();

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
        isDetailMode = false;
        flower = flowerData;
        batches = sourceBatches?
            .Where(b => b != null && b.flower == flowerData && b.quantity > 0)
            .OrderBy(b => b.remainingFreshnessDays)
            .ToList() ?? new List<InventorySystem.InventoryBatch>();

        isExpanded = false;
        Refresh();
    }

    /// <summary>
    /// BindLotDetail（バインド・ロット・ディテール）
    /// 鮮度別ロットを、同じInventoryFlowerGroupItemの見た目で1行表示します。
    /// 元の花名と花画像は空欄にし、この行自身は展開しません。
    /// </summary>
    public void BindLotDetail(InventorySystem.InventoryBatch batch)
    {
        isDetailMode = true;
        isExpanded = false;
        flower = batch?.flower;
        batches = batch != null
            ? new List<InventorySystem.InventoryBatch> { batch }
            : new List<InventorySystem.InventoryBatch>();

        if (expandedContainer != null)
            expandedContainer.gameObject.SetActive(false);

        RefreshDetail(batch, hideName: true, showPurchasePrice: true);
    }

    /// <summary>
    /// BindBouquetComponent（バインド・ブーケ・コンポーネント）
    /// 花束の材料を同じInventoryFlowerGroupItemの見た目で1行表示します。
    /// 花束内では仕入価格は表示しません。
    /// </summary>
    public void BindBouquetComponent(BouquetSystem.BouquetComponent component)
    {
        isDetailMode = true;
        isExpanded = false;
        flower = component?.flower;
        batches.Clear();

        if (expandedContainer != null)
            expandedContainer.gameObject.SetActive(false);

        if (component?.flower == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        SetFlowerImage(component.flower, true);
        SetHeaderTexts(
            component.flower.flowerName,
            component.flower.color,
            component.quantity,
            component.OldestRemainingFreshnessDays,
            string.Empty);

        SetFixedDetailHeight();
    }

    public void Refresh()
    {
        if (isDetailMode)
            return;

        if (flower == null || batches.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        SetFlowerImage(flower, true);

        int totalQuantity = batches.Sum(b => b.quantity);
        int oldestFreshness = batches.Min(b => b.remainingFreshnessDays);

        SetHeaderTexts(
            flower.flowerName,
            flower.color,
            totalQuantity,
            oldestFreshness,
            $"{flower.purchasePrice:N0}円");

        PositionHeaderAtTop();
        PositionExpandedContainerBelowHeader();
        BuildLotItems();
        ApplyExpandedState();
    }

    private void RefreshDetail(InventorySystem.InventoryBatch batch, bool hideName, bool showPurchasePrice)
    {
        if (batch?.flower == null || batch.quantity <= 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        SetFlowerImage(batch.flower, !hideName);
        SetHeaderTexts(
            hideName ? string.Empty : batch.flower.flowerName,
            batch.flower.color,
            batch.quantity,
            batch.remainingFreshnessDays,
            showPurchasePrice ? $"{batch.flower.purchasePrice:N0}円" : string.Empty);

        SetFixedDetailHeight();
    }

    private void SetFlowerImage(FlowerData flowerData, bool visible)
    {
        if (flowerImage == null) return;

        Sprite sprite = visible ? FlowerSpriteLoader.GetSprite(flowerData) : null;
        flowerImage.sprite = sprite;
        flowerImage.gameObject.SetActive(visible && sprite != null);
        flowerImage.preserveAspect = true;
        flowerImage.raycastTarget = false;
    }

    private void SetHeaderTexts(string displayName, string color, int quantity, int freshnessDays, string price)
    {
        if (nameText != null)
            nameText.text = displayName;

        if (colorText != null)
            colorText.text = color;

        if (quantityText != null)
            quantityText.text = $"×{quantity}";

        if (freshnessText != null)
            freshnessText.text = $"あと{freshnessDays}日";

        if (purchasePriceText != null)
            purchasePriceText.text = price;
    }

    private void ToggleExpanded()
    {
        if (isDetailMode) return;

        isExpanded = !isExpanded;
        ApplyExpandedState();
        ForceRebuildParentLayout();
    }

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
        ClearLotItems();

        if (expandedContainer == null || lotGroupItemPrefab == null)
            return;

        foreach (InventorySystem.InventoryBatch batch in batches)
        {
            InventoryFlowerGroupItemUI item = Instantiate(lotGroupItemPrefab, expandedContainer);
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

    private void ClearLotItems()
    {
        foreach (InventoryFlowerGroupItemUI item in spawnedLotItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedLotItems.Clear();
    }

    private void ApplyExpandedState()
    {
        if (expandedContainer != null)
            expandedContainer.gameObject.SetActive(isExpanded && !isDetailMode);

        UpdatePreferredHeight();
        PositionHeaderAtTop();
        PositionExpandedContainerBelowHeader();
    }

    private void SetFixedDetailHeight()
    {
        PositionHeaderAtTop();

        if (rootLayoutElement != null)
        {
            rootLayoutElement.preferredHeight = headerHeight;
            rootLayoutElement.minHeight = headerHeight;
            rootLayoutElement.flexibleHeight = 0f;
        }

        if (rootRectTransform != null)
            rootRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerHeight);
    }

    private void UpdatePreferredHeight()
    {
        if (rootLayoutElement == null) return;

        float targetHeight = headerHeight;

        if (isExpanded && !isDetailMode)
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
        if (lotGroupItemPrefab != null)
        {
            LayoutElement layout = lotGroupItemPrefab.GetComponent<LayoutElement>();
            if (layout != null && layout.preferredHeight > 0f)
                return layout.preferredHeight;

            if (lotGroupItemPrefab.transform is RectTransform rect && rect.rect.height > 0f)
                return rect.rect.height;
        }

        return headerHeight;
    }

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
        PositionExpandedContainerBelowHeader();
        Canvas.ForceUpdateCanvases();
    }
}
