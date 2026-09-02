using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 来客数に関わる補正を一か所に集約します。
///
/// 基本ルール：
/// ・「来客率 +○%」同士は加算してから1回だけ掛ける。
///   例：+20% / +20% / +20% → 合計+60% → ×1.60
/// ・「来客数 +○人」は倍率計算後に固定人数として加算する。
/// ・同じsourceKeyを再登録した場合は重複せず上書きする。
///
/// 現在はトレンド・依頼報酬を自動で集計し、
/// 今後は家具・天気・イベント等をRegisterOrUpdateModifierから追加できます。
/// </summary>
public class VisitorModifierSystem : MonoBehaviour
{
    [Serializable]
    public class VisitorModifier
    {
        [Tooltip("補正元を一意に識別するキー。例：furniture.welcome_mat")]
        public string sourceKey;

        [Tooltip("Inspectorやログで確認するための表示名。")]
        public string displayName;

        [Tooltip("来客率補正。+20%なら0.20、-30%なら-0.30。")]
        public float percentBonus;

        [Tooltip("倍率計算後に足す固定人数。+3人なら3、-2人なら-2。")]
        public int flatBonus;
    }

    public readonly struct VisitorModifierSummary
    {
        public readonly float trendPercent;
        public readonly float requestPercent;
        public readonly float registeredPercent;
        public readonly int registeredFlat;

        public float TotalPercent => trendPercent + requestPercent + registeredPercent;
        public float Multiplier => Mathf.Max(0f, 1f + TotalPercent);

        public VisitorModifierSummary(
            float trendPercent,
            float requestPercent,
            float registeredPercent,
            int registeredFlat)
        {
            this.trendPercent = trendPercent;
            this.requestPercent = requestPercent;
            this.registeredPercent = registeredPercent;
            this.registeredFlat = registeredFlat;
        }
    }

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private RequestSystem requestSystem;

    [Header("登録済み補正（家具・天気・イベント等）")]
    [SerializeField] private List<VisitorModifier> registeredModifiers = new();

    public IReadOnlyList<VisitorModifier> RegisteredModifiers => registeredModifiers;

    /// <summary>
    /// 現在有効な来客補正をまとめて返します。
    /// 来客率はすべて「足し算」で合算します。
    /// </summary>
    public VisitorModifierSummary GetTodaySummary()
    {
        float trendPercent = TrendSystem.GetVisitorBonusPercent(shopManager);

        float requestPercent = 0f;
        if (requestSystem != null)
            requestPercent = Mathf.Max(0f, requestSystem.GetVisitorMultiplierForToday() - 1f);

        float registeredPercent = 0f;
        int registeredFlat = 0;

        if (registeredModifiers != null)
        {
            foreach (VisitorModifier modifier in registeredModifiers)
            {
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.sourceKey))
                    continue;

                registeredPercent += modifier.percentBonus;
                registeredFlat += modifier.flatBonus;
            }
        }

        return new VisitorModifierSummary(
            trendPercent,
            requestPercent,
            registeredPercent,
            registeredFlat);
    }

    /// <summary>
    /// 基礎来客数・日ごとのランダム揺れ・各種補正を使って最終来客数を計算します。
    /// fixedBonusVisitorsは開店直後+5人など、今回だけ足したい固定人数用です。
    /// </summary>
    public int CalculateVisitorCount(int baseVisitors, float randomMultiplier, int fixedBonusVisitors = 0)
    {
        baseVisitors = Mathf.Max(0, baseVisitors);
        randomMultiplier = Mathf.Max(0f, randomMultiplier);

        VisitorModifierSummary summary = GetTodaySummary();

        float randomizedBase = baseVisitors * randomMultiplier;
        int percentageAdjusted = Mathf.RoundToInt(randomizedBase * summary.Multiplier);
        int finalVisitors = percentageAdjusted + summary.registeredFlat + fixedBonusVisitors;

        return Mathf.Max(1, finalVisitors);
    }

    /// <summary>
    /// 家具・天気・イベント等から補正を登録/更新します。
    /// 同じsourceKeyなら上書きされるため、二重加算を防げます。
    /// </summary>
    public void RegisterOrUpdateModifier(
        string sourceKey,
        float percentBonus = 0f,
        int flatBonus = 0,
        string displayName = null)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            Debug.LogWarning("VisitorModifierSystem: sourceKeyが空の補正は登録できません。");
            return;
        }

        registeredModifiers ??= new List<VisitorModifier>();

        VisitorModifier existing = registeredModifiers.FirstOrDefault(m =>
            m != null && string.Equals(m.sourceKey, sourceKey, StringComparison.Ordinal));

        if (existing == null)
        {
            registeredModifiers.Add(new VisitorModifier
            {
                sourceKey = sourceKey,
                displayName = string.IsNullOrWhiteSpace(displayName) ? sourceKey : displayName,
                percentBonus = percentBonus,
                flatBonus = flatBonus
            });
        }
        else
        {
            existing.displayName = string.IsNullOrWhiteSpace(displayName) ? existing.displayName : displayName;
            existing.percentBonus = percentBonus;
            existing.flatBonus = flatBonus;
        }
    }

    public bool RemoveModifier(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || registeredModifiers == null)
            return false;

        int removed = registeredModifiers.RemoveAll(m =>
            m != null && string.Equals(m.sourceKey, sourceKey, StringComparison.Ordinal));
        return removed > 0;
    }

    public void ClearRegisteredModifiers()
    {
        registeredModifiers?.Clear();
    }

    [ContextMenu("DEBUG: 今日の来客補正をログ表示")]
    private void DebugPrintTodayModifiers()
    {
        VisitorModifierSummary summary = GetTodaySummary();
        Debug.Log(
            $"来客補正 / トレンド:{summary.trendPercent * 100f:+0.#;-0.#;0}% " +
            $"依頼:{summary.requestPercent * 100f:+0.#;-0.#;0}% " +
            $"その他:{summary.registeredPercent * 100f:+0.#;-0.#;0}% " +
            $"固定:{summary.registeredFlat:+#;-#;0}人 " +
            $"=> 合計来客率:{summary.TotalPercent * 100f:+0.#;-0.#;0}% (×{summary.Multiplier:0.###})");
    }
}
