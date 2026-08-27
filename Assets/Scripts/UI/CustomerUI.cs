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
        RefreshState();
    }

    /// <summary>
    /// OpenShop（オープン・ショップ）＝開店する。
    /// 今日の来客を生成して、先客順の待ち行列へ入れます。
    /// </summary>
    public void OpenShop()
    {
        if (customerSystem == null) return;

        customerSystem.GenerateTodayCustomers();
        waitingCustomers.Clear();

        foreach (CustomerSystem.VisitingCustomer customer in customerSystem.TodayCustomers)
            waitingCustomers.Enqueue(customer);

        totalVisitors = waitingCustomers.Count;
        processedVisitors = 0;

        if (resultText != null)
            resultText.text = "開店しました！";

        RefreshState();
    }

    /// <summary>
    /// ProcessNextCustomer（プロセス・ネクスト・カスタマー）
    /// Process＝処理する、Next Customer＝次の客。
    /// 待っている客を1人だけ処理します。
    /// </summary>
    public void ProcessNextCustomer()
    {
        if (waitingCustomers.Count == 0)
        {
            if (resultText != null)
                resultText.text = totalVisitors > 0 ? "本日の営業が終了しました" : "まだ開店していません";
            RefreshState();
            return;
        }

        CustomerSystem.VisitingCustomer customer = waitingCustomers.Dequeue();
        processedVisitors++;

        if (purchaseSystem != null)
        {
            CustomerPurchaseSystem.PurchaseResult result = purchaseSystem.TryPurchase(customer);
            if (resultText != null)
                resultText.text = result != null ? result.message : "購入処理に失敗しました";
        }

        RefreshState();
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
                    ? $"次のお客：{next.data.displayName}　予算 {next.data.budget:N0}円"
                    : "次のお客：不明";
            }
            else
            {
                currentCustomerText.text = "待っているお客はいません";
            }
        }

        if (nextCustomerButton != null)
            nextCustomerButton.interactable = waitingCustomers.Count > 0;
    }
}
