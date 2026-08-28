using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 作成済み花束一覧の1行分を表示します。
/// </summary>
public class BouquetCreatedItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text compositionText;
    [SerializeField] private TMP_Text priceText;

    [Header("操作")]
    [SerializeField] private Button disassembleButton;

    private BouquetSystem.BouquetData bouquet;
    private Action<BouquetSystem.BouquetData> onDisassemble;

    private void Awake()
    {
        if (disassembleButton != null)
            disassembleButton.onClick.AddListener(Disassemble);
    }

    private void OnDestroy()
    {
        if (disassembleButton != null)
            disassembleButton.onClick.RemoveListener(Disassemble);
    }

    public void Bind(BouquetSystem.BouquetData bouquet, Action<BouquetSystem.BouquetData> onDisassemble)
    {
        this.bouquet = bouquet;
        this.onDisassemble = onDisassemble;
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

        if (compositionText != null)
            compositionText.text = $"{bouquet.DistinctFlowerCount}種類 / {bouquet.TotalQuantity}本";

        if (priceText != null)
            priceText.text = $"{bouquet.salePrice:N0}円";
    }

    private void Disassemble()
    {
        if (bouquet == null) return;
        onDisassemble?.Invoke(bouquet);
    }
}
