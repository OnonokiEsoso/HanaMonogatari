using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 開店から、その日の客を先客順に自動処理する営業UIです。
/// ShopTabUIの「開店」ボタンからOpenShopを直接呼び出し、
/// SalesVisualControllerの演出を1人ずつ自動再生します。
/// このコンポーネントは営業画面の切り替えで非表示にならない常駐GameObjectに置いてください。
/// </summary>
public class CustomerUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private CustomerPurchaseSystem purchaseSystem;
    [SerializeField] private ShopTabUI shopTabUI;
    [SerializeField] private SalesVisualController salesVisualController;

    [Header("表示（任意）")]
    [SerializeField] private TMP_Text visitorCountText;
    [SerializeField] private TMP_Text currentCustomerText;
    [SerializeField] private TMP_Text resultText;

    [Header("自動営業")]
    [Tooltip("開店してから最初のお客が入ってくるまでの待ち時間。")]
    [Min(0f)] [SerializeField] private float firstCustomerDelay = 0.5f;
    [Tooltip("1人の退店演出が終わってから次のお客が入ってくるまでの待ち時間。")]
    [Min(0f)] [SerializeField] private float nextCustomerDelay = 1.0f;

    private readonly Queue<CustomerSystem.VisitingCustomer> waitingCustomers = new();
    private int totalVisitors;
    private int processedVisitors;
    private int purchaseCount;
    private int totalSales;
    private bool isShopOpen;
    private bool hasFinishedToday;
    private bool isProcessingCustomer;
    private Coroutine businessRoutine;

    public bool IsShopOpen => isShopOpen;
    public bool HasFinishedToday => hasFinishedToday;
    public int TotalVisitors => totalVisitors;
    public int ProcessedVisitors => processedVisitors;
    public int PurchaseCount => purchaseCount;
    public int TotalSales => totalSales;

    public event Action OnBusinessFinished;

    private void Start()
    {
        SetShopOpen(false);
        RefreshState();
    }

    /// <summary>
    /// OpenShop（オープン・ショップ）＝開店する。
    /// 上部の「開店」タブから直接呼ばれます。
    /// 今日の来客を生成したあと、全員を先客順に自動で処理します。
    /// </summary>
    public void OpenShop()
    {
        if (isShopOpen || hasFinishedToday || businessRoutine != null) return;
        if (customerSystem == null) return;

        customerSystem.GenerateTodayCustomers();
        waitingCustomers.Clear();

        foreach (CustomerSystem.VisitingCustomer customer in customerSystem.TodayCustomers)
            waitingCustomers.Enqueue(customer);

        totalVisitors = waitingCustomers.Count;
        processedVisitors = 0;
        purchaseCount = 0;
        totalSales = 0;

        // 先にDailyResultPanelへ切り替える。
        // CustomerUI自身は常駐GameObjectに置くため、この切り替えで非アクティブになりません。
        SetShopOpen(true);

        if (resultText != null)
            resultText.text = "開店しました！";

        RefreshState();

        if (waitingCustomers.Count == 0)
        {
            FinishBusinessDay();
            return;
        }

        businessRoutine = StartCoroutine(ProcessAllCustomersRoutine());
    }

    private IEnumerator ProcessAllCustomersRoutine()
    {
        if (firstCustomerDelay > 0f)
            yield return new WaitForSeconds(firstCustomerDelay);

        while (isShopOpen && waitingCustomers.Count > 0)
        {
            yield return ProcessOneCustomerRoutine();

            if (waitingCustomers.Count > 0 && nextCustomerDelay > 0f)
                yield return new WaitForSeconds(nextCustomerDelay);
        }

        businessRoutine = null;

        if (isShopOpen && waitingCustomers.Count == 0)
            FinishBusinessDay();
    }

    private IEnumerator ProcessOneCustomerRoutine()
    {
        if (waitingCustomers.Count == 0)
            yield break;

        isProcessingCustomer = true;

        CustomerSystem.VisitingCustomer customer = waitingCustomers.Dequeue();
        processedVisitors++;
        RefreshState();

        CustomerPurchaseSystem.PurchaseResult result = null;
        if (purchaseSystem != null)
        {
            result = purchaseSystem.TryPurchase(customer);
            if (result != null && result.purchased)
            {
                purchaseCount++;
                totalSales += result.salePrice;
            }

            if (resultText != null)
                resultText.text = result != null ? result.message : "購入処理に失敗しました";
        }

        if (salesVisualController != null)
            yield return salesVisualController.PlayCustomerSequence(customer, result);

        isProcessingCustomer = false;
        RefreshState();
    }

    /// <summary>
    /// デバッグ用。通常営業では自動処理を使用します。
    /// </summary>
    public void ProcessNextCustomer()
    {
        if (!isShopOpen || isProcessingCustomer || businessRoutine != null || waitingCustomers.Count == 0)
            return;

        StartCoroutine(ProcessOneCustomerRoutine());
    }

    private void FinishBusinessDay()
    {
        if (hasFinishedToday) return;

        SetShopOpen(false);
        hasFinishedToday = true;
        isProcessingCustomer = false;

        if (resultText != null)
            resultText.text = "本日の営業が終了しました";

        RefreshState();
        OnBusinessFinished?.Invoke();
    }

    public void PrepareNextDay()
    {
        StopAllCoroutines();
        businessRoutine = null;
        isProcessingCustomer = false;
        waitingCustomers.Clear();
        totalVisitors = 0;
        processedVisitors = 0;
        purchaseCount = 0;
        totalSales = 0;
        hasFinishedToday = false;
        SetShopOpen(false);

        if (salesVisualController != null)
            salesVisualController.HideAllCustomers();

        if (resultText != null)
            resultText.text = "開店前です";

        RefreshState();
    }

    private void SetShopOpen(bool open)
    {
        isShopOpen = open;

        if (shopTabUI != null)
            shopTabUI.SetBusinessOpen(isShopOpen);
    }

    private void RefreshState()
    {
        if (visitorCountText != null)
            visitorCountText.text = $"来客：{processedVisitors}/{totalVisitors}人　待ち {waitingCustomers.Count}人";

        if (currentCustomerText != null)
        {
            if (isProcessingCustomer)
            {
                currentCustomerText.text = "ただいま会計中です";
            }
            else if (waitingCustomers.Count > 0)
            {
                CustomerSystem.VisitingCustomer next = waitingCustomers.Peek();
                currentCustomerText.text = next?.data != null
                    ? $"次のお客：{next.data.displayName}　目的 {CustomerSystem.GetPurposeLabel(next.purpose)}　予算 {next.data.budget:N0}円"
                    : "次のお客：不明";
            }
            else
            {
                currentCustomerText.text = "待っているお客はいません";
            }
        }
    }
}
