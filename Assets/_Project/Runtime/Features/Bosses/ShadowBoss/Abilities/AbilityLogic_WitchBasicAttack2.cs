using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public class AbilityLogic_WitchBasicAttack2 : AbilityLogic
{
    // 이 클래스의 책임:
    // 마녀 보스의 평타2 패턴을 실행하고 마녀 중심 도넛 범위를 경고한 뒤 지연 피해를 적용한다.

    private const float FallbackWarningSeconds = 1.4f;
    private readonly HashSet<GameObject> damagedTargets = new();

    [Header("Donut Range")]
    [SerializeField, Min(0f)] private float warningSeconds = FallbackWarningSeconds;
    [SerializeField, Min(0.1f)] private float fallbackOuterRadius = 6f;
    [SerializeField, Min(0f)] private float innerSafeDiameterScale = 1.5f;
    [SerializeField, Min(0f)] private float minimumInnerSafeRadius = 0.75f;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private float damageAmount = 1f;
    [SerializeField] private AttackTelegraphStyle donutTelegraphStyle;

    [Header("Hit Presentation")]
    [SerializeField] [Min(1)] private int hitEffectCount = 8;
    [SerializeField] [Min(1)] private int hitParticleCount = 1;
    [SerializeField] [Range(0f, 1f)] private float hitPresentationRadiusLerp = 0.75f;
    [SerializeField] private WorldPresentationHook hitPresentation;
    [HideInInspector, FormerlySerializedAs("hitEffectPrefab")]
    [SerializeField] private GameObject legacyHitEffectPrefab;
    [HideInInspector, FormerlySerializedAs("hitEffectLocalOffset")]
    [SerializeField] private Vector3 legacyHitEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [HideInInspector, FormerlySerializedAs("hitEffectLifetimeSeconds")]
    [SerializeField] private float legacyHitEffectLifetimeSeconds = 0.35f;
    [HideInInspector, FormerlySerializedAs("hitEffectScaleMultiplier")]
    [SerializeField] private Vector3 legacyHitEffectScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("hitEffectRotationOffsetZ")]
    [SerializeField] private float legacyHitEffectRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("hitParticlePrefab")]
    [SerializeField] private GameObject legacyHitParticlePrefab;
    [HideInInspector, FormerlySerializedAs("hitParticleLocalOffset")]
    [SerializeField] private Vector3 legacyHitParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [HideInInspector, FormerlySerializedAs("hitParticleLifetimeOverrideSeconds")]
    [SerializeField] private float legacyHitParticleLifetimeOverrideSeconds;
    [HideInInspector, FormerlySerializedAs("useUnscaledHitParticleTime")]
    [SerializeField] private bool legacyUseUnscaledHitParticleTime;
    [HideInInspector, FormerlySerializedAs("hitParticleScaleMultiplier")]
    [SerializeField] private Vector3 legacyHitParticleScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("hitParticleRotationOffsetZ")]
    [SerializeField] private float legacyHitParticleRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("hitSound")]
    [SerializeField] private SoundRef legacyHitSound;
    [HideInInspector, FormerlySerializedAs("hitCameraShake")]
    [SerializeField] private CameraShakeHook legacyHitCameraShake = CameraShakeHook.Create(0.14f, 1f, 0.22f, 0.05f);

    private void OnValidate()
    {
        MigrateLegacyHitPresentation();
    }

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        MigrateLegacyHitPresentation();

        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        GE_Damage_Spec resolvedDamageEffect = ResolveDamageEffect(witch);
        if (witch == null || resolvedDamageEffect == null)
            yield break;

        Vector3 center = witch.transform.position;
        float outerRadius = ComputeOuterRadius(witch);
        float innerSafeRadius = ComputeInnerSafeRadius(initialTarget != null ? initialTarget.transform : witch.CurrentTarget);
        float resolvedWarningSeconds = GetWarningSeconds();
        IAttackTelegraphHandle ringTelegraph = null;

        witch.PlayPatternAttackMotion();
        ringTelegraph = SpawnRingTelegraph(witch, center, outerRadius, innerSafeRadius, resolvedWarningSeconds);

        yield return new WaitForSeconds(resolvedWarningSeconds);
        if (ringTelegraph != null)
            ringTelegraph.Release();

        PlayHitPresentation(center, outerRadius, innerSafeRadius);
        DealRingDamage(witch, center, outerRadius, innerSafeRadius, initialTarget);
    }

    /// <summary>평타2 전용 도넛 텔레그래프를 생성하고 표시합니다.</summary>
    private IAttackTelegraphHandle SpawnRingTelegraph(
        Witch witch,
        Vector3 center,
        float outerRadius,
        float innerSafeRadius,
        float warningDuration)
    {
        if (witch == null)
            return null;

        IAttackTelegraphPresenter telegraphService = AttackTelegraphPresenterResolver.Resolve(witch);
        if (telegraphService == null)
            return null;

        AttackTelegraphSpec spec = AttackTelegraphSpecUtility.WithThinWarningOutline(AttackTelegraphSpec.CreateRing(
            center,
            outerRadius * 2f,
            innerSafeRadius * 2f,
            warningDuration,
            donutTelegraphStyle));

        return telegraphService.SpawnDetachedView(spec);
    }

    private void DealRingDamage(Witch witch, Vector3 center, float outerRadius, float innerSafeRadius, GameObject initialTarget)
    {
        CombatHitPayload payload = MakeHitPayload(witch);
        if (payload == null)
            return;

        LayerMask damageMask = GetDamageMask(witch, initialTarget);
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, outerRadius, damageMask);
        damagedTargets.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (targetRoot == null || targetRoot == witch.gameObject)
                continue;

            if (!damagedTargets.Add(targetRoot))
                continue;

            float distanceToCenter = Vector2.Distance(center, targetRoot.transform.position);
            if (distanceToCenter < innerSafeRadius || distanceToCenter > outerRadius)
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(center));
        }

        if (damagedTargets.Count == 0 && initialTarget != null)
        {
            float distanceToCenter = Vector2.Distance(center, initialTarget.transform.position);
            if (distanceToCenter >= innerSafeRadius && distanceToCenter <= outerRadius)
                CombatHitPayloadApplier.Apply(initialTarget, payload, initialTarget.transform.position);
        }
    }

    private CombatHitPayload MakeHitPayload(Witch witch)
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: ResolveDamageAmount(witch),
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: witch.AbilitySystem,
            sourceSpec: null,
            damageEffect: ResolveDamageEffect(witch),
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: witch.gameObject);
    }

    private LayerMask GetDamageMask(Witch witch, GameObject initialTarget)
    {
        Transform currentTarget = initialTarget != null ? initialTarget.transform : witch.CurrentTarget;
        return currentTarget != null
            ? (LayerMask)(1 << currentTarget.gameObject.layer)
            : (LayerMask)0;
    }

    private float ComputeOuterRadius(Witch witch)
    {
        float outerRadius = 0f;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null)
                continue;

            Vector3 candleCenter = witch.GetCandleCenter(candle);
            float candleDistance = Vector2.Distance(witch.transform.position, candleCenter);
            float candleExtent = GetObjectExtentRadius(candle.gameObject);
            outerRadius = Mathf.Max(outerRadius, candleDistance + candleExtent);
        }

        return Mathf.Max(fallbackOuterRadius, outerRadius);
    }

    private float ComputeInnerSafeRadius(Transform targetTransform)
    {
        if (targetTransform == null)
            return minimumInnerSafeRadius;

        float targetSize = 1f;
        Collider2D targetCollider = targetTransform.GetComponent<Collider2D>();
        if (targetCollider != null)
            targetSize = Mathf.Max(targetCollider.bounds.size.x, targetCollider.bounds.size.y);

        return Mathf.Max(minimumInnerSafeRadius, targetSize * innerSafeDiameterScale * 0.5f);
    }

    private float GetObjectExtentRadius(GameObject gameObject)
    {
        if (gameObject == null)
            return 0f;

        Collider2D collider = gameObject.GetComponent<Collider2D>();
        if (collider != null)
            return Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);

        SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            return Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);

        return 0f;
    }

    private void PlayHitPresentation(Vector3 center, float outerRadius, float innerSafeRadius)
    {
        SpawnPresentationBurst(
            hitPresentation.effect,
            Mathf.Max(1, hitEffectCount),
            center,
            outerRadius,
            innerSafeRadius);
        SpawnPresentationBurst(
            hitPresentation.particle,
            Mathf.Max(1, hitParticleCount),
            center,
            outerRadius,
            innerSafeRadius);

        WorldPresentationPlayback.PlaySignalOnly(
            hitPresentation,
            WorldPresentationContext.AtWorld(
                instigator: null,
                position: center,
                fallbackDirection: Vector3.up,
                target: null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: null));
    }

    private void SpawnPresentationBurst(
        SpawnedPresentationHook visualHook,
        int spawnCount,
        Vector3 center,
        float outerRadius,
        float innerSafeRadius)
    {
        if (!visualHook.HasContent)
            return;

        float spawnRadius = Mathf.Lerp(innerSafeRadius, outerRadius, Mathf.Clamp01(hitPresentationRadiusLerp));

        if (spawnCount <= 1)
        {
            WorldPresentationPlayback.SpawnOneShotDeferredAsync(
                visualHook,
                WorldPresentationContext.AtWorld(
                    instigator: null,
                    position: center,
                    fallbackDirection: Vector3.up,
                    sourceObject: this,
                    rotation: Quaternion.identity,
                    causer: null));
            return;
        }

        float angleStep = 360f / spawnCount;
        for (int i = 0; i < spawnCount; i++)
        {
            float angleDeg = angleStep * i;
            Vector3 direction = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
            Vector3 ringPoint = center + (direction * spawnRadius);

            WorldPresentationPlayback.SpawnOneShotDeferredAsync(
                visualHook,
                WorldPresentationContext.AtWorld(
                    instigator: null,
                    position: ringPoint,
                    fallbackDirection: direction,
                    sourceObject: this,
                    rotation: Quaternion.LookRotation(Vector3.forward, direction),
                    causer: null));
        }
    }

    private void MigrateLegacyHitPresentation()
    {
        if (legacyHitEffectPrefab != null && !hitPresentation.effect.HasContent)
        {
            hitPresentation.effect.prefab = legacyHitEffectPrefab;
            hitPresentation.effect.localOffset = legacyHitEffectLocalOffset;
            hitPresentation.effect.rotationOffsetZ = legacyHitEffectRotationOffsetZ;
            hitPresentation.effect.scaleMultiplier = legacyHitEffectScaleMultiplier;
            hitPresentation.effect.lifetimeOverrideSeconds = legacyHitEffectLifetimeSeconds;
        }

        if (legacyHitParticlePrefab != null && !hitPresentation.particle.HasContent)
        {
            hitPresentation.particle.prefab = legacyHitParticlePrefab;
            hitPresentation.particle.localOffset = legacyHitParticleLocalOffset;
            hitPresentation.particle.rotationOffsetZ = legacyHitParticleRotationOffsetZ;
            hitPresentation.particle.scaleMultiplier = legacyHitParticleScaleMultiplier;
            hitPresentation.particle.lifetimeOverrideSeconds = legacyHitParticleLifetimeOverrideSeconds;
            hitPresentation.particle.useUnscaledTime = legacyUseUnscaledHitParticleTime;
        }

        if (!hitPresentation.HasSound && legacyHitSound.IsSet)
            hitPresentation.sound = legacyHitSound;

        if (!hitPresentation.HasShake && legacyHitCameraShake.amplitude > 0f)
            hitPresentation.cameraShake = legacyHitCameraShake;
    }

    private float GetWarningSeconds()
    {
        return Mathf.Max(0f, warningSeconds);
    }

    private GE_Damage_Spec ResolveDamageEffect(Witch witch)
    {
        return damageEffect;
    }

    private float ResolveDamageAmount(Witch witch)
    {
        return damageAmount;
    }
}
