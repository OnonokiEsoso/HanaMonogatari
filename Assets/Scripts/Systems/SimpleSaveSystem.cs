using System;
using UnityEngine;

/// <summary>
/// 「つづきから」用の簡易セーブです。
/// ゲームの主要進行だけを保存し、在庫・花束・家具・開発・交配・依頼・チャレンジなどの細かな状態は保存しません。
/// ShopManagerの状態変化時に自動保存します。
/// </summary>
public class SimpleSaveSystem : MonoBehaviour
{
    private const string SaveKey = "HanaMonogatari.SimpleSave.v1";

    [Serializable]
    private class SimpleSaveData
    {
        public int gameYear;
        public int month;
        public int day;
        public int money;
        public int shopRating;
        public int supplierLevel;
        public int cumulativePurchaseAmount;
    }

    [SerializeField] private ShopManager shopManager;

    public static bool HasSaveData => PlayerPrefs.HasKey(SaveKey) && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(SaveKey, string.Empty));

    private void Awake()
    {
        ResolveShopManager();
    }

    private void OnEnable()
    {
        ResolveShopManager();
        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;
    }

    public void SaveNow()
    {
        ResolveShopManager();
        if (shopManager == null)
        {
            Debug.LogWarning("SimpleSaveSystem: ShopManagerが見つからないため保存できません。");
            return;
        }

        SimpleSaveData data = new()
        {
            gameYear = Mathf.Max(1, shopManager.GameYear),
            month = Mathf.Clamp(shopManager.CurrentMonth, 1, 12),
            day = Mathf.Clamp(shopManager.CurrentDay, 1, ShopManager.DaysPerMonth),
            money = Mathf.Max(0, shopManager.Money),
            shopRating = Mathf.Clamp(shopManager.ShopRating, 0, 10000),
            supplierLevel = Mathf.Clamp(shopManager.SupplierLevel, 1, 10),
            cumulativePurchaseAmount = Mathf.Max(0, shopManager.CumulativePurchaseAmount)
        };

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public bool TryLoad()
    {
        ResolveShopManager();
        if (shopManager == null || !HasSaveData)
            return false;

        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        SimpleSaveData data;
        try
        {
            data = JsonUtility.FromJson<SimpleSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SimpleSaveSystem: セーブデータを読み込めませんでした。{exception.Message}");
            return false;
        }

        if (data == null)
            return false;

        shopManager.ApplyDebugStartupState(
            overrideDate: true,
            debugYear: Mathf.Max(1, data.gameYear),
            debugMonth: Mathf.Clamp(data.month, 1, 12),
            debugDay: Mathf.Clamp(data.day, 1, ShopManager.DaysPerMonth),
            overrideMoney: true,
            debugMoney: Mathf.Max(0, data.money),
            overrideShopRating: true,
            debugShopRating: Mathf.Clamp(data.shopRating, 0, 10000),
            overrideSupplierLevel: true,
            debugSupplierLevel: Mathf.Clamp(data.supplierLevel, 1, 10),
            overrideCumulativePurchaseAmount: true,
            debugCumulativePurchaseAmount: Mathf.Max(0, data.cumulativePurchaseAmount));

        Debug.Log($"簡易セーブを読み込みました：{shopManager.DateDisplayText} / 所持金{shopManager.Money:N0}円 / 店評価{shopManager.ShopRating:N0}");
        return true;
    }

    public static void DeleteSave()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    private void HandleShopStateChanged()
    {
        SaveNow();
    }

    private void ResolveShopManager()
    {
        if (shopManager == null)
            shopManager = GetComponent<ShopManager>();
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
    }
}
