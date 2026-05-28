using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 전방 화염 방사 패턴을 실행하며, 부채꼴 예고, 지속 피해, 술 장판 점화를 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DragonFireBreath", menuName = "GAS/Ability Logic/Dragon/AL_DragonFireBreath")]
public sealed class AbilityLogic_DragonFireBreath : AbilityLogic
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
    [SerializeField, Min(0f)] private float mouthFallbackForwardOffset = 0.45f;
    [SerializeField] private float puddleIgniteRadiusPadding = -0.25f;
    [SerializeField] private float puddleIgniteRangePadding = -0.15f;
    [SerializeField] private float puddleIgniteAnglePadding = -4f;

    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;
    [SerializeField] private bool useWallClippedWarningTelegraph = true;
    [SerializeField] private LayerMask warningTelegraphWallLayers;
    [SerializeField, Min(2)] private int warningTelegraphSampleCount = 48;
    [SerializeField, Min(0f)] private float warningTelegraphWallSkinWidth = 0.03f;

    [Header("Presentation")]
    [SerializeField] private WorldPresentationHook inhalePreparePresentation;
    [SerializeField] private SoundRef fireBreathLoopSound;
    [SerializeField] private GameObject fireBreathVisualPrefab;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
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
            dragon.PlayPatternTrigger(DragonAnimationKeys.Idle);
        }
    }

    private IEnumerator RunFireBreathSequence(
        DragonController dragon,
        AttackTelegraphService telegraphService,
        AbilitySpec spec)
    {
        dragon.PlayPatternTrigger(DragonAnimationKeys.FirePrepare);
        ConeAimSnapshot aim = default;
        List<FollowedPresentationVisual> inhalePrepareVisuals = SpawnInhalePreparePresentation(dragon);

        try
        {
            float elapsed = 0f;
            while (elapsed < prepareSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                aim = ResolveAimSnapshot(dragon, syncFacing: true);
                UpdateFollowedPresentationVisuals(inhalePrepareVisuals, dragon, aim.FireDirection);
                ShowOrUpdateWarningTelegraph(telegraphService, aim, prepareSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            ReleaseFollowedPresentationVisuals(inhalePrepareVisuals);
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

            dragon.FacePatternDirection(aim.WarningDirection);
            dragon.PlayPatternTrigger(DragonAnimationKeys.Fire);
            yield return RunFixedFireBreath(dragon, aim, spec);
        }
        finally
        {
            dragon.PopFaceTargetLock();
        }
    }

    private IEnumerator RunFixedFireBreath(DragonController dragon, ConeAimSnapshot aim, AbilitySpec spec)
    {
        GameObject fireBreathVisualObject = null;
        IConePatternVisual2D fireBreathVisual = null;
        AudioHandle fireBreathSoundHandle = AudioHandle.Invalid;

        try
        {
            fireBreathSoundHandle = SoundPlaybackUtility.Play(
                fireBreathLoopSound,
                instigator: dragon.gameObject,
                causer: dragon.gameObject,
                target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                position: aim.FireOrigin,
                sourceObject: this);

            fireBreathVisualObject = CreateFireBreathVisual(dragon, aim.FireOrigin, out fireBreathVisual);
            fireBreathVisual?.Play(new ConePatternVisualSpec2D(
                aim.FireOrigin,
                aim.FireDirection,
                aim.FireRange,
                angleDegrees,
                activeSeconds));

            yield return RunFireBreath(dragon, aim.WarningOrigin, aim.WarningDirection, spec);
        }
        finally
        {
            SoundPlaybackUtility.Stop(fireBreathSoundHandle);
            fireBreathVisual?.Stop();
            if (fireBreathVisualObject != null)
                Destroy(fireBreathVisualObject);
        }
    }

    private IEnumerator RunFireBreath(
        DragonController dragon,
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

    private ConeAimSnapshot ResolveAimSnapshot(DragonController dragon, bool syncFacing)
    {
        Vector2 direction = dragon.GetDirectionToTargetOrFacing();
        if (syncFacing)
            dragon.FacePatternDirection(direction);

        Vector2 warningOrigin = ResolveWarningOrigin(dragon, direction);
        Vector2 fireOrigin = ResolveFireOrigin(dragon, direction);
        Vector2 warningCenterEnd = warningOrigin + direction.normalized * range;
        Vector2 fireDirection = warningCenterEnd - fireOrigin;
        float fireRange = fireDirection.magnitude;
        if (fireDirection.sqrMagnitude <= 0.0001f)
            fireDirection = direction;

        return new ConeAimSnapshot(
            warningOrigin,
            direction,
            fireOrigin,
            fireDirection,
            fireRange);
    }

    private GameObject CreateFireBreathVisual(DragonController dragon, Vector2 origin, out IConePatternVisual2D visual)
    {
        visual = null;
        if (fireBreathVisualPrefab == null)
            return null;

        GameObject visualObject = Instantiate(fireBreathVisualPrefab, origin, Quaternion.identity);
        visual = ResolveConeVisual(visualObject);
        if (visual != null)
            return visualObject;

        Debug.LogWarning("[DragonFireBreath] Fire breath visual prefab does not contain an IConePatternVisual2D component.", dragon);
        Destroy(visualObject);
        return null;
    }

    /// <summary>
    /// 책임:
    /// 화염 방사 준비 동작의 들이쉬는 연출을 기존 브레스 입 소켓 위치와 조준 방향을 기준으로 재생한다.
    /// </summary>
    private List<FollowedPresentationVisual> SpawnInhalePreparePresentation(DragonController dragon)
    {
        List<FollowedPresentationVisual> visuals = new();
        if (dragon == null || !inhalePreparePresentation.HasAnyContent)
            return visuals;

        Vector2 direction = dragon.GetDirectionToTargetOrFacing();
        WorldPresentationContext context = BuildInhalePresentationContext(dragon, direction);

        WorldPresentationRuntime.PlaySignalOnly(inhalePreparePresentation, context);
        AddFollowedPresentationVisual(visuals, inhalePreparePresentation.effect, context);
        AddFollowedPresentationVisual(visuals, inhalePreparePresentation.particle, context);
        return visuals;
    }

    /// <summary>
    /// 책임:
    /// 준비 이펙트가 좌우 반전으로 변하는 브레스 입 위치를 따라가도록 현재 소켓 위치로 갱신한다.
    /// </summary>
    private void UpdateFollowedPresentationVisuals(
        List<FollowedPresentationVisual> visuals,
        DragonController dragon,
        Vector2 direction)
    {
        if (visuals == null || visuals.Count == 0 || dragon == null)
            return;

        WorldPresentationContext context = BuildInhalePresentationContext(dragon, direction);
        for (int i = visuals.Count - 1; i >= 0; i--)
        {
            if (!visuals[i].IsValid)
            {
                visuals.RemoveAt(i);
                continue;
            }

            visuals[i].Apply(context);
        }
    }

    /// <summary>
    /// 책임:
    /// 화염 방사 준비 시간이 끝나거나 취소될 때 소켓 추적용 준비 이펙트를 정리한다.
    /// </summary>
    private static void ReleaseFollowedPresentationVisuals(List<FollowedPresentationVisual> visuals)
    {
        if (visuals == null)
            return;

        for (int i = 0; i < visuals.Count; i++)
            visuals[i].Release();

        visuals.Clear();
    }

    private static void AddFollowedPresentationVisual(
        List<FollowedPresentationVisual> visuals,
        SpawnedPresentationHook hook,
        WorldPresentationContext context)
    {
        if (visuals == null || !hook.HasContent)
            return;

        GameObject instance = PresentationSpawnService.SpawnPersistent(hook, context);
        if (instance != null)
            visuals.Add(new FollowedPresentationVisual(instance, hook));
    }

    private WorldPresentationContext BuildInhalePresentationContext(DragonController dragon, Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 origin = ResolveFireOrigin(dragon, safeDirection);
        float angleDeg = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;

        return WorldPresentationContext.AtWorld(
            instigator: dragon.gameObject,
            position: origin,
            fallbackDirection: safeDirection,
            target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
            sourceObject: this,
            rotation: Quaternion.Euler(0f, 0f, angleDeg));
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

    private Vector2 ResolveWarningOrigin(DragonController dragon, Vector2 direction)
    {
        if (dragon == null)
            return Vector2.zero;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        return (Vector2)dragon.transform.position + safeDirection * originForwardOffset;
    }

    private Vector2 ResolveFireOrigin(DragonController dragon, Vector2 direction)
    {
        if (dragon == null)
            return Vector2.zero;

        return dragon.ResolveFireBreathMouthPosition(direction, mouthFallbackForwardOffset);
    }

    private void ShowOrUpdateWarningTelegraph(
        AttackTelegraphService telegraphService,
        ConeAimSnapshot aim,
        float duration)
    {
        if (telegraphService == null)
            return;

        float rotationDeg = Mathf.Atan2(aim.WarningDirection.y, aim.WarningDirection.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateSector(
            aim.WarningOrigin,
            range,
            angleDegrees,
            rotationDeg,
            duration,
            warningTelegraphStyle);
        if (useWallClippedWarningTelegraph)
            spec = spec.WithWallClipping(
                warningTelegraphWallLayers,
                warningTelegraphSampleCount,
                warningTelegraphWallSkinWidth);

        if (telegraphService.HasActiveTelegraph)
            telegraphService.UpdateCurrentGeometry(spec);
        else
            telegraphService.Show(spec);
    }

    private void ApplyDamage(DragonController dragon, Vector2 origin, Vector2 direction)
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
            finalKnockbackImpulse: 0f,
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
    /// 화염 방사 한 회차가 사용할 경고/판정 원뿔과 실제 입 연출 원뿔 정보를 스냅샷으로 고정한다.
    /// </summary>
    private readonly struct ConeAimSnapshot
    {
        public ConeAimSnapshot(
            Vector2 warningOrigin,
            Vector2 warningDirection,
            Vector2 fireOrigin,
            Vector2 fireDirection,
            float fireRange)
        {
            WarningOrigin = warningOrigin;
            WarningDirection = warningDirection.sqrMagnitude > 0.0001f ? warningDirection.normalized : Vector2.right;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : WarningDirection;
            FireRange = Mathf.Max(0.01f, fireRange);
        }

        public Vector2 WarningOrigin { get; }
        public Vector2 WarningDirection { get; }
        public Vector2 FireOrigin { get; }
        public Vector2 FireDirection { get; }
        public float FireRange { get; }
    }

    /// <summary>
    /// 책임:
    /// PresentationSpawnService로 생성된 준비 이펙트 인스턴스를 특정 월드 프레젠테이션 문맥에 맞춰 갱신하고 해제한다.
    /// </summary>
    private readonly struct FollowedPresentationVisual
    {
        private readonly GameObject instance;
        private readonly SpawnedPresentationHook hook;

        public FollowedPresentationVisual(GameObject instance, SpawnedPresentationHook hook)
        {
            this.instance = instance;
            this.hook = hook;
        }

        public bool IsValid => instance != null;

        public void Apply(in WorldPresentationContext context)
        {
            if (instance == null)
                return;

            Transform instanceTransform = instance.transform;
            Quaternion rotation = context.Rotation * Quaternion.Euler(0f, 0f, hook.rotationOffsetZ);
            Vector3 position = context.Position + (context.Rotation * hook.localOffset);
            instanceTransform.SetPositionAndRotation(position, rotation);
        }

        public void Release()
        {
            if (instance != null)
                PresentationSpawnService.Release(instance);
        }
    }
}
