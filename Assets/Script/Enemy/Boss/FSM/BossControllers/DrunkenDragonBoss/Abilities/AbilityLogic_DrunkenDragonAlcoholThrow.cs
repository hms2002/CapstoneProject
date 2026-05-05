using System.Collections;
using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 술병 투척 패턴을 실행하며, 목표 주변 위치를 예고한 뒤 술통 충돌 피해와 술 장판 생성을 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DrunkenDragonAlcoholThrow", menuName = "GAS/Ability Logic/Drunken Dragon/AL_DrunkenDragonAlcoholThrow")]
public sealed class AbilityLogic_DrunkenDragonAlcoholThrow : AbilityLogic
{
    [Header("Puddle")]
    [SerializeField] private AlcoholPuddleArea alcoholPuddlePrefab;
    [SerializeField] private DrunkenDragonThrownKegActor thrownKegPrefab;
    [SerializeField, Min(0f)] private float targetScatterRadius = 1.2f;

    [Header("Thrown Keg")]
    [SerializeField, Min(0.01f)] private float kegTravelSeconds = 0.45f;
    [SerializeField] private float kegSpinDegrees = 540f;
    [SerializeField, Min(0f)] private float launchForwardOffset = 0.4f;
    [SerializeField, Min(0f)] private float launchUpOffset = 0.8f;

    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec kegDamageEffect;
    [SerializeField, Min(0f)] private float kegDamageAmount = 1f;
    [SerializeField, Min(0f)] private float kegKnockbackImpulse;
    [SerializeField, Min(0f)] private float missedImpactDamageRadius = 1.35f;

    [Header("Impact Presentation")]
    [SerializeField] private WorldPresentationHook kegImpactPresentation;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float prepareSeconds = 0.45f;
    [SerializeField, Min(0f)] private float impactWarningSeconds = 0.45f;

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle impactTelegraphStyle;
    [SerializeField] private AttackTelegraphStyle aimLineTelegraphStyle;
    [SerializeField, Min(0.1f)] private float telegraphDiameter = 2.7f;
    [SerializeField, Min(0.02f)] private float aimLineWidth = 0.08f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DrunkenDragonController dragon = system != null ? system.GetComponent<DrunkenDragonController>() : null;
        if (dragon == null || alcoholPuddlePrefab == null)
            yield break;

        AttackTelegraphService telegraphService = dragon.GetComponent<AttackTelegraphService>();
        int count = 1;
        List<Vector3> impactPositions = new(count) { ResolveTrackedImpactPosition(dragon) };

        dragon.FacePatternDirection(ResolveDirectionToImpact(dragon, impactPositions.Count > 0 ? impactPositions[0] : dragon.transform.position));
        dragon.PushFaceTargetLock();
        try
        {
            dragon.SpeakSituation(BossSpeechSituationEnum.AlcoholThrowPrepare);
            dragon.PlayPatternTrigger(DrunkenDragonAnimationKeys.ThrowReady);
            yield return TrackImpactTelegraphs(dragon, telegraphService, impactPositions, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            for (int i = 0; i < count; i++)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                Vector3 impactPosition = impactPositions[i];
                dragon.FacePatternDirection(ResolveDirectionToImpact(dragon, impactPosition));

                telegraphService?.HideCurrent();
                dragon.PlayPatternTrigger(DrunkenDragonAnimationKeys.Throw);
                yield return ThrowKegThenSpawnPuddle(dragon, system, impactPosition, spec);
            }
        }
        finally
        {
            telegraphService?.HideCurrent();
            dragon.PopFaceTargetLock();
        }
    }

    /// <summary>
    /// 책임:
    /// 술통 투척 조준 중 조준선과 착탄 원이 플레이어를 따라다니게 하고, 종료 시점의 마지막 위치를 투척 목표로 고정한다.
    /// </summary>
    private IEnumerator TrackImpactTelegraphs(
        DrunkenDragonController dragon,
        AttackTelegraphService telegraphService,
        List<Vector3> impactPositions,
        AbilitySpec spec)
    {
        float duration = Mathf.Max(0f, prepareSeconds + impactWarningSeconds);
        if (duration <= 0f)
        {
            if (impactPositions != null && impactPositions.Count > 0)
                impactPositions[0] = ResolveTrackedImpactPosition(dragon);

            yield break;
        }

        Vector3 impactPosition = ResolveTrackedImpactPosition(dragon);
        AttackTelegraphView impactView = SpawnImpactTelegraph(telegraphService, impactPosition, duration);
        AttackTelegraphView aimLineView = SpawnAimLineTelegraph(telegraphService, dragon, impactPosition, duration);

        float elapsed = 0f;
        try
        {
            while (elapsed < duration)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                impactPosition = ResolveTrackedImpactPosition(dragon);
                if (impactPositions != null && impactPositions.Count > 0)
                    impactPositions[0] = impactPosition;

                dragon.FacePatternDirection(ResolveDirectionToImpact(dragon, impactPosition));
                UpdateImpactTelegraph(impactView, impactPosition, duration);
                UpdateAimLineTelegraph(aimLineView, dragon, impactPosition, duration);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            impactView?.HideImmediate();
            aimLineView?.HideImmediate();
        }
    }

    private static Vector2 ResolveDirectionToImpact(DrunkenDragonController dragon, Vector3 impactPosition)
    {
        if (dragon == null)
            return Vector2.right;

        Vector2 direction = impactPosition - dragon.transform.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : dragon.GetDirectionToTargetOrFacing();
    }

    private Vector3 ResolveTrackedImpactPosition(DrunkenDragonController dragon)
    {
        Vector3 center = dragon.CurrentTarget != null ? dragon.CurrentTarget.position : dragon.transform.position;
        if (targetScatterRadius <= 0f)
            return center;

        return center;
    }

    private AttackTelegraphView SpawnImpactTelegraph(AttackTelegraphService telegraphService, Vector3 impactPosition, float duration)
    {
        if (telegraphService == null)
            return null;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            impactPosition,
            telegraphDiameter,
            duration,
            impactTelegraphStyle);

        return telegraphService.SpawnDetachedView(spec);
    }

    private void UpdateImpactTelegraph(AttackTelegraphView view, Vector3 impactPosition, float duration)
    {
        if (view == null)
            return;

        view.UpdateGeometry(AttackTelegraphSpec.CreateCircle(
            impactPosition,
            telegraphDiameter,
            duration,
            impactTelegraphStyle));
    }

    private AttackTelegraphView SpawnAimLineTelegraph(
        AttackTelegraphService telegraphService,
        DrunkenDragonController dragon,
        Vector3 impactPosition,
        float duration)
    {
        if (telegraphService == null)
            return null;

        return telegraphService.SpawnDetachedView(CreateAimLineSpec(dragon, impactPosition, duration));
    }

    private void UpdateAimLineTelegraph(
        AttackTelegraphView view,
        DrunkenDragonController dragon,
        Vector3 impactPosition,
        float duration)
    {
        if (view == null)
            return;

        view.UpdateGeometry(CreateAimLineSpec(dragon, impactPosition, duration));
    }

    private AttackTelegraphSpec CreateAimLineSpec(
        DrunkenDragonController dragon,
        Vector3 impactPosition,
        float duration)
    {
        Vector3 start = dragon != null ? dragon.transform.position : impactPosition;
        Vector2 toImpact = impactPosition - start;
        float length = Mathf.Max(0.01f, toImpact.magnitude);
        Vector3 center = start + (Vector3)(toImpact.normalized * (length * 0.5f));
        float rotationDeg = Mathf.Atan2(toImpact.y, toImpact.x) * Mathf.Rad2Deg;
        return AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, aimLineWidth),
            rotationDeg,
            duration,
            aimLineTelegraphStyle != null ? aimLineTelegraphStyle : impactTelegraphStyle);
    }

    private void SpawnAlcoholPuddle(Vector3 impactPosition)
    {
        impactPosition.z = 0f;
        Object.Instantiate(alcoholPuddlePrefab, impactPosition, Quaternion.identity);
    }

    private IEnumerator ThrowKegThenSpawnPuddle(
        DrunkenDragonController dragon,
        AbilitySystem sourceSystem,
        Vector3 impactPosition,
        AbilitySpec spec)
    {
        if (thrownKegPrefab == null)
        {
            SpawnAlcoholPuddle(impactPosition);
            yield break;
        }

        bool didImpact = false;
        Vector3 launchPosition = ResolveLaunchPosition(dragon, impactPosition);
        DrunkenDragonThrownKegActor keg = Object.Instantiate(thrownKegPrefab, launchPosition, Quaternion.identity);
        keg.Launch(
            launchPosition,
            impactPosition,
            kegTravelSeconds,
            kegSpinDegrees,
            ResolveTargetMask(dragon),
            (resolvedImpactPosition, hitTarget) =>
            {
                if (IsAbilityCancelled(spec))
                    return;

                didImpact = true;
                if (hitTarget != null)
                    ApplyKegDamage(sourceSystem, dragon, hitTarget, resolvedImpactPosition);
                else
                    ApplyMissedImpactAreaDamage(sourceSystem, dragon, resolvedImpactPosition);

                PlayKegImpactPresentation(dragon, hitTarget, resolvedImpactPosition, impactPosition);
                SpawnAlcoholPuddle(resolvedImpactPosition);
            });

        while (!didImpact && !IsAbilityCancelled(spec))
            yield return null;

        if (IsAbilityCancelled(spec) && keg != null)
            Object.Destroy(keg.gameObject);
    }

    private Vector3 ResolveLaunchPosition(DrunkenDragonController dragon, Vector3 impactPosition)
    {
        Vector3 origin = dragon != null ? dragon.transform.position : impactPosition;
        Vector2 toImpact = impactPosition - origin;
        Vector2 direction = toImpact.sqrMagnitude > 0.0001f ? toImpact.normalized : Vector2.right;
        return origin + (Vector3)(direction * launchForwardOffset) + (Vector3.up * launchUpOffset);
    }

    private LayerMask ResolveTargetMask(DrunkenDragonController dragon)
    {
        Transform target = dragon != null ? dragon.CurrentTarget : null;
        return target != null ? (LayerMask)(1 << target.gameObject.layer) : Physics2D.DefaultRaycastLayers;
    }

    private void ApplyKegDamage(
        AbilitySystem sourceSystem,
        DrunkenDragonController dragon,
        GameObject hitTarget,
        Vector3 hitPosition)
    {
        if (sourceSystem == null || dragon == null || hitTarget == null || kegDamageEffect == null || kegDamageAmount <= 0f)
            return;

        CombatDamageSnapshot snapshot = new(
            finalHpDamage: kegDamageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: kegKnockbackImpulse,
            elementBuildUps: null,
            isCriticalHit: false);

        CombatHitPayload payload = CombatHitPayload.FromSnapshot(
            sourceSystem: sourceSystem,
            sourceSpec: null,
            damageEffect: kegDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);

        CombatHitPayloadApplier.Apply(hitTarget, payload, hitPosition);
    }

    /// <summary>
    /// 책임:
    /// 술통이 플레이어 또는 바닥에 충돌한 순간의 월드 VFX를 재생한다.
    /// </summary>
    private void PlayKegImpactPresentation(
        DrunkenDragonController dragon,
        GameObject hitTarget,
        Vector3 impactPosition,
        Vector3 intendedImpactPosition)
    {
        if (!kegImpactPresentation.HasVisuals)
            return;

        Vector3 fallbackDirection = intendedImpactPosition - (dragon != null ? dragon.transform.position : impactPosition);
        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            fallbackDirection = Vector3.up;

        WorldPresentationRuntime.Play(
            kegImpactPresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon != null ? dragon.gameObject : null,
                position: impactPosition,
                fallbackDirection: fallbackDirection.normalized,
                target: hitTarget,
                sourceObject: this,
                causer: dragon != null ? dragon.gameObject : null));
    }

    private void ApplyMissedImpactAreaDamage(
        AbilitySystem sourceSystem,
        DrunkenDragonController dragon,
        Vector3 impactPosition)
    {
        if (sourceSystem == null || dragon == null || kegDamageEffect == null || kegDamageAmount <= 0f || missedImpactDamageRadius <= 0f)
            return;

        LayerMask targetMask = ResolveTargetMask(dragon);
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPosition, missedImpactDamageRadius, targetMask);
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            ApplyKegDamage(sourceSystem, dragon, targetRoot, hits[i].ClosestPoint(impactPosition));
            return;
        }
    }
}
