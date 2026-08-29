using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 営業画面の見た目を担当します。
/// 客を右側からレジ前へ移動させ、購入内容・金額・一言を表示したあと右側へ退店させます。
/// </summary>
public class SalesVisualController : MonoBehaviour
{
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
    [SerializeField] private TMP_Text purchaseText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text commentText;

    [Header("移動位置")]
    [Tooltip("レジ前に停止するX座標。客画像ごとの現在Y座標はそのまま使います。")]
    [SerializeField] private float counterX = 120f;
    [Tooltip("画面右外のX座標。入店開始位置と退店先に使います。")]
    [SerializeField] private float outsideRightX = 850f;

    [Header("演出時間")]
    [Min(0.05f)] [SerializeField] private float enterDuration = 0.65f;
    [Min(0f)] [SerializeField] private float purchaseDisplayDelay = 0.35f;
    [Min(0f)] [SerializeField] private float priceDisplayDelay = 0.45f;
    [Min(0f)] [SerializeField] private float commentDisplayDuration = 1.1f;
    [Min(0.05f)] [SerializeField] private float exitDuration = 0.65f;

    private RectTransform activeCustomer;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        HideAllCustomers();
        ClearCheckoutDisplay();
    }

    /// <summary>
    /// PlayCustomerSequence（プレイ・カスタマー・シークエンス）
    /// 客の入店→会計→一言→退店を順番に再生します。
    /// </summary>
    public IEnumerator PlayCustomerSequence(
        CustomerSystem.VisitingCustomer customer,
        CustomerPurchaseSystem.PurchaseResult result)
    {
        if (isPlaying)
            yield break;

        isPlaying = true;
        ClearCheckoutDisplay();
        HideAllCustomers();

        activeCustomer = GetCustomerImage(customer?.data?.customerType ?? CustomerType.Housewife);
        if (activeCustomer == null)
        {
            Debug.LogWarning("SalesVisualController: 対応する客画像が設定されていません。");
            isPlaying = false;
            yield break;
        }

        Vector2 current = activeCustomer.anchoredPosition;
        activeCustomer.anchoredPosition = new Vector2(outsideRightX, current.y);
        activeCustomer.gameObject.SetActive(true);

        yield return MoveX(activeCustomer, counterX, enterDuration);

        if (speechBubble != null)
            speechBubble.SetActive(true);

        if (purchaseText != null)
            purchaseText.text = BuildPurchaseText(result);

        if (purchaseDisplayDelay > 0f)
            yield return new WaitForSeconds(purchaseDisplayDelay);

        if (priceText != null)
            priceText.text = result != null && result.purchased
                ? $"{result.salePrice:N0}円"
                : string.Empty;

        if (priceDisplayDelay > 0f)
            yield return new WaitForSeconds(priceDisplayDelay);

        if (commentText != null)
            commentText.text = BuildComment(result);

        if (commentDisplayDuration > 0f)
            yield return new WaitForSeconds(commentDisplayDuration);

        ClearCheckoutDisplay();
        yield return MoveX(activeCustomer, outsideRightX, exitDuration);

        activeCustomer.gameObject.SetActive(false);
        activeCustomer = null;
        isPlaying = false;
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
            t = t * t * (3f - 2f * t); // SmoothStep
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

        // PurchaseResult.messageには「客名（目的）が［購入内容］を購入　合計...」の形で購入内容が入っています。
        // 複数種類購入にも対応するため、その部分だけを営業画面用に抜き出します。
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

    private void ClearCheckoutDisplay()
    {
        if (speechBubble != null)
            speechBubble.SetActive(false);

        if (purchaseText != null)
            purchaseText.text = string.Empty;

        if (priceText != null)
            priceText.text = string.Empty;

        if (commentText != null)
            commentText.text = string.Empty;
    }

    private static void SetActive(RectTransform target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }
}
