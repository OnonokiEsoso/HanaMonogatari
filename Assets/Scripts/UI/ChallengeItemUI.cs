using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チャレンジ一覧に並ぶ1件分の表示です。
/// 題名・内容・受取ボタンだけで成立し、内容欄に進捗と報酬もまとめて表示します。
/// </summary>
public class ChallengeItemUI : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonText;

    private ChallengeSystem challengeSystem;
    private ChallengeDefinition challenge;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(HandleClaimClicked);
    }

    public void Bind(ChallengeSystem system, ChallengeDefinition definition)
    {
        challengeSystem = system;
        challenge = definition;

        AutoFindReferences();

        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(HandleClaimClicked);
            claimButton.onClick.AddListener(HandleClaimClicked);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (challenge == null)
            return;

        if (titleText != null)
            titleText.text = challenge.title;

        if (descriptionText != null)
        {
            string progress = challengeSystem != null ? challengeSystem.GetProgressText(challenge) : string.Empty;
            string reward = challengeSystem != null ? challengeSystem.GetRewardText(challenge) : string.Empty;
            descriptionText.text = $"{challenge.description}\n進捗：{progress}\n{reward}";
        }

        bool claimed = challengeSystem != null && challengeSystem.IsClaimed(challenge);
        bool completed = challengeSystem != null && challengeSystem.IsCompleted(challenge);

        if (claimButton != null)
            claimButton.interactable = completed && !claimed;

        if (claimButtonText != null)
            claimButtonText.text = claimed ? "受取済み" : completed ? "受け取る" : "未達成";
    }

    private void HandleClaimClicked()
    {
        if (challengeSystem == null || challenge == null)
            return;

        challengeSystem.TryClaim(challenge);
        Refresh();
    }

    private void AutoFindReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        Button[] buttons = GetComponentsInChildren<Button>(true);

        if (titleText == null)
        {
            foreach (TMP_Text text in texts)
            {
                if (text.gameObject.name == "ChallengeTitleText" || text.gameObject.name == "TitleText")
                {
                    titleText = text;
                    break;
                }
            }
        }

        if (descriptionText == null)
        {
            foreach (TMP_Text text in texts)
            {
                if (text.gameObject.name == "ChallengeDescriptionText" || text.gameObject.name == "DescriptionText" || text.gameObject.name == "ContentText")
                {
                    descriptionText = text;
                    break;
                }
            }
        }

        if (claimButton == null)
        {
            foreach (Button button in buttons)
            {
                if (button.gameObject.name == "ClaimButton" || button.gameObject.name == "ReceiveButton")
                {
                    claimButton = button;
                    break;
                }
            }
        }

        if (claimButtonText == null && claimButton != null)
            claimButtonText = claimButton.GetComponentInChildren<TMP_Text>(true);
    }
}
