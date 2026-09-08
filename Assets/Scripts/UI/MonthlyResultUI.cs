using System.Collections;
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

    [Header("数値演出")]
    [Tooltip("各数値が0から最終値まで到達する時間です。")]
    [Min(0.1f)] [SerializeField] private float countAnimationDuration = 1.8f;

    [Header("操作")]
    [SerializeField] private Button nextMonthButton;

    private bool isShowing;
    private bool paymentCompleted;
    private Coroutine countAnimationCoroutine;

    private void Awake()
    {
        if (nextMonthButton != null)
            nextMonthButton.onClick.AddListener(GoToNextMonth);
    }

    private void OnDestroy()
    {
        if (nextMonthButton != null)
            nextMonthButton.onClick.RemoveListener(GoToNextMonth);
    }

    /// <summary>
    /// ゲーム開始時などに月間集計パネルを確実に隠します。
    /// ShowMonthlyResultで初めて表示した直後にStartが走って消えてしまう問題を避けるため、
    /// MonthlyResultUI自身のStartでは非表示にしません。
    /// </summary>
    public void HideImmediate()
    {
        if (countAnimationCoroutine != null)
        {
            StopCoroutine(countAnimationCoroutine);
            countAnimationCoroutine = null;
        }

        isShowing = false;
        paymentCompleted = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 月間結果を表示します。維持費はこの時点ではまだ徴収しません。
    /// </summary>
    public void ShowMonthlyResult()
    {
        if (shopManager == null)
        {
            Debug.LogWarning("MonthlyResultUI: ShopManagerが設定されていません。");
            return;
        }

        isShowing = true;
        paymentCompleted = false;
        gameObject.SetActive(true);

        int moneyBefore = shopManager.Money;
        int maintenance = shopManager.MonthlyMaintenanceCost;
        int moneyAfter = moneyBefore - maintenance;

        if (titleText != null)
            titleText.text = $"{shopManager.CurrentMonth}月の営業結果";

        if (maintenanceTitleText != null)
            maintenanceTitleText.text = "月末の支払い";

        if (nextMonthButton != null)
            nextMonthButton.interactable = false;

        SetAnimatedValues(0, 0, 0, 0, 0, 0, 0, 0, 0);

        if (countAnimationCoroutine != null)
            StopCoroutine(countAnimationCoroutine);

        countAnimationCoroutine = StartCoroutine(AnimateResultValues(
            shopManager.MonthlySales,
            shopManager.MonthlyPurchaseCost,
            shopManager.MonthlyProfit,
            shopManager.MonthlyVisitors,
            shopManager.MonthlyBuyers,
            shopManager.MonthlyShopRatingGain,
            maintenance,
            moneyBefore,
            moneyAfter));

        Debug.Log($"月間集計を表示しました：{shopManager.CurrentMonth}月 / 売上{shopManager.MonthlySales:N0}円");
    }

    private IEnumerator AnimateResultValues(
        int sales,
        int purchaseCost,
        int profit,
        int visitors,
        int buyers,
        int shopRatingGain,
        int maintenance,
        int moneyBefore,
        int moneyAfter)
    {
        float duration = Mathf.Max(0.1f, countAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            SetAnimatedValues(
                Mathf.RoundToInt(sales * progress),
                Mathf.RoundToInt(purchaseCost * progress),
                Mathf.RoundToInt(profit * progress),
                Mathf.RoundToInt(visitors * progress),
                Mathf.RoundToInt(buyers * progress),
                Mathf.RoundToInt(shopRatingGain * progress),
                Mathf.RoundToInt(maintenance * progress),
                Mathf.RoundToInt(moneyBefore * progress),
                Mathf.RoundToInt(moneyAfter * progress));

            yield return null;
        }

        SetAnimatedValues(
            sales,
            purchaseCost,
            profit,
            visitors,
            buyers,
            shopRatingGain,
            maintenance,
            moneyBefore,
            moneyAfter);

        countAnimationCoroutine = null;

        if (nextMonthButton != null)
            nextMonthButton.interactable = true;
    }

    private void SetAnimatedValues(
        int sales,
        int purchaseCost,
        int profit,
        int visitors,
        int buyers,
        int shopRatingGain,
        int maintenance,
        int moneyBefore,
        int moneyAfter)
    {
        if (salesText != null)
            salesText.text = $"売上：{sales:N0}円";

        if (purchaseCostText != null)
            purchaseCostText.text = $"仕入れ：{purchaseCost:N0}円";

        if (profitText != null)
            profitText.text = $"営業利益：{profit:N0}円";

        if (visitorsText != null)
            visitorsText.text = $"来客数：{visitors}人";

        if (buyersText != null)
            buyersText.text = $"購入者数：{buyers}人";

        if (shopRatingGainText != null)
            shopRatingGainText.text = $"店評価：+{shopRatingGain}";

        if (maintenanceCostText != null)
            maintenanceCostText.text = $"店舗維持費：-{maintenance:N0}円";

        if (moneyAfterPaymentText != null)
            moneyAfterPaymentText.text = $"所持金：{moneyBefore:N0}円 → {moneyAfter:N0}円";
    }

    private void GoToNextMonth()
    {
        if (!isShowing || paymentCompleted || shopManager == null) return;

        paymentCompleted = true;
        if (nextMonthButton != null)
            nextMonthButton.interactable = false;

        if (countAnimationCoroutine != null)
        {
            StopCoroutine(countAnimationCoroutine);
            countAnimationCoroutine = null;
        }

        shopManager.PayMonthlyMaintenance();
        shopManager.ResetMonthlyStatistics();

        isShowing = false;
        gameObject.SetActive(false);

        if (dailyResultUI != null)
            dailyResultUI.CompleteDayAfterMonthlyResult();
    }
}
