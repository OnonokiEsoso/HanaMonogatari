using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// 依頼の発生・受注・辞退・期限・達成判定を管理します。
/// 受注した依頼の成功確認は「開店する」を押した瞬間に行います。
/// </summary>
public class RequestSystem : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private PricingSystem pricingSystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("発生ルール")]
    [Range(0f, 1f)]
    [SerializeField] private float dailyOfferChance = 0.15f;

    [Header("現在の状態（確認用）")]
    [SerializeField] private RequestData currentRequest;
    [SerializeField] private RequestData lastResolvedRequest;
    [SerializeField] private int lastProcessedAbsoluteDay = -1;

    [Header("依頼報酬：来客率ボーナス（確認用）")]
    [SerializeField] private float activeVisitorBonusPercent;
    [SerializeField] private int visitorBonusStartAbsoluteDay = -1;
    [SerializeField] private int visitorBonusEndAbsoluteDay = -1;

    [Header("謎のお通げ：当日限定効果（確認用）")]
    [SerializeField] private FlowerData activeMysterySaleFlower;
    [SerializeField] private int activeMysterySaleAbsoluteDay = -1;

    public RequestData CurrentRequest => currentRequest;
    public RequestData LastResolvedRequest => lastResolvedRequest;
    public bool HasOfferedRequest => currentRequest != null && currentRequest.state == RequestState.Offered;
    public bool HasAcceptedRequest => currentRequest != null && currentRequest.state == RequestState.Accepted;
    public bool HasActiveRequest => HasOfferedRequest || HasAcceptedRequest;
    public bool IsMysterySaleActiveToday =>
        activeMysterySaleFlower != null && activeMysterySaleAbsoluteDay == GetCurrentAbsoluteDay();

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

        if (activeMysterySaleAbsoluteDay != today)
        {
            activeMysterySaleFlower = null;
            activeMysterySaleAbsoluteDay = -1;
        }

        if (visitorBonusEndAbsoluteDay >= 0 && today > visitorBonusEndAbsoluteDay)
        {
            activeVisitorBonusPercent = 0f;
            visitorBonusStartAbsoluteDay = -1;
            visitorBonusEndAbsoluteDay = -1;
        }

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
    /// 「開店する」を押した時に呼びます。
    /// 条件達成ならその場で成功。未達成でも期限前なら依頼は継続し、期限最終日の開店時だけ失敗にします。
    /// </summary>
    public void ResolveAcceptedRequestAtOpening()
    {
        if (!HasAcceptedRequest || shopManager == null)
            return;

        RequestData request = currentRequest;
        bool completed = request.requestType switch
        {
            RequestType.BouquetOrder => TryCompleteBouquetRequest(request),
            RequestType.MysteryMessage => TryCompleteMysteryRequest(request),
            _ => false
        };

        if (completed)
            return;

        int today = GetCurrentAbsoluteDay();
        if (today >= request.deadlineAbsoluteDay)
        {
            Debug.Log($"依頼失敗：{request.title} / 開店時に条件を満たしていませんでした。");
            FailCurrentRequest();
        }
        else
        {
            int remaining = request.GetRemainingDays(today);
            Debug.Log($"依頼未達成：{request.title} / まだ期限内です（残り{remaining}日）。");
        }
    }

    /// <summary>
    /// 今日有効な依頼報酬の来客率倍率を返します。
    /// 例：+25%なら1.25。成功当日は含めず、翌日から指定日数だけ有効です。
    /// </summary>
    public float GetVisitorMultiplierForToday()
    {
        if (shopManager == null || activeVisitorBonusPercent <= 0f)
            return 1f;

        int today = GetCurrentAbsoluteDay();
        bool active = visitorBonusStartAbsoluteDay >= 0 &&
                      visitorBonusEndAbsoluteDay >= visitorBonusStartAbsoluteDay &&
                      today >= visitorBonusStartAbsoluteDay &&
                      today <= visitorBonusEndAbsoluteDay;

        return active ? 1f + activeVisitorBonusPercent : 1f;
    }

    /// <summary>
    /// 謎のお通げ成功日の各来客に対して呼びます。
    /// 通常購入とは完全に別枠で指定花を1個・777円で追加購入します。
    /// 予算や好み、購入確率は見ません。在庫が無ければ何も起きません。
    /// </summary>
    public bool TrySellMysteryBonusFlower(out FlowerData flower, out int price)
    {
        flower = null;
        price = 0;

        if (!IsMysterySaleActiveToday || inventorySystem == null || shopManager == null)
            return false;

        if (inventorySystem.GetTotalQuantity(activeMysterySaleFlower) <= 0)
            return false;

        if (!inventorySystem.TryRemoveFlower(activeMysterySaleFlower, 1))
            return false;

        flower = activeMysterySaleFlower;
        price = 777;
        shopManager.AddMoney(price);
        return true;
    }

    public bool CompleteCurrentRequest()
    {
        if (!HasAcceptedRequest)
            return false;

        RequestData completed = currentRequest;

        if (completed.rewardShopRating > 0 && shopManager != null)
            shopManager.AddShopRating(completed.rewardShopRating);

        if (completed.rewardVisitorBonusPercent > 0f && completed.rewardVisitorBonusDays > 0)
            ActivateVisitorBonus(completed.rewardVisitorBonusPercent, completed.rewardVisitorBonusDays);

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

    private void ActivateVisitorBonus(float bonusPercent, int days)
    {
        if (shopManager == null || bonusPercent <= 0f || days <= 0)
            return;

        int today = GetCurrentAbsoluteDay();
        activeVisitorBonusPercent = bonusPercent;
        visitorBonusStartAbsoluteDay = today + 1;
        visitorBonusEndAbsoluteDay = visitorBonusStartAbsoluteDay + days - 1;

        Debug.Log($"依頼報酬：翌日から{days}日間、来客率+{bonusPercent * 100f:0.#}%");
    }

    private bool TryCompleteBouquetRequest(RequestData request)
    {
        if (bouquetSystem == null || request == null)
            return false;

        BouquetSystem.BouquetData matchingBouquet = bouquetSystem.Bouquets.FirstOrDefault(bouquet =>
            BouquetMatchesRequest(bouquet, request));

        if (matchingBouquet == null)
            return false;

        string bouquetName = matchingBouquet.bouquetName;
        if (!bouquetSystem.RemoveBouquet(matchingBouquet))
            return false;

        Debug.Log($"依頼納品：{bouquetName}を渡しました。");
        CompleteCurrentRequest();
        return true;
    }

    private static bool BouquetMatchesRequest(BouquetSystem.BouquetData bouquet, RequestData request)
    {
        if (bouquet?.components == null || request == null)
            return false;

        if (!string.Equals(
                bouquet.bouquetName?.Trim(),
                request.requiredBouquetName?.Trim(),
                StringComparison.Ordinal))
            return false;

        if (bouquet.salePrice <= 0 || bouquet.salePrice > request.bouquetMaxPrice)
            return false;

        if (bouquet.TotalQuantity < request.bouquetMinFlowerCount ||
            bouquet.TotalQuantity > request.bouquetMaxFlowerCount)
            return false;

        return bouquet.components.Any(component =>
            component?.flower != null &&
            component.quantity > 0 &&
            string.Equals(
                component.flower.color?.Trim(),
                request.requiredColor?.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private bool TryCompleteMysteryRequest(RequestData request)
    {
        if (request == null || inventorySystem == null || pricingSystem == null)
            return false;

        FlowerData target = FindMysteryTargetFlower(request);
        if (target == null)
            return false;

        if (inventorySystem.GetTotalQuantity(target) <= 0)
            return false;

        if (pricingSystem.GetSalePrice(target) != request.targetSalePrice)
            return false;

        activeMysterySaleFlower = target;
        activeMysterySaleAbsoluteDay = GetCurrentAbsoluteDay();

        Debug.Log($"謎のお通げ成功：{target.flowerName}（{target.color}）が本日、全来客の追加購入対象になりました。");
        CompleteCurrentRequest();
        return true;
    }

    private FlowerData FindMysteryTargetFlower(RequestData request)
    {
        if (request == null || inventorySystem == null)
            return null;

        return inventorySystem.Batches
            .Where(batch => batch?.flower != null && batch.quantity > 0)
            .Select(batch => batch.flower)
            .FirstOrDefault(flower =>
                string.Equals(flower.flowerName, request.targetFlowerName, StringComparison.Ordinal) &&
                string.Equals(flower.color, request.targetFlowerColor, StringComparison.Ordinal));
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

    private RequestData CreateMysteryRequest(int offeredDay)
    {
        if (inventorySystem == null)
        {
            Debug.LogWarning("RequestSystem: 謎のお通げを作るにはInventorySystemが必要です。");
            return null;
        }

        FlowerData[] ownedFlowers = inventorySystem.Batches
            .Where(batch => batch?.flower != null && batch.quantity > 0)
            .Select(batch => batch.flower)
            .Distinct()
            .ToArray();

        if (ownedFlowers.Length == 0)
        {
            Debug.Log("謎のお通げ：対象にできる所持花がないため、今回は発生しませんでした。");
            return null;
        }

        FlowerData target = ownedFlowers[UnityEngine.Random.Range(0, ownedFlowers.Length)];

        return new RequestData
        {
            requestId = $"mystery_{offeredDay}",
            requestType = RequestType.MysteryMessage,
            state = RequestState.Offered,
            title = "謎のお通げ",
            requesterName = "？？？",
            description = $"『{target.flowerName}（{target.color}）を777円にせよ……』",
            offeredAbsoluteDay = offeredDay,
            durationDays = 1,
            targetFlowerName = target.flowerName,
            targetFlowerColor = target.color,
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
