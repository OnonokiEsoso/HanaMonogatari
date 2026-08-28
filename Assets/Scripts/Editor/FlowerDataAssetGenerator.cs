using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 商品マスタからFlowerDataアセットを一括生成・更新するEditorツール。
/// 同名異色は別商品として、色ごとに別アセットを作成します。
/// </summary>
public static class FlowerDataAssetGenerator
{
    private const string RootFolder = "Assets/ScriptableObjects";
    private const string FlowerFolder = RootFolder + "/Flowers";
    private const string DatabasePath = RootFolder + "/FlowerDatabase.asset";

    private readonly struct FlowerMasterRow
    {
        public readonly string assetKey;
        public readonly string flowerName;
        public readonly string[] colors;
        public readonly int popularity;
        public readonly int price;
        public readonly int freshness;
        public readonly int spring;
        public readonly int summer;
        public readonly int autumn;
        public readonly int winter;
        public readonly string category;
        public readonly bool bouquet;
        public readonly int difficulty;

        public FlowerMasterRow(string assetKey, string flowerName, string[] colors, int popularity, int price,
            int freshness, int spring, int summer, int autumn, int winter, string category, bool bouquet, int difficulty)
        {
            this.assetKey = assetKey;
            this.flowerName = flowerName;
            this.colors = colors;
            this.popularity = popularity;
            this.price = price;
            this.freshness = freshness;
            this.spring = spring;
            this.summer = summer;
            this.autumn = autumn;
            this.winter = winter;
            this.category = category;
            this.bouquet = bouquet;
            this.difficulty = difficulty;
        }
    }

    [MenuItem("HanaMonogatari/Data/全FlowerDataを作成・更新")]
    public static void CreateAllFlowerData()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(FlowerFolder);

        var rows = GetMasterRows();
        var createdAssets = new List<FlowerData>();
        int sortOrder = 1;

        // Excelの「ソート時の振り分け番号」は、下のマスタ行と各colorsの並び順で1～83を振っています。
        foreach (FlowerMasterRow row in rows)
        {
            foreach (string color in row.colors)
            {
                createdAssets.Add(CreateOrUpdateFlower(row, color, sortOrder));
                sortOrder++;
            }
        }

        FlowerDatabase database = AssetDatabase.LoadAssetAtPath<FlowerDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<FlowerDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.flowers = createdAssets;
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"FlowerDataを一括生成・更新しました。商品数: {createdAssets.Count}件 / 元商品: {rows.Count}種類 / ソート番号: 1～{createdAssets.Count}");
        Selection.activeObject = database;
    }

    private static List<FlowerMasterRow> GetMasterRows()
    {
        return new List<FlowerMasterRow>
        {
            new("Gerbera", "ガーベラ", C("赤","桃","橙","黄","白"), 3, 50, 7, 2,3,2,3, "切り花", true, 1),
            new("Gypsophila", "カスミソウ", C("白","桃"), 3, 80, 9, 3,2,3,2, "切り花", true, 1),
            new("Rose", "バラ", C("赤","桃","白","黄"), 4, 80, 7, 2,2,2,2, "切り花", true, 1),
            new("Carnation", "カーネーション", C("赤","桃","白","黄"), 4, 50, 14, 2,5,5,2, "切り花", true, 1),

            new("Tulip", "チューリップ", C("赤","桃","黄","白","紫"), 3, 80, 6, 1,8,10,1, "切り花", true, 2),
            new("Pansy", "パンジー", C("黄","紫","白","青","橙"), 4, 100, 20, 2,8,2,2, "鉢花", false, 2),
            new("Daffodil", "スイセン", C("白","黄"), 5, 80, 7, 5,10,7,2, "切り花", true, 2),

            new("Sunflower", "ヒマワリ", C("黄"), 4, 100, 7, 5,1,5,8, "切り花", true, 3),
            new("Pachira", "パキラ", C("緑"), 5, 700, 30, 4,2,4,4, "観葉植物", false, 3),
            new("Lily", "ユリ", C("白","桃","黄","橙"), 5, 150, 10, 3,3,3,3, "切り花", true, 3),
            new("SweetPea", "スイートピー", C("桃","白","紫","赤"), 5, 60, 7, 1,10,8,1, "切り花", true, 3),
            new("Hydrangea", "アジサイ", C("青","紫","桃","白"), 5, 180, 7, 5,1,5,8, "切り花", true, 3),
            new("Monstera", "モンステラ", C("緑"), 6, 1000, 30, 5,3,5,5, "観葉植物", false, 3),

            new("SensitivePlant", "オジギソウ", C("緑","桃"), 3, 300, 20, 5,5,5,10, "観葉植物", false, 4),
            new("Cosmos", "コスモス", C("桃","白","赤"), 4, 70, 6, 10,8,1,10, "切り花", true, 4),
            new("Cyclamen", "シクラメン", C("赤","桃","白","紫"), 6, 800, 30, 5,10,5,2, "鉢花", false, 4),
            new("Dahlia", "ダリア", C("赤","桃","白","黄","紫"), 7, 180, 6, 3,8,3,3, "切り花", true, 4),

            new("LemonSlice", "レモンスライス", C("黄"), 5, 600, 30, 3,3,4,10, "鉢花", false, 5),
            new("Poinsettia", "ポインセチア", C("赤","白","桃"), 7, 700, 30, 10,10,6,2, "鉢花", false, 5),
            new("CherryBranch", "桜（枝）", C("桃","白"), 7, 250, 6, 3,10,10,10, "枝物", true, 5),

            new("TropicalFlower", "トロピカルフラワー", C("赤","橙","黄","桃"), 7, 250, 10, 8,3,8,8, "切り花", true, 6),
            new("PitcherPlant", "ウツボカズラ", C("緑","赤"), 8, 1200, 30, 7,7,7,8, "食虫植物", false, 6),

            new("CrownOfThorns", "花麒麟", C("赤","桃","黄","橙","白"), 6, 700, 45, 5,5,5,7, "多肉植物", false, 7),
            new("FireworkPelargonium", "ファイヤーワークスペラルゴニウム", C("赤","桃","白"), 8, 800, 30, 6,6,6,10, "鉢花", false, 7),

            new("EgretOrchid", "サギソウ", C("白"), 10, 2000, 14, 8,8,8,10, "鉢花", false, 8),

            new("TitanArum", "ショクダイオオコンニャク", C("ミックス"), 7, 30000, 2, 10,10,10,10, "希少植物", false, 10),
            new("QueenOfTheNight", "月下美人", C("白"), 10, 15000, 1, 8,8,8,9, "希少植物", false, 10),
            new("BlueRose", "青バラ", C("青"), 10, 100000, 3, 10,10,10,10, "切り花", true, 10),
            new("BlackRose", "黒バラ", C("黒"), 10, 1000000, 3, 10,10,10,10, "切り花", true, 10),
        };
    }

    private static string[] C(params string[] colors) => colors;

    private static FlowerData CreateOrUpdateFlower(FlowerMasterRow row, string color, int sortOrder)
    {
        string assetName = $"{row.assetKey}_{ColorKey(color)}";
        string assetPath = $"{FlowerFolder}/{assetName}.asset";
        FlowerData data = AssetDatabase.LoadAssetAtPath<FlowerData>(assetPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<FlowerData>();
            AssetDatabase.CreateAsset(data, assetPath);
        }

        data.flowerName = row.flowerName;
        data.color = color;
        data.basePopularity = row.popularity;
        data.purchasePrice = row.price;
        data.freshnessDays = row.freshness;
        data.springRarity = row.spring;
        data.summerRarity = row.summer;
        data.autumnRarity = row.autumn;
        data.winterRarity = row.winter;
        data.productCategory = row.category;
        data.canUseInBouquet = row.bouquet;
        data.arrivalDifficulty = row.difficulty;
        data.sortOrder = sortOrder;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static string ColorKey(string color)
    {
        return color switch
        {
            "赤" => "Red",
            "桃" => "Pink",
            "白" => "White",
            "黄" => "Yellow",
            "橙" => "Orange",
            "紫" => "Purple",
            "青" => "Blue",
            "緑" => "Green",
            "黒" => "Black",
            "ミックス" => "Mix",
            _ => color
        };
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent ?? "Assets", folderName);
    }
}
