using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FlowerData から Resources/Flowers 内の対応Spriteを自動取得します。
/// 通常花は flower_[flowerId]_[colorId].png、交配花は flower_hybrid_[id].png を使用します。
/// </summary>
public static class FlowerSpriteLoader
{
    private const string ResourcesFolder = "Flowers/";

    private static readonly Dictionary<string, string> FlowerIds = new()
    {
        { "ガーベラ", "gerbera" }, { "カスミソウ", "babys_breath" }, { "バラ", "rose" },
        { "カーネーション", "carnation" }, { "チューリップ", "tulip" }, { "パンジー", "pansy" },
        { "スイセン", "daffodil" }, { "ヒマワリ", "sunflower" }, { "パキラ", "pachira" },
        { "ユリ", "lily" }, { "スイートピー", "sweet_pea" }, { "アジサイ", "hydrangea" },
        { "モンステラ", "monstera" }, { "オジギソウ", "mimosa" }, { "コスモス", "cosmos" },
        { "シクラメン", "cyclamen" }, { "ダリア", "dahlia" }, { "レモンスライス", "lemon_slice" },
        { "ポインセチア", "poinsettia" }, { "桜（枝）", "cherry_blossom_branch" },
        { "桜(枝)", "cherry_blossom_branch" }, { "トロピカルフラワー", "tropical_flower" },
        { "ウツボカズラ", "nepenthes" }, { "花麒麟", "crown_of_thorns" },
        { "ファイヤーワークスペラルゴニウム", "firework_pelargonium" },
        { "サギソウ", "white_egret_orchid" }, { "ショクダイオオコンニャク", "titan_arum" },
        { "月下美人", "queen_of_night" }, { "青バラ", "blue_rose" }, { "黒バラ", "black_rose" }
    };

    private static readonly Dictionary<string, string> HybridResourceNames = new()
    {
        { "ガーバラ", "flower_hybrid_gerbara" },
        { "アジワリ", "flower_hybrid_ajiwari" },
        { "スイートモス", "flower_hybrid_sweet_mos" },
        { "パンスライス", "flower_hybrid_pan_slice" },
        { "紫バラ", "flower_hybrid_purple_rose" },
        { "ユリップ", "flower_hybrid_lilip" },
        { "コスミソウ", "flower_hybrid_cosmisou" },
        { "ダリネーション", "flower_hybrid_dalination" },
        { "スイーセンピー", "flower_hybrid_suisen_pea" },
        { "シクラジサイ", "flower_hybrid_cycla_ajisai" },
        { "ヒマセチア", "flower_hybrid_himasetia" },
        { "サギュリ", "flower_hybrid_sagyuri" },
        { "トロピカリア", "flower_hybrid_tropicalia" },
        { "ジギステラ", "flower_hybrid_jigistera" },
        { "ウツボキリン", "flower_hybrid_utsubo_kirin" },
        { "月下ユリ", "flower_hybrid_gekkayuri" },
        { "ファイヤーコスモス", "flower_hybrid_fire_cosmos" },
        { "スイートサクラ", "flower_hybrid_sweet_sakura" },
        { "レモンセチア", "flower_hybrid_lemonsetia" },
        { "チューラメン", "flower_hybrid_tulamen" },
        { "ガーネーション", "flower_hybrid_gernation" },
        { "カスミユリ", "flower_hybrid_kasumiyuri" },
        { "アジダリア", "flower_hybrid_ajidahlia" },
        { "スイバラ", "flower_hybrid_suibara" },
        { "ポインジー", "flower_hybrid_poinji" }
    };

    private static readonly Dictionary<string, string> ColorIds = new()
    {
        { "赤", "red" }, { "桃", "pink" }, { "ピンク", "pink" }, { "橙", "orange" },
        { "オレンジ", "orange" }, { "黄", "yellow" }, { "黄色", "yellow" }, { "白", "white" },
        { "紫", "purple" }, { "青", "blue" }, { "黒", "black" }, { "緑", "green" }, { "ミックス", "mix" }
    };

    private static readonly Dictionary<string, Sprite> Cache = new();
    private static readonly HashSet<string> MissingWarnings = new();

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
                $"（花名: {flower.flowerName} / 色: {flower.GetColorDisplayText()}）");
        }

        return sprite;
    }

    public static string GetResourceName(string flowerName, string color)
    {
        if (string.IsNullOrWhiteSpace(flowerName))
            return null;

        string normalizedName = flowerName.Trim();
        if (HybridResourceNames.TryGetValue(normalizedName, out string hybridResourceName))
            return hybridResourceName;

        if (string.IsNullOrWhiteSpace(color))
            return null;

        if (!FlowerIds.TryGetValue(normalizedName, out string flowerId))
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
