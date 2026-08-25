using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 初期FlowerDataアセットをUnity Editor上で生成するためのツール。
/// 同名異色は別商品として扱う仕様に合わせ、色ごとに別アセットを作成します。
/// </summary>
public static class FlowerDataAssetGenerator
{
    private const string RootFolder = "Assets/ScriptableObjects";
    private const string FlowerFolder = RootFolder + "/Flowers";

    [MenuItem("HanaMonogatari/Data/初期FlowerDataを作成")]
    public static void CreateInitialFlowerData()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(FlowerFolder);

        // 最新の商品マスタに基づくバラ。
        // 同名異色は別商品なので、まず4色を生成してデータ運用を確認する。
        CreateFlower(
            assetName: "Rose_Red",
            flowerName: "バラ",
            color: "赤",
            basePopularity: 4,
            purchasePrice: 80,
            freshnessDays: 7,
            springRarity: 2,
            summerRarity: 2,
            autumnRarity: 2,
            winterRarity: 2,
            productCategory: "切り花",
            canUseInBouquet: true,
            arrivalDifficulty: 1);

        CreateFlower(
            assetName: "Rose_Pink",
            flowerName: "バラ",
            color: "桃",
            basePopularity: 4,
            purchasePrice: 80,
            freshnessDays: 7,
            springRarity: 2,
            summerRarity: 2,
            autumnRarity: 2,
            winterRarity: 2,
            productCategory: "切り花",
            canUseInBouquet: true,
            arrivalDifficulty: 1);

        CreateFlower(
            assetName: "Rose_White",
            flowerName: "バラ",
            color: "白",
            basePopularity: 4,
            purchasePrice: 80,
            freshnessDays: 7,
            springRarity: 2,
            summerRarity: 2,
            autumnRarity: 2,
            winterRarity: 2,
            productCategory: "切り花",
            canUseInBouquet: true,
            arrivalDifficulty: 1);

        CreateFlower(
            assetName: "Rose_Yellow",
            flowerName: "バラ",
            color: "黄",
            basePopularity: 4,
            purchasePrice: 80,
            freshnessDays: 7,
            springRarity: 2,
            summerRarity: 2,
            autumnRarity: 2,
            winterRarity: 2,
            productCategory: "切り花",
            canUseInBouquet: true,
            arrivalDifficulty: 1);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("初期FlowerDataを作成しました: バラ（赤・桃・白・黄）");
    }

    private static void CreateFlower(
        string assetName,
        string flowerName,
        string color,
        int basePopularity,
        int purchasePrice,
        int freshnessDays,
        int springRarity,
        int summerRarity,
        int autumnRarity,
        int winterRarity,
        string productCategory,
        bool canUseInBouquet,
        int arrivalDifficulty)
    {
        string assetPath = $"{FlowerFolder}/{assetName}.asset";

        // 既に存在する場合は誤って上書きしない。
        if (AssetDatabase.LoadAssetAtPath<FlowerData>(assetPath) != null)
        {
            Debug.Log($"既に存在するためスキップ: {assetPath}");
            return;
        }

        FlowerData data = ScriptableObject.CreateInstance<FlowerData>();
        data.flowerName = flowerName;
        data.color = color;
        data.basePopularity = basePopularity;
        data.purchasePrice = purchasePrice;
        data.freshnessDays = freshnessDays;
        data.springRarity = springRarity;
        data.summerRarity = summerRarity;
        data.autumnRarity = autumnRarity;
        data.winterRarity = winterRarity;
        data.productCategory = productCategory;
        data.canUseInBouquet = canUseInBouquet;
        data.arrivalDifficulty = arrivalDifficulty;

        AssetDatabase.CreateAsset(data, assetPath);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent ?? "Assets", folderName);
    }
}
