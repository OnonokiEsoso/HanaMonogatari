using System;

/// <summary>
/// CustomerType（カスタマー・タイプ）＝客タイプ。
/// </summary>
public enum CustomerType
{
    Housewife,
    Student,
    Grandmother,
    Wealthy,
    Child,
    OfficeWorker
}

/// <summary>
/// 客タイプごとの基本条件。
/// 予算、好む人気度・珍しさ、常連化に必要な購入回数などを保持します。
/// </summary>
[Serializable]
public class CustomerData
{
    public CustomerType customerType;
    public string displayName;

    public int budget;

    public int minPopularity;
    public int maxPopularity;

    public int minRarity;
    public int maxRarity;

    public int regularPointMax;

    /// <summary>
    /// 客タイプ抽選用の基本重み。
    /// 総来客数ではなく、どの客タイプが来るかの割合に使います。
    /// </summary>
    public float spawnWeight = 1f;

    public CustomerData(
        CustomerType customerType,
        string displayName,
        int budget,
        int minPopularity,
        int maxPopularity,
        int minRarity,
        int maxRarity,
        int regularPointMax,
        float spawnWeight = 1f)
    {
        this.customerType = customerType;
        this.displayName = displayName;
        this.budget = budget;
        this.minPopularity = minPopularity;
        this.maxPopularity = maxPopularity;
        this.minRarity = minRarity;
        this.maxRarity = maxRarity;
        this.regularPointMax = regularPointMax;
        this.spawnWeight = spawnWeight;
    }
}
