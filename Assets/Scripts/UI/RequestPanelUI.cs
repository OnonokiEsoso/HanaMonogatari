using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホームから開く依頼確認パネルです。
/// 提示中は「受ける / 断る」を表示し、受注後は依頼内容の確認専用になります。
/// パネルの初期表示/非表示はHierarchy側のActive状態で管理します。
/// </summary>
public class RequestPanelUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private RequestSystem requestSystem;

    [Header("パネル")]
    [SerializeField] private GameObject requestPanel;

    [Header("テキスト")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text requesterText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text deadlineText;
    [SerializeField] private TMP_Text rewardText;

    [Header("ボタン")]
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (acceptButton != null)
            acceptButton.onClick.AddListener(HandleAcceptClicked);

        if (declineButton != null)
            declineButton.onClick.AddListener(HandleDeclineClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        // ここでHidePanel()は呼ばない。
        // パネルがHierarchyで非アクティブ開始の場合、初回ShowPanel()でSetActive(true)になった瞬間に
        // Awakeが走り、その中で再び非表示にすると「1回目だけ開かない」状態になるため。
    }

    private void OnEnable()
    {
        if (requestSystem != null)
            requestSystem.OnRequestChanged += HandleRequestChanged;
    }

    private void OnDisable()
    {
        if (requestSystem != null)
            requestSystem.OnRequestChanged -= HandleRequestChanged;
    }

    private void OnDestroy()
    {
        if (acceptButton != null)
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);

        if (declineButton != null)
            declineButton.onClick.RemoveListener(HandleDeclineClicked);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    public void ShowPanel()
    {
        if (requestPanel != null)
            requestPanel.SetActive(true);
        else
            gameObject.SetActive(true);

        Refresh();
    }

    public void HidePanel()
    {
        if (requestPanel != null && requestPanel != gameObject)
            requestPanel.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void Refresh()
    {
        RequestData request = requestSystem != null ? requestSystem.CurrentRequest : null;

        if (request == null)
        {
            ShowNoRequestState();
            return;
        }

        if (titleText != null)
            titleText.text = request.title;

        if (requesterText != null)
            requesterText.text = $"依頼人：{request.requesterName}";

        if (descriptionText != null)
            descriptionText.text = BuildDescriptionText(request);

        if (deadlineText != null)
            deadlineText.text = BuildDeadlineText(request);

        if (rewardText != null)
            rewardText.text = BuildRewardText(request);

        bool isOffered = request.state == RequestState.Offered;

        if (acceptButton != null)
            acceptButton.gameObject.SetActive(isOffered);

        if (declineButton != null)
            declineButton.gameObject.SetActive(isOffered);

        if (closeButton != null)
            closeButton.gameObject.SetActive(true);
    }

    private void ShowNoRequestState()
    {
        if (titleText != null)
            titleText.text = "依頼";

        if (requesterText != null)
            requesterText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = "現在、依頼はありません。";

        if (deadlineText != null)
            deadlineText.text = string.Empty;

        if (rewardText != null)
            rewardText.text = string.Empty;

        if (acceptButton != null)
            acceptButton.gameObject.SetActive(false);

        if (declineButton != null)
            declineButton.gameObject.SetActive(false);

        if (closeButton != null)
            closeButton.gameObject.SetActive(true);
    }

    private void HandleAcceptClicked()
    {
        if (requestSystem == null)
            return;

        if (requestSystem.AcceptCurrentRequest())
            Refresh();
    }

    private void HandleDeclineClicked()
    {
        if (requestSystem == null)
            return;

        if (requestSystem.DeclineCurrentRequest())
            HidePanel();
    }

    private void HandleRequestChanged(RequestData request)
    {
        if (IsPanelVisible())
            Refresh();
    }

    private bool IsPanelVisible()
    {
        if (requestPanel != null)
            return requestPanel.activeInHierarchy;

        return gameObject.activeInHierarchy;
    }

    private static string BuildDescriptionText(RequestData request)
    {
        if (request == null)
            return string.Empty;

        bool hasMessage = !string.IsNullOrWhiteSpace(request.requesterMessage);
        bool hasCondition = !string.IsNullOrWhiteSpace(request.description);

        if (hasMessage && hasCondition)
            return $"{request.requesterMessage}\n\n【条件】\n{request.description}";

        if (hasMessage)
            return request.requesterMessage;

        return request.description ?? string.Empty;
    }

    private string BuildDeadlineText(RequestData request)
    {
        if (request == null)
            return string.Empty;

        if (request.state == RequestState.Offered)
        {
            if (!string.IsNullOrWhiteSpace(request.deadlineLabel))
                return $"期限：{request.deadlineLabel}";

            return request.durationDays <= 1
                ? "期限：受注した当日中"
                : $"期限：受注日を含めて{request.durationDays}日";
        }

        if (request.state == RequestState.Accepted && requestSystem != null)
        {
            int remainingDays = requestSystem.GetCurrentRequestRemainingDays();
            return remainingDays <= 1
                ? "期限：本日中"
                : $"期限：あと{remainingDays}日";
        }

        return string.Empty;
    }

    private static string BuildRewardText(RequestData request)
    {
        if (request == null)
            return string.Empty;

        if (request.requestType == RequestType.MysteryMessage)
            return "報酬：当日限定／全来店客が指定花を1つ追加購入";

        if (request.rewardShopRating > 0 && request.rewardVisitorBonusPercent > 0f && request.rewardVisitorBonusDays > 0)
        {
            int visitorPercent = Mathf.RoundToInt(request.rewardVisitorBonusPercent * 100f);
            string daysText = request.rewardVisitorBonusDays == 1
                ? "翌日"
                : $"翌日から{request.rewardVisitorBonusDays}日間";
            return $"報酬：店評価 +{request.rewardShopRating}、{daysText} 来客率 +{visitorPercent}%";
        }

        if (request.rewardShopRating > 0)
            return $"報酬：店評価 +{request.rewardShopRating}";

        return "報酬：---";
    }
}
