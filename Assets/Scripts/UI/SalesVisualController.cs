using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 営業画面の見た目を担当します。
/// 客を右側からレジ前へ移動させ、購入内容・金額・一言を表示したあと右側へ退店させます。
/// 各客画像のInspector上の配置位置を、その客固有のレジ前停止位置として使用します。
/// 吹き出し本体は常時表示し、話者に応じて主人公用・第三者用の吹き出しパーツを切り替えます。
/// </summary>
public class SalesVisualController : MonoBehaviour
{
    private enum BubbleSpeaker
    {
        None,
        Protagonist,
        ThirdParty
    }

    [Header("客画像")]
    [SerializeField] private RectTransform housewifeImage;
    [SerializeField] private RectTransform studentImage1;
    [SerializeField] private RectTransform studentImage2;
    [SerializeField] private RectTransform grandmotherImage;
    [SerializeField] private RectTransform wealthyImage;
    [SerializeField] private RectTransform childImage;
    [SerializeField] private RectTransform officeWorkerImage;

    [Header("会計表示")]
    [SerializeField] private GameObject speechBubble;
    [Tooltip("主人公が話す時だけ表示する吹き出しの一部を設定します。")]
    [SerializeField] private GameObject protagonistBubblePart;
    [Tooltip("お客など第三者が話す時だけ表示する吹き出しの一部を設定します。以前の Text Background Image に相当します。")]
    [FormerlySerializedAs("textBackgroundImage")]
    [SerializeField] private GameObject thirdPartyBubblePart;
    [SerializeField] private TMP_Text purchaseText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text commentText;

    [Header("開店確認")]
    [Tooltip("吹き出し内などに置く『開店する』ボタンを設定します。開店確認中だけ表示されます。")]
    [SerializeField] private Button openShopConfirmButton;
    [Tooltip("常駐GameObjectに置いたCustomerUIを設定します。")]
    [SerializeField] private CustomerUI customerUI;

    [Header("移動位置")]
    [Tooltip("各客画像の現在位置から、右へどれだけ離れた場所を入店開始・退店位置にするか。レジ前停止位置は各画像のInspector上の現在位置をそのまま使います。")]
    [FormerlySerializedAs("outsideRightX")]
    [SerializeField] private float outsideRightOffset = 850f;

    [Header("演出時間")]
    [Min(0.05f)] [SerializeField] private float enterDuration = 0.65f;
    [Min(0f)] [SerializeField] private float purchaseDisplayDelay = 0.35f;
    [Min(0f)] [SerializeField] private float priceDisplayDelay = 0.45f;
    [Min(0f)] [SerializeField] private float commentDisplayDuration = 1.1f;
    [Min(0.05f)] [SerializeField] private float exitDuration = 0.65f;

    private readonly Dictionary<RectTransform, Vector2> customerCounterPositions = new();
    private RectTransform activeCustomer;
    private bool isPlaying;
    private BubbleSpeaker currentSpeaker = BubbleSpeaker.None;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        CacheCustomerCounterPositions();
        HideAllCustomers();

        if (speechBubble != null)
            speechBubble.SetActive(true);

        if (openShopConfirmButton != null)
        {
            openShopConfirmButton.onClick.AddListener(ConfirmOpenShop);
            openShopConfirmButton.gameObject.SetActive(false);
        }

        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
    }

    private void OnDestroy()
    {
        if (openShopConfirmButton != null)
            openShopConfirmButton.onClick.RemoveListener(ConfirmOpenShop);
    }

    /// <summary>
    /// ShowOpenConfirmation（ショー・オープン・コンファメーション）
    /// DailyResultPanelへ入った直後、主人公の吹き出しとして「開店する？」を表示します。
    /// </summary>
    public void ShowOpenConfirmation()
    {
        HideAllCustomers();
        ClearCheckoutText();

        if (speechBubble != null)
            speechBubble.SetActive(true);

        currentSpeaker = BubbleSpeaker.Protagonist;
        SetPurchaseText("開店する？");

        if (openShopConfirmButton != null)
            openShopConfirmButton.gameObject.SetActive(true);
    }

    private void ConfirmOpenShop()
    {
        if (customerUI == null)
        {
            Debug.LogWarning("SalesVisualController: CustomerUIが設定されていません。");
            return;
        }

        if (openShopConfirmButton != null)
            openShopConfirmButton.gameObject.SetActive(false);

        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
        customerUI.OpenShop();
    }

    /// <summary>
    /// 営業開始時に確認表示を消し、常時表示の吹き出し本体だけ残します。
    /// </summary>
    public void PrepareForBusiness()
    {
        if (speechBubble != null)
            speechBubble.SetActive(true);

        if (openShopConfirmButton != null)
            openShopConfirmButton.gameObject.SetActive(false);

        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
    }

    private void CacheCustomerCounterPositions()
    {
        customerCounterPositions.Clear();
        CachePosition(housewifeImage);
        CachePosition(studentImage1);
        CachePosition(studentImage2);
        CachePosition(grandmotherImage);
        CachePosition(wealthyImage);
        CachePosition(childImage);
        CachePosition(officeWorkerImage);
    }

    private void CachePosition(RectTransform target)
    {
        if (target != null)
            customerCounterPositions[target] = target.anchoredPosition;
    }

    public IEnumerator PlayCustomerSequence(
        CustomerSystem.VisitingCustomer customer,
        CustomerPurchaseSystem.PurchaseResult result)
    {
        if (isPlaying)
            yield break;

        isPlaying = true;
        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
        HideAllCustomers();

        if (speechBubble != null)
            speechBubble.SetActive(true);

        if (openShopConfirmButton != null)
            openShopConfirmButton.gameObject.SetActive(false);

        activeCustomer = GetCustomerImage(customer?.data?.customerType ?? CustomerType.Housewife);
        if (activeCustomer == null)
        {
            Debug.LogWarning("SalesVisualController: 対応する客画像が設定されていません。");
            isPlaying = false;
            yield break;
        }

        Vector2 counterPosition = GetCounterPosition(activeCustomer);
        float outsideX = counterPosition.x + Mathf.Abs(outsideRightOffset);

        activeCustomer.anchoredPosition = new Vector2(outsideX, counterPosition.y);
        activeCustomer.gameObject.SetActive(true);

        yield return MoveX(activeCustomer, counterPosition.x, enterDuration);

        // ここからはお客など第三者側の発言・会計表示。
        currentSpeaker = BubbleSpeaker.ThirdParty;
        SetPurchaseText(BuildPurchaseText(result));

        if (purchaseDisplayDelay > 0f)
            yield return new WaitForSeconds(purchaseDisplayDelay);

        SetPriceText(result != null && result.purchased
            ? $"{result.salePrice:N0}円"
            : string.Empty);

        if (priceDisplayDelay > 0f)
            yield return new WaitForSeconds(priceDisplayDelay);

        SetCommentText(BuildComment(result));

        if (commentDisplayDuration > 0f)
            yield return new WaitForSeconds(commentDisplayDuration);

        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
        yield return MoveX(activeCustomer, outsideX, exitDuration);

        activeCustomer.anchoredPosition = counterPosition;
        activeCustomer.gameObject.SetActive(false);
        activeCustomer = null;
        isPlaying = false;
    }

    private Vector2 GetCounterPosition(RectTransform target)
    {
        if (target != null && customerCounterPositions.TryGetValue(target, out Vector2 position))
            return position;

        return target != null ? target.anchoredPosition : Vector2.zero;
    }

    private IEnumerator MoveX(RectTransform target, float destinationX, float duration)
    {
        if (target == null) yield break;

        Vector2 start = target.anchoredPosition;
        Vector2 end = new Vector2(destinationX, start.y);

        if (duration <= 0f)
        {
            target.anchoredPosition = end;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            target.anchoredPosition = Vector2.LerpUnclamped(start, end, t);
            yield return null;
        }

        target.anchoredPosition = end;
    }

    private RectTransform GetCustomerImage(CustomerType customerType)
    {
        return customerType switch
        {
            CustomerType.Housewife => housewifeImage,
            CustomerType.Student => UnityEngine.Random.value < 0.5f && studentImage2 != null ? studentImage2 : studentImage1,
            CustomerType.Grandmother => grandmotherImage,
            CustomerType.Wealthy => wealthyImage,
            CustomerType.Child => childImage,
            CustomerType.OfficeWorker => officeWorkerImage,
            _ => housewifeImage
        };
    }

    private static string BuildPurchaseText(CustomerPurchaseSystem.PurchaseResult result)
    {
        if (result == null || !result.purchased)
            return "今回は購入なし";

        string message = result.message ?? string.Empty;
        int start = message.IndexOf("）が", StringComparison.Ordinal);
        if (start >= 0)
        {
            start += 2;
            int end = message.IndexOf("を購入", start, StringComparison.Ordinal);
            if (end > start)
                return message.Substring(start, end - start);
        }

        if (result.bouquet != null)
            return $"{result.bouquet.bouquetName} ×1";

        if (result.flower != null)
            return $"{result.flower.flowerName}（{result.flower.color}）";

        return "お花を購入";
    }

    private static string BuildComment(CustomerPurchaseSystem.PurchaseResult result)
    {
        if (result == null || !result.purchased)
            return "今日はやめておこうかな。";

        return result.satisfactionLevel switch
        {
            CustomerPurchaseSystem.SatisfactionLevel.Best => PickRandom(
                "すごく素敵！ また来ます！",
                "いいお買い物ができた！",
                "これは気に入ったな。"),
            CustomerPurchaseSystem.SatisfactionLevel.Good => PickRandom(
                "いい感じですね。",
                "うん、これにしてよかった。",
                "ありがとうございます。"),
            _ => PickRandom(
                "まあ、これでいいかな。",
                "今回はこれにしよう。",
                "うん、ありがとう。")
        };
    }

    private static string PickRandom(params string[] values)
    {
        if (values == null || values.Length == 0) return string.Empty;
        return values[UnityEngine.Random.Range(0, values.Length)];
    }

    public void HideAllCustomers()
    {
        SetActive(housewifeImage, false);
        SetActive(studentImage1, false);
        SetActive(studentImage2, false);
        SetActive(grandmotherImage, false);
        SetActive(wealthyImage, false);
        SetActive(childImage, false);
        SetActive(officeWorkerImage, false);
    }

    private void SetPurchaseText(string value)
    {
        if (purchaseText != null)
            purchaseText.text = value ?? string.Empty;

        RefreshBubbleParts();
    }

    private void SetPriceText(string value)
    {
        if (priceText != null)
            priceText.text = value ?? string.Empty;

        RefreshBubbleParts();
    }

    private void SetCommentText(string value)
    {
        if (commentText != null)
            commentText.text = value ?? string.Empty;

        RefreshBubbleParts();
    }

    private void ClearCheckoutText()
    {
        if (purchaseText != null)
            purchaseText.text = string.Empty;

        if (priceText != null)
            priceText.text = string.Empty;

        if (commentText != null)
            commentText.text = string.Empty;

        RefreshBubbleParts();
    }

    /// <summary>
    /// 話者とテキスト有無に応じて、吹き出しの方向用パーツを切り替えます。
    /// 主人公発言時は主人公用のみ、客など第三者発言時は第三者用のみ表示します。
    /// </summary>
    private void RefreshBubbleParts()
    {
        bool hasText =
            (purchaseText != null && !string.IsNullOrWhiteSpace(purchaseText.text)) ||
            (priceText != null && !string.IsNullOrWhiteSpace(priceText.text)) ||
            (commentText != null && !string.IsNullOrWhiteSpace(commentText.text));

        if (protagonistBubblePart != null)
            protagonistBubblePart.SetActive(hasText && currentSpeaker == BubbleSpeaker.Protagonist);

        if (thirdPartyBubblePart != null)
            thirdPartyBubblePart.SetActive(hasText && currentSpeaker == BubbleSpeaker.ThirdParty);
    }

    private static void SetActive(RectTransform target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }
}
