using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホームのチャレンジボタンから開く一覧パネルです。
/// ChallengeSystemの定義をChallengeItemUIプレハブとして縦に並べます。
/// </summary>
public class ChallengePanelUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ChallengeSystem challengeSystem;

    [Header("パネル")]
    [SerializeField] private GameObject challengePanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text monthTitleText;

    [Header("一覧")]
    [SerializeField] private Transform challengeListContent;
    [SerializeField] private ChallengeItemUI challengeItemPrefab;

    private readonly List<ChallengeItemUI> spawnedItems = new();

    public bool IsVisible => GetPanelRoot() != null && GetPanelRoot().activeSelf;

    private void Awake()
    {
        ResolveReferences();
        AutoFindReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (challengeSystem != null)
            challengeSystem.OnChanged += HandleChallengeChanged;
    }

    private void OnDisable()
    {
        if (challengeSystem != null)
            challengeSystem.OnChanged -= HandleChallengeChanged;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    public void ShowPanel()
    {
        ResolveReferences();
        AutoFindReferences();

        GameObject root = GetPanelRoot();
        if (root != null)
            root.SetActive(true);

        RefreshAll();
    }

    public void HidePanel()
    {
        GameObject root = GetPanelRoot();
        if (root != null)
            root.SetActive(false);
    }

    public void RefreshAll()
    {
        RefreshTitle();
        RebuildList();
    }

    private void RefreshTitle()
    {
        if (monthTitleText == null)
            return;

        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        monthTitleText.text = shopManager != null
            ? $"{shopManager.CurrentMonth}月のチャレンジ"
            : "チャレンジ";
    }

    private void RebuildList()
    {
        foreach (ChallengeItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (challengeSystem == null || challengeListContent == null || challengeItemPrefab == null)
            return;

        foreach (ChallengeDefinition challenge in challengeSystem.Challenges)
        {
            if (challenge == null)
                continue;

            ChallengeItemUI item = Instantiate(challengeItemPrefab, challengeListContent);
            item.gameObject.SetActive(true);
            item.Bind(challengeSystem, challenge);
            spawnedItems.Add(item);
        }
    }

    private void HandleChallengeChanged()
    {
        if (!IsVisible)
            return;

        foreach (ChallengeItemUI item in spawnedItems)
            if (item != null)
                item.Refresh();

        RefreshTitle();
    }

    private GameObject GetPanelRoot()
    {
        return challengePanel != null ? challengePanel : gameObject;
    }

    private void ResolveReferences()
    {
        if (challengeSystem == null)
            challengeSystem = FindFirstObjectByType<ChallengeSystem>();
    }

    private void AutoFindReferences()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        Button[] buttons = GetComponentsInChildren<Button>(true);
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (closeButton == null)
        {
            foreach (Button button in buttons)
            {
                if (button.gameObject.name == "CloseButton" || button.gameObject.name == "BackButton")
                {
                    closeButton = button;
                    break;
                }
            }
        }

        if (monthTitleText == null)
        {
            foreach (TMP_Text text in texts)
            {
                if (text.gameObject.name == "TitleText" || text.gameObject.name == "ChallengeTitleText")
                {
                    monthTitleText = text;
                    break;
                }
            }
        }

        if (challengeListContent == null)
        {
            foreach (Transform t in transforms)
            {
                if (t.gameObject.name == "Content" || t.gameObject.name == "ChallengeContent")
                {
                    challengeListContent = t;
                    break;
                }
            }
        }
    }
}
