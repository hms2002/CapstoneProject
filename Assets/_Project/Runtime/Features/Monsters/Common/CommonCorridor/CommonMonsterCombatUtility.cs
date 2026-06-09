using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 공통 몬스터 구현들이 반복해서 사용하는 방향 계산, 피해 payload 생성, 단발 범위 피해 같은 순수 전투 보조 함수를 제공한다.
/// - 몬스터별 FSM/패턴 구조를 공통화하지 않고, 중복되기 쉬운 계산만 안전하게 공유한다.
/// </summary>
public static class CommonMonsterCombatUtility
{
    public const float PlayerBaseSpeedFallback = 4f;
    private static readonly RaycastHit2D[] DashWallHits = new RaycastHit2D[8];

    public static void TriggerAnimation(Component owner, CommonMonsterAnimationCue cue)
    {
        if (owner == null)
            return;

        CommonMonsterAnimatorBridge bridge = owner.GetComponent<CommonMonsterAnimatorBridge>();
        if (bridge == null)
            return;

        switch (cue)
        {
            case CommonMonsterAnimationCue.AttackReady:
                bridge.TriggerAttackReady();
                break;
            case CommonMonsterAnimationCue.Attack:
                bridge.TriggerAttack();
                break;
            case CommonMonsterAnimationCue.Recover:
                bridge.TriggerRecover();
                break;
            case CommonMonsterAnimationCue.Die:
                bridge.TriggerDie();
                break;
            case CommonMonsterAnimationCue.Jump:
                bridge.TriggerJump();
                break;
            case CommonMonsterAnimationCue.Land:
                bridge.TriggerLand();
                break;
            case CommonMonsterAnimationCue.LandEnd:
                bridge.TriggerLandEnd();
                break;
        }
    }

    public static Vector2 DirectionTo(GameObject from, GameObject target, bool fallbackLeft)
    {
        Vector2 direction = target != null && from != null
            ? (Vector2)target.transform.position - (Vector2)from.transform.position
            : Vector2.zero;

        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        return fallbackLeft ? Vector2.left : Vector2.right;
    }

    public static bool InRange(Transform origin, GameObject target, float range)
    {
        if (origin == null || target == null)
            return false;

        Vector2 delta = target.transform.position - origin.position;
        return delta.sqrMagnitude <= range * range;
    }

    /// <summary>
    /// 책임:
    /// - 원거리 조준선과 투사체 방향 계산이 발밑/root 대신 목적별 전투 기준점을 겨냥하도록 계산한다.
    /// - provider가 없는 기존 객체도 hurtbox/sprite/root 순 fallback으로 안정적으로 지원한다.
    /// </summary>
    public static Vector2 ResolveAimPoint(GameObject target, CombatAimPointKind kind = CombatAimPointKind.ProjectileTarget)
    {
        if (target == null)
            return Vector2.zero;

        if (kind == CombatAimPointKind.Root)
            return target.transform.position;

        CombatAimPointProvider2D provider = target.GetComponentInChildren<CombatAimPointProvider2D>();
        if (provider != null)
            return provider.Resolve(kind);

        Collider2D collider = ResolvePreferredCollider(target);
        if (collider != null)
            return collider.bounds.center;

        SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null && renderer.enabled)
            return renderer.bounds.center;

        return target.transform.position;
    }

    public static Vector2 ResolveTelegraphAimPoint(GameObject target)
    {
        return ResolveAimPoint(target, CombatAimPointKind.ProjectileTarget);
    }

    public static Vector2 DirectionToAimPoint(Vector2 origin, GameObject target, bool fallbackLeft)
    {
        Vector2 targetPoint = ResolveAimPoint(target, CombatAimPointKind.ProjectileTarget);
        Vector2 direction = target != null
            ? targetPoint - origin
            : Vector2.zero;

        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        return fallbackLeft ? Vector2.left : Vector2.right;
    }

    private static Collider2D ResolvePreferredCollider(GameObject target)
    {
        CombatHurtbox2D[] hurtboxes = target.GetComponentsInChildren<CombatHurtbox2D>();
        for (int i = 0; i < hurtboxes.Length; i++)
        {
            CombatHurtbox2D hurtbox = hurtboxes[i];
            if (hurtbox == null || !hurtbox.enabled)
                continue;

            Collider2D hurtboxCollider = hurtbox.GetComponent<Collider2D>();
            if (hurtboxCollider != null && hurtboxCollider.enabled)
                return hurtboxCollider;
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
        Collider2D triggerFallback = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;

            if (!collider.isTrigger)
                return collider;

            if (triggerFallback == null)
                triggerFallback = collider;
        }

        return triggerFallback;
    }

    public static float ResolvePlayerBaseSpeed(Transform target, float fallback = PlayerBaseSpeedFallback)
    {
        if (target == null)
            return fallback;

        AttributeStatSource statSource = target.GetComponent<AttributeStatSource>();
        if (statSource == null)
            return fallback;

        float baseSpeed = statSource.Get(StatId.MoveSpeedBase);
        if (baseSpeed > 0f)
            return baseSpeed;

        float finalSpeed = statSource.Get(StatId.MoveSpeedFinal);
        return finalSpeed > 0f ? finalSpeed : fallback;
    }

    public static CombatHitPayload BuildPayload(
        AbilitySystem system,
        AbilitySpec spec,
        GE_Damage_Spec damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject causer,
        float damageAmount,
        float knockbackImpulse)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: system,
            sourceSpec: spec,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: causer);
    }

    public static bool TryApplyCircleDamage(
        Vector2 center,
        float diameter,
        LayerMask targetLayers,
        GameObject self,
        CombatHitPayload payload)
    {
        if (payload == null || !payload.IsValid())
            return false;

        float radius = Mathf.Max(0.01f, diameter * 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, targetLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (targetRoot == null || targetRoot == self)
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(center));
            return true;
        }

        return false;
    }

    public static Vector2 ResolveDashWallSlideDelta(
        Vector2 origin,
        Vector2 desiredDelta,
        float castRadius,
        LayerMask obstacleLayers,
        float skinWidth)
    {
        if (desiredDelta.sqrMagnitude <= 0.000001f || obstacleLayers.value == 0)
            return desiredDelta;

        float radius = Mathf.Max(0.01f, castRadius);
        float skin = Mathf.Max(0f, skinWidth);
        Vector2 firstMove = ResolveCastMove(origin, desiredDelta, radius, obstacleLayers, skin, out RaycastHit2D hit);
        if (hit.collider == null)
            return firstMove;

        Vector2 remaining = desiredDelta - firstMove;
        Vector2 slide = remaining - hit.normal * Vector2.Dot(remaining, hit.normal);
        if (slide.sqrMagnitude <= 0.000001f)
            return firstMove;

        Vector2 slideOrigin = origin + firstMove;
        Vector2 slideMove = ResolveCastMove(slideOrigin, slide, radius, obstacleLayers, skin, out _);
        return firstMove + slideMove;
    }

    private static Vector2 ResolveCastMove(
        Vector2 origin,
        Vector2 desiredDelta,
        float castRadius,
        LayerMask obstacleLayers,
        float skinWidth,
        out RaycastHit2D nearestHit)
    {
        nearestHit = default;
        float distance = desiredDelta.magnitude;
        if (distance <= 0.0001f)
            return Vector2.zero;

        Vector2 direction = desiredDelta / distance;
        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = obstacleLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.CircleCast(origin, castRadius, direction, filter, DashWallHits, distance);
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = DashWallHits[i];
            if (hit.collider == null || hit.collider.isTrigger || hit.distance < 0f)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        if (nearestHit.collider == null)
            return desiredDelta;

        float allowedDistance = Mathf.Max(0f, nearestDistance - skinWidth);
        return direction * Mathf.Min(distance, allowedDistance);
    }
}

/// <summary>
/// 책임:
/// - 공통 복도 몬스터 코드가 Animator 파라미터 이름을 직접 알지 않도록 애니메이션 의도만 표현한다.
/// </summary>
public enum CommonMonsterAnimationCue
{
    AttackReady,
    Attack,
    Recover,
    Die,
    Jump,
    Land,
    LandEnd
}
