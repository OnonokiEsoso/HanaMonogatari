using UnityEngine;

/// <summary>
/// ゲーム内の効果音(SE)を一括管理します。
/// Inspectorの各AudioClip欄へSE素材をドラッグ＆ドロップして使用します。
/// 短い効果音を重ねて鳴らせるようにAudioSource.PlayOneShotを使用します。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SEManager : MonoBehaviour
{
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

    [Header("達成・成長")]
    [SerializeField] private AudioClip challengeCompleteSE;
    [SerializeField] private AudioClip rewardClaimSE;
    [SerializeField] private AudioClip levelUpSE;
    [SerializeField] private AudioClip unlockSE;

    [Header("リザルト")]
    [SerializeField] private AudioClip resultCountSE;
    [SerializeField] private AudioClip resultCompleteSE;

    [Header("再生設定")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = volume;
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

    public void PlayChallengeComplete() => Play(challengeCompleteSE);
    public void PlayRewardClaim() => Play(rewardClaimSE);
    public void PlayLevelUp() => Play(levelUpSE);
    public void PlayUnlock() => Play(unlockSE);

    public void PlayResultCount() => Play(resultCountSE);
    public void PlayResultComplete() => Play(resultCompleteSE);

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
            audioSource.volume = volume;
    }
}
