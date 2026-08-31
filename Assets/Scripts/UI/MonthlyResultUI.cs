using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 月末の簡易集計と店舗維持費の支払い画面を管理します。
/// 各月10日目の営業終了後に表示され、「次の月へ」で維持費を支払って翌月へ進みます。
/// </summary>
public class MonthlyResultUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private DailyResultUI dailyResultUI;

    [Header("集計表示")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text salesText;
    [SerializeField] private TMP_Text purchaseCostText;
    [SerializeField] private TMP_Text profitText;
    [SerializeField] private TMP_Text visitorsText;
    [SerializeField] private TMP_Text buyersText;
    [SerializeField] private TMP_Text shopRatingGainText;

    [Header("維持費表示")]
    [SerializeField] private TMP_Text maintenanceTitleText;
    [SerializeField] private TMP_Text maintenanceCostText;
    [SerializeField] private TMP_Text moneyAfterPaymentText;

    [Header("操作")]
    [SerializeField] private Button nextMonthButton;

    private bool isShowing;
    private bool paymentCompleted;

    private void Awake()
    {
        if (nextMonthButton != null)
            nextMonthButton.onClick.AddListener(GoToNextMonth);
    }

    private void Start()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (nextMonthButton != null)
            nextMonthButton.onClick.RemoveListener(GoToNextMonth);
    }

    /// <summary>
    /// 月間結果を表示します。維持費はこの時点ではまだ徴収しません。
    /// </summary>
    public void ShowMonthlyResult()
    {
        if (shopManager == null) return;

        isShowing = true;
        paymentCompleted = false;
        gameObject.SetActive(true);

        int moneyBefore = shopManager.Money;
        int maintenance = shopManager.MonthlyMaintenanceCost;
        int moneyAfter = moneyBefore - maintenance;

        if (titleText != null)
            titleText.text = $"{shopManager.CurrentMonth}月の営業結果";

        if (salesText != null)
            salesText.text = $"売上：{shopManager.MonthlySales:N0}円";

        if (purchaseCostText != null)
            purchaseCostText.text = $"仕入れ：{shopManager.MonthlyPurchaseCost:N0}円";

        if (profitText != null)
            profitText.text = $"営業利益：{shopManager.MonthlyProfit:N0}円";

        if (visitorsText != null)
            visitorsText.text = $"来客数：{shopManager.MonthlyVisitors}人";

        if (buyersText != null)
            buyersText.text = $"購入者数：{shopManager.MonthlyBuyers}人";

        if (shopRatingGainText != null)
            shopRatingGainText.text = $"店評価：+{shopManager.MonthlyShopRatingGain}";

        if (maintenanceTitleText != null)
            maintenanceTitleText.text = "月末の支払い";

        if (maintenanceCostText != null)
            maintenanceCostText.text = $"店舗維持費：-{maintenance:N0}円";

        if (moneyAfterPaymentText != null)
            moneyAfterPaymentText.text = $"所持金：{moneyBefore:N0}円 → {moneyAfter:N0}円";

        if (nextMonthButton != null)
            nextMonthButton.interactable = true;
    }

    private void GoToNextMonth()
    {
        if (!isShowing || paymentCompleted || shopManager == null) return;

        paymentCompleted = true;
        if (nextMonthButton != null)
            nextMonthButton.interactable = false;

        shopManager.PayMonthlyMaintenance();
        shopManager.ResetMonthlyStatistics();

        isShowing = false;
        gameObject.SetActive(false);

        if (dailyResultUI != null)
            dailyResultUI.CompleteDayAfterMonthlyResult();
    }
}
