using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在庫画面で花束1つを表示します。
/// 閉じているときは材料カードを重ねて表示し、クリックで下へ展開します。
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

    private BouquetSystem.BouquetData bouquet;
    private BouquetSystem bouquetSystem;
    private Action onChanged;
    private bool isExpanded;

    private readonly List<InventoryItemUI> previewItems = new();
    private readonly List<InventoryItemUI> expandedItems = new();

    private void Awake()
    {
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

    /// <summary>
    /// Bind（バインド）＝花束データをこの表示へ結び付ける。
    /// </summary>
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
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    private void ApplyExpandedState()
    {
        if (collapsedPreviewContainer != null)
            collapsedPreviewContainer.gameObject.SetActive(!isExpanded);

        if (expandedContainer != null)
            expandedContainer.gameObject.SetActive(isExpanded);
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
            item.Bind(component, false);

            if (item.transform is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = previewOffset * i;
            }

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
            item.Bind(component, false);
            expandedItems.Add(item);
        }
    }

    private void DeleteBouquet()
    {
        if (bouquetSystem == null || bouquet == null) return;

        // 「削除」は材料を失わないよう、花束を解体して在庫へ戻す処理にします。
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
