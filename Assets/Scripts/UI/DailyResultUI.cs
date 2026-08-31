using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1日の営業結果表示と翌日への進行を管理します。
/// 営業終了時は主人公が吹き出しで結果を報告し、共通ボタンの「閉店する」から翌日へ進みます。
/// </summary>
public class DailyResultUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CustomerUI customerUI;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private BouquetSystem bouquetSystem;
    [SerializeField] private SupplierUI supplierUI;
    [SerializeField] private ShopTabUI shopTabUI;
    [SerializeField] private SalesVisualController salesVisualController;

    [Header("旧結果表示（任意・未使用でもOK）")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text visitorsText;
    [SerializeField] private TMP_Text buyersText;
    [SerializeField] private TMP_Text salesText;
    [SerializeField] private Button nextDayButton;

    private void Awake()
    {
        if (nextDayButton != null)
            nextDayButton.onClick.AddListener(GoToNextDay);
    }

    private void OnEnable()
    {
        if (customerUI != null)
            customerUI.OnBusinessFinished += ShowResult;

        if (salesVisualController != null)
            salesVisualController.OnCloseShopRequested += GoToNextDay;
    }

    private void OnDisable()
    {
        if (customerUI != null)
            customerUI.OnBusinessFinished -= ShowResult;

        if (salesVisualController != null)
            salesVisualController.OnCloseShopRequested -= GoToNextDay;
    }

    private void OnDestroy()
    {
        if (nextDayButton != null)
            nextDayButton.onClick.RemoveListener(GoToNextDay);
    }

    private void Start()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    public void ShowResult()
    {
        if (customerUI == null || shopManager == null) return;

        if (dateText != null)
            dateText.text = $"{shopManager.GameYear}年目 {shopManager.CurrentMonth}月{shopManager.CurrentDay}日の営業結果";

        if (visitorsText != null)
            visitorsText.text = $"来客数：{customerUI.TotalVisitors}人";

        if (buyersText != null)
            buyersText.text = $"購入者：{customerUI.PurchaseCount}人";

        if (salesText != null)
            salesText.text = $"売上：{customerUI.TotalSales:N0}円";

        if (salesVisualController != null)
        {
            salesVisualController.ShowBusinessResult(
                customerUI.TotalSales,
                customerUI.PurchaseCount,
                customerUI.TotalVisitors);
        }
        else if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 閉店時に購入者数×1%のラッピング差し入れ抽選を行ったあと、
    /// 鮮度低下 → 日付更新 → 翌日の入荷生成 の順に処理します。
    /// </summary>
    public void GoToNextDay()
    {
        if (shopManager == null || customerUI == null) return;
        if (!customerUI.HasFinishedToday) return;

        shopManager.TryGiveClosingWrappingGift(customerUI.PurchaseCount);

        if (inventorySystem != null)
            inventorySystem.AdvanceFreshnessOneDay();

        if (bouquetSystem != null)
            bouquetSystem.AdvanceFreshnessOneDay();

        shopManager.AdvanceDay();

        if (supplierUI != null)
            supplierUI.RegenerateTodayArrivals();

        customerUI.PrepareNextDay();

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (shopTabUI != null)
            shopTabUI.ShowSupplierTab();
    }
}
