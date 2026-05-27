using System.Collections;
using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 돌진 콤보 패턴을 실행하며, 경고 표시와 돌진 이동, 돌진 중 접촉 피해를 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DragonDashCombo", menuName = "GAS/Ability Logic/Dragon/AL_DragonDashCombo")]
public sealed class AbilityLogic_DragonDashCombo : AbilityLogic
{
    private readonly HashSet<GameObject> damagedTargets = new();

    [Header("Combo")]
    [SerializeField, Min(1)] private int maxDashCount = 1;
    [SerializeField, Range(0f, 1f)] private float secondDashChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float thirdDashChance = 0.3f;
    [SerializeField, Min(0f)] private float betweenDashDelaySeconds = 0.15f;
    [SerializeField] private float[] delayAfterDashByDash = { 0.15f, 0.12f, 0f };

    [Header("Dash")]
    [SerializeField, Min(0f)] private float warningSeconds = 0.9f;
    [SerializeField] private float[] warningSecondsByDash = { 0.9f, 1.0f, 1.1f };
    [SerializeField, Min(0.1f)] private float dashDistance = 4.5f;
    [SerializeField, Min(0.01f)] private float dashDurationSeconds = 0.28f;
    [SerializeField] private float[] dashDurationSecondsByDash = { 0.18f, 0.16f, 0.14f };
    [SerializeField, Min(0.1f)] private float hitWidth = 1.4f;
    [SerializeField] private float[] hitWidthsByDash = { 1.4f, 2f, 2.5f };
    [SerializeField, Min(0.1f)] private float contactHitDepth = 1.2f;
    [SerializeField, Min(0f)] private float hitForwardPadding = 0.4f;
    [SerializeField, Min(0f)] private float rangeIncreasePerDash = 0.75f;

    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse;

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;

    [Header("Presentation")]
    [SerializeField] private WorldPresentationHook dashAttackPresentation;
    [SerializeField] private WorldPresentationHook dashHitPresentation;

    [Header("Afterimage")]
    [SerializeField] private bool enableDashAfterimage = true;
    [SerializeField, Min(0.01f)] private float afterimageEmissionInterval = 0.035f;
    [SerializeField, Min(0.01f)] private float afterimageLifetimeSeconds = 0.18f;
    [SerializeField] private Color afterimageColor = new(1f, 0.35f, 0.18f, 0.42f);
    [SerializeField] private bool clearAfterimagesOnCancel;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
        if (dragon == null)
            yield break;

        AbilityMotionController2D motion = dragon.GetComponent<AbilityMotionController2D>();
        EntityCollisionProfile2D collisionProfile = dragon.GetComponent<EntityCollisionProfile2D>();
        AttackTelegraphService telegraphService = dragon.GetComponent<AttackTelegraphService>();
        int dashCount = ResolveDashCount();
        dragon.RuntimeData.SetLastDashComboCount(dashCount);

        try
        {
            for (int i = 0; i < dashCount; i++)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                Vector2 start = dragon.transform.position;
                Vector2 direction = dragon.GetDirectionToTargetOrFacing(start);
                float resolvedDistance = dashDistance + (rangeIncreasePerDash * i);
                float resolvedHitWidth = ResolveHitWidth(i);
                float resolvedWarningSeconds = ResolveWarningSeconds(i);
                float resolvedDashDuration = ResolveDashDurationSeconds(i);

                dragon.FacePatternDirection(direction);
                dragon.PushFaceTargetLock();
                try
                {
                    dragon.PlayPatternTrigger(DragonAnimationKeys.DashReady);
                    ShowDashTelegraph(telegraphService, start, direction, resolvedDistance, resolvedHitWidth, resolvedWarningSeconds);

                    yield return WaitForSecondsUnlessCancelled(resolvedWarningSeconds, spec);
                    if (IsAbilityCancelled(spec))
                        yield break;

                    telegraphService?.HideCurrent();
                    dragon.PlayPatternTrigger(DragonAnimationKeys.DashAttack);
                    PlayDashAttackPresentation(dragon, start, direction, resolvedDistance, resolvedHitWidth);

                    damagedTargets.Clear();
                    yield return RunDashWithActorPassThrough(
                        dragon,
                        motion,
                        collisionProfile,
                        start,
                        direction,
                        resolvedDistance,
                        resolvedHitWidth,
                        resolvedDashDuration,
                        spec);

                    if (IsAbilityCancelled(spec))
                        yield break;

                    float resolvedDelayAfterDash = ResolveDelayAfterDash(i);
                    if (i < dashCount - 1)
                        yield return WaitForSecondsUnlessCancelled(resolvedDelayAfterDash, spec);
                }
                finally
                {
                    dragon.PopFaceTargetLock();
                }
            }
        }
        finally
        {
            StopDashAfterimage(dragon, IsAbilityCancelled(spec) && clearAfterimagesOnCancel);
            telegraphService?.HideCurrent();
            damagedTargets.Clear();
        }
    }

    private IEnumerator RunDashWithActorPassThrough(
        DragonController dragon,
        AbilityMotionController2D motion,
        EntityCollisionProfile2D collisionProfile,
        Vector2 start,
        Vector2 direction,
        float distance,
        float resolvedHitWidth,
        float duration,
        AbilitySpec spec)
    {
        collisionProfile?.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);

        try
        {
            BeginDashAfterimage(dragon);
            motion?.StartLunge(start, direction, distance, duration);
            yield return TickDashContactDamage(dragon, direction, resolvedHitWidth, duration, spec);
        }
        finally
        {
            StopDashAfterimage(dragon, IsAbilityCancelled(spec) && clearAfterimagesOnCancel);

            if (IsAbilityCancelled(spec))
                motion?.CancelMotion();

            collisionProfile?.RestoreDefaultMode();
        }
    }

    private int ResolveDashCount()
    {
        int count = 1;
        int cappedMax = Mathf.Clamp(maxDashCount, 1, 3);

        if (cappedMax >= 2 && Random.value <= secondDashChance)
            count = 2;

        if (cappedMax >= 3 && count == 2 && Random.value <= thirdDashChance)
            count = 3;

        return count;
    }

    private float ResolveWarningSeconds(int dashIndex)
    {
        if (warningSecondsByDash != null && dashIndex >= 0 && dashIndex < warningSecondsByDash.Length)
            return Mathf.Max(0f, warningSecondsByDash[dashIndex]);

        return Mathf.Max(0f, warningSeconds);
    }

    private float ResolveDashDurationSeconds(int dashIndex)
    {
        if (dashDurationSecondsByDash != null && dashIndex >= 0 && dashIndex < dashDurationSecondsByDash.Length)
            return Mathf.Max(0.01f, dashDurationSecondsByDash[dashIndex]);

        return Mathf.Max(0.01f, dashDurationSeconds);
    }

    private float ResolveDelayAfterDash(int dashIndex)
    {
        if (delayAfterDashByDash != null && dashIndex >= 0 && dashIndex < delayAfterDashByDash.Length)
            return Mathf.Max(0f, delayAfterDashByDash[dashIndex]);

        return Mathf.Max(0f, betweenDashDelaySeconds);
    }

    private float ResolveHitWidth(int dashIndex)
    {
        if (hitWidthsByDash != null && dashIndex >= 0 && dashIndex < hitWidthsByDash.Length)
            return Mathf.Max(0.1f, hitWidthsByDash[dashIndex]);

        return Mathf.Max(0.1f, hitWidth);
    }

    private void ShowDashTelegraph(
        AttackTelegraphService telegraphService,
        Vector2 start,
        Vector2 direction,
        float distance,
        float resolvedHitWidth,
        float duration)
    {
        if (telegraphService == null)
            return;

        float length = Mathf.Max(0.1f, distance + hitForwardPadding);
        Vector3 center = start + (direction * (length * 0.5f));
        float rotationDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec warningSpec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, resolvedHitWidth),
            rotationDeg,
            duration,
            warningTelegraphStyle);

        telegraphService.Show(warningSpec);
    }

    private IEnumerator TickDashContactDamage(
        DragonController dragon,
        Vector2 direction,
        float resolvedHitWidth,
        float duration,
        AbilitySpec spec)
    {
        if (dragon == null || damageEffect == null || duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            ApplyDashContactDamage(dragon, direction, resolvedHitWidth);
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        if (!IsAbilityCancelled(spec))
            ApplyDashContactDamage(dragon, direction, resolvedHitWidth);
    }

    private void ApplyDashContactDamage(
        DragonController dragon,
        Vector2 direction,
        float resolvedHitWidth)
    {
        if (dragon == null || damageEffect == null)
            return;

        float length = Mathf.Max(0.1f, contactHitDepth + hitForwardPadding);
        Vector2 center = (Vector2)dragon.transform.position + (direction * (length * 0.5f));
        float rotationDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        LayerMask targetMask = ResolveTargetMask(dragon);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(length, resolvedHitWidth), rotationDeg, targetMask);
        CombatHitPayload payload = MakeHitPayload(dragon);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            if (!damagedTargets.Add(targetRoot))
                continue;

            Vector2 hitPoint = hits[i].ClosestPoint(center);
            CombatHitPayloadApplier.Apply(targetRoot, payload, hitPoint);
            PlayDashHitPresentation(dragon, targetRoot, hitPoint, direction);
        }
    }

    /// <summary>
    /// 책임:
    /// 돌진 콤보의 실제 공격 범위를 맞았는지와 무관하게 월드 연출로 출력한다.
    /// </summary>
    private void PlayDashAttackPresentation(
        DragonController dragon,
        Vector2 start,
        Vector2 direction,
        float distance,
        float resolvedHitWidth)
    {
        if (dragon == null || !dashAttackPresentation.HasAnyContent)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float length = Mathf.Max(0.1f, distance + hitForwardPadding);
        Vector3 center = start + (safeDirection * (length * 0.5f));
        float angleDeg = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
        Vector3 presentationScale = new(length, Mathf.Max(0.1f, resolvedHitWidth), 1f);

        WorldPresentationContext context = WorldPresentationContext.AtWorld(
            instigator: dragon.gameObject,
            position: center,
            fallbackDirection: safeDirection,
            target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
            sourceObject: this,
            rotation: Quaternion.Euler(0f, 0f, angleDeg));

        WorldPresentationRuntime.PlaySignalOnly(dashAttackPresentation, context);
        SpawnScaledDashAttackVisual(dashAttackPresentation.effect, context, presentationScale);
        SpawnScaledDashAttackVisual(dashAttackPresentation.particle, context, presentationScale);
    }

    private static void SpawnScaledDashAttackVisual(
        SpawnedPresentationHook hook,
        WorldPresentationContext context,
        Vector3 presentationScale)
    {
        if (!hook.HasContent)
            return;

        GameObject instance = PresentationSpawnService.SpawnOneShot(hook, context);
        if (instance == null)
            return;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, presentationScale);
    }

    /// <summary>
    /// 책임:
    /// 돌진 콤보 중 피해가 확정된 위치에 AL이 지정한 공격 적중 연출을 재생한다.
    /// </summary>
    private void PlayDashHitPresentation(
        DragonController dragon,
        GameObject targetRoot,
        Vector2 hitPoint,
        Vector2 direction)
    {
        if (dragon == null || !dashHitPresentation.HasAnyContent)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float angleDeg = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;

        WorldPresentationRuntime.Play(
            dashHitPresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon.gameObject,
                position: hitPoint,
                fallbackDirection: safeDirection,
                target: targetRoot,
                sourceObject: this,
                rotation: Quaternion.Euler(0f, 0f, angleDeg)));
    }

    /// <summary>
    /// 책임:
    /// 실제 돌진 이동 구간에만 취룡 본체 SpriteRenderer 잔상 방출을 시작한다.
    /// </summary>
    private void BeginDashAfterimage(DragonController dragon)
    {
        if (!enableDashAfterimage || dragon == null)
            return;

        SpriteAfterimageEmitter2D emitter = dragon.GetComponent<SpriteAfterimageEmitter2D>();
        if (emitter == null)
            emitter = dragon.gameObject.AddComponent<SpriteAfterimageEmitter2D>();

        Transform sourceRoot = dragon.BodyVisualRoot != null ? dragon.BodyVisualRoot : dragon.transform;
        emitter.Begin(sourceRoot, afterimageEmissionInterval, afterimageLifetimeSeconds, afterimageColor);
    }

    /// <summary>
    /// 책임:
    /// 돌진 잔상 생성을 멈추고, 강제 취소 정책에 따라 이미 생성된 잔상까지 정리한다.
    /// </summary>
    private static void StopDashAfterimage(DragonController dragon, bool clearGhosts)
    {
        if (dragon == null)
            return;

        SpriteAfterimageEmitter2D emitter = dragon.GetComponent<SpriteAfterimageEmitter2D>();
        if (emitter == null)
            return;

        emitter.StopEmission();
        if (clearGhosts)
            emitter.ClearSpawnedGhosts();
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
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);
    }
}
