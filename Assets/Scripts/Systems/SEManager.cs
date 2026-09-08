using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲーム内の効果音(SE)を一括管理します。
/// Inspectorの各AudioClip欄へSE素材をドラッグ＆ドロップして使用します。
/// 短い効果音を重ねて鳴らせるようにAudioSource.PlayOneShotを使用します。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SEManager : MonoBehaviour
{
    private const string VolumePrefsKey = "SEVolume";

    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private BouquetSystem bouquetSystem;
    [SerializeField] private ChallengeSystem challengeSystem;
    [SerializeField] private DevelopmentSystem developmentSystem;

    [Header("UI")]
    [SerializeField] private AudioClip buttonClickSE;
    [SerializeField] private AudioClip backSE;
    [SerializeField] private AudioClip tabChangeSE;
    [SerializeField] private AudioClip errorSE;

    [Header("お店・営業")]
    [SerializeField] private AudioClip openShopSE;
    [SerializeField] private AudioClip customerEnterSE;
    [SerializeField] private AudioClip purchaseSE;
    [SerializeField] private AudioClip closeShopSE;

    [Header("仕入れ・商品")]
    [SerializeField] private AudioClip supplierPurchaseSE;
    [SerializeField] private AudioClip priceChangeSE;
    [SerializeField] private AudioClip bouquetAddSE;
    [SerializeField] private AudioClip bouquetCompleteSE;
    [Tooltip("花束作成時にラッピングを使用した時の音です。")]
    [SerializeField] private AudioClip wrappingSE;

    [Header("達成・成長")]
    [SerializeField] private AudioClip challengeCompleteSE;
    [SerializeField] private AudioClip rewardClaimSE;
    [SerializeField] private AudioClip unlockSE;

    [Header("リザルト")]
    [SerializeField] private AudioClip resultCountSE;
    [SerializeField] private AudioClip resultCompleteSE;
    [Tooltip("月間リザルト画面が表示された瞬間の音です。")]
    [SerializeField] private AudioClip monthlyResultSE;

    [Header("再生設定")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    private AudioSource audioSource;
    private int lastBouquetCount;
    private bool lastDevelopmentFeatureUnlocked;
    private readonly HashSet<Button> boundButtons = new();
    private readonly HashSet<ChallengeDefinition> completedChallenges = new();
    private readonly HashSet<DevelopmentId> completedDevelopments = new();
    private Coroutine buttonBindingCoroutine;

    public float Volume => volume;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefsKey, volume));
        audioSource.volume = volume;

        ResolveReferences();

        lastBouquetCount = bouquetSystem != null ? bouquetSystem.Bouquets.Count : 0;
        lastDevelopmentFeatureUnlocked = developmentSystem != null && developmentSystem.IsDevelopmentFeatureUnlocked;
        CaptureCompletedChallenges(false);
        CaptureCompletedDevelopments(false);
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (shopManager != null)
            shopManager.OnStateChanged += HandleShopStateChanged;

        if (bouquetSystem != null)
            bouquetSystem.OnBouquetsChanged += HandleBouquetsChanged;

        if (challengeSystem != null)
            challengeSystem.OnChanged += HandleChallengeChanged;

        if (developmentSystem != null)
            developmentSystem.OnChanged += HandleDevelopmentChanged;

        if (buttonBindingCoroutine == null)
            buttonBindingCoroutine = StartCoroutine(ButtonBindingRoutine());
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleShopStateChanged;

        if (bouquetSystem != null)
            bouquetSystem.OnBouquetsChanged -= HandleBouquetsChanged;

        if (challengeSystem != null)
            challengeSystem.OnChanged -= HandleChallengeChanged;

        if (developmentSystem != null)
            developmentSystem.OnChanged -= HandleDevelopmentChanged;

        if (buttonBindingCoroutine != null)
        {
            StopCoroutine(buttonBindingCoroutine);
            buttonBindingCoroutine = null;
        }
    }

    /// <summary>
    /// 任意のAudioClipをSEとして1回再生します。
    /// PlayOneShotを使うため、すでに別のSEが鳴っていても重ねて再生できます。
    /// </summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayButtonClick() => Play(buttonClickSE);
    public void PlayBack() => Play(backSE);
    public void PlayTabChange() => Play(tabChangeSE);
    public void PlayError() => Play(errorSE);

    public void PlayOpenShop() => Play(openShopSE);
    public void PlayCustomerEnter() => Play(customerEnterSE);
    public void PlayPurchase() => Play(purchaseSE);
    public void PlayCloseShop() => Play(closeShopSE);

    public void PlaySupplierPurchase() => Play(supplierPurchaseSE);
    public void PlayPriceChange() => Play(priceChangeSE);
    public void PlayBouquetAdd() => Play(bouquetAddSE);
    public void PlayBouquetComplete() => Play(bouquetCompleteSE);
    public void PlayWrapping() => Play(wrappingSE);

    public void PlayChallengeComplete() => Play(challengeCompleteSE);
    public void PlayRewardClaim() => Play(rewardClaimSE);
    public void PlayUnlock() => Play(unlockSE);

    public void PlayResultCount() => Play(resultCountSE);
    public void PlayResultComplete() => Play(resultCompleteSE);
    public void PlayMonthlyResult() => Play(monthlyResultSE);

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
            audioSource.volume = volume;

        PlayerPrefs.SetFloat(VolumePrefsKey, volume);
        PlayerPrefs.Save();
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();

        if (bouquetSystem == null)
            bouquetSystem = FindFirstObjectByType<BouquetSystem>();

        if (challengeSystem == null)
            challengeSystem = FindFirstObjectByType<ChallengeSystem>();

        if (developmentSystem == null)
            developmentSystem = FindFirstObjectByType<DevelopmentSystem>();
    }

    private IEnumerator ButtonBindingRoutine()
    {
        while (true)
        {
            BindNewButtons();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private void BindNewButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button == null || boundButtons.Contains(button))
                continue;

            boundButtons.Add(button);
            Button capturedButton = button;
            capturedButton.onClick.AddListener(() => PlayButtonSound(capturedButton));
        }

        boundButtons.RemoveWhere(button => button == null);
    }

    private void PlayButtonSound(Button button)
    {
        if (button == null)
            return;

        string objectName = button.gameObject.name ?? string.Empty;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        string labelText = label != null ? label.text ?? string.Empty : string.Empty;
        string combined = objectName + " " + labelText;

        if (ContainsAny(combined, "開店する", "OpenShop"))
        {
            PlayOpenShop();
            return;
        }

        if (ContainsAny(combined, "閉店する", "CloseShop"))
        {
            PlayCloseShop();
            return;
        }

        if (ContainsAny(combined, "Back", "Close", "Cancel", "戻る", "閉じる", "キャンセル"))
        {
            PlayBack();
            return;
        }

        if (ContainsAny(combined, "Tab", "タブ"))
        {
            PlayTabChange();
            return;
        }

        PlayButtonClick();
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        if (string.IsNullOrEmpty(source))
            return false;

        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value) && source.Contains(value, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void HandleShopStateChanged()
    {
        if (developmentSystem != null)
        {
            bool featureUnlocked = developmentSystem.IsDevelopmentFeatureUnlocked;
            if (featureUnlocked && !lastDevelopmentFeatureUnlocked)
                PlayUnlock();
            lastDevelopmentFeatureUnlocked = featureUnlocked;
        }
    }

    private void HandleBouquetsChanged()
    {
        if (bouquetSystem == null)
            return;

        int currentCount = bouquetSystem.Bouquets.Count;
        if (currentCount > lastBouquetCount)
            PlayWrapping();

        lastBouquetCount = currentCount;
    }

    private void HandleChallengeChanged()
    {
        CaptureCompletedChallenges(true);
    }

    private void CaptureCompletedChallenges(bool playNewCompletion)
    {
        if (challengeSystem == null)
            return;

        HashSet<ChallengeDefinition> current = new();
        bool foundNewCompletion = false;

        foreach (ChallengeDefinition challenge in challengeSystem.GetVisibleChallenges())
        {
            if (challenge == null || !challengeSystem.IsCompleted(challenge))
                continue;

            current.Add(challenge);
            if (!completedChallenges.Contains(challenge))
                foundNewCompletion = true;
        }

        completedChallenges.Clear();
        foreach (ChallengeDefinition challenge in current)
            completedChallenges.Add(challenge);

        if (playNewCompletion && foundNewCompletion)
            PlayChallengeComplete();
    }

    private void HandleDevelopmentChanged()
    {
        CaptureCompletedDevelopments(true);
    }

    private void CaptureCompletedDevelopments(bool playNewCompletion)
    {
        if (developmentSystem == null)
            return;

        HashSet<DevelopmentId> current = new();
        bool foundNewCompletion = false;

        foreach (DevelopmentDefinition definition in developmentSystem.Definitions)
        {
            if (definition == null || !developmentSystem.IsCompleted(definition.id))
                continue;

            current.Add(definition.id);
            if (!completedDevelopments.Contains(definition.id))
                foundNewCompletion = true;
        }

        completedDevelopments.Clear();
        foreach (DevelopmentId id in current)
            completedDevelopments.Add(id);

        if (playNewCompletion && foundNewCompletion)
            PlayUnlock();
    }
}
