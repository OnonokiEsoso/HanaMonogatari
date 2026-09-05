using UnityEngine;

/// <summary>
/// ゲーム開始時に使うデバッグ設定を一か所へまとめます。
/// Debug Mode がOFFなら、下の設定はすべて無視されます。
/// </summary>
[DefaultExecutionOrder(-10000)]
public class DebugManager : MonoBehaviour
{
    [Header("デバッグ")]
    [Tooltip("ONの時だけ下のデバッグ設定をゲーム開始時に適用します。")]
    [SerializeField] private bool debugMode;

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private FurnitureSystem furnitureSystem;
    [SerializeField] private WeatherSystem weatherSystem;
    [SerializeField] private CheckoutItemSystem checkoutItemSystem;
    [SerializeField] private DevelopmentSystem developmentSystem;

    [Header("年月日を指定")]
    [SerializeField] private bool useDateOverride;
    [Min(1)] [SerializeField] private int startYear = 1;
    [Range(1, 12)] [SerializeField] private int startMonth = 4;
    [Range(1, ShopManager.DaysPerMonth)] [SerializeField] private int startDay = 1;

    [Header("初期所持金を指定")]
    [SerializeField] private bool useMoneyOverride;
    [Min(0)] [SerializeField] private int startMoney = 10000;

    [Header("初期店評価を指定")]
    [Tooltip("店評価は0～10000。10000でクリア状態になります。")]
    [SerializeField] private bool useShopRatingOverride;
    [Range(0, 10000)] [SerializeField] private int startShopRating;

    [Header("初期仕入先Lvを指定")]
    [Tooltip("仕入先Lvを直接指定します。累計仕入額による自動Lv判定よりこちらを優先します。")]
    [SerializeField] private bool useSupplierLevelOverride;
    [Range(1, 10)] [SerializeField] private int startSupplierLevel = 1;

    [Header("初期累計仕入額を指定")]
    [Tooltip("仕入先Lv条件や中盤以降のテスト用。仕入先Lv指定がOFFなら、この金額から到達可能Lvを再計算します。")]
    [SerializeField] private bool useCumulativePurchaseOverride;
    [Min(0)] [SerializeField] private int startCumulativePurchaseAmount;

    [Header("天候デバッグ")]
    [Tooltip("ONにするとゲーム開始時から天候を下の値へ固定します。日送り後も固定されたままです。")]
    [SerializeField] private bool useRainOverride;
    [SerializeField] private bool startAsRainy;

    [Header("レジ横商品デバッグ")]
    [Tooltip("ONにすると、指定したレジ横商品を仕入先Lv・通常入荷確率を無視してその日の仕入れに必ず出します。")]
    [SerializeField] private bool useCheckoutOfferOverride;
    [SerializeField] private CheckoutItemSystem.DebugForcedOffer forcedCheckoutOffer = CheckoutItemSystem.DebugForcedOffer.None;

    [Tooltip("ONにすると、1年目4月1日にキープパワーを必ず仕入れへ出します。通常テストではOFF推奨。")]
    [SerializeField] private bool forceKeepPowerOnFirstDay;

    [Tooltip("ONにすると、購入条件と残り予算を満たす客がキープパワーを100%購入します。")]
    [SerializeField] private bool forceKeepPowerPurchaseChance;

    [Header("開発デバッグ")]
    [Tooltip("ONにするとゲーム開始時に枯ラサン～枯ラサンついまで全て開発済みにします。作成と新種開発のテスト用です。")]
    [SerializeField] private bool completeAllDevelopmentsOnStart;

    [Header("開発品の初期所持数")]
    [Tooltip("ONにすると、自社開発品5種の初期在庫を下の数量だけ追加します。交配や作成テスト用です。")]
    [SerializeField] private bool useDevelopmentItemStockOverride;
    [Min(0)] [SerializeField] private int startKarasanStock;
    [Min(0)] [SerializeField] private int startSodatsuChoStock;
    [Min(0)] [SerializeField] private int startSodatsuTsubuStock;
    [Min(0)] [SerializeField] private int startSodatsuEkiStock;
    [Min(0)] [SerializeField] private int startKarasanTsuiStock;

    public bool IsDebugMode => debugMode;

    private void Awake()
    {
        if (!debugMode)
            return;

        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();

        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();

        if (weatherSystem == null)
            weatherSystem = FindFirstObjectByType<WeatherSystem>();

        if (checkoutItemSystem == null)
            checkoutItemSystem = FindFirstObjectByType<CheckoutItemSystem>();

        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();

        Debug.LogWarning("【デバッグモードを使用中】通常プレイ用の開始状態ではありません。");

        if (shopManager != null)
        {
            shopManager.ApplyDebugStartupState(
                useDateOverride,
                startYear,
                startMonth,
                startDay,
                useMoneyOverride,
                startMoney,
                useShopRatingOverride,
                startShopRating,
                useSupplierLevelOverride,
                startSupplierLevel,
                useCumulativePurchaseOverride,
                startCumulativePurchaseAmount);
        }
        else
        {
            Debug.LogWarning("DebugManager: ShopManagerが見つからないため、開始状態の上書きを適用できませんでした。");
        }

        if (useRainOverride)
        {
            if (weatherSystem != null)
            {
                weatherSystem.SetDebugRainOverride(true, startAsRainy);
            }
            else if (furnitureSystem != null)
            {
                furnitureSystem.SetRainyToday(startAsRainy);
                Debug.LogWarning("DebugManager: WeatherSystemが見つからないため、家具側の雨フラグだけを変更しました。");
            }
            else
            {
                Debug.LogWarning("DebugManager: WeatherSystem / FurnitureSystemが見つからないため、雨状態を適用できませんでした。");
            }
        }

        if (checkoutItemSystem != null)
        {
            checkoutItemSystem.ApplyDebugSettings(
                useCheckoutOfferOverride,
                forcedCheckoutOffer,
                forceKeepPowerOnFirstDay,
                forceKeepPowerPurchaseChance);

            if (useDevelopmentItemStockOverride)
            {
                AddDebugDevelopmentStock(DevelopmentSystem.KarasanItemId, startKarasanStock);
                AddDebugDevelopmentStock(DevelopmentSystem.SodatsuChoItemId, startSodatsuChoStock);
                AddDebugDevelopmentStock(DevelopmentSystem.SodatsuTsubuItemId, startSodatsuTsubuStock);
                AddDebugDevelopmentStock(DevelopmentSystem.SodatsuEkiItemId, startSodatsuEkiStock);
                AddDebugDevelopmentStock(DevelopmentSystem.KarasanTsuiItemId, startKarasanTsuiStock);
            }
        }
        else if (useCheckoutOfferOverride || forceKeepPowerOnFirstDay || forceKeepPowerPurchaseChance || useDevelopmentItemStockOverride)
        {
            Debug.LogWarning("DebugManager: CheckoutItemSystemが見つからないため、レジ横商品・開発品在庫デバッグを適用できませんでした。");
        }

        if (completeAllDevelopmentsOnStart)
        {
            if (developmentSystem != null)
            {
                developmentSystem.ApplyDebugCompleteAllDevelopments();
            }
            else
            {
                Debug.LogWarning("DebugManager: DevelopmentSystemが見つからないため、全開発完了デバッグを適用できませんでした。");
            }
        }

        PrintAppliedSettings();
    }

    private void AddDebugDevelopmentStock(string itemId, int quantity)
    {
        if (checkoutItemSystem == null || quantity <= 0)
            return;

        checkoutItemSystem.AddStock(itemId, quantity, false);
    }

    [ContextMenu("DEBUG: 現在の設定をログ表示")]
    public void PrintAppliedSettings()
    {
        if (!debugMode)
        {
            Debug.Log("DebugManager: デバッグモードはOFFです。");
            return;
        }

        string dateText = useDateOverride ? $"{startYear}年目 {startMonth}月{startDay}日" : "通常値";
        string moneyText = useMoneyOverride ? $"{startMoney:N0}円" : "通常値";
        string ratingText = useShopRatingOverride ? startShopRating.ToString("N0") : "通常値";
        string supplierText = useSupplierLevelOverride ? $"Lv.{startSupplierLevel}" : "通常値";
        string cumulativeText = useCumulativePurchaseOverride ? $"{startCumulativePurchaseAmount:N0}円" : "通常値";
        string rainText = useRainOverride ? (startAsRainy ? "雨固定" : "晴れ固定") : "通常抽選";
        string checkoutOfferText = useCheckoutOfferOverride ? forcedCheckoutOffer.ToString() : "通常抽選";
        string firstDayKeepPowerText = forceKeepPowerOnFirstDay ? "ON" : "OFF";
        string keepPowerPurchaseText = forceKeepPowerPurchaseChance ? "100%" : "通常確率";
        string developmentText = completeAllDevelopmentsOnStart ? "全開発済み" : "通常進行";
        string developmentStockText = useDevelopmentItemStockOverride
            ? $"枯ラサン:{startKarasanStock} / そだーつ長:{startSodatsuChoStock} / そだーつ粒:{startSodatsuTsubuStock} / そだーつ液:{startSodatsuEkiStock} / 枯ラサンつい:{startKarasanTsuiStock}"
            : "通常値";

        Debug.Log(
            $"DebugManager設定 / 日付:{dateText} / 所持金:{moneyText} / 店評価:{ratingText} / " +
            $"仕入先:{supplierText} / 累計仕入額:{cumulativeText} / 天候:{rainText} / " +
            $"レジ横強制入荷:{checkoutOfferText} / 初日キープパワー:{firstDayKeepPowerText} / " +
            $"キープパワー購入:{keepPowerPurchaseText} / 開発:{developmentText} / 開発品初期在庫:{developmentStockText}");
    }
}
