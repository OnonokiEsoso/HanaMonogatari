using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ChallengeType
{
    MonthlySales,
    MonthlyBuyers,
    OwnFurniture
}

public enum ChallengeRewardType
{
    Money,
    ShopRating,
    Wrapping
}

[Serializable]
public class ChallengeDefinition
{
    public string id;
    public string title;
    [TextArea] public string description;
    public ChallengeType type;
    [Min(1)] public int targetValue = 1;
    public ChallengeRewardType rewardType;
    [Min(1)] public int rewardAmount = 1;
}

/// <summary>
/// 月ごとのチャレンジを管理します。
/// 現在は3件を基本セットとして表示し、達成後に報酬を受け取れます。
/// 月が変わると受取状態をリセットします。
/// </summary>
public class ChallengeSystem : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private FurnitureSystem furnitureSystem;
    [SerializeField] private BouquetSystem bouquetSystem;

    [Header("チャレンジ定義")]
    [SerializeField] private List<ChallengeDefinition> challenges = new();

    [Header("今月の状態")]
    [SerializeField] private List<string> claimedChallengeIds = new();
    [SerializeField] private int observedYear = -1;
    [SerializeField] private int observedMonth = -1;

    public IReadOnlyList<ChallengeDefinition> Challenges => challenges;

    public event Action OnChanged;

    private void Awake()
    {
        ResolveReferences();
        EnsureDefaultChallenges();
        SyncMonthIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (shopManager != null)
            shopManager.OnStateChanged += HandleStateChanged;
        if (furnitureSystem != null)
            furnitureSystem.OnChanged += HandleStateChanged;

        SyncMonthIfNeeded();
    }

    private void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnStateChanged -= HandleStateChanged;
        if (furnitureSystem != null)
            furnitureSystem.OnChanged -= HandleStateChanged;
    }

    public int GetCurrentValue(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return 0;

        ResolveReferences();

        return challenge.type switch
        {
            ChallengeType.MonthlySales => shopManager != null ? Mathf.Max(0, shopManager.MonthlySales) : 0,
            ChallengeType.MonthlyBuyers => shopManager != null ? Mathf.Max(0, shopManager.MonthlyBuyers) : 0,
            ChallengeType.OwnFurniture => furnitureSystem != null ? Mathf.Max(0, furnitureSystem.OwnedCount) : 0,
            _ => 0
        };
    }

    public bool IsCompleted(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return false;
        return GetCurrentValue(challenge) >= Mathf.Max(1, challenge.targetValue);
    }

    public bool IsClaimed(ChallengeDefinition challenge)
    {
        if (challenge == null || string.IsNullOrWhiteSpace(challenge.id))
            return false;
        return claimedChallengeIds != null && claimedChallengeIds.Contains(challenge.id);
    }

    public bool TryClaim(ChallengeDefinition challenge)
    {
        if (challenge == null || IsClaimed(challenge) || !IsCompleted(challenge))
            return false;

        ResolveReferences();

        switch (challenge.rewardType)
        {
            case ChallengeRewardType.Money:
                shopManager?.AddMoney(challenge.rewardAmount);
                break;
            case ChallengeRewardType.ShopRating:
                shopManager?.AddShopRating(challenge.rewardAmount);
                break;
            case ChallengeRewardType.Wrapping:
                bouquetSystem?.AddWrapping(challenge.rewardAmount);
                break;
        }

        claimedChallengeIds ??= new List<string>();
        claimedChallengeIds.Add(challenge.id);
        OnChanged?.Invoke();

        Debug.Log($"チャレンジ達成報酬を受け取りました：{challenge.title} / {GetRewardText(challenge)}");
        return true;
    }

    public string GetProgressText(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return string.Empty;

        int current = GetCurrentValue(challenge);
        int target = Mathf.Max(1, challenge.targetValue);

        return challenge.type switch
        {
            ChallengeType.MonthlySales => $"{Mathf.Min(current, target):N0} / {target:N0}円",
            ChallengeType.MonthlyBuyers => $"{Mathf.Min(current, target):N0} / {target:N0}人",
            ChallengeType.OwnFurniture => $"{Mathf.Min(current, target):N0} / {target:N0}個",
            _ => $"{Mathf.Min(current, target):N0} / {target:N0}"
        };
    }

    public string GetRewardText(ChallengeDefinition challenge)
    {
        if (challenge == null)
            return string.Empty;

        return challenge.rewardType switch
        {
            ChallengeRewardType.Money => $"報酬：{challenge.rewardAmount:N0}円",
            ChallengeRewardType.ShopRating => $"報酬：店評価 +{challenge.rewardAmount:N0}",
            ChallengeRewardType.Wrapping => $"報酬：ラッピング ×{challenge.rewardAmount:N0}",
            _ => string.Empty
        };
    }

    private void HandleStateChanged()
    {
        SyncMonthIfNeeded();
        OnChanged?.Invoke();
    }

    private void SyncMonthIfNeeded()
    {
        if (shopManager == null)
            return;

        int year = shopManager.GameYear;
        int month = shopManager.CurrentMonth;

        if (observedYear < 0 || observedMonth < 0)
        {
            observedYear = year;
            observedMonth = month;
            return;
        }

        if (observedYear == year && observedMonth == month)
            return;

        observedYear = year;
        observedMonth = month;
        claimedChallengeIds ??= new List<string>();
        claimedChallengeIds.Clear();
        OnChanged?.Invoke();
    }

    private void EnsureDefaultChallenges()
    {
        if (challenges != null && challenges.Count > 0)
            return;

        challenges = new List<ChallengeDefinition>
        {
            new()
            {
                id = "monthly_buyers_20",
                title = "はじめての繁盛店",
                description = "今月20人のお客さんに商品を買ってもらおう。",
                type = ChallengeType.MonthlyBuyers,
                targetValue = 20,
                rewardType = ChallengeRewardType.ShopRating,
                rewardAmount = 50
            },
            new()
            {
                id = "monthly_sales_15000",
                title = "売上を伸ばそう",
                description = "今月の売上を15,000円以上にしよう。",
                type = ChallengeType.MonthlySales,
                targetValue = 15000,
                rewardType = ChallengeRewardType.Money,
                rewardAmount = 3000
            },
            new()
            {
                id = "own_furniture_1",
                title = "お店を飾ってみよう",
                description = "家具を1個購入してみよう。",
                type = ChallengeType.OwnFurniture,
                targetValue = 1,
                rewardType = ChallengeRewardType.Wrapping,
                rewardAmount = 2
            }
        };
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();
        if (furnitureSystem == null)
            furnitureSystem = FindFirstObjectByType<FurnitureSystem>();
        if (bouquetSystem == null)
            bouquetSystem = FindFirstObjectByType<BouquetSystem>();
    }
}
