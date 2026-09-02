using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 依頼の発生・受注・辞退・期限・達成判定を管理します。
/// 受注した依頼の条件確認は「開店する」を押した瞬間に行います。
/// 花束依頼は開店時に条件を満たす花束を確保し、通常客の営業終了後に依頼主へ販売して完了します。
/// </summary>
public class RequestSystem : MonoBehaviour
{
    [Serializable]
    private class TimedVisitorBonus
    {
        public string sourceKey;
        [Min(0f)] public float percentBonus;
        public int startAbsoluteDay;
        public int endAbsoluteDay;
    }

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
    [SerializeField] private string lastOpeningRequestMessage;

    [Header("依頼報酬：来客率ボーナス（確認用）")]
    [Tooltip("複数の依頼報酬が重なった場合も上書きせず、それぞれの有効期間中は加算します。")]
    [SerializeField] private List<TimedVisitorBonus> activeVisitorBonuses = new();

    [Header("花束依頼：本日受取予定（確認用）")]
    [SerializeField] private RequestData pendingBouquetRequest;
    [SerializeField] private BouquetSystem.BouquetData pendingBouquetPickup;

    [Header("謎のお通げ：当日限定効果（確認用）")]
    [SerializeField] private FlowerData activeMysterySaleFlower;
    [SerializeField] private int activeMysterySaleAbsoluteDay = -1;

    public RequestData CurrentRequest => currentRequest;
    public RequestData LastResolvedRequest => lastResolvedRequest;
    public string LastOpeningRequestMessage => lastOpeningRequestMessage;
    public bool HasOfferedRequest => currentRequest != null && currentRequest.state == RequestState.Offered;
    public bool HasAcceptedRequest => currentRequest != null && currentRequest.state == RequestState.Accepted;
    public bool HasActiveRequest => HasOfferedRequest || HasAcceptedRequest;
    public bool HasPendingBouquetPickup => pendingBouquetRequest != null && pendingBouquetPickup != null;
    public bool IsMysterySaleActiveToday =>
        activeMysterySaleFlower != null && activeMysterySaleAbsoluteDay == GetCurrentAbsoluteDay();

    public event Action<RequestData> OnRequestOffered;
    public event Action<RequestData> OnRequestChanged;
    public event Action<RequestData> OnRequestResolved;

    private void Start()
    {
        ProcessNewDay();
    }

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

        activeVisitorBonuses ??= new List<TimedVisitorBonus>();
        activeVisitorBonuses.RemoveAll(bonus =>
            bonus == null || bonus.percentBonus <= 0f || today > bonus.endAbsoluteDay);

        if (pendingBouquetPickup != null || pendingBouquetRequest != null)
        {
            Debug.LogWarning("RequestSystem: 前日の依頼用花束予約が残っていたため解除しました。");
            pendingBouquetPickup = null;
            pendingBouquetRequest = null;
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
            OfferRandomRequest();
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

    public void ResolveAcceptedRequestAtOpening()
    {
        lastOpeningRequestMessage = string.Empty;

        if (!HasAcceptedRequest || shopManager == null)
            return;

        RequestData request = currentRequest;
        bool conditionMet = request.requestType switch
        {
            RequestType.BouquetOrder => TryReserveBouquetRequest(request),
            RequestType.MysteryMessage => TryCompleteMysteryRequest(request),
            _ => false
        };

        if (conditionMet)
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

    public float GetVisitorMultiplierForToday()
    {
        if (shopManager == null)
            return 1f;

        int today = GetCurrentAbsoluteDay();
        activeVisitorBonuses ??= new List<TimedVisitorBonus>();

        float totalPercent = activeVisitorBonuses
            .Where(bonus => bonus != null &&
                            bonus.percentBonus > 0f &&
                            today >= bonus.startAbsoluteDay &&
                            today <= bonus.endAbsoluteDay)
            .Sum(bonus => bonus.percentBonus);

        return 1f + Mathf.Max(0f, totalPercent);
    }

    public bool TryCompletePendingBouquetPickup(
        out RequestData request,
        out BouquetSystem.BouquetData bouquet,
        out int salePrice,
        out string successMessage)
    {
        request = pendingBouquetRequest;
        bouquet = pendingBouquetPickup;
        salePrice = 0;
        successMessage = string.Empty;

        if (request == null || bouquet == null || shopManager == null)
            return false;

        if (currentRequest != request || currentRequest.state != RequestState.Accepted)
        {
            pendingBouquetRequest = null;
            pendingBouquetPickup = null;
            return false;
        }

        salePrice = Mathf.Max(0, bouquet.salePrice);
        if (salePrice > 0)
            shopManager.AddMoney(salePrice);

        string rawSuccessMessage = request.successMessage;

        if (!CompleteCurrentRequest())
            return false;

        successMessage = rawSuccessMessage;
        pendingBouquetRequest = null;
        pendingBouquetPickup = null;

        Debug.Log($"依頼受取完了：{request.requesterName} / {bouquet.bouquetName} / {salePrice:N0}円");
        return true;
    }

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
            ActivateVisitorBonus(
                completed.requestId,
                completed.rewardVisitorBonusPercent,
                completed.rewardVisitorBonusDays);

        if (!string.IsNullOrWhiteSpace(completed.successMessage))
        {
            lastOpeningRequestMessage = string.IsNullOrWhiteSpace(completed.requesterName)
                ? completed.successMessage
                : $"{completed.requesterName}「{completed.successMessage}」";
            Debug.Log(lastOpeningRequestMessage);
        }

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

    private void ActivateVisitorBonus(string sourceKey, float bonusPercent, int days)
    {
        if (shopManager == null || bonusPercent <= 0f || days <= 0)
            return;

        int today = GetCurrentAbsoluteDay();
        int startDay = today + 1;
        int endDay = startDay + days - 1;

        activeVisitorBonuses ??= new List<TimedVisitorBonus>();

        string key = string.IsNullOrWhiteSpace(sourceKey)
            ? $"request_bonus_{today}_{activeVisitorBonuses.Count}"
            : sourceKey;

        TimedVisitorBonus existing = activeVisitorBonuses.FirstOrDefault(bonus =>
            bonus != null && string.Equals(bonus.sourceKey, key, StringComparison.Ordinal));

        if (existing == null)
        {
            activeVisitorBonuses.Add(new TimedVisitorBonus
            {
                sourceKey = key,
                percentBonus = bonusPercent,
                startAbsoluteDay = startDay,
                endAbsoluteDay = endDay
            });
        }
        else
        {
            existing.percentBonus = bonusPercent;
            existing.startAbsoluteDay = startDay;
            existing.endAbsoluteDay = endDay;
        }

        Debug.Log($"依頼報酬：翌日から{days}日間、来客率+{bonusPercent * 100f:0.#}%（他の依頼報酬と加算）");
    }

    private bool TryReserveBouquetRequest(RequestData request)
    {
        if (request == null)
            return false;

        if (bouquetSystem == null)
        {
            Debug.LogError("RequestSystem: BouquetSystemが設定されていないため、花束依頼を判定できません。InspectorのBouquet Systemを確認してください。");
            return false;
        }

        if (HasPendingBouquetPickup)
            return pendingBouquetRequest == request;

        if (bouquetSystem.Bouquets == null || bouquetSystem.Bouquets.Count == 0)
        {
            Debug.LogWarning($"依頼判定：作成済み花束が0個です。依頼={request.requestId}");
            return false;
        }

        BouquetSystem.BouquetData matchingBouquet = null;

        foreach (BouquetSystem.BouquetData bouquet in bouquetSystem.Bouquets)
        {
            if (BouquetMatchesRequest(bouquet, request, out string mismatchReason))
            {
                matchingBouquet = bouquet;
                break;
            }

            if (bouquet != null)
            {
                Debug.Log(
                    $"依頼判定NG：花束『{bouquet.bouquetName}』 / " +
                    $"価格{bouquet.salePrice:N0}円 / 本数{bouquet.TotalQuantity} / 理由：{mismatchReason}");
            }
        }

        if (matchingBouquet == null)
        {
            Debug.LogWarning(
                $"依頼判定：条件に合う花束が見つかりませんでした。" +
                $"依頼名={NormalizeBouquetName(request.requiredBouquetName)} / " +
                $"上限={request.bouquetMaxPrice}円 / " +
                $"本数={request.bouquetMinFlowerCount}～{request.bouquetMaxFlowerCount} / " +
                $"色={request.requiredColor}");
            return false;
        }

        if (!bouquetSystem.RemoveBouquet(matchingBouquet))
        {
            Debug.LogError($"依頼判定：一致した花束『{matchingBouquet.bouquetName}』の予約処理に失敗しました。");
            return false;
        }

        pendingBouquetRequest = request;
        pendingBouquetPickup = matchingBouquet;

        Debug.Log(
            $"依頼判定OK：『{matchingBouquet.bouquetName}』 " +
            $"{matchingBouquet.salePrice:N0}円 / {matchingBouquet.TotalQuantity}本 を依頼用に確保しました。");
        return true;
    }

    private static bool BouquetMatchesRequest(
        BouquetSystem.BouquetData bouquet,
        RequestData request,
        out string mismatchReason)
    {
        mismatchReason = string.Empty;

        if (bouquet == null)
        {
            mismatchReason = "花束データがnull";
            return false;
        }

        if (request == null)
        {
            mismatchReason = "依頼データがnull";
            return false;
        }

        if (bouquet.components == null || bouquet.components.Count == 0)
        {
            mismatchReason = "花束の中身が空";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.requiredBouquetName))
        {
            string actualName = NormalizeBouquetName(bouquet.bouquetName);
            string requiredName = NormalizeBouquetName(request.requiredBouquetName);

            if (!string.Equals(actualName, requiredName, StringComparison.OrdinalIgnoreCase))
            {
                mismatchReason = $"名前不一致（実際『{actualName}』 / 必要『{requiredName}』）";
                return false;
            }
        }

        if (request.bouquetMaxPrice > 0)
        {
            if (bouquet.salePrice <= 0)
            {
                mismatchReason = "販売価格が0円以下";
                return false;
            }

            if (bouquet.salePrice > request.bouquetMaxPrice)
            {
                mismatchReason = $"価格超過（{bouquet.salePrice:N0}円 > {request.bouquetMaxPrice:N0}円）";
                return false;
            }
        }

        int totalQuantity = bouquet.TotalQuantity;

        if (request.bouquetMinFlowerCount > 0 && totalQuantity < request.bouquetMinFlowerCount)
        {
            mismatchReason = $"本数不足（{totalQuantity}本 < {request.bouquetMinFlowerCount}本）";
            return false;
        }

        if (request.bouquetMaxFlowerCount > 0 && totalQuantity > request.bouquetMaxFlowerCount)
        {
            mismatchReason = $"本数超過（{totalQuantity}本 > {request.bouquetMaxFlowerCount}本）";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.requiredColor))
        {
            string requiredColor = NormalizeColor(request.requiredColor);
            bool hasRequiredColor = bouquet.components.Any(component =>
                component?.flower != null &&
                component.quantity > 0 &&
                string.Equals(
                    NormalizeColor(component.flower.color),
                    requiredColor,
                    StringComparison.OrdinalIgnoreCase));

            if (!hasRequiredColor)
            {
                string bouquetColors = string.Join("・", bouquet.components
                    .Where(component => component?.flower != null && component.quantity > 0)
                    .Select(component => NormalizeColor(component.flower.color))
                    .Where(color => !string.IsNullOrWhiteSpace(color))
                    .Distinct());

                mismatchReason = $"指定色なし（必要『{requiredColor}』 / 花束『{bouquetColors}』）";
                return false;
            }
        }

        return true;
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

    private void OfferRandomRequest()
    {
        if (shopManager == null || HasActiveRequest)
            return;

        int today = GetCurrentAbsoluteDay();
        currentRequest = UnityEngine.Random.Range(0, 7) switch
        {
            0 => CreateBouquetRequest01(today),
            1 => CreateMysteryRequest(today),
            2 => CreateBouquetRequest03(today),
            3 => CreateBouquetRequest04(today),
            4 => CreateBouquetRequest05(today),
            5 => CreateBouquetRequest06(today),
            _ => CreateBouquetRequest07(today)
        };

        if (currentRequest == null)
            return;

        Debug.Log($"新しい依頼：{currentRequest.title} / {currentRequest.requesterName} / {currentRequest.requestId}");
        OnRequestOffered?.Invoke(currentRequest);
        OnRequestChanged?.Invoke(currentRequest);
    }

    private void OfferRequest(RequestType type)
    {
        if (shopManager == null || HasActiveRequest)
            return;

        int today = GetCurrentAbsoluteDay();
        currentRequest = type switch
        {
            RequestType.BouquetOrder => CreateRandomBouquetRequest(today),
            RequestType.MysteryMessage => CreateMysteryRequest(today),
            _ => null
        };

        if (currentRequest == null)
            return;

        Debug.Log($"新しい依頼：{currentRequest.title} / {currentRequest.requesterName} / {currentRequest.requestId}");
        OnRequestOffered?.Invoke(currentRequest);
        OnRequestChanged?.Invoke(currentRequest);
    }

    private static RequestData CreateRandomBouquetRequest(int offeredDay)
    {
        return UnityEngine.Random.Range(0, 6) switch
        {
            0 => CreateBouquetRequest01(offeredDay),
            1 => CreateBouquetRequest03(offeredDay),
            2 => CreateBouquetRequest04(offeredDay),
            3 => CreateBouquetRequest05(offeredDay),
            4 => CreateBouquetRequest06(offeredDay),
            _ => CreateBouquetRequest07(offeredDay)
        };
    }

    private static RequestData CreateBouquetRequest01(int offeredDay)
    {
        return CreateBouquetRequest(
            offeredDay,
            "01",
            "サラリーマン",
            "妻の誕生日プレゼントで花束を渡したいのでお願いします",
            "5000円以下で、赤色を入れた5～7本の花束を作る。花束名は「誕生日おめでとう」にする。開店時に条件を満たした花束があれば達成。",
            "発注日含め3日",
            3,
            5000,
            "赤",
            5,
            7,
            "誕生日おめでとう",
            50,
            0.25f,
            3,
            "助かりました！ これなら妻もきっと喜んでくれます。ありがとうございます！");
    }

    private static RequestData CreateBouquetRequest03(int offeredDay)
    {
        return CreateBouquetRequest(
            offeredDay,
            "03",
            "子ども",
            "だいすきな人に、かわいい花束をプレゼントしたい！ おこづかいで買えるようにお願い！",
            "600円以下の花束、花束名は「ハッピーフラワーズ」",
            "発注日の翌日",
            2,
            600,
            null,
            0,
            0,
            "ハッピーフラワーズ",
            20,
            0f,
            0,
            "わあっ、かわいい！ これならきっと喜んでくれる！ ありがとう！");
    }

    private static RequestData CreateBouquetRequest04(int offeredDay)
    {
        return CreateBouquetRequest(
            offeredDay,
            "04",
            "サラリーマン",
            "いつも支えてくれている人へ、感謝を伝える花束を贈りたいんです。少し立派なものをお願いします。",
            "7000円以下で7本以上の花束を作る。花束名は「いつもありがとう」",
            "発注日の翌日",
            2,
            7000,
            null,
            7,
            0,
            "いつもありがとう",
            60,
            0.15f,
            2,
            "立派ですね！ これならちゃんと感謝を伝えられそうです。助かりました！");
    }

    private static RequestData CreateBouquetRequest05(int offeredDay)
    {
        return CreateBouquetRequest(
            offeredDay,
            "05",
            "学生",
            "友だちの誕生日に花束を渡したいです。赤い花を入れて、学生でも買えるくらいでお願いします！",
            "3000円以下で、赤色を入れた3～5本の花束を作る、花束名は「誕生日おめでとう」",
            "発注日の翌日",
            2,
            3000,
            "赤",
            3,
            5,
            "誕生日おめでとう",
            35,
            0f,
            0,
            "いい感じ！ これなら誕生日に渡すのが楽しみです。ありがとうございます！");
    }

    private static RequestData CreateBouquetRequest06(int offeredDay)
    {
        return CreateBouquetRequest(
            offeredDay,
            "06",
            "学生",
            "これからも仲良くしたい人に渡す花束がほしいです。明るい黄色の花を入れてください！",
            "4000円以下で、黄色を入れた3～5本の花束を作る、花束名は「これからもよろしくね」",
            "発注日の翌日",
            2,
            4000,
            "黄",
            3,
            5,
            "これからもよろしくね",
            40,
            0.10f,
            1,
            "すごく明るくていいですね！ これなら気持ちもちゃんと伝わりそうです！");
    }

    private static RequestData CreateBouquetRequest07(int offeredDay)
    {
        return CreateBouquetRequest(
            offeredDay,
            "07",
            "おばあさん",
            "お盆に供える花束をお願いしたいんです。派手すぎず、きちんとしたものにしてくださいな。",
            "4000円以下で、5～7本の花束を作る、花束名は「盆花」",
            "発注日の翌日",
            2,
            4000,
            null,
            5,
            7,
            "盆花",
            45,
            0f,
            0,
            "ありがとうねえ。これなら安心してお供えできますよ。");
    }

    private static RequestData CreateBouquetRequest(
        int offeredDay,
        string variantId,
        string requesterName,
        string requesterMessage,
        string description,
        string deadlineLabel,
        int durationDays,
        int maxPrice,
        string requiredColor,
        int minFlowerCount,
        int maxFlowerCount,
        string requiredBouquetName,
        int rewardShopRating,
        float rewardVisitorBonusPercent,
        int rewardVisitorBonusDays,
        string successMessage)
    {
        return new RequestData
        {
            requestId = $"bouquet_{variantId}_{offeredDay}",
            requestType = RequestType.BouquetOrder,
            state = RequestState.Offered,
            title = "花束のお願い",
            requesterName = requesterName,
            requesterMessage = requesterMessage,
            description = description,
            deadlineLabel = deadlineLabel,
            successMessage = successMessage,
            offeredAbsoluteDay = offeredDay,
            durationDays = durationDays,
            bouquetMaxPrice = maxPrice,
            requiredColor = requiredColor,
            bouquetMinFlowerCount = minFlowerCount,
            bouquetMaxFlowerCount = maxFlowerCount,
            requiredBouquetName = requiredBouquetName,
            rewardShopRating = rewardShopRating,
            rewardVisitorBonusPercent = rewardVisitorBonusPercent,
            rewardVisitorBonusDays = rewardVisitorBonusDays
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
        string targetLabel = $"{target.flowerName}（{target.color}）";

        return new RequestData
        {
            requestId = $"mystery_{offeredDay}",
            requestType = RequestType.MysteryMessage,
            state = RequestState.Offered,
            title = "謎のお通げ",
            requesterName = "？？？",
            requesterMessage = $"{targetLabel}を777円にするのじゃ、さすれば良いことが起こるだろう",
            description = $"{targetLabel}を777円に設定し、開店時に在庫と価格条件を満たしていれば達成。当日は来店客全員が通常購入とは別枠で指定花を1つ777円で追加購入する。在庫がなくなったら終了。",
            deadlineLabel = "当日中",
            successMessage = "……よくぞ成し遂げた。今日は不思議と、その花に皆の手が伸びるじゃろう……。",
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

        if (resultState != RequestState.Completed)
        {
            pendingBouquetRequest = null;
            pendingBouquetPickup = null;
        }
    }

    private int GetCurrentAbsoluteDay()
    {
        if (shopManager == null)
            return 0;

        return (shopManager.GameYear - 1) * ShopManager.DaysPerYear + shopManager.DayOfYear;
    }

    private static string NormalizeBouquetName(string bouquetName)
    {
        if (string.IsNullOrWhiteSpace(bouquetName))
            return string.Empty;

        string normalized = bouquetName.Trim();
        char[] removable = { '「', '」', '『', '』', '"', '\'', '“', '”', '‘', '’' };
        normalized = new string(normalized.Where(c => !removable.Contains(c)).ToArray());
        normalized = normalized.Replace(" ", string.Empty).Replace("　", string.Empty);
        return normalized;
    }

    private static string NormalizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return string.Empty;

        return color.Trim() switch
        {
            "桃" or "桃色" or "ピンク" => "ピンク",
            "橙" or "橙色" or "オレンジ" => "オレンジ",
            "黄色" => "黄",
            "赤色" => "赤",
            "青色" => "青",
            "紫色" => "紫",
            "白色" => "白",
            "緑色" => "緑",
            _ => color.Trim()
        };
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
