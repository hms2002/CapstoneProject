using UnityEngine;
using UnityGAS;

public abstract class Slime : Mob, IMobAttackDecisionSource
{
    private const float SplitWakeSeconds = 1f;
    private const float PlayerBaseSpeedFallback = 4f;

    private MobAbilityCoordinator abilityCoordinator;
    private float wakeTime;

    /// <summary>분열로 생성된 슬라임의 대기 시간과 타깃을 설정합니다.</summary>
    public virtual void InitSplit(Transform nextTarget)
    {
        wakeTime = Time.time + SplitWakeSeconds;
        ApplyStats();

        if (nextTarget != null)
            SetTarget(nextTarget);
    }

    public abstract bool TryBuildAttackRequest(out MobAttackRequest request);

    /// <summary>공격 상태에 들어갈 때 필요한 기본 처리를 수행합니다.</summary>
    public virtual void OnAttackStateEntered(MobAttackRequest request)
    {
    }

    /// <summary>공격 상태가 끝날 때 필요한 기본 처리를 수행합니다.</summary>
    public virtual void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
    }

    /// <summary>슬라임 종류별 기본 스탯을 적용합니다.</summary>
    protected abstract void ApplyStats();

    /// <summary>MobAbilityCoordinator를 찾아서 보관합니다.</summary>
    protected void CacheCoordinator()
    {
        abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator == null)
            abilityCoordinator = gameObject.AddComponent<MobAbilityCoordinator>();
    }

    /// <summary>현재 실행 중인 몬스터 어빌리티를 취소합니다.</summary>
    protected void CancelAbility()
    {
        abilityCoordinator?.CancelActiveAbility(true);
    }

    /// <summary>슬라임 이름, 체력, 크기를 적용합니다.</summary>
    protected void SetStats(string slimeName, float maxHealth, float visualScale)
    {
        enemyName = slimeName;
        transform.localScale = new Vector3(visualScale, visualScale, 1f);

        if (attributeSet == null) return;

        attributeSet.TrySetBaseValue(maxHealthDef, maxHealth, this);
        attributeSet.TrySetBaseValue(healthDef, maxHealth, this);
    }

    /// <summary>슬라임이 지금 행동 가능한 상태인지 확인합니다.</summary>
    protected bool CanAct()
    {
        return !isDead && Time.time >= wakeTime;
    }

    /// <summary>분열 대기 시간이 끝났는지 확인합니다.</summary>
    protected bool CanMove()
    {
        return Time.time >= wakeTime;
    }

    /// <summary>플레이어 기본 이동속도 기준으로 추적 속도를 맞춥니다.</summary>
    protected void UpdateSpeed(float speedMultiplier)
    {
        if (ChaseIntent == null) return;

        float playerSpeed = GetPlayerSpeed();
        float ownSpeed = attributeStatSource != null
            ? attributeStatSource.Get(StatId.MoveSpeedFinal)
            : 0f;
        if (ownSpeed <= 0f)
            ownSpeed = playerSpeed;

        if (ownSpeed <= 0f)
        {
            ChaseIntent.SetSpeedScale(speedMultiplier);
            return;
        }

        ChaseIntent.SetSpeedScale(playerSpeed * speedMultiplier / ownSpeed);
    }

    /// <summary>플레이어의 기본 이동속도를 가져옵니다.</summary>
    protected float GetPlayerSpeed()
    {
        if (target == null) return PlayerBaseSpeedFallback;

        AttributeStatSource targetStatSource = target.GetComponent<AttributeStatSource>();
        if (targetStatSource == null) return PlayerBaseSpeedFallback;

        float baseSpeed = targetStatSource.Get(StatId.MoveSpeedBase);
        if (baseSpeed > 0f) return baseSpeed;

        float finalSpeed = targetStatSource.Get(StatId.MoveSpeedFinal);
        return finalSpeed > 0f ? finalSpeed : PlayerBaseSpeedFallback;
    }

    /// <summary>대상이 지정한 범위 안에 있는지 확인합니다.</summary>
    protected bool InRange(GameObject targetObject, float range)
    {
        if (targetObject == null) return false;

        Vector2 toTarget = targetObject.transform.position - transform.position;
        return toTarget.sqrMagnitude <= range * range;
    }

    /// <summary>명시 타깃이 없으면 현재 추적 타깃을 반환합니다.</summary>
    protected GameObject GetTarget(GameObject explicitTarget)
    {
        return explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;
    }

    /// <summary>대상을 향하는 방향을 계산합니다.</summary>
    protected Vector2 GetDirection(GameObject targetObject)
    {
        Vector2 direction = targetObject != null
            ? (Vector2)targetObject.transform.position - (Vector2)transform.position
            : Vector2.zero;

        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    /// <summary>공격 적중 시 사용할 피해 정보를 만듭니다.</summary>
    protected CombatHitPayload MakePayload(
        AbilitySystem system,
        AbilitySpec spec,
        GE_Damage_Spec damageEffect,
        GE_Knockback_Spec knockbackEffect,
        float damageAmount,
        float knockbackImpulse)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: system != null ? system : abilitySystem,
            sourceSpec: spec,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    /// <summary>분열된 자식 슬라임을 원형으로 생성합니다.</summary>
    protected void SpawnSplit<T>(GameObject splitPrefab, int splitCount, float splitSpread) where T : Slime
    {
        if (splitPrefab == null) return;

        Vector3 center = transform.position;
        Vector2[] dirs = GetDirs(splitCount);

        for (int i = 0; i < dirs.Length; i++)
        {
            GameObject spawned = Instantiate(
                splitPrefab,
                center + (Vector3)(dirs[i] * splitSpread),
                Quaternion.identity);
            if (spawned == null) continue;

            if (spawned.TryGetComponent(out T nextSlime))
                nextSlime.InitSplit(target);
        }
    }

    /// <summary>분열 수에 맞춰 원형 배치 방향을 만듭니다.</summary>
    protected static Vector2[] GetDirs(int count)
    {
        if (count <= 0) return System.Array.Empty<Vector2>();
        if (count == 1) return new[] { Vector2.right };

        Vector2[] dirs = new Vector2[count];
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float rad = step * i * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        return dirs;
    }

    /// <summary>필요한 어빌리티를 AbilitySystem에 등록합니다.</summary>
    protected void GiveAbility(AbilityDefinition ability)
    {
        if (abilitySystem == null || ability == null) return;
        if (abilitySystem.FindSpec(ability) != null) return;

        abilitySystem.GiveAbility(ability);
    }
}
