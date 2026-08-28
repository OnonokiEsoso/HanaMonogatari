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
/// 同名でも色が違う場合は、別のFlowerDataアセットとして作成します。
/// </summary>
[CreateAssetMenu(fileName = "NewFlowerData", menuName = "HanaMonogatari/Flower Data")]
public class FlowerData : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("商品名。例：バラ、チューリップ、モンステラ")]
    public string flowerName;

    [Tooltip("商品の色。例：赤、白、黄、緑")]
    public string color;

    [Range(1, 10)]
    [Tooltip("基本人気度。1～10")]
    public int basePopularity = 5;

    [Min(0)]
    [Tooltip("1本・1個あたりの基準仕入れ価格")]
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
    [Tooltip("仕入先での入荷難易度。1～10")]
    public int arrivalDifficulty = 1;

    [Header("表示順")]
    [Min(1)]
    [Tooltip("一覧で並べる順番。Excelの『ソート時の振り分け番号』に対応します。")]
    public int sortOrder = 9999;

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
