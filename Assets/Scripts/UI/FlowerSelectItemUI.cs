using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新種開発の花選択一覧で使う1件分のUIです。
/// FlowerImage / FlowerNameText / ColorText / QuantityText / SelectButton を子に持つプレハブを想定します。
/// </summary>
public class FlowerSelectItemUI : MonoBehaviour
{
    [SerializeField] private Image flowerImage;
    [SerializeField] private TMP_Text flowerNameText;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Button selectButton;

    private FlowerData flower;
    private Action<FlowerData> onSelected;

    private void Awake()
    {
        AutoFindReferences();
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(FlowerData targetFlower, int quantity, bool selectable, Action<FlowerData> selectedCallback)
    {
        flower = targetFlower;
        onSelected = selectedCallback;

        if (flowerNameText != null)
            flowerNameText.text = flower != null ? flower.flowerName : "---";

        if (colorText != null)
            colorText.text = flower != null ? flower.GetColorDisplayText() : string.Empty;

        if (quantityText != null)
            quantityText.text = $"所持：{Mathf.Max(0, quantity)}";

        if (selectButton != null)
            selectButton.interactable = selectable && flower != null && quantity > 0;

        if (flowerImage != null)
        {
            Sprite sprite = FlowerSpriteLoader.GetSprite(flower);
            flowerImage.sprite = sprite;
            flowerImage.preserveAspect = true;
            flowerImage.enabled = sprite != null;
        }
    }

    private void HandleClicked()
    {
        if (flower != null)
            onSelected?.Invoke(flower);
    }

    private void AutoFindReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        Image[] images = GetComponentsInChildren<Image>(true);
        Button[] buttons = GetComponentsInChildren<Button>(true);

        if (flowerImage == null)
            flowerImage = images.FirstOrDefault(x => x.gameObject.name == "FlowerImage");
        if (flowerNameText == null)
            flowerNameText = texts.FirstOrDefault(x => x.gameObject.name == "FlowerNameText");
        if (colorText == null)
            colorText = texts.FirstOrDefault(x => x.gameObject.name == "ColorText");
        if (quantityText == null)
            quantityText = texts.FirstOrDefault(x => x.gameObject.name == "QuantityText");
        if (selectButton == null)
            selectButton = buttons.FirstOrDefault(x => x.gameObject.name == "SelectButton");
    }
}
