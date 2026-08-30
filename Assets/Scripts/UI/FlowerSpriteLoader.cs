using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FlowerData の「花名 + 色」から、Resources/Flowers 内の対応Spriteを自動取得します。
/// 画像ファイル名は flower_[flowerId]_[colorId].png の命名規則を使用します。
/// </summary>
public static class FlowerSpriteLoader
{
    private const string ResourcesFolder = "Flowers/";

    private static readonly Dictionary<string, string> FlowerIds = new()
    {
        { "ガーベラ", "gerbera" },
        { "カスミソウ", "babys_breath" },
        { "バラ", "rose" },
        { "カーネーション", "carnation" },
        { "チューリップ", "tulip" },
        { "パンジー", "pansy" },
        { "スイセン", "daffodil" },
        { "ヒマワリ", "sunflower" },
        { "パキラ", "pachira" },
        { "ユリ", "lily" },
        { "スイートピー", "sweet_pea" },
        { "アジサイ", "hydrangea" },
        { "モンステラ", "monstera" },
        { "オジギソウ", "mimosa" },
        { "コスモス", "cosmos" },
        { "シクラメン", "cyclamen" },
        { "ダリア", "dahlia" },
        { "レモンスライス", "lemon_slice" },
        { "ポインセチア", "poinsettia" },
        { "桜（枝）", "cherry_blossom_branch" },
        { "桜(枝)", "cherry_blossom_branch" },
        { "トロピカルフラワー", "tropical_flower" },
        { "ウツボカズラ", "nepenthes" },
        { "花麒麟", "crown_of_thorns" },
        { "ファイヤーワークスペラルゴニウム", "firework_pelargonium" },
        { "サギソウ", "white_egret_orchid" },
        { "ショクダイオオコンニャク", "titan_arum" },
        { "月下美人", "queen_of_night" },
        { "青バラ", "blue_rose" },
        { "黒バラ", "black_rose" }
    };

    private static readonly Dictionary<string, string> ColorIds = new()
    {
        { "赤", "red" },
        { "桃", "pink" },
        { "ピンク", "pink" },
        { "橙", "orange" },
        { "オレンジ", "orange" },
        { "黄", "yellow" },
        { "黄色", "yellow" },
        { "白", "white" },
        { "紫", "purple" },
        { "青", "blue" },
        { "黒", "black" },
        { "緑", "green" },
        { "ミックス", "mix" }
    };

    private static readonly Dictionary<string, Sprite> Cache = new();
    private static readonly HashSet<string> MissingWarnings = new();

    /// <summary>
    /// GetSprite（ゲット・スプライト）
    /// 花名と色から対応するSpriteを取得します。
    /// 見つからない場合は null を返します。
    /// </summary>
    public static Sprite GetSprite(FlowerData flower)
    {
        if (flower == null)
            return null;

        string resourceName = GetResourceName(flower.flowerName, flower.color);
        if (string.IsNullOrEmpty(resourceName))
            return null;

        if (Cache.TryGetValue(resourceName, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(ResourcesFolder + resourceName);
        Cache[resourceName] = sprite;

        if (sprite == null && MissingWarnings.Add(resourceName))
        {
            Debug.LogWarning(
                $"FlowerSpriteLoader: 花画像が見つかりません。Assets/Resources/Flowers/{resourceName}.png を確認してください。" +
                $"（花名: {flower.flowerName} / 色: {flower.color}）");
        }

        return sprite;
    }

    /// <summary>
    /// 花名・色から Resources 上のファイル名（拡張子なし）を生成します。
    /// </summary>
    public static string GetResourceName(string flowerName, string color)
    {
        if (string.IsNullOrWhiteSpace(flowerName) || string.IsNullOrWhiteSpace(color))
            return null;

        if (!FlowerIds.TryGetValue(flowerName.Trim(), out string flowerId))
        {
            Debug.LogWarning($"FlowerSpriteLoader: 未登録の花名です: {flowerName}");
            return null;
        }

        if (!ColorIds.TryGetValue(color.Trim(), out string colorId))
        {
            Debug.LogWarning($"FlowerSpriteLoader: 未登録の色です: {color}");
            return null;
        }

        return $"flower_{flowerId}_{colorId}";
    }
}
