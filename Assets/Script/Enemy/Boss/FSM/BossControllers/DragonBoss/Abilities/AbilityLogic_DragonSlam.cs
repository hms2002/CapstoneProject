using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 내려치기 패턴을 실행하며, 목표 위치 예고, 도약 이동, 착지 피해와 넉백을 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DragonSlam", menuName = "GAS/Ability Logic/Dragon/AL_DragonSlam")]
public sealed class AbilityLogic_DragonSlam : AbilityLogic
{
    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float travelSeconds = 1.4f;
    [SerializeField, Min(1f)] private float travelEaseOutPower = 3.5f;
    [SerializeField, Min(0f)] private float airborneVisualHeight = 1.8f;
    [SerializeField, Min(0f)] private float airborneBodyZHeight = 1f;
    [SerializeField] private AnimationCurve jumpHeightCurve = new(
        new Keyframe(0f, 0f),
        new Keyframe(0.18f, 1f),
        new Keyframe(0.78f, 0.95f),
        new Keyframe(0.92f, 0.35f),
        new Keyframe(1f, 0f));
    [SerializeField, Min(0.01f)] private float landingDropSeconds = 0.18f;
    [SerializeField, Min(1f)] private float landingDropSharpness = 3.5f;

    [Header("Impact")]
    [SerializeField, Min(0.1f)] private float impactDiameter = 3.2f;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 12f;
    [SerializeField] private SoundRef impactSound;
    [SerializeField] private CameraShakeHook impactCameraShake = CameraShakeHook.Create(
        amplitude: 0.22f,
        amplitudeMultiplier: 1f,
        maxAmplitude: 0.45f,
        minIntervalSeconds: 0.04f);
    [SerializeField] private GameObject impactVisualPrefab;
    [SerializeField] private Vector3 impactVisualOffset = Vector3.zero;
    [SerializeField] private Vector3 impactVisualScale = Vector3.one;
    [SerializeField, Min(0f)] private float impactVisualLifetime = 1.2f;

    [Header("Jump Presentation")]
    [SerializeField] private WorldPresentationHook jumpStartPresentation;
    [SerializeField] private WorldPresentationHook landingPresentation;

    [Header("Keg Scatter")]
    [SerializeField] private AlcoholPuddleArea alcoholPuddlePrefab;
    [SerializeField] private DragonThrownKegActor thrownKegPrefab;
    [SerializeField, Min(0)] private int scatteredKegCount = 4;
    [SerializeField, Min(1)] private int scatteredKegArenaSampleAttempts = 80;
    [SerializeField, Min(0f)] private float scatteredKegRadius = 3f;
    [SerializeField, Min(0f)] private float scatteredKegWarningSeconds = 1f;
    [SerializeField, Min(0.01f)] private float scatteredKegTravelSeconds = 0.35f;
    [SerializeField, Min(0f)] private float scatteredKegDropHeight = 2.2f;
    [SerializeField] private float scatteredKegSpinDegrees = 420f;
    [SerializeField, Min(0.1f)] private float scatteredKegTelegraphDiameter = 4.8f;
    [SerializeField, Min(0f)] private float scatteredKegMissedDamageRadius = 2.4f;

    [Header("Scattered Keg Presentation")]
    [SerializeField] private WorldPresentationHook scatteredKegImpactPresentation;

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle impactTelegraphStyle;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
        if (dragon == null)
            yield break;

        AbilityMotionController2D motion = dragon.GetComponent<AbilityMotionController2D>();
        EntityCollisionProfile2D collisionProfile = dragon.GetComponent<EntityCollisionProfile2D>();
        AttackTelegraphService telegraphService = dragon.GetComponent<AttackTelegraphService>();
        CombatHeightState2D heightState = EnsureHeightState(dragon);

        Vector2 start = dragon.transform.position;
        Vector2 impactPosition = ResolveImpactPosition(dragon, initialTarget);
        float duration = Mathf.Max(0.01f, travelSeconds);

        AttackTelegraphView impactTelegraph = ShowImpactTelegraph(telegraphService, impactPosition, duration);
        collisionProfile?.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);
        heightState?.SetAirborne(0f, airborneBodyZHeight);
        dragon.FacePatternDirection(impactPosition - start);
        dragon.PushFaceTargetLock();

        try
        {
            try
            {
                PlayJumpStartPresentation(dragon, start);
                dragon.PlayPatternTrigger(DragonAnimationKeys.Jump);
                dragon.BeginJumpAfterimage();
                MoveToImpactPosition(motion, dragon, start, impactPosition, duration);

                yield return TweenJumpHeight(heightState, duration, spec);
            }
            finally
            {
                dragon.StopJumpAfterimage(IsAbilityCancelled(spec));

                if (IsAbilityCancelled(spec))
                    motion?.CancelMotion();

                heightState?.SetGrounded();
                if (impactTelegraph != null)
                    impactTelegraph.HideImmediate();

                dragon.PopFaceTargetLock();
            }

            if (IsAbilityCancelled(spec))
                yield break;

            dragon.PlayPatternTrigger(DragonAnimationKeys.Landing);
            PlayLandingPresentation(dragon, impactPosition);
            PlayImpactPresentation(dragon, impactPosition);
            ApplyImpactDamage(dragon, impactPosition);
            yield return ScatterKegsAfterImpact(dragon, system, telegraphService, impactPosition, spec);
        }
        finally
        {
            collisionProfile?.RestoreDefaultMode();
        }
    }

    private static CombatHeightState2D EnsureHeightState(DragonController dragon)
    {
        if (dragon == null)
            return null;

        CombatHeightState2D heightState = dragon.GetComponent<CombatHeightState2D>();
        if (heightState != null)
            return heightState;

        return dragon.gameObject.AddComponent<CombatHeightState2D>();
    }

    private IEnumerator TweenJumpHeight(CombatHeightState2D heightState, float duration, AbilitySpec spec)
    {
        if (heightState == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float normalizedHeight = ResolveJumpCurveValue(normalizedTime);
            normalizedHeight *= ResolveLandingDropMultiplier(elapsed, duration);
            heightState.SetAirborne(airborneVisualHeight * normalizedHeight, airborneBodyZHeight);

            elapsed += Time.deltaTime;
            yield return null;
        }

        heightState.SetAirborne(0f, airborneBodyZHeight);
    }

    private float ResolveJumpCurveValue(float normalizedTime)
    {
        if (jumpHeightCurve == null || jumpHeightCurve.length == 0)
            return Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI);

        return Mathf.Max(0f, jumpHeightCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
    }

    private float ResolveLandingDropMultiplier(float elapsed, float duration)
    {
        float dropDuration = Mathf.Clamp(landingDropSeconds, 0.01f, duration);
        float dropStart = Mathf.Max(0f, duration - dropDuration);
        if (elapsed < dropStart)
            return 1f;

        float normalizedDrop = Mathf.Clamp01((elapsed - dropStart) / dropDuration);
        return 1f - Mathf.Pow(normalizedDrop, landingDropSharpness);
    }

    private Vector2 ResolveImpactPosition(DragonController dragon, GameObject initialTarget)
    {
        if (dragon != null && dragon.CurrentTarget != null)
            return dragon.CurrentTarget.position;

        if (initialTarget != null)
            return initialTarget.transform.position;

        return dragon != null ? dragon.transform.position : Vector3.zero;
    }

    private void MoveToImpactPosition(
        AbilityMotionController2D motion,
        DragonController dragon,
        Vector2 start,
        Vector2 impactPosition,
        float duration)
    {
        Vector2 delta = impactPosition - start;
        if (motion != null && delta.sqrMagnitude > 0.0001f)
        {
            motion.StartLunge(start, delta.normalized, delta.magnitude, duration, travelEaseOutPower);
            return;
        }

        if (dragon != null)
            dragon.transform.position = impactPosition;
    }

    private AttackTelegraphView ShowImpactTelegraph(
        AttackTelegraphService telegraphService,
        Vector2 impactPosition,
        float duration)
    {
        if (telegraphService == null)
            return null;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            impactPosition,
            impactDiameter,
            duration,
            impactTelegraphStyle);

        spec = AttackTelegraphSpecUtility.WithThinWarningOutline(spec);
        return telegraphService.SpawnDetachedView(spec);
    }

    private void ApplyImpactDamage(DragonController dragon, Vector2 impactPosition)
    {
        if (dragon == null || damageEffect == null || damageAmount <= 0f)
            return;

        float radius = Mathf.Max(0.05f, impactDiameter * 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPosition, radius, ResolveTargetMask(dragon));
        CombatHitPayload payload = MakeHitPayload(dragon);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hits[i].ClosestPoint(impactPosition));
        }
    }

    /// <summary>
    /// 책임:
    /// 착지 순간의 화면 흔들림, 사운드, 월드 이펙트를 한 지점에서 재생해 내려찍기 피드백을 집중시킨다.
    /// </summary>
    private void PlayImpactPresentation(DragonController dragon, Vector2 impactPosition)
    {
        if (dragon == null)
            return;

        Vector3 origin = new Vector3(impactPosition.x, impactPosition.y, dragon.transform.position.z);
        impactCameraShake.TryPlay(
            dragon.gameObject,
            Vector3.down,
            debugReason: "Dragon.SlamImpact");

        SoundPlaybackUtility.Play(
            impactSound,
            instigator: dragon.gameObject,
            causer: dragon.gameObject,
            target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
            position: origin,
            sourceObject: dragon);

        if (impactVisualPrefab == null)
            return;

        GameObject visual = Object.Instantiate(
            impactVisualPrefab,
            origin + impactVisualOffset,
            Quaternion.identity);

        visual.transform.localScale = impactVisualScale;
        if (impactVisualLifetime > 0f)
            Object.Destroy(visual, impactVisualLifetime);
    }

    /// <summary>
    /// 책임:
    /// 취룡이 도약을 시작하는 발밑 위치에 먼지/압력 이펙트를 재생한다.
    /// </summary>
    private void PlayJumpStartPresentation(DragonController dragon, Vector2 startPosition)
    {
        if (!jumpStartPresentation.HasAnyContent)
            return;

        Vector3 position = new(startPosition.x, startPosition.y, dragon != null ? dragon.transform.position.z : 0f);
        WorldPresentationRuntime.Play(
            jumpStartPresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon != null ? dragon.gameObject : null,
                position: position,
                fallbackDirection: Vector3.down,
                target: dragon != null && dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                sourceObject: this,
                causer: dragon != null ? dragon.gameObject : null));
    }

    /// <summary>
    /// 책임:
    /// 취룡이 착지하는 발밑 위치에 먼지/충격 이펙트를 재생한다.
    /// </summary>
    private void PlayLandingPresentation(DragonController dragon, Vector2 impactPosition)
    {
        if (!landingPresentation.HasAnyContent)
            return;

        Vector3 position = new(impactPosition.x, impactPosition.y, dragon != null ? dragon.transform.position.z : 0f);
        WorldPresentationRuntime.Play(
            landingPresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon != null ? dragon.gameObject : null,
                position: position,
                fallbackDirection: Vector3.down,
                target: dragon != null && dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                sourceObject: this,
                causer: dragon != null ? dragon.gameObject : null));
    }

    private IEnumerator ScatterKegsAfterImpact(
        DragonController dragon,
        AbilitySystem sourceSystem,
        AttackTelegraphService telegraphService,
        Vector2 impactPosition,
        AbilitySpec spec)
    {
        if (dragon == null || alcoholPuddlePrefab == null || scatteredKegCount <= 0)
            yield break;

        List<Vector3> kegTargets = BuildScatteredKegTargets(dragon, impactPosition);
        ShowScatteredKegTelegraphs(telegraphService, kegTargets);

        yield return WaitForSecondsUnlessCancelled(scatteredKegWarningSeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        if (thrownKegPrefab == null)
        {
            for (int i = 0; i < kegTargets.Count; i++)
                ResolveScatteredKegImpact(sourceSystem, dragon, kegTargets[i], null);

            yield break;
        }

        int pendingImpacts = kegTargets.Count;
        List<DragonThrownKegActor> activeKegs = new(kegTargets.Count);
        for (int i = 0; i < kegTargets.Count; i++)
        {
            if (IsAbilityCancelled(spec))
                break;

            Vector3 target = kegTargets[i];
            DragonThrownKegActor keg = Object.Instantiate(thrownKegPrefab, target, Quaternion.identity);
            activeKegs.Add(keg);
            keg.LaunchVerticalDrop(
                target,
                scatteredKegDropHeight,
                scatteredKegTravelSeconds,
                scatteredKegSpinDegrees,
                ResolveTargetMask(dragon),
                (resolvedImpactPosition, hitTarget) =>
                {
                    if (IsAbilityCancelled(spec))
                        return;

                    ResolveScatteredKegImpact(sourceSystem, dragon, resolvedImpactPosition, hitTarget);
                    pendingImpacts--;
                });
        }

        while (pendingImpacts > 0 && !IsAbilityCancelled(spec))
            yield return null;

        if (IsAbilityCancelled(spec))
        {
            for (int i = 0; i < activeKegs.Count; i++)
            {
                if (activeKegs[i] != null)
                    Object.Destroy(activeKegs[i].gameObject);
            }
        }
    }

    /// <summary>
    /// 책임:
    /// 내려찍기 후 수직 낙하 술통의 목표 지점을 arena bounds 안에서 고르고, bounds가 없으면 기존 착지 주변 산포로 대체한다.
    /// </summary>
    private List<Vector3> BuildScatteredKegTargets(DragonController dragon, Vector2 impactPosition)
    {
        int count = Mathf.Max(0, scatteredKegCount);
        List<Vector3> results = new(count);
        if (count <= 0)
            return results;

        if (TryBuildArenaRandomKegTargets(dragon, count, results))
            return results;

        return BuildFallbackScatteredKegTargets(impactPosition, count);
    }

    private bool TryBuildArenaRandomKegTargets(
        DragonController dragon,
        int count,
        List<Vector3> results)
    {
        if (dragon == null || results == null)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (!dragon.TryGetRandomArenaPoint(scatteredKegArenaSampleAttempts, out Vector2 point))
                return false;

            results.Add(point);
        }

        return results.Count == count;
    }

    private List<Vector3> BuildFallbackScatteredKegTargets(Vector2 impactPosition, int count)
    {
        List<Vector3> results = new(count);
        if (scatteredKegRadius <= 0f)
        {
            for (int i = 0; i < count; i++)
                results.Add(impactPosition);

            return results;
        }

        float angleOffset = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + ((Mathf.PI * 2f) / count * i);
            float radius = Random.Range(scatteredKegRadius * 0.65f, scatteredKegRadius);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            results.Add((Vector3)impactPosition + (Vector3)(direction * radius));
        }

        return results;
    }

    private void ShowScatteredKegTelegraphs(AttackTelegraphService telegraphService, IReadOnlyList<Vector3> kegTargets)
    {
        if (telegraphService == null || kegTargets == null || scatteredKegWarningSeconds <= 0f)
            return;

        for (int i = 0; i < kegTargets.Count; i++)
        {
            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
                kegTargets[i],
                scatteredKegTelegraphDiameter,
                scatteredKegWarningSeconds,
                impactTelegraphStyle);

            spec = AttackTelegraphSpecUtility.WithThinWarningOutline(spec);
            telegraphService.SpawnDetachedView(spec);
        }
    }

    private void ResolveScatteredKegImpact(
        AbilitySystem sourceSystem,
        DragonController dragon,
        Vector3 impactPosition,
        GameObject hitTarget)
    {
        if (hitTarget != null)
            ApplyScatteredKegDamage(sourceSystem, dragon, hitTarget, impactPosition);
        else
            ApplyScatteredKegMissedAreaDamage(sourceSystem, dragon, impactPosition);

        PlayScatteredKegImpactPresentation(dragon, hitTarget, impactPosition);
        SpawnAlcoholPuddle(impactPosition);
    }

    private void SpawnAlcoholPuddle(Vector3 impactPosition)
    {
        if (alcoholPuddlePrefab == null)
            return;

        impactPosition.z = 0f;
        Object.Instantiate(alcoholPuddlePrefab, impactPosition, Quaternion.identity);
    }

    private void ApplyScatteredKegDamage(
        AbilitySystem sourceSystem,
        DragonController dragon,
        GameObject hitTarget,
        Vector3 hitPosition)
    {
        if (sourceSystem == null || dragon == null || hitTarget == null || damageEffect == null || damageAmount <= 0f)
            return;

        CombatHitPayload payload = MakeHitPayload(dragon);
        CombatHitPayloadApplier.Apply(hitTarget, payload, hitPosition);
    }

    /// <summary>
    /// 책임:
    /// 내려찍기 후 수직 낙하한 술통이 터지는 순간의 월드 VFX를 재생한다.
    /// </summary>
    private void PlayScatteredKegImpactPresentation(
        DragonController dragon,
        GameObject hitTarget,
        Vector3 impactPosition)
    {
        if (!scatteredKegImpactPresentation.HasAnyContent)
            return;

        Vector3 fallbackDirection = dragon != null
            ? impactPosition - dragon.transform.position
            : Vector3.up;

        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            fallbackDirection = Vector3.up;

        WorldPresentationRuntime.Play(
            scatteredKegImpactPresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon != null ? dragon.gameObject : null,
                position: impactPosition,
                fallbackDirection: fallbackDirection.normalized,
                target: hitTarget,
                sourceObject: this,
                causer: dragon != null ? dragon.gameObject : null));
    }

    private void ApplyScatteredKegMissedAreaDamage(
        AbilitySystem sourceSystem,
        DragonController dragon,
        Vector3 impactPosition)
    {
        if (sourceSystem == null || dragon == null || scatteredKegMissedDamageRadius <= 0f)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPosition, scatteredKegMissedDamageRadius, ResolveTargetMask(dragon));
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            ApplyScatteredKegDamage(sourceSystem, dragon, targetRoot, hits[i].ClosestPoint(impactPosition));
            return;
        }
    }

    private LayerMask ResolveTargetMask(DragonController dragon)
    {
        Transform target = dragon != null ? dragon.CurrentTarget : null;
        return target != null ? (LayerMask)(1 << target.gameObject.layer) : Physics2D.DefaultRaycastLayers;
    }

    private CombatHitPayload MakeHitPayload(DragonController dragon)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: dragon.AbilitySystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);
    }
}
