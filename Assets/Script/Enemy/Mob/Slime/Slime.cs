using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public abstract class Slime : Mob, IMobAttackDecisionSource, IPitFallDeathHandler
{
    private const float SplitWakeSeconds = 1f;
    private const float PlayerBaseSpeedFallback = 4f;

    [Header("Split Landing")]
    [Tooltip("분열체가 본체에서 튀어나와 착지점에 도달하는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float splitLandingSeconds = 0.45f;
    [Tooltip("분열체가 튀어나올 때 보이는 포물선 높이입니다.")]
    [SerializeField, Min(0f)] private float splitLandingArcHeight = 0.55f;
    [Tooltip("분열체 착지점이 벽/사물 콜라이더 밖이나 내부로 잡히지 않도록 검사할 레이어입니다. 비워두면 Wall, Default, Non_FightCollision을 사용합니다.")]
    [FormerlySerializedAs("splitLandingWallLayers")]
    [SerializeField] private LayerMask splitLandingBlockedLayers;
    [Tooltip("충돌체 앞에서 분열체가 멈출 때 남길 여유 거리입니다.")]
    [FormerlySerializedAs("splitLandingWallSkin")]
    [SerializeField, Min(0f)] private float splitLandingBlockedSkin = 0.08f;
    [Tooltip("착지점 주변 충돌체 겹침을 검사할 반지름입니다.")]
    [SerializeField, Min(0.01f)] private float splitLandingProbeRadius = 0.18f;
    [Tooltip("착지점이 충돌체와 겹치면 본체 방향으로 당겨 재검사할 횟수입니다.")]
    [SerializeField, Range(1, 8)] private int splitLandingResolveSteps = 4;

    private MobAbilityCoordinator abilityCoordinator;
    private float wakeTime;
    private bool isPitFallDeath;
    private readonly RaycastHit2D[] splitLandingRaycastHits = new RaycastHit2D[8];
    private readonly Collider2D[] splitLandingOverlapHits = new Collider2D[8];

    /// <summary>분열로 생성된 슬라임의 대기 시간과 타깃을 설정합니다.</summary>
    public virtual void InitSplit(Transform nextTarget)
    {
        wakeTime = Time.time + SplitWakeSeconds;
        isPitFallDeath = false;
        ApplyStats();

        if (nextTarget != null)
            SetTarget(nextTarget);
    }

    public abstract bool TryBuildAttackRequest(out MobAttackRequest request);

    /// <summary>구덩이 사망 여부를 사망 분열 규칙에서 확인할 수 있게 제공합니다.</summary>
    protected bool IsPitFallDeath => isPitFallDeath;

    /// <summary>
    /// 책임:
    /// - PitFallReaction2D가 낙하 완료 후 슬라임에게 구덩이 사망 처리를 요청하는 진입점이다.
    /// - 일반 사망과 달리 분열을 생략해야 하므로 사망 플래그를 기록한 뒤 공통 사망 경로를 탄다.
    /// </summary>
    public virtual void HandlePitFallDeath(PitFallContext context)
    {
        if (isDead)
            return;

        isPitFallDeath = true;
        RequestDeath(context.TrapObject);
    }

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

    /// <summary>분열된 자식 슬라임을 본체 위치에서 생성한 뒤 착지점까지 포물선으로 튀어나가게 합니다.</summary>
    protected void SpawnSplit<T>(GameObject splitPrefab, int splitCount, float splitSpread) where T : Slime
    {
        if (splitPrefab == null) return;

        Vector3 center = transform.position;
        Vector2[] dirs = GetDirs(splitCount);

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 landingPosition = ResolveSplitLandingPosition(center, dirs[i], splitSpread);
            GameObject spawned = Instantiate(
                splitPrefab,
                center,
                Quaternion.identity);
            if (spawned == null) continue;

            if (spawned.TryGetComponent(out T nextSlime))
            {
                nextSlime.InitSplit(target);
                StartSplitLandingMotion(spawned, center, landingPosition);
                RegisterLockTrackedChild(spawned);
            }
        }
    }

    /// <summary>분열체에 착지 모션 컴포넌트를 보장하고, 착지 전 피격 불가 상태를 시작합니다.</summary>
    private void StartSplitLandingMotion(GameObject spawned, Vector3 startPosition, Vector3 landingPosition)
    {
        SlimeSplitLandingMotion2D landingMotion = spawned.GetComponent<SlimeSplitLandingMotion2D>();
        if (landingMotion == null)
            landingMotion = spawned.AddComponent<SlimeSplitLandingMotion2D>();

        landingMotion.Begin(startPosition, landingPosition, splitLandingSeconds, splitLandingArcHeight);
    }

    /// <summary>분열체 착지점이 충돌체 너머나 내부에 잡히지 않도록 안전한 지상 좌표로 보정합니다.</summary>
    private Vector3 ResolveSplitLandingPosition(Vector3 center, Vector2 direction, float splitSpread)
    {
        if (direction.sqrMagnitude <= 0.0001f || splitSpread <= 0f)
            return center;

        LayerMask blockedLayers = ResolveSplitLandingBlockedLayers();
        Vector2 normalizedDirection = direction.normalized;
        Vector2 start = center;
        float safeDistance = splitSpread;

        if (blockedLayers.value != 0)
        {
            RaycastHit2D blockerHit = FindNearestSplitLandingBlocker(start, normalizedDirection, splitSpread, blockedLayers);
            if (blockerHit.collider != null)
                safeDistance = Mathf.Max(0f, blockerHit.distance - splitLandingBlockedSkin);
        }

        Vector2 candidate = start + normalizedDirection * safeDistance;
        return ResolveNonBlockedLanding(start, candidate, blockedLayers);
    }

    /// <summary>후보 착지점이 충돌체와 겹치면 본체 쪽으로 단계적으로 되돌리며 안전한 위치를 찾습니다.</summary>
    private Vector3 ResolveNonBlockedLanding(Vector2 center, Vector2 candidate, LayerMask blockedLayers)
    {
        if (blockedLayers.value == 0 || !IsSplitLandingBlocked(candidate, blockedLayers))
            return candidate;

        int steps = Mathf.Max(1, splitLandingResolveSteps);
        for (int i = 1; i <= steps; i++)
        {
            float t = 1f - (float)i / steps;
            Vector2 fallback = Vector2.Lerp(center, candidate, t);
            if (!IsSplitLandingBlocked(fallback, blockedLayers))
                return fallback;
        }

        return center;
    }

    /// <summary>분열체 착지점 검사에 사용할 충돌체 레이어를 가져옵니다.</summary>
    private LayerMask ResolveSplitLandingBlockedLayers()
    {
        if (splitLandingBlockedLayers.value != 0)
            return splitLandingBlockedLayers;

        int mask = 0;
        int wallLayer = LayerMask.NameToLayer("Wall");
        int defaultLayer = LayerMask.NameToLayer("Default");
        int nonFightCollisionLayer = LayerMask.NameToLayer("Non_FightCollision");

        if (wallLayer >= 0)
            mask |= 1 << wallLayer;
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;
        if (nonFightCollisionLayer >= 0)
            mask |= 1 << nonFightCollisionLayer;

        return mask;
    }

    /// <summary>분열 이동 경로에서 가장 가까운 non-trigger 충돌체를 찾습니다.</summary>
    private RaycastHit2D FindNearestSplitLandingBlocker(Vector2 start, Vector2 direction, float distance, LayerMask blockedLayers)
    {
        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = blockedLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.Raycast(start, direction, filter, splitLandingRaycastHits, distance);
        RaycastHit2D nearestHit = default;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = splitLandingRaycastHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestHit = hit;
                nearestDistance = hit.distance;
            }
        }

        return nearestHit;
    }

    /// <summary>지정 위치가 non-trigger 충돌체와 겹치는지 검사합니다.</summary>
    private bool IsSplitLandingBlocked(Vector2 position, LayerMask blockedLayers)
    {
        if (blockedLayers.value == 0)
            return false;

        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = blockedLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.OverlapCircle(position, splitLandingProbeRadius, filter, splitLandingOverlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = splitLandingOverlapHits[i];
            if (hit != null && !hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
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
