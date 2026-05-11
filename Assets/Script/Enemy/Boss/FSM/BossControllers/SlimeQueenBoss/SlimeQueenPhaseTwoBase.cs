using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 2페이즈 개체가 공유하는 접촉 피해와 향후 패턴 실행 기반입니다.
/// </summary>
public abstract class SlimeQueenPhaseTwoBase : SlimeQueenBossBase, ISlimeQueenBodyInflateHost
{
    [Header("Phase 2 Contact")]
    [Tooltip("2페이즈 퀸이 플레이어와 접촉했을 때 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float contactDamage = 1f;

    [Tooltip("2페이즈 접촉 피해를 다시 적용할 수 있는 최소 간격입니다.")]
    [SerializeField, Min(0f)] private float contactDamageCooldownSeconds = 1f;

    [Tooltip("2페이즈 접촉 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec contactDamageEffect;

    [Space(8)]

    [Header("Phase 2 - Repeated Slam")]
    [Tooltip("연속 내려찍기 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle slamWarningStyle;

    [Tooltip("연속 내려찍기 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float slamWarningDiameter = 2.8f;

    [Tooltip("연속 내려찍기 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float slamDamageDiameter = 2.8f;

    [Tooltip("연속 내려찍기 사이의 텀입니다.")]
    [SerializeField, Min(0.1f)] private float slamIntervalSeconds = 1f;

    [Tooltip("연속 내려찍기를 반복할 횟수입니다.")]
    [SerializeField, Min(1)] private int slamCount = 3;

    [Tooltip("연속 내려찍기 점프 중간 지점에서 올라갈 포물선 높이입니다.")]
    [SerializeField, Min(0f)] private float slamArcHeight = 2.8f;

    [Tooltip("연속 내려찍기 착지 시 플레이어에게 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float slamDamage = 1f;

    [Tooltip("연속 내려찍기 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec slamDamageEffect;

    [Space(8)]

    [Header("Phase 2 - Body Inflate Impact")]
    [Tooltip("몸 부풀림 원형 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle bodyInflateWarningStyle;

    [Tooltip("몸 부풀림 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateWarningDiameter = 4f;

    [Tooltip("몸 부풀림 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateWarningSeconds = 1.4f;

    [Tooltip("몸 부풀림 실제 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateImpactDiameter = 4f;

    [Tooltip("몸 부풀림이 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactDamage = 1.5f;

    [Tooltip("몸 부풀림 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec bodyInflateImpactDamageEffect;

    [Tooltip("몸 부풀림 넉백에 사용할 GAS Knockback Effect입니다.")]
    [SerializeField] private GE_Knockback_Spec bodyInflateImpactKnockbackEffect;

    [Tooltip("몸 부풀림 넉백 세기입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactKnockbackImpulse = 8f;

    private float nextContactDamageTime;

    public int Phase2SlamCount => Mathf.Max(1, slamCount);

    public float Phase2SlamIntervalSeconds => Mathf.Max(0.1f, slamIntervalSeconds);

    public float BodyInflateWarningSeconds => bodyInflateWarningSeconds;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyContactDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyContactDamage(other);
    }

    /// <summary>패턴이 설정된 2페이즈 보스만 일반 패턴 루프를 사용하고, 비어 있으면 대기 상태를 사용합니다.</summary>
    protected override BossCombatIdleState CreateCombatIdleState()
    {
        if (ConfiguredPhaseCount > 0)
            return base.CreateCombatIdleState();

        return new PhaseTwoWaitingState(this);
    }

    /// <summary>페이즈 2 연속 내려찍기 경고 원을 표시합니다.</summary>
    public void ShowPhase2SlamWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            landingPosition,
            slamWarningDiameter,
            Phase2SlamIntervalSeconds,
            slamWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>페이즈 2 내려찍기 착지 위치를 현재 타겟 위치로 계산합니다.</summary>
    public bool TryGetPhase2SlamLandingPosition(GameObject explicitTarget, out Vector3 landingPosition)
    {
        Transform targetTransform = explicitTarget != null ? explicitTarget.transform : CurrentTarget;
        if (targetTransform == null)
        {
            landingPosition = transform.position;
            return false;
        }

        landingPosition = targetTransform.position;
        landingPosition.z = transform.position.z;
        return true;
    }

    /// <summary>페이즈 2 내려찍기 포물선 진행도에 맞춰 보스 위치를 이동시킵니다.</summary>
    public void SetPhase2SlamPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        Vector3 groundPosition = Vector3.Lerp(startPosition, landingPosition, clampedTime);
        float arcOffset = Mathf.Sin(clampedTime * Mathf.PI) * slamArcHeight;

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = groundPosition + Vector3.up * arcOffset;
    }

    /// <summary>페이즈 2 내려찍기 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToPhase2SlamLanding(Vector3 landingPosition)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = landingPosition;
    }

    /// <summary>페이즈 2 내려찍기 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyPhase2SlamDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
        if (slamDamage <= 0f || CurrentTarget == null || slamDamageEffect == null)
            return;

        float damageRadius = Mathf.Max(0.1f, slamDamageDiameter * 0.5f);
        float sqrDistance = ((Vector2)(CurrentTarget.position - landingPosition)).sqrMagnitude;
        if (sqrDistance > damageRadius * damageRadius)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            sourceSpec,
            slamDamageEffect,
            null,
            CurrentTarget.gameObject,
            slamDamage,
            0f,
            null,
            0f,
            null,
            landingPosition,
            gameObject);
    }

    /// <summary>몸 부풀림 원형 경고를 보스 위치에 표시합니다.</summary>
    public void ShowBodyInflateWarning()
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            transform.position,
            bodyInflateWarningDiameter,
            bodyInflateWarningSeconds,
            bodyInflateWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>몸 부풀림 범위 안의 플레이어에게 피해와 넉백을 적용합니다.</summary>
    public void ApplyBodyInflateImpact(AbilitySpec sourceSpec)
    {
        if (bodyInflateImpactDamage <= 0f || CurrentTarget == null || bodyInflateImpactDamageEffect == null)
            return;

        float radius = Mathf.Max(0.1f, bodyInflateImpactDiameter * 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!HasPlayerTagInHierarchy(hits[i].transform))
                continue;

            GameObject contactTarget = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (contactTarget == null || !contactTarget.CompareTag("Player"))
                continue;

            Vector3 hitWorldPosition = hits[i].ClosestPoint(transform.position);
            CombatDamageAction.ApplyDamageAndEmitHit(
                AbilitySystem,
                sourceSpec,
                bodyInflateImpactDamageEffect,
                bodyInflateImpactKnockbackEffect,
                contactTarget,
                bodyInflateImpactDamage,
                0f,
                null,
                bodyInflateImpactKnockbackImpulse,
                null,
                hitWorldPosition,
                gameObject);
            return;
        }
    }

    /// <summary>플레이어와 접촉 중이면 GAS 피해를 적용합니다.</summary>
    private void TryApplyContactDamage(Collider2D other)
    {
        if (IsPatternMoveDamageBlocked || IsDead || other == null)
            return;

        if (contactDamage <= 0f || contactDamageEffect == null || Time.time < nextContactDamageTime)
            return;

        if (!HasPlayerTagInHierarchy(other.transform))
            return;

        GameObject contactTarget = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (contactTarget == null || !contactTarget.CompareTag("Player"))
            return;

        Vector3 hitWorldPosition = other.ClosestPoint(transform.position);
        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            null,
            contactDamageEffect,
            null,
            contactTarget,
            contactDamage,
            0f,
            null,
            0f,
            null,
            hitWorldPosition,
            gameObject);

        nextContactDamageTime = Time.time + Mathf.Max(0f, contactDamageCooldownSeconds);
    }

    private sealed class PhaseTwoWaitingState : BossCombatIdleState
    {
        public PhaseTwoWaitingState(BossControllerBase boss) : base(boss)
        {
        }

        public override void OnEnter()
        {
            LogState("2페이즈 패턴 구현 대기 상태입니다.");
        }

        public override void OnUpdate()
        {
        }
    }
}
