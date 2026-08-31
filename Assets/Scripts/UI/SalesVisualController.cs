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

    private enum ActionButtonMode
    {
        None,
        OpenShop,
        CloseShop
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
    [Tooltip("お客など第三者が話す時だけ表示する吹き出しの一部を設定します。")]
    [FormerlySerializedAs("textBackgroundImage")]
    [SerializeField] private GameObject thirdPartyBubblePart;
    [SerializeField] private TMP_Text purchaseText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text commentText;

    [Header("開店・閉店ボタン")]
    [Tooltip("開店確認と営業結果で共通利用するボタンを設定します。")]
    [FormerlySerializedAs("openShopConfirmButton")]
    [SerializeField] private Button shopActionButton;
    [Tooltip("Shop Action Button の文字。『開店する』『閉店する』を自動で切り替えます。")]
    [SerializeField] private TMP_Text shopActionButtonText;
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
    private ActionButtonMode actionButtonMode = ActionButtonMode.None;

    public bool IsPlaying => isPlaying;

    public event Action OnCloseShopRequested;

    private void Awake()
    {
        CacheCustomerCounterPositions();
        HideAllCustomers();

        if (speechBubble != null)
            speechBubble.SetActive(true);

        if (shopActionButton != null)
        {
            shopActionButton.onClick.AddListener(HandleShopActionButton);
            shopActionButton.gameObject.SetActive(false);
        }

        currentSpeaker = BubbleSpeaker.None;
        actionButtonMode = ActionButtonMode.None;
        ClearCheckoutText();
    }

    private void OnDestroy()
    {
        if (shopActionButton != null)
            shopActionButton.onClick.RemoveListener(HandleShopActionButton);
    }

    public void ShowOpenConfirmation()
    {
        HideAllCustomers();
        ClearCheckoutText();

        if (speechBubble != null)
            speechBubble.SetActive(true);

        currentSpeaker = BubbleSpeaker.Protagonist;
        SetPurchaseText("開店する？");
        ShowActionButton(ActionButtonMode.OpenShop, "開店する");
    }

    public void ShowBusinessResult(int totalSales, int purchaseCount, int totalVisitors)
    {
        HideAllCustomers();
        ClearCheckoutText();

        if (speechBubble != null)
            speechBubble.SetActive(true);

        currentSpeaker = BubbleSpeaker.Protagonist;

        string resultMessage =
            $"今日は{totalSales:N0}円の売上だったよ！\n" +
            $"{totalVisitors}人来てくれて、そのうち{purchaseCount}人がお買い物してくれたよ。";

        SetPurchaseText(resultMessage);
        ShowActionButton(ActionButtonMode.CloseShop, "閉店する");
    }

    private void HandleShopActionButton()
    {
        switch (actionButtonMode)
        {
            case ActionButtonMode.OpenShop:
                ConfirmOpenShop();
                break;
            case ActionButtonMode.CloseShop:
                ConfirmCloseShop();
                break;
        }
    }

    private void ConfirmOpenShop()
    {
        if (customerUI == null)
        {
            Debug.LogWarning("SalesVisualController: CustomerUIが設定されていません。");
            return;
        }

        HideActionButton();
        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
        customerUI.OpenShop();
    }

    private void ConfirmCloseShop()
    {
        HideActionButton();
        currentSpeaker = BubbleSpeaker.None;
        ClearCheckoutText();
        OnCloseShopRequested?.Invoke();
    }

    private void ShowActionButton(ActionButtonMode mode, string label)
    {
        actionButtonMode = mode;

        if (shopActionButtonText != null)
            shopActionButtonText.text = label;

        if (shopActionButton != null)
            shopActionButton.gameObject.SetActive(true);
    }

    private void HideActionButton()
    {
        actionButtonMode = ActionButtonMode.None;
        if (shopActionButton != null)
            shopActionButton.gameObject.SetActive(false);
    }

    public void PrepareForBusiness()
    {
        if (speechBubble != null)
            speechBubble.SetActive(true);

        HideActionButton();
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

        HideActionButton();

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

        currentSpeaker = BubbleSpeaker.ThirdParty;
        SetPurchaseText(BuildPurchaseText(result));

        if (purchaseDisplayDelay > 0f)
            yield return new WaitForSeconds(purchaseDisplayDelay);

        SetPriceText(result != null && result.purchased
            ? $"{result.salePrice:N0}円"
            : string.Empty);

        if (priceDisplayDelay > 0f)
            yield return new WaitForSeconds(priceDisplayDelay);

        SetCommentText(BuildComment(customer, result));

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

    /// <summary>
    /// お客の種類 × 満足度（または購入なし）に応じた3種類のセリフからランダムに選びます。
    /// </summary>
    private static string BuildComment(
        CustomerSystem.VisitingCustomer customer,
        CustomerPurchaseSystem.PurchaseResult result)
    {
        CustomerType type = customer?.data?.customerType ?? CustomerType.Housewife;

        if (result == null || !result.purchased)
            return GetNoPurchaseComment(type);

        return result.satisfactionLevel switch
        {
            CustomerPurchaseSystem.SatisfactionLevel.Best => GetBestComment(type),
            CustomerPurchaseSystem.SatisfactionLevel.Good => GetGoodComment(type),
            _ => GetOkayComment(type)
        };
    }

    private static string GetBestComment(CustomerType type)
    {
        return type switch
        {
            CustomerType.Housewife => PickRandom(
                "まあ、素敵！ 家に飾るのが楽しみね。",
                "これ、すごくいいわ。また見に来るわね。",
                "今日はいいお花に出会えたわ。"),
            CustomerType.Student => PickRandom(
                "これめっちゃいい！ 部屋に飾りたい！",
                "かわいい！ これにして正解かも。",
                "お、これ好き！ ネットに上げなきゃ"),
            CustomerType.Grandmother => PickRandom(
                "まあまあ、きれいねえ。大事に飾るわ。",
                "とっても素敵ね。いいものを選べたわ。",
                "こういうお花、好きなのよ。うれしいわ。"),
            CustomerType.Wealthy => PickRandom(
                "これは素晴らしい。実に気に入ったよ。",
                "いいね。こういうものを探していたんだ。",
                "見事だね。また良いものを見せてほしい。"),
            CustomerType.Child => PickRandom(
                "わあ！ これすっごくきれい！",
                "やったー！ このお花にする！",
                "これだいすき！ おうちにかざる！"),
            CustomerType.OfficeWorker => PickRandom(
                "これ、すごくいいですね。喜んでもらえそうです。",
                "いいものが見つかりました。助かりました。",
                "これは素敵ですね。またお願いしたいです。"),
            _ => "すごく素敵！"
        };
    }

    private static string GetGoodComment(CustomerType type)
    {
        return type switch
        {
            CustomerType.Housewife => PickRandom(
                "うん、これなら家に飾るのにちょうどいいわね。",
                "いい感じね。これにするわ。",
                "これなら長く楽しめそうね。"),
            CustomerType.Student => PickRandom(
                "いい感じ！ これにしよう。",
                "これなら予算もちょうどいいかな。",
                "うん、結構好きかも。これください。"),
            CustomerType.Grandmother => PickRandom(
                "きれいねえ。これをいただこうかしら。",
                "うん、いいお花ね。これにするわ。",
                "ちょうどよさそうね。ありがとう。"),
            CustomerType.Wealthy => PickRandom(
                "ほお、よい。これをいただこう。",
                "なかなかいいね。これにしよう。",
                "このくらいなら十分満足だよ。"),
            CustomerType.Child => PickRandom(
                "これかわいい！ これにする！",
                "うん！ このお花すき！",
                "きれいだね！ これください！"),
            CustomerType.OfficeWorker => PickRandom(
                "いいですね。これなら安心して渡せそうです。",
                "うん、これにしましょう。ちょうどよさそうです。",
                "これなら良さそうですね。お願いします。"),
            _ => "いい感じですね。"
        };
    }

    private static string GetOkayComment(CustomerType type)
    {
        return type switch
        {
            CustomerType.Housewife => PickRandom(
                "うん、今日はこれにしておこうかしら。",
                "悪くないわね。これをもらうわ。",
                "ちょうど欲しかったし、これにするわね。"),
            CustomerType.Student => PickRandom(
                "まあ、これならいいかな。",
                "うん、今日はこれにしとこう。",
                "強いて言うなら、これにします。"),
            CustomerType.Grandmother => PickRandom(
                "そうねえ、今日はこれにしましょう。",
                "うん、これならよさそうね。",
                "せっかくだし、これをいただくわ。"),
            CustomerType.Wealthy => PickRandom(
                "まあ、今日はこれにしておこう。",
                "悪くはないね。これをいただくよ。",
                "うん、今回はこれでいいだろう。"),
            CustomerType.Child => PickRandom(
                "うん、これにしようかな。",
                "えーっとぉ、これにする！",
                "じゃあ今日はこれにするね。"),
            CustomerType.OfficeWorker => PickRandom(
                "そうですね、今日はこれにします。",
                "時間もないし、これでお願いできますか。",
                "うん、これなら大丈夫そうですね。"),
            _ => "今回はこれにしよう。"
        };
    }

    private static string GetNoPurchaseComment(CustomerType type)
    {
        return type switch
        {
            CustomerType.Housewife => PickRandom(
                "今日は見るだけにしておこうかしら。",
                "また今度、ゆっくり選びに来るわね。",
                "今日は決めきれないわ。また来るわね。"),
            CustomerType.Student => PickRandom(
                "うーん、今日はやめとこうかな。",
                "もうちょっと考えてからにしよう。",
                "またお金ある時に見に来ようかな。"),
            CustomerType.Grandmother => PickRandom(
                "今日は見るだけにしておくわね。",
                "また今度、いい日に寄らせてもらうわ。",
                "今日は決めずに帰ろうかしらね。"),
            CustomerType.Wealthy => PickRandom(
                "今日は見送ろう。また寄らせてもらうよ。",
                "今回は決めずにおこう。",
                "また別の日に見せてもらおうかな。"),
            CustomerType.Child => PickRandom(
                "今日は見るだけにする！",
                "うーん、またこんどにする！",
                "どれにするか決められないや。"),
            CustomerType.OfficeWorker => PickRandom(
                "今日は決めずに、また寄ります。",
                "もう少し考えてみます。ありがとうございました。",
                "今回は見送ります。またお願いします。"),
            _ => "今日はやめておこうかな。"
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
