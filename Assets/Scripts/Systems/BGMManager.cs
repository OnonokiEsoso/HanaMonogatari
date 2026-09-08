using UnityEngine;

/// <summary>
/// ゲーム内の画面・状態ごとのBGMを一括管理します。
/// 現在のプロジェクトはMain_Scene内で各画面を切り替える構成なので、
/// UnityのSceneではなく「ゲーム内シーン（場面）」単位でBGMを切り替えます。
/// Inspectorの各AudioClip欄へBGM素材をドラッグ＆ドロップして使用します。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    private const string VolumePrefsKey = "BGMVolume";

    public enum BGMScene
    {
        None,
        Title,
        Home,
        Supplier,
        Business,
        DailyResult,
        MonthlyResult
    }

    [Header("BGM素材")]
    [SerializeField] private AudioClip titleBGM;
    [SerializeField] private AudioClip homeBGM;
    [SerializeField] private AudioClip supplierBGM;
    [SerializeField] private AudioClip businessBGM;
    [SerializeField] private AudioClip monthlyResultBGM;

    [Header("再生設定")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;
    [SerializeField] private BGMScene startScene = BGMScene.Home;

    private AudioSource audioSource;
    private BGMScene currentScene = BGMScene.None;

    public BGMScene CurrentScene => currentScene;
    public float Volume => volume;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefsKey, volume));
        audioSource.volume = volume;
    }

    private void Start()
    {
        Play(startScene);
    }

    /// <summary>
    /// 指定したゲーム内シーンのBGMへ切り替えます。
    /// 同じBGMをすでに再生中なら最初から再生し直しません。
    /// </summary>
    public void Play(BGMScene scene)
    {
        AudioClip clip = GetClip(scene);

        if (clip == null)
        {
            Debug.LogWarning($"BGMManager: {scene} のBGMが設定されていません。");
            return;
        }

        if (currentScene == scene && audioSource.clip == clip && audioSource.isPlaying)
            return;

        currentScene = scene;
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.Play();
    }

    public void PlayTitle() => Play(BGMScene.Title);
    public void PlayHome() => Play(BGMScene.Home);
    public void PlaySupplier() => Play(BGMScene.Supplier);
    public void PlayBusiness() => Play(BGMScene.Business);
    public void PlayDailyResult() => Play(BGMScene.Business);
    public void PlayMonthlyResult() => Play(BGMScene.MonthlyResult);

    public void Stop()
    {
        if (audioSource != null)
            audioSource.Stop();

        currentScene = BGMScene.None;
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
            audioSource.volume = volume;

        PlayerPrefs.SetFloat(VolumePrefsKey, volume);
        PlayerPrefs.Save();
    }

    private AudioClip GetClip(BGMScene scene)
    {
        return scene switch
        {
            BGMScene.Title => titleBGM,
            BGMScene.Home => homeBGM,
            BGMScene.Supplier => supplierBGM,
            BGMScene.Business => businessBGM,
            BGMScene.DailyResult => businessBGM,
            BGMScene.MonthlyResult => monthlyResultBGM,
            _ => null
        };
    }
}
