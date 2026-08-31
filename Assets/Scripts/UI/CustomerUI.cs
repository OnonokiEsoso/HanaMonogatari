using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 開店から、その日の客を先客順に自動処理する営業UIです。
/// 花/花束購入後、残り予算があれば設置中のレジ横商品を最大1個だけ追加購入判定します。
/// </summary>
public class CustomerUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private CustomerPurchaseSystem purchaseSystem;
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;
    [SerializeField] private ShopManager shopManager;
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
                TryAddCheckoutPurchase(customer, result);
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

    private void TryAddCheckoutPurchase(CustomerSystem.VisitingCustomer customer, CustomerPurchaseSystem.PurchaseResult result)
    {
        if (checkoutItemSystem == null || customer?.data == null || result == null || !result.purchased)
            return;

        float budgetMultiplier = TrendSystem.GetBudgetMultiplier(shopManager);
        int effectiveBudget = Mathf.Max(0, Mathf.RoundToInt(customer.data.budget * budgetMultiplier));
        int remainingBudget = Mathf.Max(0, effectiveBudget - result.salePrice);
        bool boughtBouquet = result.bouquet != null;

        CheckoutItemSystem.AddonSaleResult addon = checkoutItemSystem.TrySellAddon(boughtBouquet, remainingBudget);
        if (!addon.purchased) return;

        result.salePrice += addon.price;
        result.message += $"　＋{addon.itemName} ×1（{addon.price:N0}円）";
        Debug.Log($"レジ横追加購入：{customer.data.displayName} / {addon.itemName} / {addon.price:N0}円");
    }

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
                int budget = next?.data != null
                    ? Mathf.RoundToInt(next.data.budget * TrendSystem.GetBudgetMultiplier(shopManager))
                    : 0;
                currentCustomerText.text = next?.data != null
                    ? $"次のお客：{next.data.displayName}　目的 {CustomerSystem.GetPurposeLabel(next.purpose)}　予算 {budget:N0}円"
                    : "次のお客：不明";
            }
            else
            {
                currentCustomerText.text = "待っているお客はいません";
            }
        }
    }
}
