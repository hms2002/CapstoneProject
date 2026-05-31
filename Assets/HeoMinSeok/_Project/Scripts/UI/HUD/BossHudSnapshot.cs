using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 보스 HUD 슬롯이 표시할 단일 체력/그로기 채널 값을 담는다.
/// - HUD 계산 유틸리티와 슬롯 snapshot 사이의 임시 값 전달 형식을 제공한다.
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
/// 책임 :
/// - 보스 HUD 슬롯 하나가 한 프레임에 표시해야 하는 이름, 체력, 그로기 상태를 담는다.
/// - 다중 보스 HUD가 보스 구체 타입을 몰라도 슬롯 단위 표시만 갱신할 수 있게 한다.
/// </summary>
public struct BossHudSlotSnapshot
{
    public string DisplayName { get; private set; }
    public float HealthRatio { get; private set; }
    public bool HasGroggyGauge { get; private set; }
    public float GroggyRatio { get; private set; }
    public bool IsGroggy { get; private set; }
    public bool IsDefeated { get; private set; }
    public BossHudHealthBarTheme HealthBarTheme { get; private set; }

    public BossHudSlotSnapshot(
        string displayName,
        float healthRatio,
        bool hasGroggyGauge,
        float groggyRatio,
        bool isGroggy,
        bool isDefeated,
        BossHudHealthBarTheme healthBarTheme)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        HealthRatio = Mathf.Clamp01(healthRatio);
        HasGroggyGauge = hasGroggyGauge;
        GroggyRatio = Mathf.Clamp01(groggyRatio);
        IsGroggy = isGroggy;
        IsDefeated = isDefeated;
        HealthBarTheme = healthBarTheme;
    }

    public static BossHudSlotSnapshot FromBoss(
        BossControllerBase boss,
        string displayNameOverride,
        bool isDefeated,
        BossHudHealthBarTheme healthBarTheme)
    {
        string displayName = BossHudValueUtility.ResolveBossDisplayName(boss, displayNameOverride);
        if (isDefeated)
        {
            return new BossHudSlotSnapshot(
                displayName,
                0f,
                false,
                0f,
                false,
                true,
                healthBarTheme);
        }

        BossHudChannelSnapshot channel = BossHudValueUtility.BuildBossChannel(boss, null, true);
        return new BossHudSlotSnapshot(
            displayName,
            channel.HealthRatio,
            channel.HasGroggyGauge,
            channel.GroggyRatio,
            channel.IsGroggy,
            isDefeated,
            healthBarTheme);
    }
}

/// <summary>
/// 책임 :
/// - 보스 HUD 슬롯이 보스 객체에서 이름, 체력, 그로기 표시 값을 읽는 공통 계산을 제공한다.
/// - UI 계층이 보스별 구체 타입을 몰라도 단일 슬롯 snapshot을 만들 수 있게 한다.
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
