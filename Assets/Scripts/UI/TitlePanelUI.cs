using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面を管理します。
/// 「はじめから」は現在のシーンを再読み込みして完全な初期状態からゲームを開始します。
/// セーブ機能は未実装のため、「つづきから」は現時点では無効化します。
/// </summary>
public class TitlePanelUI : MonoBehaviour
{
    [Header("タイトル")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private TMP_Text versionText;
    [Tooltip("空ならApplication.versionを使用します。verは自動で付与します。")]
    [SerializeField] private string versionOverride = "0.0.9";

    [Header("タイトルボタン")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("ゲーム画面")]
    [SerializeField] private HomeDashboardUI homeDashboardUI;
    [SerializeField] private ShopTabUI shopTabUI;

    [Header("設定")]
    [Tooltip("既存のSettingsPanelUIをそのままタイトルから開きます。")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;

    [Header("クレジット")]
    [Tooltip("任意。CreditsPanelを作った場合に設定します。")]
    [SerializeField] private GameObject creditsPanel;
    [Tooltip("任意。CreditsPanel内の閉じるボタン。")]
    [SerializeField] private Button creditsCloseButton;

    private static bool enterGameAfterSceneReload;
    private bool sceneReloadRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        enterGameAfterSceneReload = false;
    }

    private void Awake()
    {
        AutoFindReferences();

        if (newGameButton != null)
            newGameButton.onClick.AddListener(HandleNewGameClicked);
        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(HandleSettingsClicked);
        if (creditsButton != null)
            creditsButton.onClick.AddListener(HandleCreditsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitClicked);
        if (creditsCloseButton != null)
            creditsCloseButton.onClick.AddListener(HideCredits);

        RefreshVersionText();

        // セーブシステムをまだ持っていないため、見た目だけ押せる状態にはしない。
        if (continueButton != null)
            continueButton.interactable = false;

        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    private IEnumerator Start()
    {
        // ShopTabUI等の起動時表示が終わってから、タイトル/ゲーム画面の最終状態を決めます。
        yield return null;
        yield return null;

        if (enterGameAfterSceneReload)
        {
            enterGameAfterSceneReload = false;
            EnterGame();
        }
        else
        {
            ShowTitle();
        }
    }

    private void OnDestroy()
    {
        if (newGameButton != null)
            newGameButton.onClick.RemoveListener(HandleNewGameClicked);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinueClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(HandleCreditsClicked);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitClicked);
        if (creditsCloseButton != null)
            creditsCloseButton.onClick.RemoveListener(HideCredits);
    }

    public void ShowTitle()
    {
        ResolveGameplayReferences();
        homeDashboardUI?.HideHome();

        if (titlePanel != null)
            titlePanel.SetActive(true);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        RefreshVersionText();
    }

    private void EnterGame()
    {
        ResolveGameplayReferences();

        if (titlePanel != null)
            titlePanel.SetActive(false);
        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        // 開店タブのホームを正式な開始画面として表示します。
        if (shopTabUI != null)
            shopTabUI.ShowBusinessHome();
        else
            homeDashboardUI?.ShowHome();
    }

    private void HandleNewGameClicked()
    {
        if (sceneReloadRequested)
            return;

        sceneReloadRequested = true;
        enterGameAfterSceneReload = true;

        if (newGameButton != null)
            newGameButton.interactable = false;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        // Build Settings外から実行している場合の保険。
        if (!string.IsNullOrWhiteSpace(activeScene.name))
        {
            SceneManager.LoadScene(activeScene.name);
            return;
        }

        Debug.LogError("TitlePanelUI: 現在のシーンを再読み込みできませんでした。");
        sceneReloadRequested = false;
        enterGameAfterSceneReload = false;
        if (newGameButton != null)
            newGameButton.interactable = true;
    }

    private void HandleContinueClicked()
    {
        // セーブ/ロード実装後に接続する入口だけ用意しています。
        Debug.Log("TitlePanelUI: つづきからはセーブシステム実装後に使用できます。");
    }

    private void HandleSettingsClicked()
    {
        if (settingsPanelUI == null)
            settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>(FindObjectsInactive.Include);

        if (settingsPanelUI != null)
        {
            settingsPanelUI.ShowPanel();
            return;
        }

        Debug.LogWarning("TitlePanelUI: SettingsPanelUIが見つかりません。Inspectorで設定してください。");
    }

    private void HandleCreditsClicked()
    {
        if (creditsPanel == null)
        {
            Debug.LogWarning("TitlePanelUI: CreditsPanelが未設定です。");
            return;
        }

        creditsPanel.SetActive(true);
    }

    public void HideCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    private void HandleQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RefreshVersionText()
    {
        if (versionText == null)
            return;

        string version = string.IsNullOrWhiteSpace(versionOverride)
            ? Application.version
            : versionOverride.Trim();

        versionText.text = $"ver {version}";
    }

    private void ResolveGameplayReferences()
    {
        if (homeDashboardUI == null)
            homeDashboardUI = FindFirstObjectByType<HomeDashboardUI>(FindObjectsInactive.Include);
        if (shopTabUI == null)
            shopTabUI = FindFirstObjectByType<ShopTabUI>(FindObjectsInactive.Include);
    }

    private void AutoFindReferences()
    {
        if (titlePanel == null)
            titlePanel = gameObject;

        Button[] buttons = titlePanel.GetComponentsInChildren<Button>(true);
        TMP_Text[] texts = titlePanel.GetComponentsInChildren<TMP_Text>(true);

        if (newGameButton == null)
            newGameButton = buttons.FirstOrDefault(x => x.gameObject.name == "NewGameButton");
        if (continueButton == null)
            continueButton = buttons.FirstOrDefault(x => x.gameObject.name == "ContinueButton");
        if (settingsButton == null)
            settingsButton = buttons.FirstOrDefault(x => x.gameObject.name == "SettingsButton");
        if (creditsButton == null)
            creditsButton = buttons.FirstOrDefault(x => x.gameObject.name == "CreditsButton");
        if (quitButton == null)
            quitButton = buttons.FirstOrDefault(x => x.gameObject.name == "QuitButton");
        if (versionText == null)
            versionText = texts.FirstOrDefault(x => x.gameObject.name == "VersionText");

        ResolveGameplayReferences();

        if (settingsPanelUI == null)
            settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>(FindObjectsInactive.Include);
    }
}
