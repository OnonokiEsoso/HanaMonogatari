using System;
using UnityEngine;

public enum DevelopmentId
{
    Karasan,
    SodatsuCho,
    SodatsuTsubu,
    SodatsuEki,
    KarasanTsui
}

public enum DevelopmentJobType
{
    None,
    Development,
    Production
}

[Serializable]
public class DevelopmentDefinition
{
    public DevelopmentId id;
    public string displayName;

    [Header("開発")]
    [Min(0)] public int developmentCost;
    [Min(1)] public int developmentDays = 1;
    [Min(0)] public int requiredShopRating;
    public DevelopmentId[] prerequisiteDevelopments = Array.Empty<DevelopmentId>();

    [Tooltip("開発材料として必要なレジ横商品ID。空なら不要。")]
    public string requiredCheckoutItemId;
    [Min(0)] public int requiredCheckoutItemQuantity;

    [Tooltip("2種類目のレジ横商品材料。枯ラサンつい等で使用。")]
    public string requiredCheckoutItemId2;
    [Min(0)] public int requiredCheckoutItemQuantity2;

    [Tooltip("花を1つ消費する開発か。")]
    public bool requiresFlower;
    [Range(1, 10)] public int minimumFlowerArrivalDifficulty = 1;

    [Header("作成")]
    [Tooltip("開発後に作成した時、レジ横在庫へ追加する商品ID。")]
    public string producedCheckoutItemId;
    [Min(1)] public int productionQuantity = 1;
    [Min(0)] public int productionCost;
    [Min(1)] public int productionDays = 1;
}

[Serializable]
public class DevelopmentProgressState
{
    public DevelopmentId id;
    public bool completed;
}

[Serializable]
public class DevelopmentJobState
{
    public DevelopmentJobType jobType = DevelopmentJobType.None;
    public DevelopmentId targetId;
    [Min(0)] public int remainingDays;

    public bool IsActive => jobType != DevelopmentJobType.None && remainingDays > 0;

    public void Clear()
    {
        jobType = DevelopmentJobType.None;
        remainingDays = 0;
    }
}
