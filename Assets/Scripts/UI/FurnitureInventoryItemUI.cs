using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面の家具一覧で、購入済み家具1件を表示します。
/// 倉庫のレジ横商品Prefabを複製した家具専用Prefabへ付けて使用します。
/// </summary>
public class FurnitureInventoryItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text categoryText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text installedStateText;

    [Header("家具では使わない表示（任意）")]
    [Tooltip("倉庫Prefabから残した詳細展開部分。家具一覧では不要なら設定すると自動で非表示にします。")]
    [SerializeField] private GameObject expandedContainer;

    [Header("操作")]
    [SerializeField] private Button installButton;
    [SerializeField] private TMP_Text installButtonText;

    private FurnitureSystem furnitureSystem;
    private FurnitureData furniture;

    private void Awake()
    {
        if (installButton != null)
            installButton.onClick.AddListener(ToggleInstalled);

        if (expandedContainer != null)
            expandedContainer.SetActive(false);
    }

    private void OnDestroy()
    {
        if (installButton != null)
            installButton.onClick.RemoveListener(ToggleInstalled);
    }

    public void Bind(FurnitureSystem system, FurnitureData definition)
    {
        furnitureSystem = system;
        furniture = definition;
        Refresh();
    }

    public void Refresh()
    {
        if (furnitureSystem == null || furniture == null)
            return;

        bool installed = furnitureSystem.IsInstalled(furniture.id);

        if (itemImage != null)
        {
            Sprite sprite = furnitureSystem.LoadSprite(furniture);
            itemImage.sprite = sprite;
            itemImage.enabled = sprite != null;
            itemImage.preserveAspect = true;
            itemImage.raycastTarget = false;
        }

        if (nameText != null)
            nameText.text = furniture.displayName;

        if (categoryText != null)
            categoryText.text = "家具";

        if (quantityText != null)
            quantityText.text = "所持中";

        if (installedStateText != null)
            installedStateText.text = installed ? "設置中" : "未設置";

        if (installButtonText != null)
            installButtonText.text = installed ? "撤去" : "設置";

        if (installButton != null)
            installButton.interactable = true;

        if (expandedContainer != null)
            expandedContainer.SetActive(false);
    }

    private void ToggleInstalled()
    {
        if (furnitureSystem == null || furniture == null)
            return;

        if (furnitureSystem.IsInstalled(furniture.id))
            furnitureSystem.Uninstall(furniture.id);
        else
            furnitureSystem.TryInstall(furniture.id);

        Refresh();
    }
}
