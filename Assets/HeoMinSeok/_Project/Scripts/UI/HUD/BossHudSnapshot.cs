using UnityEngine;
using UnityGAS;

/// <summary>
/// 보스 HUD가 보스 구체 타입을 몰라도 표시 값을 읽기 위한 계약입니다.
/// </summary>
public interface IBossHudSource
{
    int Priority { get; }
    bool OwnsBoss(BossControllerBase boss);
    bool TryBuildSnapshot(out BossHudSnapshot snapshot);
}

/// <summary>
/// 보스 HUD에 표시할 단일 체력/그로기 채널 값입니다.
/// </summary>
public struct BossHudChannelSnapshot
{
    public string Label { get; private set; }
    public float HealthRatio { get; private set; }
    public bool HasGroggyGauge { get; private set; }
    public float GroggyRatio { get; private set; }
    public bool IsGroggy { get; private set; }

    public BossHudChannelSnapshot(
        string label,
        float healthRatio,
        bool hasGroggyGauge,
        float groggyRatio,
        bool isGroggy)
    {
        Label = label;
        HealthRatio = Mathf.Clamp01(healthRatio);
        HasGroggyGauge = hasGroggyGauge;
        GroggyRatio = Mathf.Clamp01(groggyRatio);
        IsGroggy = isGroggy;
    }

    public static BossHudChannelSnapshot Empty(string label)
    {
        return new BossHudChannelSnapshot(label, 0f, false, 0f, false);
    }
}

/// <summary>
/// 보스 HUD 한 프레임에 필요한 표시 값 묶음입니다.
/// </summary>
public struct BossHudSnapshot
{
    public string DisplayName { get; private set; }
    public int ChannelCount { get; private set; }
    public BossHudChannelSnapshot PrimaryChannel { get; private set; }
    public BossHudChannelSnapshot SecondaryChannel { get; private set; }
    public bool UseSplitHealthPresentation { get; private set; }
    public string SplitHealthLeftLabel { get; private set; }
    public string SplitHealthRightLabel { get; private set; }

    public bool IsVisible => ChannelCount > 0;

    public bool HasAnyGroggyGauge =>
        (ChannelCount >= 1 && PrimaryChannel.HasGroggyGauge) ||
        (ChannelCount >= 2 && SecondaryChannel.HasGroggyGauge);

    public bool HasAnyGroggyChannel =>
        (ChannelCount >= 1 && PrimaryChannel.IsGroggy) ||
        (ChannelCount >= 2 && SecondaryChannel.IsGroggy);

    public static BossHudSnapshot Single(
        string displayName,
        BossHudChannelSnapshot channel,
        bool useSplitHealthPresentation,
        string splitHealthLeftLabel,
        string splitHealthRightLabel)
    {
        return new BossHudSnapshot
        {
            DisplayName = displayName,
            ChannelCount = 1,
            PrimaryChannel = channel,
            SecondaryChannel = BossHudChannelSnapshot.Empty(null),
            UseSplitHealthPresentation = useSplitHealthPresentation,
            SplitHealthLeftLabel = splitHealthLeftLabel,
            SplitHealthRightLabel = splitHealthRightLabel
        };
    }

    public static BossHudSnapshot Dual(
        string displayName,
        BossHudChannelSnapshot primaryChannel,
        BossHudChannelSnapshot secondaryChannel)
    {
        return new BossHudSnapshot
        {
            DisplayName = displayName,
            ChannelCount = 2,
            PrimaryChannel = primaryChannel,
            SecondaryChannel = secondaryChannel,
            UseSplitHealthPresentation = false,
            SplitHealthLeftLabel = null,
            SplitHealthRightLabel = null
        };
    }
}

/// <summary>
/// HUD source들이 공통으로 쓰는 보스 표시 값 계산 유틸리티입니다.
/// </summary>
public static class BossHudValueUtility
{
    public static BossHudChannelSnapshot BuildBossChannel(
        BossControllerBase boss,
        string label,
        bool showGroggyFallback)
    {
        if (boss == null)
            return BossHudChannelSnapshot.Empty(label);

        bool hasGroggyGauge = TryGetBossGroggyRatio(boss, out float groggyRatio, out bool isGroggy);
        if (!hasGroggyGauge && showGroggyFallback)
        {
            hasGroggyGauge = true;
            groggyRatio = 0f;
            isGroggy = boss.HasGroggyTag();
        }

        return new BossHudChannelSnapshot(
            label,
            boss.CurrentHealthRatio,
            hasGroggyGauge,
            groggyRatio,
            isGroggy);
    }

    public static bool TryGetBossGroggyRatio(BossControllerBase boss, out float ratio, out bool isGroggy)
    {
        ratio = 0f;
        isGroggy = false;

        if (boss == null)
            return false;

        StaggerGaugeSystem staggerGaugeSystem = boss.GetComponent<StaggerGaugeSystem>();
        if (staggerGaugeSystem == null)
            return false;

        bool hasGroggyTag = boss.HasGroggyTag();
        GameplayEffectRunner effectRunner = boss.GetComponent<GameplayEffectRunner>();
        GameplayEffect groggyEffect = staggerGaugeSystem.staggeredEffect;
        if (effectRunner != null && groggyEffect != null && groggyEffect.duration > 0f)
        {
            float remaining = effectRunner.GetRemainingTime(groggyEffect, boss.gameObject);
            if (remaining > 0.001f)
            {
                ratio = 1f - Mathf.Clamp01(remaining / groggyEffect.duration);
                isGroggy = true;
                return true;
            }
        }

        AttributeSet attributeSet = boss.AttributeSet;
        if (attributeSet == null)
            return false;

        if (staggerGaugeSystem.currentGaugeAttribute == null || staggerGaugeSystem.maxGaugeAttribute == null)
            return false;

        float current = attributeSet.GetAttributeValue(staggerGaugeSystem.currentGaugeAttribute);
        float max = attributeSet.GetAttributeValue(staggerGaugeSystem.maxGaugeAttribute);
        ratio = max > 0f ? 1f - Mathf.Clamp01(current / max) : 0f;
        isGroggy = hasGroggyTag;
        return true;
    }

    public static string ResolveBossDisplayName(BossControllerBase boss, string displayNameOverride)
    {
        if (boss == null)
            return string.Empty;

        string resolvedBossName = string.IsNullOrWhiteSpace(displayNameOverride)
            ? boss.EnemyName
            : displayNameOverride;

        if (string.IsNullOrWhiteSpace(resolvedBossName))
            resolvedBossName = boss.gameObject.name;

        return resolvedBossName;
    }
}

/// <summary>
/// 기존 단일 보스를 공통 HUD snapshot으로 변환하는 기본 source입니다.
/// </summary>
internal sealed class SingleBossHudSource : IBossHudSource
{
    private BossControllerBase boss;
    private string displayNameOverride;

    public int Priority => 0;

    public SingleBossHudSource Bind(BossControllerBase nextBoss, string nextDisplayNameOverride)
    {
        boss = nextBoss;
        displayNameOverride = nextDisplayNameOverride;
        return this;
    }

    public void Clear()
    {
        boss = null;
        displayNameOverride = null;
    }

    public bool OwnsBoss(BossControllerBase candidate)
    {
        return candidate != null && boss == candidate;
    }

    public bool TryBuildSnapshot(out BossHudSnapshot snapshot)
    {
        snapshot = default;
        if (boss == null)
            return false;

        IBossSplitHealthPresentation splitHealthPresentation = boss as IBossSplitHealthPresentation;
        bool useSplitHealth = splitHealthPresentation != null && splitHealthPresentation.ShowSplitHealthPresentation;
        snapshot = BossHudSnapshot.Single(
            BossHudValueUtility.ResolveBossDisplayName(boss, displayNameOverride),
            BossHudValueUtility.BuildBossChannel(boss, null, true),
            useSplitHealth,
            useSplitHealth ? splitHealthPresentation.SplitHealthLeftLabel : null,
            useSplitHealth ? splitHealthPresentation.SplitHealthRightLabel : null);
        return true;
    }
}
