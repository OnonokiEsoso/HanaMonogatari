using System;
using UnityEngine;

/// <summary>
/// 依頼の発生・受注・辞退・期限・完了/失敗状態を管理します。
/// ver0.0.4の最初の段階では、依頼内容の判定処理は別実装に分離します。
/// </summary>
public class RequestSystem : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;

    [Header("発生ルール")]
    [Range(0f, 1f)]
    [SerializeField] private float dailyOfferChance = 0.15f;

    [Header("現在の状態（確認用）")]
    [SerializeField] private RequestData currentRequest;
    [SerializeField] private RequestData lastResolvedRequest;
    [SerializeField] private int lastProcessedAbsoluteDay = -1;

    public RequestData CurrentRequest => currentRequest;
    public RequestData LastResolvedRequest => lastResolvedRequest;
    public bool HasOfferedRequest => currentRequest != null && currentRequest.state == RequestState.Offered;
    public bool HasAcceptedRequest => currentRequest != null && currentRequest.state == RequestState.Accepted;
    public bool HasActiveRequest => HasOfferedRequest || HasAcceptedRequest;

    public event Action<RequestData> OnRequestOffered;
    public event Action<RequestData> OnRequestChanged;
    public event Action<RequestData> OnRequestResolved;

    private void Start()
    {
        ProcessNewDay();
    }

    /// <summary>
    /// 朝に1回呼びます。
    /// 前日の未受注依頼を流し、期限切れを失敗にしたあと、空きがあれば15%で新規依頼を抽選します。
    /// </summary>
    public void ProcessNewDay()
    {
        if (shopManager == null)
        {
            Debug.LogWarning("RequestSystem: ShopManagerが設定されていません。");
            return;
        }

        int today = GetCurrentAbsoluteDay();
        if (lastProcessedAbsoluteDay == today)
            return;

        lastProcessedAbsoluteDay = today;

        if (currentRequest != null)
        {
            if (currentRequest.state == RequestState.Offered && currentRequest.offeredAbsoluteDay < today)
                ResolveCurrentRequest(RequestState.Declined);
            else if (currentRequest.state == RequestState.Accepted && today > currentRequest.deadlineAbsoluteDay)
                ResolveCurrentRequest(RequestState.Failed);
        }

        if (HasActiveRequest)
            return;

        if (UnityEngine.Random.value < dailyOfferChance)
            OfferRequest(UnityEngine.Random.value < 0.5f ? RequestType.BouquetOrder : RequestType.MysteryMessage);
    }

    public bool AcceptCurrentRequest()
    {
        if (!HasOfferedRequest || shopManager == null)
            return false;

        int today = GetCurrentAbsoluteDay();
        currentRequest.state = RequestState.Accepted;
        currentRequest.acceptedAbsoluteDay = today;
        currentRequest.deadlineAbsoluteDay = today + Mathf.Max(1, currentRequest.durationDays) - 1;

        Debug.Log($"依頼受注：{currentRequest.title} / 期限残り{currentRequest.durationDays}日");
        OnRequestChanged?.Invoke(currentRequest);
        return true;
    }

    public bool DeclineCurrentRequest()
    {
        if (!HasOfferedRequest)
            return false;

        ResolveCurrentRequest(RequestState.Declined);
        return true;
    }

    /// <summary>
    /// 条件判定側から成功時に呼びます。
    /// 店評価報酬はここで即時付与します。来客数+25%の期間報酬は次段階で効果システムへ接続します。
    /// </summary>
    public bool CompleteCurrentRequest()
    {
        if (!HasAcceptedRequest)
            return false;

        RequestData completed = currentRequest;

        if (completed.rewardShopRating > 0 && shopManager != null)
            shopManager.AddShopRating(completed.rewardShopRating);

        ResolveCurrentRequest(RequestState.Completed);
        return true;
    }

    public bool FailCurrentRequest()
    {
        if (!HasAcceptedRequest)
            return false;

        ResolveCurrentRequest(RequestState.Failed);
        return true;
    }

    public int GetCurrentRequestRemainingDays()
    {
        if (!HasAcceptedRequest || shopManager == null)
            return 0;

        return currentRequest.GetRemainingDays(GetCurrentAbsoluteDay());
    }

    private void OfferRequest(RequestType type)
    {
        if (shopManager == null || HasActiveRequest)
            return;

        int today = GetCurrentAbsoluteDay();
        currentRequest = type switch
        {
            RequestType.BouquetOrder => CreateBouquetRequest(today),
            RequestType.MysteryMessage => CreateMysteryRequest(today),
            _ => null
        };

        if (currentRequest == null)
            return;

        Debug.Log($"新しい依頼：{currentRequest.title} / {currentRequest.requesterName}");
        OnRequestOffered?.Invoke(currentRequest);
        OnRequestChanged?.Invoke(currentRequest);
    }

    private static RequestData CreateBouquetRequest(int offeredDay)
    {
        return new RequestData
        {
            requestId = $"bouquet_{offeredDay}",
            requestType = RequestType.BouquetOrder,
            state = RequestState.Offered,
            title = "花束のお願い",
            requesterName = "サラリーマン",
            description = "5000円以下で赤色を入れた5～7本の花束を作っておいてください！ 花束名は『誕生日おめでとう』でお願いします。",
            offeredAbsoluteDay = offeredDay,
            durationDays = 3,
            bouquetMaxPrice = 5000,
            requiredColor = "赤",
            bouquetMinFlowerCount = 5,
            bouquetMaxFlowerCount = 7,
            requiredBouquetName = "誕生日おめでとう",
            rewardShopRating = 50,
            rewardVisitorBonusPercent = 0.25f,
            rewardVisitorBonusDays = 3
        };
    }

    private static RequestData CreateMysteryRequest(int offeredDay)
    {
        return new RequestData
        {
            requestId = $"mystery_{offeredDay}",
            requestType = RequestType.MysteryMessage,
            state = RequestState.Offered,
            title = "謎のお通げ",
            requesterName = "？？？",
            description = "奇妙な依頼が届いている……。指定された花を777円にすると何かが起こるらしい。",
            offeredAbsoluteDay = offeredDay,
            durationDays = 1,
            targetSalePrice = 777,
            rewardShopRating = 0,
            rewardVisitorBonusPercent = 0f,
            rewardVisitorBonusDays = 0
        };
    }

    private void ResolveCurrentRequest(RequestState resultState)
    {
        if (currentRequest == null)
            return;

        currentRequest.state = resultState;
        lastResolvedRequest = currentRequest;

        Debug.Log($"依頼終了：{lastResolvedRequest.title} / {resultState}");
        OnRequestResolved?.Invoke(lastResolvedRequest);
        OnRequestChanged?.Invoke(lastResolvedRequest);

        currentRequest = null;
    }

    private int GetCurrentAbsoluteDay()
    {
        if (shopManager == null)
            return 0;

        return (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
    }

    [ContextMenu("DEBUG: 花束依頼を発生")]
    private void DebugOfferBouquetRequest()
    {
        if (currentRequest == null)
            OfferRequest(RequestType.BouquetOrder);
    }

    [ContextMenu("DEBUG: 謎のお通げを発生")]
    private void DebugOfferMysteryRequest()
    {
        if (currentRequest == null)
            OfferRequest(RequestType.MysteryMessage);
    }
}
