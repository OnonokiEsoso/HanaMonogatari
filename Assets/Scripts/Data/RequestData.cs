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
/// 依頼者の要望文・達成条件・期限・報酬・成功時セリフをまとめて保持します。
/// </summary>
[Serializable]
public class RequestData
{
    public string requestId;
    public RequestType requestType;
    public RequestState state;

    public string title;
    public string requesterName;
    public string requesterMessage;
    public string description;
    public string deadlineLabel;
    public string successMessage;

    public int offeredAbsoluteDay;
    public int acceptedAbsoluteDay = -1;
    public int deadlineAbsoluteDay = -1;
    public int durationDays = 1;

    // 花束依頼
    // 0 / 空文字は「その条件を指定しない」として扱います。
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
