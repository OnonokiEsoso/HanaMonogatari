using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホームの設定ボタンから開く設定パネルです。
/// BGM音量・SE音量を0～100%のスライダーで変更します。
/// このコンポーネントは、非表示になるSettingsPanel自身ではなく
/// HomeDashboardなど常に有効なGameObjectへ付けて使用してください。
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private BGMManager bgmManager;
    [SerializeField] private SEManager seManager;

    [Header("パネル")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeButton;

    [Header("BGM音量")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TMP_Text bgmVolumeText;

    [Header("SE音量")]
    [SerializeField] private Slider seVolumeSlider;
    [SerializeField] private TMP_Text seVolumeText;

    private void Awake()
    {
        ResolveManagers();
        SetupSlider(bgmVolumeSlider);
        SetupSlider(seVolumeSlider);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(HandleBGMVolumeChanged);

        if (seVolumeSlider != null)
            seVolumeSlider.onValueChanged.AddListener(HandleSEVolumeChanged);

        SyncFromManagers();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(ShowPanel);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(HandleBGMVolumeChanged);

        if (seVolumeSlider != null)
            seVolumeSlider.onValueChanged.RemoveListener(HandleSEVolumeChanged);
    }

    public void ShowPanel()
    {
        ResolveManagers();
        SyncFromManagers();

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void HidePanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void HandleBGMVolumeChanged(float value)
    {
        ResolveManagers();
        bgmManager?.SetVolume(value);
        RefreshVolumeText(bgmVolumeText, value);
    }

    private void HandleSEVolumeChanged(float value)
    {
        ResolveManagers();
        seManager?.SetVolume(value);
        RefreshVolumeText(seVolumeText, value);
    }

    private void SyncFromManagers()
    {
        ResolveManagers();

        float bgmVolume = bgmManager != null ? bgmManager.Volume : 0.5f;
        float seVolume = seManager != null ? seManager.Volume : 0.7f;

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.SetValueWithoutNotify(bgmVolume);

        if (seVolumeSlider != null)
            seVolumeSlider.SetValueWithoutNotify(seVolume);

        RefreshVolumeText(bgmVolumeText, bgmVolume);
        RefreshVolumeText(seVolumeText, seVolume);
    }

    private void ResolveManagers()
    {
        if (bgmManager == null)
            bgmManager = FindFirstObjectByType<BGMManager>();

        if (seManager == null)
            seManager = FindFirstObjectByType<SEManager>();
    }

    private static void SetupSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private static void RefreshVolumeText(TMP_Text target, float value)
    {
        if (target != null)
            target.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }
}
