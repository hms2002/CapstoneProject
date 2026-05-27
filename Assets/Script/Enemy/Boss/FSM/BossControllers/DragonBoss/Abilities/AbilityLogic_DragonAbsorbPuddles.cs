using System.Collections;
using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 흡수 패턴을 실행하며, 중앙 이동, 플레이어 흡입, 모든 활성 장판의 흡수 탄막화를 조율한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DragonAbsorbPuddles", menuName = "GAS/Ability Logic/Dragon/AL_DragonAbsorbPuddles")]
public sealed class AbilityLogic_DragonAbsorbPuddles : AbilityLogic
{
    private readonly HashSet<PuddleAreaBase> activeAbsorbProjectiles = new();

    [Header("Center Jump")]
    [SerializeField, Min(0.01f)] private float centerJumpSeconds = 0.9f;
    [SerializeField, Min(1f)] private float centerJumpEaseOutPower = 2.5f;
    [SerializeField, Min(0f)] private float centerJumpVisualHeight = 1.4f;
    [SerializeField, Min(0f)] private float centerJumpBodyZHeight = 1f;
    [SerializeField] private AnimationCurve centerJumpHeightCurve = new(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1f),
        new Keyframe(0.78f, 0.85f),
        new Keyframe(1f, 0f));
    [SerializeField, Min(0.01f)] private float centerLandingDropSeconds = 0.14f;
    [SerializeField, Min(1f)] private float centerLandingDropSharpness = 3f;
    [SerializeField, Min(0.01f)] private float centerArriveDistance = 0.08f;

    [Header("Center Impact")]
    [SerializeField, Min(0.1f)] private float centerImpactDiameter = 3.2f;
    [SerializeField] private GE_Damage_Spec centerImpactDamageEffect;
    [SerializeField] private GE_Knockback_Spec centerImpactKnockbackEffect;
    [SerializeField, Min(0f)] private float centerImpactDamageAmount = 1f;
    [SerializeField, Min(0f)] private float centerImpactKnockbackImpulse = 1500f;
    [SerializeField] private AttackTelegraphStyle centerImpactTelegraphStyle;

    [Header("Absorb")]
    [SerializeField, Min(0.01f)] private float alcoholAbsorbSpeed = 3.5f;
    [SerializeField, Min(0.01f)] private float fireAbsorbSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float alcoholStaggerRecoveryMaxRatio = 0.1f;
    [SerializeField, Range(0f, 1f)] private float fireStaggerBuildUpMaxRatio = 0.1f;
    [SerializeField, Min(0.1f)] private float maxAbsorbSeconds = 12f;
    [SerializeField] private bool logAbsorbResult = true;

    [Header("Presentation")]
    [SerializeField] private WorldPresentationHook inhalePresentation;
    [SerializeField] private WorldPresentationHook centerLandingPresentation;

    [Header("Player Pull")]
    [SerializeField] private bool pullTargetDuringAbsorb = true;
    [SerializeField, Min(0f)] private float pullSpeed = 1.6f;
    [SerializeField, Min(0.01f)] private float pullVelocityRefreshSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float pullVelocityDurationSeconds = 0.12f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
        if (dragon == null)
            yield break;

        dragon.PushFaceTargetLock();

        try
        {
            yield return MoveToArenaCenter(dragon, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            dragon.SpeakSituation(BossSpeechSituationEnum.AbsorbStart);
            dragon.PlayPatternTrigger(DragonAnimationKeys.Inhale);
            PlayInhalePresentation(dragon);
            yield return RunAbsorb(dragon, spec);
        }
        finally
        {
            ClearTrackedPuddles(restoreActiveProjectiles: IsAbilityCancelled(spec));
            RemoveTargetPull(dragon);
            dragon.PopFaceTargetLock();
            dragon.PlayPatternTrigger(DragonAnimationKeys.Idle);
        }
    }

    private IEnumerator MoveToArenaCenter(DragonController dragon, AbilitySpec spec)
    {
        if (dragon == null)
            yield break;

        CombatHeightState2D heightState = EnsureHeightState(dragon);
        AttackTelegraphService telegraphService = dragon.GetComponent<AttackTelegraphService>();
        Vector2 start = dragon.transform.position;
        Vector2 target = dragon.ArenaCenterPosition;
        if (Vector2.Distance(start, target) <= centerArriveDistance)
            target = start;

        float duration = Mathf.Max(0.01f, centerJumpSeconds);
        float elapsed = 0f;
        AttackTelegraphView impactTelegraph = ShowCenterImpactTelegraph(telegraphService, target, duration);

        try
        {
            dragon.PlayPatternTrigger(DragonAnimationKeys.Jump);
            heightState?.SetAirborne(0f, centerJumpBodyZHeight);

            while (elapsed < duration)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float easedMoveTime = 1f - Mathf.Pow(1f - normalizedTime, centerJumpEaseOutPower);
                Vector2 position = Vector2.LerpUnclamped(start, target, easedMoveTime);
                float normalizedHeight = ResolveCenterJumpHeight(normalizedTime);
                normalizedHeight *= ResolveCenterLandingDropMultiplier(elapsed, duration);

                dragon.transform.position = position;
                heightState?.SetAirborne(centerJumpVisualHeight * normalizedHeight, centerJumpBodyZHeight);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            heightState?.SetGrounded();
            if (impactTelegraph != null)
                impactTelegraph.HideImmediate();
        }

        if (IsAbilityCancelled(spec))
            yield break;

        dragon.transform.position = target;
        dragon.PlayPatternTrigger(DragonAnimationKeys.Landing);
        PlayCenterLandingPresentation(dragon, target);
        ApplyCenterImpactDamage(dragon, target);
    }

    /// <summary>
    /// 책임:
    /// 흡입 패턴의 시작 연출을 기존 브레스 입 소켓 위치와 바라보는 방향을 기준으로 재생한다.
    /// </summary>
    private void PlayInhalePresentation(DragonController dragon)
    {
        if (dragon == null || !inhalePresentation.HasAnyContent)
            return;

        Vector2 direction = dragon.GetDirectionToTargetOrFacing();
        dragon.FacePatternDirection(direction);

        Vector2 origin = dragon.ResolveFireBreathMouthPosition(direction, fallbackForwardOffset: 0f);
        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        WorldPresentationRuntime.Play(
            inhalePresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon.gameObject,
                position: origin,
                fallbackDirection: direction,
                target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                sourceObject: this,
                rotation: Quaternion.Euler(0f, 0f, angleDeg)));
    }

    /// <summary>
    /// 책임:
    /// 흡수 패턴의 중앙 점프 착지 순간에 AL이 지정한 월드 연출을 재생한다.
    /// </summary>
    private void PlayCenterLandingPresentation(DragonController dragon, Vector2 landingPosition)
    {
        if (dragon == null || !centerLandingPresentation.HasAnyContent)
            return;

        WorldPresentationRuntime.Play(
            centerLandingPresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon.gameObject,
                position: landingPosition,
                fallbackDirection: Vector3.up,
                target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                sourceObject: this));
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

    private float ResolveCenterJumpHeight(float normalizedTime)
    {
        if (centerJumpHeightCurve == null || centerJumpHeightCurve.length == 0)
            return Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI);

        return Mathf.Max(0f, centerJumpHeightCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
    }

    private float ResolveCenterLandingDropMultiplier(float elapsed, float duration)
    {
        float dropDuration = Mathf.Clamp(centerLandingDropSeconds, 0.01f, duration);
        float dropStart = Mathf.Max(0f, duration - dropDuration);
        if (elapsed < dropStart)
            return 1f;

        float normalizedDrop = Mathf.Clamp01((elapsed - dropStart) / dropDuration);
        return 1f - Mathf.Pow(normalizedDrop, centerLandingDropSharpness);
    }

    private AttackTelegraphView ShowCenterImpactTelegraph(
        AttackTelegraphService telegraphService,
        Vector2 impactPosition,
        float duration)
    {
        if (telegraphService == null)
            return null;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            impactPosition,
            centerImpactDiameter,
            duration,
            centerImpactTelegraphStyle);

        return telegraphService.SpawnDetachedView(spec);
    }

    private void ApplyCenterImpactDamage(DragonController dragon, Vector2 impactPosition)
    {
        if (dragon == null || centerImpactDamageEffect == null || centerImpactDamageAmount <= 0f)
            return;

        float radius = Mathf.Max(0.05f, centerImpactDiameter * 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPosition, radius, ResolveTargetMask(dragon));
        CombatHitPayload payload = MakeCenterImpactPayload(dragon);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hits[i].ClosestPoint(impactPosition));
        }
    }

    private LayerMask ResolveTargetMask(DragonController dragon)
    {
        Transform target = dragon != null ? dragon.CurrentTarget : null;
        return target != null ? (LayerMask)(1 << target.gameObject.layer) : Physics2D.DefaultRaycastLayers;
    }

    private CombatHitPayload MakeCenterImpactPayload(DragonController dragon)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: centerImpactDamageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: centerImpactKnockbackImpulse,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: dragon.AbilitySystem,
            sourceSpec: null,
            damageEffect: centerImpactDamageEffect,
            knockbackEffect: centerImpactKnockbackEffect,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);
    }

    private IEnumerator RunAbsorb(DragonController dragon, AbilitySpec spec)
    {
        dragon.BeginAbsorbPatternTracking();
        ConvertAllGroundPuddles(dragon);

        float elapsed = 0f;
        float nextPullRefreshTime = 0f;
        while (activeAbsorbProjectiles.Count > 0 && elapsed < maxAbsorbSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            if (pullTargetDuringAbsorb && Time.time >= nextPullRefreshTime)
            {
                ApplyTargetPull(dragon);
                nextPullRefreshTime = Time.time + pullVelocityRefreshSeconds;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!IsAbilityCancelled(spec))
        {
            RemoveTargetPull(dragon);
            SpeakAbsorbResult(dragon);
            LogAbsorbResult(dragon);
        }
    }

    /// <summary>
    /// 책임:
    /// 흡수 패턴 결과에 따라 취룡 전용 상황 대사를 선택해 출력한다.
    /// </summary>
    private static void SpeakAbsorbResult(DragonController dragon)
    {
        if (dragon == null)
            return;

        DragonRuntimeData data = dragon.RuntimeData;
        if (data.AbsorbedFireProjectileCount > 0)
        {
            dragon.SpeakSituation(BossSpeechSituationEnum.AbsorbFireAny);
            return;
        }

        if (data.AbsorbedAlcoholProjectileCount > 0)
            dragon.SpeakSituation(BossSpeechSituationEnum.AbsorbAlcoholOnly);
    }

    private void ConvertAllGroundPuddles(DragonController dragon)
    {
        activeAbsorbProjectiles.Clear();

        PuddleManager manager = PuddleManager.ResolveForScene();
        IReadOnlyList<PuddleAreaBase> puddles = manager != null ? manager.Puddles : null;
        if (puddles == null)
            return;

        List<PuddleAreaBase> snapshot = new(puddles);
        for (int i = 0; i < snapshot.Count; i++)
        {
            PuddleAreaBase puddle = snapshot[i];
            if (puddle == null || !puddle.IsGroundActive)
                continue;

            activeAbsorbProjectiles.Add(puddle);
            puddle.Consumed += HandleTrackedPuddleConsumed;
            puddle.EnterAbsorbProjectile(
                dragon.transform,
                ResolveAbsorbSpeed(puddle),
                absorbed => HandlePuddleArrivedAtBoss(dragon, absorbed));
        }
    }

    private float ResolveAbsorbSpeed(PuddleAreaBase puddle)
    {
        return puddle != null && puddle.ElementType == PuddleElementType.Alcohol
            ? alcoholAbsorbSpeed
            : fireAbsorbSpeed;
    }

    private void HandlePuddleArrivedAtBoss(DragonController dragon, PuddleAreaBase puddle)
    {
        if (dragon == null || puddle == null)
            return;

        dragon.RecordAbsorbedPuddleProjectile(puddle.ElementType);
        float staggerRecoveryRatio = 0f;
        float staggerBuildUpRatio = 0f;
        float reducedStaggerBuildUp = 0f;
        float addedStaggerBuildUp = 0f;

        if (puddle.ElementType == PuddleElementType.Alcohol)
        {
            staggerRecoveryRatio = alcoholStaggerRecoveryMaxRatio;
            reducedStaggerBuildUp = dragon.RecoverStaggerBuildUpByMaxRatio(staggerRecoveryRatio);
        }
        else if (puddle.ElementType == PuddleElementType.Fire)
        {
            staggerBuildUpRatio = fireStaggerBuildUpMaxRatio;
            addedStaggerBuildUp = dragon.AddStaggerBuildUpByMaxRatio(staggerBuildUpRatio);
        }

        LogAbsorbedPuddleArrival(dragon, puddle, staggerRecoveryRatio, reducedStaggerBuildUp, staggerBuildUpRatio, addedStaggerBuildUp);
        puddle.MarkConsumed();
        puddle.gameObject.SetActive(false);
    }

    private void LogAbsorbedPuddleArrival(
        DragonController dragon,
        PuddleAreaBase puddle,
        float staggerRecoveryRatio,
        float reducedStaggerBuildUp,
        float staggerBuildUpRatio,
        float addedStaggerBuildUp)
    {
        if (!logAbsorbResult || dragon == null || puddle == null)
            return;

        Debug.Log(
            $"[DragonAbsorb] absorbed {puddle.ElementType} projectile. " +
            $"recoveryRatio={staggerRecoveryRatio:0.###}, reducedStagger={reducedStaggerBuildUp:0.###}, " +
            $"buildUpRatio={staggerBuildUpRatio:0.###}, addedStagger={addedStaggerBuildUp:0.###}",
            dragon);
    }

    private void HandleTrackedPuddleConsumed(PuddleAreaBase puddle)
    {
        if (puddle == null)
            return;

        puddle.Consumed -= HandleTrackedPuddleConsumed;
        activeAbsorbProjectiles.Remove(puddle);
    }

    private void ApplyTargetPull(DragonController dragon)
    {
        if (dragon == null || dragon.CurrentTarget == null || pullSpeed <= 0f)
            return;

        ExternalMovementController2D externalMovement =
            dragon.CurrentTarget.GetComponent<ExternalMovementController2D>() ??
            dragon.CurrentTarget.GetComponentInParent<ExternalMovementController2D>();
        if (externalMovement == null)
            return;

        Vector2 toBoss = (Vector2)(dragon.transform.position - dragon.CurrentTarget.position);
        if (toBoss.sqrMagnitude <= 0.0001f)
            return;

        externalMovement.RemoveTimedVelocitiesFromSource(this);
        externalMovement.AddTimedVelocity(
            toBoss.normalized * pullSpeed,
            pullVelocityDurationSeconds,
            source: this);
    }

    private void RemoveTargetPull(DragonController dragon)
    {
        if (dragon == null || dragon.CurrentTarget == null)
            return;

        ExternalMovementController2D externalMovement =
            dragon.CurrentTarget.GetComponent<ExternalMovementController2D>() ??
            dragon.CurrentTarget.GetComponentInParent<ExternalMovementController2D>();

        externalMovement?.RemoveTimedVelocitiesFromSource(this);
    }

    private void ClearTrackedPuddles(bool restoreActiveProjectiles = false)
    {
        List<PuddleAreaBase> snapshot = new(activeAbsorbProjectiles);
        for (int i = 0; i < snapshot.Count; i++)
        {
            PuddleAreaBase puddle = snapshot[i];
            if (puddle != null)
            {
                puddle.Consumed -= HandleTrackedPuddleConsumed;

                if (restoreActiveProjectiles)
                    puddle.CancelAbsorbToGround();
            }
        }

        activeAbsorbProjectiles.Clear();
    }

    private void LogAbsorbResult(DragonController dragon)
    {
        if (!logAbsorbResult || dragon == null)
            return;

        DragonRuntimeData data = dragon.RuntimeData;
        Debug.Log(
            $"[DragonAbsorb] result alcohol={data.AbsorbedAlcoholProjectileCount}, fire={data.AbsorbedFireProjectileCount}",
            dragon);
    }
}
