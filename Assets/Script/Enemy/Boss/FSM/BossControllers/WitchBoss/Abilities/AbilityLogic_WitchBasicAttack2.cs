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
    private readonly HashSet<GameObject> damagedTargets = new();

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
}
