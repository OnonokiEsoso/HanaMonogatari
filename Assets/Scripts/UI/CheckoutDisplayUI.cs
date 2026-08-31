using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 開店パネルのレジ台に、設置中のレジ横商品を最大3種類表示します。
/// 空きスロットはImage自体を非表示にします。
/// </summary>
public class CheckoutDisplayUI : MonoBehaviour
{
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;
    [SerializeField] private Image itemImage1;
    [SerializeField] private Image itemImage2;
    [SerializeField] private Image itemImage3;

    private void OnEnable()
    {
        if (checkoutItemSystem != null)
        {
            checkoutItemSystem.OnChanged -= Refresh;
            checkoutItemSystem.OnChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (checkoutItemSystem != null)
            checkoutItemSystem.OnChanged -= Refresh;
    }

    [ContextMenu("レジ横表示を更新")]
    public void Refresh()
    {
        Image[] slots = { itemImage1, itemImage2, itemImage3 };
        IReadOnlyList<CheckoutItemSystem.CheckoutItemDefinition> installed = checkoutItemSystem != null
            ? checkoutItemSystem.GetInstalledDefinitions()
            : null;

        for (int i = 0; i < slots.Length; i++)
        {
            Image image = slots[i];
            if (image == null) continue;

            Sprite sprite = null;
            if (checkoutItemSystem != null && installed != null && i < installed.Count)
                sprite = checkoutItemSystem.LoadSprite(installed[i]);

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
