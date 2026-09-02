using System;

public enum RequestType
{
    BouquetOrder,
    MysteryMessage
}

public enum RequestState
{
    None,
    Offered,
    Accepted,
    Completed,
    Failed,
    Declined
}

/// <summary>
/// 依頼1件ぶんの実行時データです。
/// ver0.0.4では「花束のお願い」「謎のお通げ」の2種類から始めます。
/// </summary>
[Serializable]
public class RequestData
{
    public string requestId;
    public RequestType requestType;
    public RequestState state;

    public string title;
    public string requesterName;
    public string description;

    public int offeredAbsoluteDay;
    public int acceptedAbsoluteDay = -1;
    public int deadlineAbsoluteDay = -1;
    public int durationDays = 1;

    // 花束依頼
    public int bouquetMaxPrice;
    public string requiredColor;
    public int bouquetMinFlowerCount;
    public int bouquetMaxFlowerCount;
    public string requiredBouquetName;

    // 謎のお通げ
    public string targetFlowerName;
    public string targetFlowerColor;
    public int targetSalePrice;

    // 報酬
    public int rewardShopRating;
    public float rewardVisitorBonusPercent;
    public int rewardVisitorBonusDays;

    public int GetRemainingDays(int currentAbsoluteDay)
    {
        if (state != RequestState.Accepted || deadlineAbsoluteDay < 0)
            return 0;

        return Math.Max(0, deadlineAbsoluteDay - currentAbsoluteDay + 1);
    }
}
