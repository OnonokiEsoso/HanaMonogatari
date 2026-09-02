using System;

public enum FurnitureId
{
    WelcomeMat,
    UmbrellaStand,
    UmbrellaBagMachine,
    Sanitizer,
    InsectKiller,
    OpenCloseSign,
    LightA,
    LightB,
    LightC,
    PendulumClock,
    NewtonsCradle,
    DrinkingBird
}

/// <summary>
/// 家具1種類ぶんの定義データです。
/// パーセント値は +3% = 0.03f の形式で保持します。
/// </summary>
[Serializable]
public class FurnitureData
{
    public FurnitureId id;
    public string displayName;
    public int purchasePrice;
    public string spriteResourcePath;

    public float visitorBonusPercent;
    public float budgetBonusPercent;

    public float summerVisitorBonusPercent;

    public float rainyVisitorBonusPercent;
    public float rainyBudgetBonusPercent;

    /// <summary>
    /// 雨による来客率減少ペナルティの下限。
    /// 例：-30%まで軽減する家具なら -0.30f。
    /// 天候システム実装時に使用します。0なら未指定です。
    /// </summary>
    public float rainyVisitorPenaltyFloorPercent;

    public bool HasRainEffect =>
        rainyVisitorBonusPercent != 0f ||
        rainyBudgetBonusPercent != 0f ||
        rainyVisitorPenaltyFloorPercent != 0f;

    public bool HasSummerEffect => summerVisitorBonusPercent != 0f;
}
