using System;
using UnityEngine;

/// <summary>
/// 日ごとの簡易天候を管理します。
/// 現在は「晴れ / 雨」の2種類で、月ごとの確率から1日1回だけ決定します。
/// 雨の日は来客率-35%、予算-3%。家具による雨対策はFurnitureSystem側の効果と合算されます。
/// </summary>
[DefaultExecutionOrder(-9000)]
public class WeatherSystem : MonoBehaviour
{
    public const float RainVisitorPenaltyPercent = -0.35f;
    public const float RainBudgetPenaltyPercent = -0.03f;

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private FurnitureSystem furnitureSystem;
    [SerializeField] private VisitorModifierSystem visitorModifierSystem;

    [Header("今日の天候（確認用）")]
    [SerializeField] private bool isRainyToday;
    [Range(0f, 1f)] [SerializeField] private float todayRainChance;

    private int resolvedAbsoluteDay = int.MinValue;
    private bool debugRainOverrideEnabled;
    private bool debugRainValue;

    public bool IsRainyToday => isRainyToday;
    public float TodayRainChance => todayRainChance;
    public string TodayWeatherLabel => isRainyToday ? "雨" : "晴れ";

    /// <summary>
    /// 雨による予算補正。TrendSystem.GetBudgetMultiplierから加算されます。
    /// WeatherSystemが存在しない通常状態では0です。
    /// </summary>
    public static float CurrentBudgetBonusPercent { get; private set; }

    public event Action OnWeatherChanged;

    private void Awake()
    {
        ResolveReferences();
        CurrentBudgetBonusPercent = 0f;
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;

        if (furnitureSystem != null)
            furnitureSystem.OnChanged += HandleFurnitureChanged;
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;

        if (furnitureSystem != null)
            furnitureSystem.OnChanged -= HandleFurnitureChanged;
    }

    private void Start()
    {
        ResolveWeatherForCurrentDay(true);
    }

    /// <summary>
    /// DebugManagerから天候を固定します。
    /// enabled=false に戻すと、現在日を通常確率でもう一度解決します。
    /// </summary>
    public void SetDebugRainOverride(bool enabled, bool rainy)
    {
        debugRainOverrideEnabled = enabled;
        debugRainValue = rainy;
        resolvedAbsoluteDay = int.MinValue;
        ResolveWeatherForCurrentDay(true);
    }

    public void ResolveWeatherForCurrentDay(bool force = false)
    {
        ResolveReferences();
        if (shopManager == null)
            return;

        int absoluteDay = (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
        if (!force && absoluteDay == resolvedAbsoluteDay)
            return;

        resolvedAbsoluteDay = absoluteDay;
        todayRainChance = GetRainChanceForMonth(shopManager.CurrentMonth);

        bool newRainState;
        if (debugRainOverrideEnabled)
        {
            newRainState = debugRainValue;
        }
        else
        {
            // 日付固定シード。同じゲーム内日付ならシーン再読込でも天候が変わりません。
            int seed = unchecked(shopManager.GameYear * 100003 + shopManager.DayOfYear * 7919 + 2467);
            var random = new System.Random(seed);
            newRainState = random.NextDouble() < todayRainChance;
        }

        bool changed = isRainyToday != newRainState;
        isRainyToday = newRainState;
        ApplyWeatherEffects();

        Debug.Log($"本日の天候：{TodayWeatherLabel}（{shopManager.CurrentMonth}月 / 雨確率{todayRainChance * 100f:0}%）");

        if (changed)
            OnWeatherChanged?.Invoke();
    }

    public static float GetRainChanceForMonth(int month)
    {
        return month switch
        {
            1 => 0.15f,
            2 => 0.15f,
            3 => 0.20f,
            4 => 0.30f,
            5 => 0.30f,
            6 => 0.40f,
            7 => 0.40f,
            8 => 0.25f,
            9 => 0.40f,
            10 => 0.30f,
            11 => 0.20f,
            12 => 0.15f,
            _ => 0.20f
        };
    }

    private void ApplyWeatherEffects()
    {
        ResolveReferences();

        if (furnitureSystem != null)
            furnitureSystem.SetRainyToday(isRainyToday);

        float visitorPenalty = 0f;
        CurrentBudgetBonusPercent = 0f;

        if (isRainyToday)
        {
            visitorPenalty = RainVisitorPenaltyPercent;
            CurrentBudgetBonusPercent = RainBudgetPenaltyPercent;

            // 傘立て等が「雨ペナルティを-30%まで軽減」を持っている場合に反映。
            if (furnitureSystem != null)
            {
                float floor = furnitureSystem.GetRainVisitorPenaltyFloorPercent();
                if (floor < 0f)
                    visitorPenalty = Mathf.Max(visitorPenalty, floor);
            }
        }

        if (visitorModifierSystem != null)
        {
            visitorModifierSystem.RegisterOrUpdateModifier(
                "weather.rain",
                visitorPenalty,
                0,
                "雨");
        }
    }

    private void HandleShopStateChanged()
    {
        // 所持金変動等でもOnStateChangedは呼ばれるため、日付が変わった時だけ再抽選します。
        ResolveWeatherForCurrentDay(false);
    }

    private void HandleFurnitureChanged()
    {
        // 雨の日に傘立てを設置/撤去した場合、雨ペナルティ軽減を即時更新します。
        ApplyWeatherEffects();
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();
        if (visitorModifierSystem == null)
            visitorModifierSystem = FindFirstObjectByType<VisitorModifierSystem>();
    }

    [ContextMenu("DEBUG: 今日の天候をログ表示")]
    private void DebugPrintWeather()
    {
        Debug.Log(
            $"天候：{TodayWeatherLabel} / 雨確率{todayRainChance * 100f:0}% / " +
            $"来客率補正{(isRainyToday ? RainVisitorPenaltyPercent : 0f) * 100f:+0.#;-0.#;0}% / " +
            $"予算補正{CurrentBudgetBonusPercent * 100f:+0.#;-0.#;0}%");
    }
}
