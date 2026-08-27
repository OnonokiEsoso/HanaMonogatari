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
    private bool isShopOpen;

    public bool IsShopOpen => isShopOpen;

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

    /// <summary>
    /// OpenShop（オープン・ショップ）＝開店する。
    /// 今日の来客を生成して、先客順の待ち行列へ入れます。
    /// 営業中はもう一度開店できません。
    /// </summary>
    public void OpenShop()
    {
        if (isShopOpen) return;
        if (customerSystem == null) return;

        customerSystem.GenerateTodayCustomers();
        waitingCustomers.Clear();

        foreach (CustomerSystem.VisitingCustomer customer in customerSystem.TodayCustomers)
            waitingCustomers.Enqueue(customer);

        totalVisitors = waitingCustomers.Count;
        processedVisitors = 0;

        SetShopOpen(true);

        if (resultText != null)
            resultText.text = "開店しました！";

        // 来客0人だった場合はその場で営業終了扱いにする。
        if (waitingCustomers.Count == 0)
            FinishBusinessDay();

        RefreshState();
    }

    /// <summary>
    /// ProcessNextCustomer（プロセス・ネクスト・カスタマー）
    /// Process＝処理する、Next Customer＝次の客。
    /// 待っている客を1人だけ処理します。
    /// </summary>
    public void ProcessNextCustomer()
    {
        if (!isShopOpen)
        {
            if (resultText != null)
                resultText.text = "まだ開店していません";
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
            if (resultText != null)
                resultText.text = result != null ? result.message : "購入処理に失敗しました";
        }

        // 最後のお客を処理したら、その日の営業を終了する。
        if (waitingCustomers.Count == 0)
            FinishBusinessDay();

        RefreshState();
    }

    /// <summary>
    /// FinishBusinessDay（フィニッシュ・ビジネス・デイ）
    /// Finish＝終える、Business Day＝営業日。
    /// その日の来客処理が終わったときの終了処理です。
    /// </summary>
    private void FinishBusinessDay()
    {
        SetShopOpen(false);

        if (resultText != null)
            resultText.text = "本日の営業が終了しました";
    }

    private void SetShopOpen(bool open)
    {
        isShopOpen = open;

        if (openShopButton != null)
            openShopButton.interactable = !isShopOpen;

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
                    ? $"次のお客：{next.data.displayName}　予算 {next.data.budget:N0}円"
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
            openShopButton.interactable = !isShopOpen;
    }
}
