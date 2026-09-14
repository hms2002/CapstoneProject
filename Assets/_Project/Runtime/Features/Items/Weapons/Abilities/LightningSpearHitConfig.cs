using System;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

/// <summary>
/// 번개 창 스킬이 생성할 히트박스와 전투 피해 페이로드 설정을 보관할 책임을 가집니다.
/// </summary>
[Serializable]
public sealed class LightningSpearHitConfig
{
    [Header("Actor")]
    [SerializeField] private MeleeHitboxActor hitboxPrefab;
    [SerializeField, Min(0.01f)] private float activeTime = 0.1f;
    [SerializeField] private Vector2 hitboxSize = new Vector2(2f, 1f);
    [SerializeField] private float forwardOffset = 1f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private LayerMask wallLayers;

    [Header("Hit Impact")]
    [SerializeField] private HitImpactCueKind hitImpactCueKind;

    [Header("Damage")]
    [SerializeField] private UnityGAS.DamagePayloadConfig damageConfig = new UnityGAS.DamagePayloadConfig();
    [SerializeField] private GameplayEffect damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private ScaledStatFormula damageFormula;
    [SerializeField] private ScaledStatFormula knockbackFormula;
    [SerializeField, Min(0f)] private float legacyDamage = 8f;
    [SerializeField, Min(0f)] private float legacyStaggerDamage;
    [SerializeField] private GameplayTag hitConfirmedTag;

    public MeleeHitboxActor HitboxPrefab => hitboxPrefab;
    public float ActiveTime => Mathf.Max(0.01f, activeTime);
    public Vector2 HitboxSize => new Vector2(Mathf.Max(0.01f, hitboxSize.x), Mathf.Max(0.01f, hitboxSize.y));
    public float ForwardOffset => forwardOffset;
    public LayerMask HitLayers => hitLayers;
    public LayerMask WallLayers => wallLayers;
    public bool HasHitbox => hitboxPrefab != null;

    public CombatHitPayload BuildPayload(AbilitySystem system, AbilitySpec spec, float damageScale = 1f)
    {
        return FragmentBladeDamageUtility.BuildPayload(
            system,
            spec,
            damageConfig,
            damageEffect,
            knockbackEffect,
            damageFormula,
            knockbackFormula,
            legacyDamage,
            legacyStaggerDamage,
            damageScale,
            hitConfirmedTag,
            hitImpactCueKind);
    }
}

// Runtime attack roles, never serialized into weapon assets.
public enum LightningSpearFeedbackKind { Sweep, Rush, RecoveredShot, Landing }

public static class LightningSpearHitFeedback
{
    public static void Configure(CombatHitPayload payload, LightningSpearFeedbackKind kind, Vector2 direction)
    {
        if (payload == null) return;
        bool sweep = kind == LightningSpearFeedbackKind.Sweep;
        bool rush = kind == LightningSpearFeedbackKind.Rush;
        float stop = sweep ? 0.12f : rush ? 0.1f : 0f;
        payload.hitFeel = new CombatHitFeelTiming
        {
            attackerStopSeconds = stop,
            targetStunSeconds = stop > 0f ? 0f : 0.05f
        };
        // Retain impact VFX/audio while replacing the ordinary camera cue for these skills.
        payload.hitCameraScale = 0f;
        payload.impactCameraOverride = null;
        if (kind == LightningSpearFeedbackKind.RecoveredShot) return;
        float punch = sweep ? 1.4f : rush ? 0.9f : 0f;
        float punchSeconds = sweep ? 0.10f : rush ? 0.12f : 0f;
        float shakeSeconds = rush ? 0.06f : 0.05f;
        float amplitude = sweep ? 0.10f : rush ? 0.12f : 0.08f;
        payload.impactCameraOverride = new CameraShakeRequest(
            1f, direction, payload.sourceSystem != null ? payload.sourceSystem.gameObject : null,
            debugReason: "LightningSpear." + kind, hasManualShakeSettingsOverride: true,
            manualShakeSettingsOverride: CameraManualShakeSettings.Create(punchSeconds + shakeSeconds, amplitude, 32f, 0.3f),
            punchDistance: punch, punchSeconds: punchSeconds);
    }
}
