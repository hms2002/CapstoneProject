using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - 번개 유물의 추가타를 발생시키고, 자기 자신이 만든 HitConfirm로 재귀 발동하지 않도록 제어한다.
/// - 번개 피해의 실제 적용 대상 수집, 전용 causer 표식 생성/해제까지 함께 관리한다.
/// </summary>
public sealed class LightningStrikeProc2D : IRelicProc
{
    public Object Token => token;

    private readonly Object token;
    private readonly GameObject owner;
    private readonly AbilitySystem ownerSystem;
    private readonly AttributeSet ownerAttributeSet;

    private readonly GameplayTag triggerTag;
    private readonly GE_Damage_Spec damageEffect;
    private readonly AttributeDefinition attackPlusAttribute;
    private readonly float baseDamage;
    private readonly float radius;
    private readonly LayerMask enemyMask;

    private readonly LightningStrikeVfx lightningPrefab;
    private readonly float cooldownSeconds;
    private float nextReadyTime;

    private readonly GameplayTag hitConfirmedTag;
    private readonly GE_Knockback_Spec knockbackEffect;
    private readonly GameObject lightningCauser;

    public LightningStrikeProc2D(
        RelicContext ctx,
        GameplayTag triggerTag,
        GE_Damage_Spec damageEffect,
        AttributeDefinition attackPlusAttribute,
        float baseDamage,
        float radius,
        LayerMask enemyMask,
        LightningStrikeVfx lightningPrefab,
        float cooldownSeconds,
        GameplayTag hitConfirmedTag = null,
        GE_Knockback_Spec knockbackEffect = null)
    {
        this.token = ctx.token;
        this.owner = ctx.owner;
        this.ownerSystem = ctx.abilitySystem;
        this.ownerAttributeSet = ctx.attributeSet;

        this.triggerTag = triggerTag;
        this.damageEffect = damageEffect;
        this.attackPlusAttribute = attackPlusAttribute;
        this.baseDamage = baseDamage;
        this.radius = radius;
        this.enemyMask = enemyMask;

        this.lightningPrefab = lightningPrefab;
        this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        this.nextReadyTime = 0f;

        this.hitConfirmedTag = hitConfirmedTag;
        this.knockbackEffect = knockbackEffect;
        this.lightningCauser = CreateLightningCauser(owner, token);
    }

    public void Handle(GameplayTag tag, AbilityEventData data)
    {
        if (triggerTag == null || tag != triggerTag) return;
        if (ownerSystem == null || damageEffect == null) return;
        if (IsSelfTriggeredLightningHit(data)) return;
        if (cooldownSeconds > 0f && Time.time < nextReadyTime) return;

        Vector3 strikePos = ResolveStrikePosition(data);

        if (lightningPrefab != null)
        {
            var vfx = Object.Instantiate(lightningPrefab);
            vfx.Play(strikePos, () => ApplyAoeDamage(strikePos));
        }
        else
        {
            ApplyAoeDamage(strikePos);
        }

        if (cooldownSeconds > 0f)
            nextReadyTime = Time.time + cooldownSeconds;
    }

    public void Tick(float deltaTime)
    {
    }

    private Vector3 ResolveStrikePosition(AbilityEventData data)
    {
        if (data.Target != null)
            return data.Target.transform.position;

        return data.WorldPosition;
    }

    private void ApplyAoeDamage(Vector3 center)
    {
        float atkPlus = 0f;
        if (ownerAttributeSet != null && attackPlusAttribute != null)
            atkPlus = ownerAttributeSet.GetAttributeValue(attackPlusAttribute);

        float finalDamage = baseDamage + atkPlus;
        if (finalDamage <= 0f)
            return;

        Collider2D[] hits = enemyMask.value != 0
            ? Physics2D.OverlapCircleAll(center, radius, enemyMask)
            : Physics2D.OverlapCircleAll(center, radius);

        if (hits == null || hits.Length == 0)
            return;

        var uniqueTargets = new List<GameObject>(hits.Length);
        var visited = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            GameObject target = ResolveTargetRoot(hit);
            if (target == null || target == owner)
                continue;

            if (!visited.Add(target))
                continue;

            uniqueTargets.Add(target);
        }

        if (uniqueTargets.Count == 0)
            return;

        var snapshot = new CombatDamageSnapshot(
            finalHpDamage: finalDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            isCriticalHit: false
        );

        CombatDamageApplicator.ApplyToTargets(
            system: ownerSystem,
            spec: null,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            targets: uniqueTargets,
            snapshot: snapshot,
            hitConfirmedTag: hitConfirmedTag,
            causer: lightningCauser != null ? lightningCauser : owner
        );
    }

    private bool IsSelfTriggeredLightningHit(AbilityEventData data)
    {
        var causerObject = data.Causer as GameObject;
        if (causerObject == null)
            return false;

        var marker = causerObject.GetComponent<LightningStrikeCauserMarker>();
        if (marker == null)
            return false;

        return marker.OwnerToken == token;
    }

    private static GameObject CreateLightningCauser(GameObject owner, Object ownerToken)
    {
        if (owner == null)
            return null;

        var go = new GameObject("LightningStrikeCauser");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(owner.transform, false);

        var marker = go.AddComponent<LightningStrikeCauserMarker>();
        marker.Initialize(ownerToken);
        return go;
    }

    private static GameObject ResolveTargetRoot(Collider2D hit)
    {
        if (hit == null)
            return null;

        return CombatTargetResolver2D.ResolveDamageTarget(hit);
    }

    public void Dispose()
    {
        if (lightningCauser != null)
            Object.Destroy(lightningCauser);
    }
}

/// <summary>
/// 책임 :
/// - 번개 유물이 만든 추가타의 causer임을 식별하는 표식 컴포넌트다.
/// - owner token을 함께 들고 있어 같은 유물 인스턴스가 만든 재귀 발동만 정확히 차단한다.
/// </summary>
public sealed class LightningStrikeCauserMarker : MonoBehaviour
{
    public Object OwnerToken { get; private set; }

    public void Initialize(Object ownerToken)
    {
        OwnerToken = ownerToken;
    }
}
