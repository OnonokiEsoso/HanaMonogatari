using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 季節。
/// 商品ごとの季節別珍しさを取得するために使用します。
/// </summary>
public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

/// <summary>
/// お花屋さんで扱う商品1種類分の基本データ。
/// 通常花は従来どおり単色で扱え、交配花は複数色を同時に持てます。
/// </summary>
[CreateAssetMenu(fileName = "NewFlowerData", menuName = "HanaMonogatari/Flower Data")]
public class FlowerData : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("商品名。例：バラ、チューリップ、モンステラ")]
    public string flowerName;

    [Tooltip("従来互換用の代表色。通常花はこれまで通りここだけ設定しても動作します。")]
    public string color;

    [Tooltip("この商品が持つ色属性。交配花など複数色の商品は、赤・緑のように複数登録します。空の場合は代表色 color を自動的に色属性として扱います。")]
    public List<string> colors = new();

    [Range(1, 10)]
    [Tooltip("基本人気度。1～10")]
    public int basePopularity = 5;

    [Min(0)]
    [Tooltip("1本・1個あたりの基準仕入れ価格。仕入れを行わない交配花では0でも構いません。")]
    public int purchasePrice;

    [Min(1)]
    [Tooltip("仕入れてから寿命を迎えるまでの日数")]
    public int freshnessDays = 1;

    [Header("季節ごとの珍しさ")]
    [Range(1, 10)]
    public int springRarity = 5;

    [Range(1, 10)]
    public int summerRarity = 5;

    [Range(1, 10)]
    public int autumnRarity = 5;

    [Range(1, 10)]
    public int winterRarity = 5;

    [Header("分類・仕入れ")]
    [Tooltip("商品カテゴリ。例：切り花、観葉植物、多肉植物、野菜苗")]
    public string productCategory;

    [Tooltip("花束の材料として使用できるか")]
    public bool canUseInBouquet;

    [Range(1, 10)]
    [Tooltip("仕入先での入荷難易度。1～10。仕入れ対象外の交配花では参照しません。")]
    public int arrivalDifficulty = 1;

    [Header("表示順")]
    [Min(1)]
    [Tooltip("一覧で並べる順番。Excelの『ソート時の振り分け番号』に対応します。")]
    public int sortOrder = 9999;

    /// <summary>
    /// この商品が持つ全色属性を返します。
    /// colors が未設定の既存データでは、従来の color を1色として返します。
    /// color と colors の両方が設定されている場合は重複を除いて統合します。
    /// </summary>
    public IReadOnlyList<string> GetColors()
    {
        List<string> result = new();

        if (!string.IsNullOrWhiteSpace(color))
            result.Add(color.Trim());

        if (colors != null)
        {
            foreach (string value in colors)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string normalized = value.Trim();
                if (!result.Contains(normalized, StringComparer.Ordinal))
                    result.Add(normalized);
            }
        }

        return result;
    }

    /// <summary>
    /// 指定した色属性を持っているか返します。
    /// 交配花が「赤・緑」を持つ場合、赤でも緑でもtrueになります。
    /// </summary>
    public bool HasColor(string targetColor)
    {
        if (string.IsNullOrWhiteSpace(targetColor))
            return false;

        string normalized = targetColor.Trim();
        return GetColors().Any(value => string.Equals(value, normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// UI表示用の色文字列を返します。例：「赤・緑」。
    /// </summary>
    public string GetColorDisplayText()
    {
        IReadOnlyList<string> values = GetColors();
        return values.Count > 0 ? string.Join("・", values) : string.Empty;
    }

    /// <summary>
    /// 複数色をまとめて設定します。
    /// 先頭色は従来互換用の代表色 color にも同期します。
    /// </summary>
    public void SetColors(IEnumerable<string> newColors)
    {
        colors = newColors?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();

        color = colors.Count > 0 ? colors[0] : string.Empty;
    }

    /// <summary>
    /// 指定した季節の珍しさを返します。
    /// </summary>
    public int GetRarity(Season season)
    {
        return season switch
        {
            Season.Spring => springRarity,
            Season.Summer => summerRarity,
            Season.Autumn => autumnRarity,
            Season.Winter => winterRarity,
            _ => springRarity
        };
    }

    /// <summary>
    /// 現在季節の珍しさから、同じ入荷難易度内で使う季節重みを返します。
    /// 仕様：季節重み = 1 + (10 - 珍しさ) × 0.15
    /// </summary>
    public float GetSeasonArrivalWeight(Season season)
    {
        int rarity = GetRarity(season);
        return 1f + (10 - rarity) * 0.15f;
    }
}
