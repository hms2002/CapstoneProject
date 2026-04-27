using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 전방 화염 방사 패턴을 실행하며, 부채꼴 예고, 지속 피해, 술 장판 점화를 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DrunkenDragonFireBreath", menuName = "GAS/Ability Logic/Drunken Dragon/AL_DrunkenDragonFireBreath")]
public sealed class AbilityLogic_DrunkenDragonFireBreath : AbilityLogic
{
    private readonly Dictionary<GameObject, float> nextDamageAllowedTimes = new();

    [Header("Timing")]
    [SerializeField, Min(0f)] private float prepareSeconds = 1.8f;
    [SerializeField, Min(0f)] private float preFireDelaySeconds = 0.2f;
    [SerializeField, Min(0.01f)] private float activeSeconds = 1.4f;
    [SerializeField, Min(1)] private int repeatCount = 3;
    [SerializeField, Min(0.01f)] private float damageIntervalSeconds = 0.65f;
    [SerializeField, Min(0.01f)] private float puddleIgniteIntervalSeconds = 0.2f;

    [Header("Shape")]
    [SerializeField, Min(0.1f)] private float range = 5f;
    [SerializeField, Range(1f, 180f)] private float angleDegrees = 55f;
    [SerializeField, Min(0f)] private float originForwardOffset = 0.45f;
    [SerializeField] private float puddleIgniteRadiusPadding = -0.25f;
    [SerializeField] private float puddleIgniteRangePadding = -0.15f;
    [SerializeField] private float puddleIgniteAnglePadding = -4f;

    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;

    [Header("Presentation")]
    [SerializeField] private GameObject fireBreathVisualPrefab;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DrunkenDragonController dragon = system != null ? system.GetComponent<DrunkenDragonController>() : null;
        if (dragon == null)
            yield break;

        AttackTelegraphService telegraphService = dragon.GetComponent<AttackTelegraphService>();

        try
        {
            for (int i = 0; i < repeatCount; i++)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                yield return RunFireBreathSequence(dragon, telegraphService, spec);
            }
        }
        finally
        {
            telegraphService?.HideCurrent();
            nextDamageAllowedTimes.Clear();
            dragon.PlayPatternTrigger(DrunkenDragonAnimationKeys.Idle);
        }
    }

    private IEnumerator RunFireBreathSequence(
        DrunkenDragonController dragon,
        AttackTelegraphService telegraphService,
        AbilitySpec spec)
    {
        dragon.PlayPatternTrigger(DrunkenDragonAnimationKeys.FirePrepare);
        ConeAimSnapshot aim = default;

        float elapsed = 0f;
        while (elapsed < prepareSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            aim = ResolveAimSnapshot(dragon, syncFacing: true);
            ShowOrUpdateWarningTelegraph(telegraphService, aim, prepareSeconds);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (IsAbilityCancelled(spec))
            yield break;

        aim = ResolveAimSnapshot(dragon, syncFacing: true);
        ShowOrUpdateWarningTelegraph(telegraphService, aim, prepareSeconds);
        telegraphService?.HideCurrent();

        dragon.PushFaceTargetLock();
        try
        {
            yield return WaitForSecondsUnlessCancelled(preFireDelaySeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            dragon.FacePatternDirection(aim.Direction);
            dragon.PlayPatternTrigger(DrunkenDragonAnimationKeys.Fire);
            yield return RunFixedFireBreath(dragon, aim, spec);
        }
        finally
        {
            dragon.PopFaceTargetLock();
        }
    }

    private IEnumerator RunFixedFireBreath(DrunkenDragonController dragon, ConeAimSnapshot aim, AbilitySpec spec)
    {
        GameObject fireBreathVisualObject = null;
        IConePatternVisual2D fireBreathVisual = null;

        try
        {
            fireBreathVisualObject = CreateFireBreathVisual(dragon, aim.Origin, out fireBreathVisual);
            fireBreathVisual?.Play(new ConePatternVisualSpec2D(
                aim.Origin,
                aim.Direction,
                range,
                angleDegrees,
                activeSeconds));

            yield return RunFireBreath(dragon, aim.Origin, aim.Direction, spec);
        }
        finally
        {
            fireBreathVisual?.Stop();
            if (fireBreathVisualObject != null)
                Destroy(fireBreathVisualObject);
        }
    }

    private IEnumerator RunFireBreath(
        DrunkenDragonController dragon,
        Vector2 origin,
        Vector2 direction,
        AbilitySpec spec)
    {
        nextDamageAllowedTimes.Clear();

        float elapsed = 0f;
        float nextPuddleIgniteTime = 0f;

        while (elapsed < activeSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            ApplyDamage(dragon, origin, direction);

            if (elapsed >= nextPuddleIgniteTime)
            {
                IgniteAlcoholPuddles(origin, direction);
                nextPuddleIgniteTime += puddleIgniteIntervalSeconds;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!IsAbilityCancelled(spec))
        {
            ApplyDamage(dragon, origin, direction);
            IgniteAlcoholPuddles(origin, direction);
        }
    }

    private ConeAimSnapshot ResolveAimSnapshot(DrunkenDragonController dragon, bool syncFacing)
    {
        Vector2 direction = dragon.GetDirectionToTargetOrFacing();
        if (syncFacing)
            dragon.FacePatternDirection(direction);

        Vector2 origin = ResolveOrigin(dragon, direction);
        return new ConeAimSnapshot(origin, direction);
    }

    private GameObject CreateFireBreathVisual(DrunkenDragonController dragon, Vector2 origin, out IConePatternVisual2D visual)
    {
        visual = null;
        if (fireBreathVisualPrefab == null)
            return null;

        GameObject visualObject = Instantiate(fireBreathVisualPrefab, origin, Quaternion.identity);
        visual = ResolveConeVisual(visualObject);
        if (visual != null)
            return visualObject;

        Debug.LogWarning("[DrunkenDragonFireBreath] Fire breath visual prefab does not contain an IConePatternVisual2D component.", dragon);
        Destroy(visualObject);
        return null;
    }

    private static IConePatternVisual2D ResolveConeVisual(GameObject visualObject)
    {
        if (visualObject == null)
            return null;

        MonoBehaviour[] behaviours = visualObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IConePatternVisual2D visual)
                return visual;
        }

        return null;
    }

    private Vector2 ResolveOrigin(DrunkenDragonController dragon, Vector2 direction)
    {
        if (dragon == null)
            return Vector2.zero;

        return dragon.ResolveFireBreathMouthPosition(direction, originForwardOffset);
    }

    private void ShowOrUpdateWarningTelegraph(AttackTelegraphService telegraphService, ConeAimSnapshot aim, float duration)
    {
        if (telegraphService == null)
            return;

        float rotationDeg = Mathf.Atan2(aim.Direction.y, aim.Direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateSector(
            aim.Origin,
            range,
            angleDegrees,
            rotationDeg,
            duration,
            warningTelegraphStyle);

        if (telegraphService.HasActiveTelegraph)
            telegraphService.UpdateCurrentGeometry(spec);
        else
            telegraphService.Show(spec);
    }

    private void ApplyDamage(DrunkenDragonController dragon, Vector2 origin, Vector2 direction)
    {
        if (dragon == null || damageEffect == null || damageAmount <= 0f)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, ResolveTargetMask(dragon));
        CombatHitPayload payload = MakeHitPayload(dragon);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            if (!IsPointInFireSector(origin, direction, hits[i].ClosestPoint(origin)))
                continue;

            if (!CanDamageTargetNow(targetRoot))
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hits[i].ClosestPoint(origin));
            nextDamageAllowedTimes[targetRoot] = Time.time + damageIntervalSeconds;
        }
    }

    private void IgniteAlcoholPuddles(Vector2 origin, Vector2 direction)
    {
        PuddleManager manager = PuddleManager.ResolveForScene();
        IReadOnlyList<PuddleAreaBase> puddles = manager != null ? manager.Puddles : null;
        if (puddles == null)
            return;

        for (int i = 0; i < puddles.Count; i++)
        {
            if (puddles[i] is not AlcoholPuddleArea alcohol || !alcohol.IsGroundActive)
                continue;

            float igniteRadius = Mathf.Max(0f, alcohol.GroundRadius + puddleIgniteRadiusPadding);
            float igniteRange = Mathf.Max(0.1f, range + puddleIgniteRangePadding);
            float igniteAngle = Mathf.Clamp(angleDegrees + puddleIgniteAnglePadding, 1f, 180f);
            if (!IsCircleOverlappingFireSector(origin, direction, alcohol.transform.position, igniteRadius, igniteRange, igniteAngle))
                continue;

            alcohol.RequestIgnite();
        }
    }

    private bool CanDamageTargetNow(GameObject targetRoot)
    {
        if (targetRoot == null)
            return false;

        return !nextDamageAllowedTimes.TryGetValue(targetRoot, out float nextAllowedTime) ||
               Time.time >= nextAllowedTime;
    }

    private bool IsPointInFireSector(Vector2 origin, Vector2 direction, Vector2 point)
    {
        Vector2 toPoint = point - origin;
        if (toPoint.sqrMagnitude > range * range)
            return false;

        if (toPoint.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector2.Angle(direction.normalized, toPoint.normalized);
        return angle <= angleDegrees * 0.5f;
    }

    private bool IsCircleOverlappingFireSector(
        Vector2 origin,
        Vector2 direction,
        Vector2 center,
        float radius,
        float sectorRange,
        float sectorAngleDegrees)
    {
        float safeRadius = Mathf.Max(0f, radius);
        Vector2 toCenter = center - origin;
        float rangeWithRadius = Mathf.Max(0.1f, sectorRange) + safeRadius;
        if (toCenter.sqrMagnitude > rangeWithRadius * rangeWithRadius)
            return false;

        if (safeRadius > 0f && toCenter.sqrMagnitude <= safeRadius * safeRadius)
            return true;

        float halfAngle = Mathf.Clamp(sectorAngleDegrees, 1f, 180f) * 0.5f;
        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (toCenter.sqrMagnitude > 0.0001f)
        {
            float angleToCenter = Vector2.Angle(normalizedDirection, toCenter.normalized);
            if (angleToCenter <= halfAngle)
                return true;
        }

        float safeSectorRange = Mathf.Max(0.1f, sectorRange);
        Vector2 leftEdge = Rotate(normalizedDirection, halfAngle) * safeSectorRange;
        Vector2 rightEdge = Rotate(normalizedDirection, -halfAngle) * safeSectorRange;
        float radiusSqr = safeRadius * safeRadius;

        return DistancePointToSegmentSqr(center, origin, origin + leftEdge) <= radiusSqr ||
               DistancePointToSegmentSqr(center, origin, origin + rightEdge) <= radiusSqr;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos);
    }

    private static float DistancePointToSegmentSqr(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= 0.0001f)
            return (point - segmentStart).sqrMagnitude;

        float t = Vector2.Dot(point - segmentStart, segment) / segmentLengthSqr;
        t = Mathf.Clamp01(t);
        Vector2 closest = segmentStart + segment * t;
        return (point - closest).sqrMagnitude;
    }

    private LayerMask ResolveTargetMask(DrunkenDragonController dragon)
    {
        Transform target = dragon != null ? dragon.CurrentTarget : null;
        return target != null ? (LayerMask)(1 << target.gameObject.layer) : Physics2D.DefaultRaycastLayers;
    }

    private CombatHitPayload MakeHitPayload(DrunkenDragonController dragon)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: dragon.AbilitySystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);
    }

    /// <summary>
    /// 책임:
    /// 화염 방사 한 회차가 사용할 조준 원점과 방향을 스냅샷으로 고정한다.
    /// </summary>
    private readonly struct ConeAimSnapshot
    {
        public ConeAimSnapshot(Vector2 origin, Vector2 direction)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }

        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
    }
}
