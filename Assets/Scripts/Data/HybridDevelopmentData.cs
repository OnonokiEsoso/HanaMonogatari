using System;
using UnityEngine;

[Serializable]
public class HybridRecipeDefinition
{
    public string hybridName;
    public string parentAName;
    public string parentBName;
    [Min(0)] public int researchCost = 30000;
    [Min(1)] public int researchDays = 15;

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
