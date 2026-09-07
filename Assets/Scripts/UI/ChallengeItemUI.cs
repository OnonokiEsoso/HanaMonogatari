using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チャレンジ一覧に並ぶ1件分の表示です。
/// 題名・内容・受取ボタンだけで成立し、内容欄に進捗と報酬もまとめて表示します。
/// Inspector参照を優先し、未設定時だけ標準名・旧名から補完します。
/// </summary>
public class ChallengeItemUI : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("ChallengeItemのTitleTextを設定します。")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("ChallengeItemのDescriptionTextを設定します。")]
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("報酬を受け取るClaimButtonを設定します。旧PrefabのDevelopmentButtonも互換対象です。")]
    [SerializeField] private Button claimButton;
    [Tooltip("ClaimButton配下のLabelText。未設定ならClaimButton内から自動取得します。")]
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
        {
            string scope = challengeSystem != null ? challengeSystem.GetScopeLabel(challenge) : string.Empty;
            titleText.text = string.IsNullOrWhiteSpace(scope)
                ? challenge.title
                : $"【{scope}】{challenge.title}";
        }

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
                if (HasName(text, "TitleText", "ChallengeTitleText"))
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
                if (HasName(text, "DescriptionText", "ChallengeDescriptionText", "ContentText"))
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
                // DevelopmentButtonは旧ChallengeItem prefabとの互換用。
                if (HasName(button, "ClaimButton", "ReceiveButton", "DevelopmentButton"))
                {
                    claimButton = button;
                    break;
                }
            }
        }

        if (claimButtonText == null && claimButton != null)
        {
            TMP_Text[] buttonTexts = claimButton.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in buttonTexts)
            {
                if (HasName(text, "LabelText", "ButtonText", "Text (TMP)"))
                {
                    claimButtonText = text;
                    break;
                }
            }

            claimButtonText ??= claimButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private static bool HasName(Component component, params string[] names)
    {
        if (component == null)
            return false;

        foreach (string candidate in names)
        {
            if (component.gameObject.name == candidate)
                return true;
        }

        return false;
    }
}
