using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 開店から、その日の客を先客順に自動処理する営業UIです。
/// 花/花束購入後、残り予算があれば設置中のレジ横商品を最大1個だけ追加購入判定します。
/// 謎のお通げ成功日は、通常購入とは別枠で指定花を1個・777円で追加購入します。
/// 花束依頼を開店時に達成していた日は、通常客全員の退店後に依頼主が最後に来店して予約花束を受け取ります。
/// </summary>
public class CustomerUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private CustomerPurchaseSystem purchaseSystem;
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;
    [SerializeField] private RequestSystem requestSystem;
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
    [Tooltip("1人の退店演出が終わってから次のお客が入ってくるまでの待ち時間。依頼主の受取前にも使用します。")]
    [Min(0f)] [SerializeField] private float nextCustomerDelay = 1.0f;

    private readonly Queue<CustomerSystem.VisitingCustomer> waitingCustomers = new();
    private int totalVisitors;
    private int processedVisitors;
    private int purchaseCount;
    private int totalSales;
    private bool isShopOpen;
    private bool hasFinishedToday;
    private bool isProcessingCustomer;
    private bool isProcessingRequestPickup;
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
        isProcessingRequestPickup = false;

        SetShopOpen(true);

        if (resultText != null)
            resultText.text = "開店しました！";

        RefreshState();

        if (waitingCustomers.Count == 0 && (requestSystem == null || !requestSystem.HasPendingBouquetPickup))
        {
            FinishBusinessDay();
            return;
        }

        businessRoutine = StartCoroutine(ProcessAllCustomersRoutine());
    }

    private IEnumerator ProcessAllCustomersRoutine()
    {
        if (firstCustomerDelay > 0f && waitingCustomers.Count > 0)
            yield return new WaitForSeconds(firstCustomerDelay);

        while (isShopOpen && waitingCustomers.Count > 0)
        {
            yield return ProcessOneCustomerRoutine();

            if (waitingCustomers.Count > 0 && nextCustomerDelay > 0f)
                yield return new WaitForSeconds(nextCustomerDelay);
        }

        if (isShopOpen && requestSystem != null && requestSystem.HasPendingBouquetPickup)
        {
            if (nextCustomerDelay > 0f)
                yield return new WaitForSeconds(nextCustomerDelay);

            yield return ProcessRequestPickupRoutine();
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
                TryAddCheckoutPurchase(customer, result);

            result = TryAddMysteryRequestPurchase(customer, result);

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

    private IEnumerator ProcessRequestPickupRoutine()
    {
        if (requestSystem == null || !requestSystem.HasPendingBouquetPickup)
            yield break;

        isProcessingCustomer = true;
        isProcessingRequestPickup = true;

        if (!requestSystem.TryCompletePendingBouquetPickup(
                out RequestData request,
                out BouquetSystem.BouquetData bouquet,
                out int salePrice,
                out string successMessage))
        {
            isProcessingRequestPickup = false;
            isProcessingCustomer = false;
            RefreshState();
            yield break;
        }

        totalVisitors++;
        processedVisitors++;
        purchaseCount++;
        totalSales += salePrice;

        if (resultText != null)
            resultText.text = $"{request.requesterName}：{bouquet.bouquetName}を依頼品として受け取りました";

        RefreshState();

        if (salesVisualController != null)
        {
            yield return salesVisualController.PlayRequestPickupSequence(
                request,
                bouquet,
                salePrice,
                successMessage);
        }

        isProcessingRequestPickup = false;
        isProcessingCustomer = false;
        RefreshState();
    }

    private CustomerPurchaseSystem.PurchaseResult TryAddMysteryRequestPurchase(
        CustomerSystem.VisitingCustomer customer,
        CustomerPurchaseSystem.PurchaseResult result)
    {
        if (requestSystem == null || customer == null)
            return result;

        if (!requestSystem.TrySellMysteryBonusFlower(out FlowerData flower, out int price))
            return result;

        string itemText = $"{flower.flowerName}（{flower.color}）×1";

        if (result == null)
        {
            result = new CustomerPurchaseSystem.PurchaseResult
            {
                customer = customer,
                purchased = true,
                flower = flower,
                bouquet = null,
                salePrice = price,
                message = $"{customer.data?.displayName ?? "お客"}：{itemText}を購入（謎のお通げ）"
            };
        }
        else if (!result.purchased)
        {
            result.purchased = true;
            result.flower = flower;
            result.bouquet = null;
            result.salePrice = price;
            result.message = $"{customer.data?.displayName ?? "お客"}：{itemText}を購入（謎のお通げ）";
        }
        else
        {
            result.salePrice += price;
            result.message = InsertMysteryItemIntoPurchaseMessage(result.message, itemText);
        }

        Debug.Log($"謎のお通げ追加購入：{customer.data?.displayName ?? "お客"} / {itemText} / +{price:N0}円");
        return result;
    }

    private void TryAddCheckoutPurchase(CustomerSystem.VisitingCustomer customer, CustomerPurchaseSystem.PurchaseResult result)
    {
        if (checkoutItemSystem == null || customer?.data == null || result == null || !result.purchased)
            return;

        int effectiveBudget = Mathf.Max(0, customer.budget);
        int remainingBudget = Mathf.Max(0, effectiveBudget - result.salePrice);
        bool boughtBouquet = result.bouquet != null;

        CheckoutItemSystem.AddonSaleResult addon = checkoutItemSystem.TrySellAddon(boughtBouquet, remainingBudget);
        if (!addon.purchased) return;

        result.salePrice += addon.price;
        result.message = InsertCheckoutItemIntoPurchaseMessage(result.message, addon.itemName);
        Debug.Log($"レジ横追加購入：{customer.data.displayName} / {addon.itemName} / {addon.price:N0}円");
    }

    private static string InsertCheckoutItemIntoPurchaseMessage(string message, string checkoutItemName)
    {
        if (string.IsNullOrWhiteSpace(checkoutItemName))
            return message;

        string addonText = $" + {checkoutItemName}";
        if (string.IsNullOrEmpty(message))
            return addonText.TrimStart();

        int purchaseIndex = message.IndexOf("を購入", StringComparison.Ordinal);
        if (purchaseIndex >= 0)
            return message.Insert(purchaseIndex, addonText);

        return message + addonText;
    }

    private static string InsertMysteryItemIntoPurchaseMessage(string message, string itemText)
    {
        string addonText = $" + {itemText}";
        if (string.IsNullOrEmpty(message))
            return $"{itemText}を購入（謎のお通げ）";

        int purchaseIndex = message.IndexOf("を購入", StringComparison.Ordinal);
        if (purchaseIndex >= 0)
            return message.Insert(purchaseIndex, addonText);

        return message + addonText + "（謎のお通げ）";
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
        isProcessingRequestPickup = false;

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
        isProcessingRequestPickup = false;
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
            if (isProcessingRequestPickup)
            {
                currentCustomerText.text = "依頼主が受け取りに来ています";
            }
            else if (isProcessingCustomer)
            {
                currentCustomerText.text = "ただいま会計中です";
            }
            else if (waitingCustomers.Count > 0)
            {
                CustomerSystem.VisitingCustomer next = waitingCustomers.Peek();
                int budget = next != null ? Mathf.Max(0, next.budget) : 0;
                currentCustomerText.text = next?.data != null
                    ? $"次のお客：{next.data.displayName}　目的 {CustomerSystem.GetPurposeLabel(next.purpose)}　予算 {budget:N0}円"
                    : "次のお客：不明";
            }
            else if (requestSystem != null && requestSystem.HasPendingBouquetPickup)
            {
                currentCustomerText.text = "通常のお客が帰ったあと、依頼主が受け取りに来ます";
            }
            else
            {
                currentCustomerText.text = "待っているお客はいません";
            }
        }
    }
}
