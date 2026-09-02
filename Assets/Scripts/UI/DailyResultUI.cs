using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1日の営業結果表示と翌日への進行を管理します。
/// 月末だけは通常の閉店処理の途中でMonthlyResultPanelを表示し、
/// 維持費支払い後に翌月へ進みます。
/// 翌日に進む時は黒幕演出で画面を覆い、その裏で日付/UIを更新します。
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
    [SerializeField] private MonthlyResultUI monthlyResultUI;
    [SerializeField] private RequestSystem requestSystem;
    [SerializeField] private DayTransitionCurtainUI dayTransitionCurtainUI;

    [Header("旧結果表示（任意・未使用でもOK）")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text visitorsText;
    [SerializeField] private TMP_Text buyersText;
    [SerializeField] private TMP_Text salesText;
    [SerializeField] private Button nextDayButton;

    private bool waitingForMonthlyResult;
    private bool isDayTransitioning;

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

        if (monthlyResultUI != null)
            monthlyResultUI.HideImmediate();
    }

    public void ShowResult()
    {
        if (customerUI == null || shopManager == null) return;

        if (dateText != null)
            dateText.text = $"{shopManager.DateDisplayText}の営業結果";

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
    /// 通常日の「閉店する」。
    /// 月末ならその日の処理を済ませたあと月間集計を表示し、日付更新は一旦止めます。
    /// </summary>
    public void GoToNextDay()
    {
        if (shopManager == null || customerUI == null) return;
        if (!customerUI.HasFinishedToday || waitingForMonthlyResult || isDayTransitioning) return;

        shopManager.RecordDailyBusinessResult(
            customerUI.TotalSales,
            customerUI.TotalVisitors,
            customerUI.PurchaseCount);

        shopManager.TryGiveClosingWrappingGift(customerUI.PurchaseCount);

        if (inventorySystem != null)
            inventorySystem.AdvanceFreshnessOneDay();

        if (bouquetSystem != null)
            bouquetSystem.AdvanceFreshnessOneDay();

        if (shopManager.IsMonthEnd)
        {
            if (monthlyResultUI == null)
            {
                Debug.LogError("DailyResultUI: 月末ですがMonthlyResultUIが設定されていません。Inspectorを確認してください。");
                return;
            }

            waitingForMonthlyResult = true;
            monthlyResultUI.ShowMonthlyResult();
            return;
        }

        BeginDayTransition();
    }

    /// <summary>
    /// MonthlyResultUIの「次の月へ」ボタンから呼ばれます。
    /// 維持費支払い後に、月末10日→翌月1日へ進めます。
    /// </summary>
    public void CompleteDayAfterMonthlyResult()
    {
        if (!waitingForMonthlyResult || isDayTransitioning) return;
        waitingForMonthlyResult = false;
        BeginDayTransition();
    }

    private void BeginDayTransition()
    {
        if (isDayTransitioning) return;

        if (dayTransitionCurtainUI == null)
        {
            // 黒幕が未設定でも従来どおり進行できるようにする。
            CompleteDayTransition();
            return;
        }

        StartCoroutine(DayTransitionRoutine());
    }

    private IEnumerator DayTransitionRoutine()
    {
        isDayTransitioning = true;

        yield return dayTransitionCurtainUI.PlayTransition(CompleteDayTransition);

        isDayTransitioning = false;
    }

    /// <summary>
    /// 黒幕が画面全体を覆っている間に呼ばれます。
    /// 日付・仕入れ・客・依頼・ホーム表示をここで翌日状態へ更新します。
    /// </summary>
    private void CompleteDayTransition()
    {
        if (shopManager == null || customerUI == null) return;

        shopManager.AdvanceDay();

        if (supplierUI != null)
            supplierUI.RegenerateTodayArrivals();

        customerUI.PrepareNextDay();

        if (requestSystem != null)
            requestSystem.ProcessNewDay();

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (shopTabUI != null)
            shopTabUI.ShowBusinessHome();
    }
}
