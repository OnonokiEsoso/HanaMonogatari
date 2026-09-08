using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホームのチャレンジボタンから開く一覧パネルです。
/// デイリー1件・月間2～3件・通算の各系列の現在段階を一覧表示します。
/// Inspector参照を優先し、未設定時だけ標準名から補完します。
/// </summary>
public class ChallengePanelUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ChallengeSystem challengeSystem;

    [Header("パネル")]
    [Tooltip("ChallengePanel本体。未設定ならこのGameObjectを使用します。")]
    [SerializeField] private GameObject challengePanel;
    [Tooltip("ChallengePanelを閉じるCloseButton。")]
    [SerializeField] private Button closeButton;
    [Tooltip("ChallengePanel上部のTitleText。")]
    [SerializeField] private TMP_Text monthTitleText;

    [Header("一覧")]
    [Tooltip("ChallengeScrollView / Viewport / Content を設定します。")]
    [SerializeField] private Transform challengeListContent;
    [Tooltip("一覧へ複製するChallengeItem prefab。")]
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
            ? $"チャレンジ　{shopManager.CurrentMonth}月 {shopManager.CurrentDay}/{ShopManager.DaysPerMonth}日"
            : "チャレンジ";
    }

    private void RebuildList()
    {
        ClearSpawnedItems();

        if (challengeSystem == null || challengeListContent == null || challengeItemPrefab == null)
            return;

        List<ChallengeDefinition> visibleChallenges = new();
        foreach (ChallengeDefinition challenge in challengeSystem.GetVisibleChallenges())
        {
            if (challenge != null)
                visibleChallenges.Add(challenge);
        }

        // クリア済みだけを先頭へ移動し、クリア済み同士・未クリア同士の元の順番は維持する。
        foreach (ChallengeDefinition challenge in visibleChallenges)
        {
            if (challengeSystem.IsCompleted(challenge))
                SpawnChallengeItem(challenge);
        }

        foreach (ChallengeDefinition challenge in visibleChallenges)
        {
            if (!challengeSystem.IsCompleted(challenge))
                SpawnChallengeItem(challenge);
        }
    }

    private void SpawnChallengeItem(ChallengeDefinition challenge)
    {
        ChallengeItemUI item = Instantiate(challengeItemPrefab, challengeListContent);
        item.gameObject.SetActive(true);
        item.Bind(challengeSystem, challenge);
        spawnedItems.Add(item);
    }

    private void ClearSpawnedItems()
    {
        foreach (ChallengeItemUI item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        spawnedItems.Clear();
    }

    private void HandleChallengeChanged()
    {
        if (!IsVisible)
            return;

        // 通算チャレンジは受取直後に次段階へ差し替わるので一覧を作り直す。
        RefreshAll();
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
                if (HasName(button, "CloseButton", "BackButton"))
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
                if (HasName(text, "TitleText", "ChallengeTitleText"))
                {
                    monthTitleText = text;
                    break;
                }
            }
        }

        if (challengeListContent == null)
        {
            foreach (Transform item in transforms)
            {
                if (HasName(item, "Content", "ChallengeContent", "ListContent"))
                {
                    challengeListContent = item;
                    break;
                }
            }
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
