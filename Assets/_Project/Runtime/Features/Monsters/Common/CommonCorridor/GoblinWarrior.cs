using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 고블린 전사의 1회 돌진 공격 판단과 돌진 문맥 생성을 소유한다.
/// - 실제 경고 표시, 돌진 이동, 충돌 피해 처리는 GoblinWarriorChargeRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(GoblinWarriorChargeRunner))]
public sealed class GoblinWarrior : Mob, IMobAttackDecisionSource
{
    [SerializeField] private AbilityDefinition chargeAbility;
    [SerializeField, Min(0f)] private float maxHealth = 6f;

    private GoblinWarriorChargeRunner runner;
    private bool hasLoggedInvalidConfig;
    private bool hasChargeFacingLock;

    private AbilityLogic_GoblinWarriorCharge Logic => chargeAbility != null ? chargeAbility.logic as AbilityLogic_GoblinWarriorCharge : null;
    private float AttackTriggerRange => Logic != null ? Mathf.Max(0.01f, Logic.DashDistance) : 0.01f;

    public readonly struct ChargeContext
    {
        public readonly GameObject Target;
        public readonly Vector2 StartPosition;
        public readonly Vector2 Direction;
        public readonly float WarningSeconds;
        public readonly float DashDistance;
        public readonly float DashSeconds;
        public readonly float WarningWidth;
        public readonly LayerMask TargetLayers;
        public readonly LayerMask DashObstacleLayers;
        public readonly float DashCastRadius;
        public readonly float DashWallSkinWidth;
        public readonly CombatHitPayload HitPayload;

        public ChargeContext(
            GameObject target,
            Vector2 startPosition,
            Vector2 direction,
            float warningSeconds,
            float dashDistance,
            float dashSeconds,
            float warningWidth,
            LayerMask targetLayers,
            LayerMask dashObstacleLayers,
            float dashCastRadius,
            float dashWallSkinWidth,
            CombatHitPayload hitPayload)
        {
            Target = target;
            StartPosition = startPosition;
            Direction = direction;
            WarningSeconds = warningSeconds;
            DashDistance = dashDistance;
            DashSeconds = dashSeconds;
            WarningWidth = warningWidth;
            TargetLayers = targetLayers;
            DashObstacleLayers = dashObstacleLayers;
            DashCastRadius = dashCastRadius;
            DashWallSkinWidth = dashWallSkinWidth;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        runner = GetComponent<GoblinWarriorChargeRunner>();
        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && chargeAbility != null)
            abilitySystem.GiveAbility(chargeAbility);
    }

    public override bool CanUseChaseMovement()
    {
        return base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    }

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        if (!HasRequiredData() || !CommonMonsterCombatUtility.InRange(transform, targetObject, AttackTriggerRange))
            return false;

        AbilityLogic_GoblinWarriorCharge logic = Logic;
        if (logic == null)
            return false;

        request = new MobAttackRequest(chargeAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 경고/준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        AcquireChargeFacingLock();
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태 종료 시 취소가 아니라면 회복 애니메이션을 요청한다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        ReleaseChargeFacingLock();
        if (!wasCancelled && !IsDead)
            CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Recover);
    }

    protected override void OnDeathStarted()
    {
        ReleaseChargeFacingLock();
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        base.OnDeathStarted();
    }

    /// <summary>
    /// 책임:
    /// - 고블린 전사의 돌진 준비부터 공격 종료까지 자동 flipX 갱신을 잠근다.
    /// - 공격 상태 진입이 중복 호출되어도 lock이 누적되지 않도록 보호한다.
    /// </summary>
    private void AcquireChargeFacingLock()
    {
        if (hasChargeFacingLock)
            return;

        PushFacingLock();
        hasChargeFacingLock = true;
    }

    /// <summary>
    /// 책임:
    /// - 고블린 전사의 돌진 패턴이 끝나거나 취소될 때 자동 flipX 갱신 잠금을 해제한다.
    /// - 취소/사망 경로에서 여러 번 호출되어도 안전하게 한 번만 해제한다.
    /// </summary>
    private void ReleaseChargeFacingLock()
    {
        if (!hasChargeFacingLock)
            return;

        PopFacingLock();
        hasChargeFacingLock = false;
    }

    public bool TryBuildChargeContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ChargeContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        if (!HasRequiredData() || !CommonMonsterCombatUtility.InRange(transform, targetObject, AttackTriggerRange))
            return false;

        Vector2 direction = CommonMonsterCombatUtility.DirectionTo(gameObject, targetObject, sprite != null && sprite.flipX);
        AbilityLogic_GoblinWarriorCharge logic = Logic;
        if (logic == null)
            return false;

        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem,
            spec,
            logic.DamageEffect,
            logic.KnockbackEffect,
            gameObject,
            logic.DamageAmount,
            logic.KnockbackImpulse);

        context = new ChargeContext(
            targetObject,
            transform.position,
            direction,
            logic.WarningSeconds,
            logic.DashDistance,
            logic.DashSeconds,
            logic.WarningWidth,
            logic.TargetLayers,
            logic.DashObstacleLayers,
            logic.DashCastRadius,
            logic.DashWallSkinWidth,
            payload);
        return true;
    }

    private void ApplyStats()
    {
        if (attributeSet == null)
            return;

        attributeSet.TrySetBaseValue(maxHealthDef, maxHealth, this);
        attributeSet.TrySetBaseValue(healthDef, maxHealth, this);
    }

    private bool HasRequiredData()
    {
        AbilityLogic_GoblinWarriorCharge logic = Logic;
        bool valid = abilitySystem != null && chargeAbility != null && logic != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(GoblinWarrior)}] 돌진 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}
