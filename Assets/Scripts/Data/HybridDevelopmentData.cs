using System;
using UnityEngine;

[Serializable]
public class HybridRecipeDefinition
{
    public string hybridName;
    public string parentAName;
    public string parentBName;

    [Header("研究")]
    [Min(0)] public int researchCost = 30000;
    [Min(1)] public int researchDays = 15;

    [Header("作成")]
    [Min(1)] public int parentAQuantity = 2;
    [Min(1)] public int parentBQuantity = 2;
    [Min(0)] public int productionCost = 3000;
    [Min(1)] public int productionDays = 2;
    [Min(1)] public int productionQuantity = 5;

    [Header("完成花データ")]
    [Range(1, 10)] public int basePopularity = 5;
    [Min(1)] public int freshnessDays = 7;
    [Range(1, 10)] public int springRarity = 5;
    [Range(1, 10)] public int summerRarity = 5;
    [Range(1, 10)] public int autumnRarity = 5;
    [Range(1, 10)] public int winterRarity = 5;
    public string productCategory = "切り花";
    public bool canUseInBouquet = true;
    [Min(1)] public int sortOrder = 9999;
    [Min(1)] public int recommendedSalePrice = 1000;

    public bool Matches(FlowerData a, FlowerData b)
    {
        if (a == null || b == null)
            return false;

        string aName = a.flowerName?.Trim();
        string bName = b.flowerName?.Trim();
        return (string.Equals(aName, parentAName, StringComparison.Ordinal) &&
                string.Equals(bName, parentBName, StringComparison.Ordinal)) ||
               (string.Equals(aName, parentBName, StringComparison.Ordinal) &&
                string.Equals(bName, parentAName, StringComparison.Ordinal));
    }
}

[Serializable]
public class HybridResearchJobState
{
    public bool active;
    public FlowerData parentA;
    public FlowerData parentB;
    public string resultHybridName;
    public bool willSucceed;
    [Min(0)] public int remainingDays;
    [Min(0)] public int paidCost;

    public void Clear()
    {
        active = false;
        parentA = null;
        parentB = null;
        resultHybridName = string.Empty;
        willSucceed = false;
        remainingDays = 0;
        paidCost = 0;
    }
}

[Serializable]
public class HybridProductionJobState
{
    public bool active;
    public string hybridName;
    public FlowerData parentA;
    public FlowerData parentB;
    [Min(0)] public int remainingDays;
    [Min(0)] public int paidCost;

    public void Clear()
    {
        active = false;
        hybridName = string.Empty;
        parentA = null;
        parentB = null;
        remainingDays = 0;
        paidCost = 0;
    }
}
