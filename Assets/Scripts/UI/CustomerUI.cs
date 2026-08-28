using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 開店から、その日の客を先客順に処理する簡易UIです。
/// </summary>
public class CustomerUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private CustomerPurchaseSystem purchaseSystem;
    [SerializeField] private ShopTabUI shopTabUI;

    [Header("ボタン")]
    [SerializeField] private Button openShopButton;
    [SerializeField] private Button nextCustomerButton;

    [Header("表示")]
    [SerializeField] private TMP_Text visitorCountText;
    [SerializeField] private TMP_Text currentCustomerText;
    [SerializeField] private TMP_Text resultText;

    private readonly Queue<CustomerSystem.VisitingCustomer> waitingCustomers = new();
    private int totalVisitors;
    private int processedVisitors;
    private int purchaseCount;
    private int totalSales;
    private bool isShopOpen;
    private bool hasFinishedToday;

    public bool IsShopOpen => isShopOpen;
    public bool HasFinishedToday => hasFinishedToday;
    public int TotalVisitors => totalVisitors;
    public int ProcessedVisitors => processedVisitors;
    public int PurchaseCount => purchaseCount;
    public int TotalSales => totalSales;

    public event Action OnBusinessFinished;

    private void Awake()
    {
        if (openShopButton != null)
            openShopButton.onClick.AddListener(OpenShop);

        if (nextCustomerButton != null)
            nextCustomerButton.onClick.AddListener(ProcessNextCustomer);
    }

    private void OnDestroy()
    {
        if (openShopButton != null)
            openShopButton.onClick.RemoveListener(OpenShop);

        if (nextCustomerButton != null)
            nextCustomerButton.onClick.RemoveListener(ProcessNextCustomer);
    }

    private void Start()
    {
        SetShopOpen(false);
        RefreshState();
    }

    public void OpenShop()
    {
        if (isShopOpen || hasFinishedToday) return;
        if (customerSystem == null) return;

        customerSystem.GenerateTodayCustomers();
        waitingCustomers.Clear();

        foreach (CustomerSystem.VisitingCustomer customer in customerSystem.TodayCustomers)
            waitingCustomers.Enqueue(customer);

        totalVisitors = waitingCustomers.Count;
        processedVisitors = 0;
        purchaseCount = 0;
        totalSales = 0;

        SetShopOpen(true);

        if (resultText != null)
            resultText.text = "開店しました！";

        if (waitingCustomers.Count == 0)
            FinishBusinessDay();

        RefreshState();
    }

    public void ProcessNextCustomer()
    {
        if (!isShopOpen)
        {
            if (resultText != null)
                resultText.text = hasFinishedToday ? "本日の営業は終了しています" : "まだ開店していません";
            RefreshState();
            return;
        }

        if (waitingCustomers.Count == 0)
        {
            FinishBusinessDay();
            RefreshState();
            return;
        }

        CustomerSystem.VisitingCustomer customer = waitingCustomers.Dequeue();
        processedVisitors++;

        if (purchaseSystem != null)
        {
            CustomerPurchaseSystem.PurchaseResult result = purchaseSystem.TryPurchase(customer);
            if (result != null && result.purchased)
            {
                purchaseCount++;
                totalSales += result.salePrice;
            }

            if (resultText != null)
                resultText.text = result != null ? result.message : "購入処理に失敗しました";
        }

        if (waitingCustomers.Count == 0)
            FinishBusinessDay();

        RefreshState();
    }

    private void FinishBusinessDay()
    {
        if (hasFinishedToday) return;

        SetShopOpen(false);
        hasFinishedToday = true;

        if (resultText != null)
            resultText.text = "本日の営業が終了しました";

        RefreshState();
        OnBusinessFinished?.Invoke();
    }

    public void PrepareNextDay()
    {
        waitingCustomers.Clear();
        totalVisitors = 0;
        processedVisitors = 0;
        purchaseCount = 0;
        totalSales = 0;
        hasFinishedToday = false;
        SetShopOpen(false);

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
            if (waitingCustomers.Count > 0)
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

        if (nextCustomerButton != null)
            nextCustomerButton.interactable = isShopOpen && waitingCustomers.Count > 0;

        if (openShopButton != null)
            openShopButton.interactable = !isShopOpen && !hasFinishedToday;
    }
}
