using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class AbilityLogic_WitchBasicAttack2 : AbilityLogic
{
    // 이 클래스의 책임:
    // 마녀 보스의 평타2 패턴을 실행하고 마녀 중심 도넛 범위를 경고한 뒤 지연 피해를 적용한다.

    private const float WarningSeconds = 1.4f;
    private const float FallbackOuterRadius = 6f;
    private const float InnerSafeDiameterScale = 1.5f;
    private const float MinimumInnerSafeRadius = 0.75f;
    private const float DefaultPresentationLifetimeSeconds = 1f;
    private readonly HashSet<GameObject> damagedTargets = new();

    [Header("Hit Presentation")]
    [SerializeField] [Min(1)] private int hitPresentationCount = 8;
    [SerializeField] [Range(0f, 1f)] private float hitPresentationRadiusLerp = 0.75f;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Vector3 hitEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [SerializeField] [Min(0f)] private float hitEffectLifetimeSeconds = 0.35f;
    [SerializeField] private Vector3 hitEffectScaleMultiplier = Vector3.one;
    [SerializeField] private float hitEffectRotationOffsetZ;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private Vector3 hitParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [SerializeField] [Min(0f)] private float hitParticleLifetimeOverrideSeconds = 0f;
    [SerializeField] private bool useUnscaledHitParticleTime;
    [SerializeField] private Vector3 hitParticleScaleMultiplier = Vector3.one;
    [SerializeField] private float hitParticleRotationOffsetZ;
    [SerializeField] private CameraShakeHook hitCameraShake = CameraShakeHook.Create(0.14f, 1f, 0.22f, 0.05f);

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        if (witch == null || witch.ProjectileDamageEffect == null)
            yield break;

        AttackTelegraphService telegraphService = witch.GetComponent<AttackTelegraphService>();
        Vector3 center = witch.transform.position;
        float outerRadius = ComputeOuterRadius(witch);
        float innerSafeRadius = ComputeInnerSafeRadius(initialTarget != null ? initialTarget.transform : witch.CurrentTarget);

        witch.PlayPatternAttackMotion();
        if (telegraphService != null)
        {
            AttackTelegraphSpec warningSpec = AttackTelegraphSpec.CreateRing(
                center,
                outerRadius * 2f,
                innerSafeRadius * 2f,
                WarningSeconds);
            telegraphService.Show(warningSpec);
        }

        yield return new WaitForSeconds(WarningSeconds);
        telegraphService?.HideCurrent();

        PlayHitPresentation(center, outerRadius, innerSafeRadius);
        DealRingDamage(witch, center, outerRadius, innerSafeRadius, initialTarget);
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
            finalHpDamage: witch.ProjectileDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: witch.AbilitySystem,
            sourceSpec: null,
            damageEffect: witch.ProjectileDamageEffect,
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

        return Mathf.Max(FallbackOuterRadius, outerRadius);
    }

    private float ComputeInnerSafeRadius(Transform targetTransform)
    {
        if (targetTransform == null)
            return MinimumInnerSafeRadius;

        float targetSize = 1f;
        Collider2D targetCollider = targetTransform.GetComponent<Collider2D>();
        if (targetCollider != null)
            targetSize = Mathf.Max(targetCollider.bounds.size.x, targetCollider.bounds.size.y);

        return Mathf.Max(MinimumInnerSafeRadius, targetSize * InnerSafeDiameterScale * 0.5f);
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
        int presentationCount = Mathf.Max(1, hitPresentationCount);
        float spawnRadius = Mathf.Lerp(innerSafeRadius, outerRadius, Mathf.Clamp01(hitPresentationRadiusLerp));

        if (presentationCount == 1)
        {
            Vector3 direction = Vector3.up;
            SpawnEffectPrefab(
                hitEffectPrefab,
                center + hitEffectLocalOffset,
                direction,
                hitEffectRotationOffsetZ,
                hitEffectScaleMultiplier,
                hitEffectLifetimeSeconds,
                useUnscaledTime: false);
            SpawnEffectPrefab(
                hitParticlePrefab,
                center + hitParticleLocalOffset,
                direction,
                hitParticleRotationOffsetZ,
                hitParticleScaleMultiplier,
                hitParticleLifetimeOverrideSeconds,
                useUnscaledHitParticleTime);
        }
        else
        {
            float angleStep = 360f / presentationCount;
            for (int i = 0; i < presentationCount; i++)
            {
                float angleDeg = angleStep * i;
                Vector3 direction = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
                Vector3 ringPoint = center + (direction * spawnRadius);

                SpawnEffectPrefab(
                    hitEffectPrefab,
                    ringPoint + hitEffectLocalOffset,
                    direction,
                    hitEffectRotationOffsetZ,
                    hitEffectScaleMultiplier,
                    hitEffectLifetimeSeconds,
                    useUnscaledTime: false);
                SpawnEffectPrefab(
                    hitParticlePrefab,
                    ringPoint + hitParticleLocalOffset,
                    direction,
                    hitParticleRotationOffsetZ,
                    hitParticleScaleMultiplier,
                    hitParticleLifetimeOverrideSeconds,
                    useUnscaledHitParticleTime);
            }
        }

        hitCameraShake.TryPlay(
            source: null,
            fallbackDirection: Vector3.up,
            debugReason: "WitchBasicAttack2.Hit");
    }

    private static void SpawnEffectPrefab(
        GameObject prefab,
        Vector3 position,
        Vector3 outwardDirection,
        float rotationOffsetZ,
        Vector3 scaleMultiplier,
        float lifetimeOverrideSeconds,
        bool useUnscaledTime)
    {
        if (prefab == null)
            return;

        Quaternion rotation = outwardDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(Vector3.forward, outwardDirection) * Quaternion.Euler(0f, 0f, rotationOffsetZ)
            : Quaternion.Euler(0f, 0f, rotationOffsetZ);

        GameObject instance = Object.Instantiate(prefab, position, rotation);
        if (instance == null)
            return;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
        ConfigureSpawnedPresentation(instance, useUnscaledTime);

        float lifetime = ResolvePresentationLifetime(instance, lifetimeOverrideSeconds);
        if (lifetime > 0f)
            Object.Destroy(instance, lifetime);
    }

    private static void ConfigureSpawnedPresentation(GameObject instance, bool useUnscaledTime)
    {
        if (instance == null)
            return;

        instance.SetActive(true);

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (useUnscaledTime)
            {
                var main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            particleSystem.Play(withChildren: true);
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            animationComponent.Play();
        }
    }

    private static float ResolvePresentationLifetime(GameObject instance, float lifetimeOverrideSeconds)
    {
        if (lifetimeOverrideSeconds > 0f)
            return lifetimeOverrideSeconds;

        float particleLifetime = ResolveParticleLifetime(instance);
        if (particleLifetime > 0f)
            return particleLifetime;

        float animationLifetime = ResolveAnimatorLifetime(instance);
        if (animationLifetime > 0f)
            return animationLifetime;

        return DefaultPresentationLifetimeSeconds;
    }

    private static float ResolveParticleLifetime(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        if (particleSystems == null || particleSystems.Length == 0)
            return 0f;

        float maxLifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            var main = particleSystem.main;
            if (main.loop)
                return DefaultPresentationLifetimeSeconds;

            float startDelay = ResolveCurveMax(main.startDelay);
            float startLifetime = ResolveCurveMax(main.startLifetime);
            maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
        }

        return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
    }

    private static float ResolveAnimatorLifetime(GameObject instance)
    {
        float maxLifetime = 0f;

        Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                if (clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, clip.length);
            }
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            foreach (AnimationState state in animationComponent)
            {
                if (state?.clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, state.clip.length);
            }
        }

        return maxLifetime > 0f ? maxLifetime + 0.05f : 0f;
    }

    private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
            _ => Mathf.Max(curve.constant, curve.constantMax)
        };
    }
}
