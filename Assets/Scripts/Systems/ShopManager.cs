using System;
using UnityEngine;

/// <summary>
/// お店全体の進行状態を管理するクラス。
/// 所持金・店評価・日付・累計仕入額・仕入先Lvをまとめて保持します。
/// </summary>
public class ShopManager : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private SupplierSystem supplierSystem;

    [Header("お店の状態")]
    [Min(0)] [SerializeField] private int money = 10000;
    [Range(0, 10000)] [SerializeField] private int shopRating = 0;
    [Min(0)] [SerializeField] private int cumulativePurchaseAmount = 0;

    [Header("日付")]
    [Min(1)] [SerializeField] private int gameYear = 1;
    [Range(1, 365)] [SerializeField] private int dayOfYear = 1;

    [Header("仕入先")]
    [Range(1, 10)] [SerializeField] private int supplierLevel = 1;

    [Header("クリア状態")]
    [SerializeField] private bool hasCleared = false;

    public int Money => money;
    public int ShopRating => shopRating;
    public int CumulativePurchaseAmount => cumulativePurchaseAmount;
    public int SupplierLevel => supplierLevel;
    public int GameYear => gameYear;
    public int DayOfYear => dayOfYear;
    public bool HasCleared => hasCleared;

    public int CurrentMonth => GetMonthAndDay(dayOfYear).month;
    public int CurrentDay => GetMonthAndDay(dayOfYear).day;
    public Season CurrentSeason => GetSeason(CurrentMonth);

    public event Action OnStateChanged;

    private void Awake()
    {
        RecalculateSupplierLevel();
        SyncSupplierSystem();
    }

    /// <summary>
    /// 仕入れなどでお金を支払います。
    /// 所持金が足りない場合はfalseを返します。
    /// </summary>
    public bool TrySpendMoney(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("ShopManager: 支払額に負の値は使用できません。");
            return false;
        }

        if (money < amount)
        {
            return false;
        }

        money -= amount;
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// 売上などで所持金を増やします。
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        money += amount;
        NotifyStateChanged();
    }

    /// <summary>
    /// 仕入先から商品を購入したときに呼びます。
    /// 支払いと累計仕入額の加算を同時に行います。
    /// </summary>
    public bool TryPurchaseFromSupplier(int totalPrice)
    {
        if (totalPrice <= 0) return false;
        if (money < totalPrice) return false;

        money -= totalPrice;
        cumulativePurchaseAmount += totalPrice;

        RecalculateSupplierLevel();
        SyncSupplierSystem();
        NotifyStateChanged();

        return true;
    }

    /// <summary>
    /// 客の満足などによって店評価を増やします。
    /// 一度でも10000に到達したらクリア状態を保持します。
    /// </summary>
    public void AddShopRating(int amount)
    {
        if (amount <= 0) return;

        shopRating = Mathf.Clamp(shopRating + amount, 0, 10000);

        if (!hasCleared && shopRating >= 10000)
        {
            hasCleared = true;
            Debug.Log("ゲームクリア！ 街で一番人気のお花屋さんになりました！");
        }

        RecalculateSupplierLevel();
        SyncSupplierSystem();
        NotifyStateChanged();
    }

    /// <summary>
    /// 必要になった場合の評価減少用。
    /// クリア済みフラグは評価が下がっても解除されません。
    /// </summary>
    public void RemoveShopRating(int amount)
    {
        if (amount <= 0) return;
        shopRating = Mathf.Clamp(shopRating - amount, 0, 10000);

        RecalculateSupplierLevel();
        SyncSupplierSystem();
        NotifyStateChanged();
    }

    /// <summary>
    /// 1日進めます。365日を超えると翌年1月1日になります。
    /// </summary>
    [ContextMenu("翌日へ進む")]
    public void AdvanceDay()
    {
        dayOfYear++;

        if (dayOfYear > 365)
        {
            dayOfYear = 1;
            gameYear++;
        }

        SyncSupplierSystem();
        NotifyStateChanged();

        Debug.Log($"{gameYear}年目 {CurrentMonth}月{CurrentDay}日 / {CurrentSeason}");
    }

    /// <summary>
    /// 累計仕入額と店評価から仕入先Lvを再計算します。
    /// </summary>
    public void RecalculateSupplierLevel()
    {
        int newLevel = 1;

        if (cumulativePurchaseAmount >= 1000) newLevel = 2;
        if (cumulativePurchaseAmount >= 3000) newLevel = 3;
        if (cumulativePurchaseAmount >= 5000) newLevel = 4;

        if (cumulativePurchaseAmount >= 8000 && shopRating >= 500) newLevel = 5;
        if (cumulativePurchaseAmount >= 10000 && shopRating >= 1000) newLevel = 6;
        if (cumulativePurchaseAmount >= 20000 && shopRating >= 2000) newLevel = 7;
        if (cumulativePurchaseAmount >= 30000 && shopRating >= 4000) newLevel = 8;
        if (cumulativePurchaseAmount >= 40000 && shopRating >= 6500) newLevel = 9;
        if (cumulativePurchaseAmount >= 50000 && shopRating >= 8500) newLevel = 10;

        if (newLevel != supplierLevel)
        {
            supplierLevel = newLevel;
            Debug.Log($"仕入先Lvが{supplierLevel}になりました！");
        }
    }

    /// <summary>
    /// ShopManagerの現在状態をSupplierSystemへ反映します。
    /// </summary>
    public void SyncSupplierSystem()
    {
        if (supplierSystem == null) return;

        supplierSystem.SetSupplierLevel(supplierLevel);
        supplierSystem.SetSeason(CurrentSeason);
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    private static Season GetSeason(int month)
    {
        return month switch
        {
            3 or 4 or 5 => Season.Spring,
            6 or 7 or 8 => Season.Summer,
            9 or 10 or 11 => Season.Autumn,
            _ => Season.Winter
        };
    }

    private static (int month, int day) GetMonthAndDay(int day)
    {
        int[] monthLengths = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        int remaining = Mathf.Clamp(day, 1, 365);

        for (int month = 0; month < monthLengths.Length; month++)
        {
            if (remaining <= monthLengths[month])
            {
                return (month + 1, remaining);
            }

            remaining -= monthLengths[month];
        }

        return (12, 31);
    }
}
