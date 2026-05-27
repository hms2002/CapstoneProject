using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 리자드맨 전사의 2연타 돌진 공격 판단과 공격 step 데이터를 소유한다.
/// - 실제 경고/돌진/피해 실행은 LizardWarriorChargeRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(LizardWarriorChargeRunner))]
public sealed class LizardWarrior : Mob, IMobAttackDecisionSource
{
    [System.Serializable]
    public struct ChargeStep
    {
        public float warningSeconds;
        public float dashDistance;
        public float dashSeconds;
        public float warningWidth;
    }

    [SerializeField] private AbilityDefinition chargeAbility;
    [SerializeField, Min(0f)] private float maxHealth = 8f;

    private LizardWarriorChargeRunner runner;
    private bool hasLoggedInvalidConfig;
    private bool hasChargeFacingLock;

    private AbilityLogic_LizardWarriorCharge Logic => chargeAbility != null ? chargeAbility.logic as AbilityLogic_LizardWarriorCharge : null;
    private float AttackTriggerRange => Logic != null ? Mathf.Max(0.01f, Logic.FirstStep.dashDistance) : 0.01f;

    public AbilityLogic_LizardWarriorCharge ChargeLogic => Logic;
    public ChargeStep FirstStep => Logic != null ? Logic.FirstStep : default;
    public ChargeStep SecondStep => Logic != null ? Logic.SecondStep : default;

    public readonly struct ChargeContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Direction;
        public readonly CombatHitPayload HitPayload;

        public ChargeContext(GameObject target, Vector2 direction, CombatHitPayload hitPayload)
        {
            Target = target;
            Direction = direction;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        runner = GetComponent<LizardWarriorChargeRunner>();
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

        AbilityLogic_LizardWarriorCharge logic = Logic;
        if (logic == null)
            return false;

        request = new MobAttackRequest(chargeAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>2연타 각각의 준비 애니메이션과 방향 고정은 Runner가 step 단위로 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
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
    /// - 리자드맨 전사의 2연타 준비부터 마지막 공격 종료까지 자동 flipX 갱신을 잠근다.
    /// - 1타와 2타 사이에 타깃 위치가 바뀌어도 공격 방향 연출이 흔들리지 않게 한다.
    /// </summary>
    public void LockFacingForChargeStep(GameObject targetObject)
    {
        if (targetObject != null)
            TryApplySpriteFacingTargetX(targetObject.transform.position.x);

        AcquireChargeFacingLock();
    }

    private void AcquireChargeFacingLock()
    {
        if (hasChargeFacingLock)
            return;

        PushFacingLock();
        hasChargeFacingLock = true;
    }

    /// <summary>
    /// 책임:
    /// - 리자드맨 전사의 2연타 패턴이 끝나거나 취소될 때 자동 flipX 갱신 잠금을 해제한다.
    /// - 취소/사망 경로에서 여러 번 호출되어도 안전하게 한 번만 해제한다.
    /// </summary>
    public void ReleaseChargeFacingLock()
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
        if (!HasRequiredData() || targetObject == null)
            return false;

        Vector2 direction = CommonMonsterCombatUtility.DirectionTo(gameObject, targetObject, sprite != null && sprite.flipX);
        AbilityLogic_LizardWarriorCharge logic = Logic;
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

        context = new ChargeContext(targetObject, direction, payload);
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
        AbilityLogic_LizardWarriorCharge logic = Logic;
        bool valid = abilitySystem != null && chargeAbility != null && logic != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(LizardWarrior)}] 돌진 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 리자드맨 전사의 2연타 돌진 시퀀스를 실행하고 각 타격의 경고, 이동, 피해 판정을 정리한다.
/// - 2타는 1타 직후 거리 조건과 무관하게 이어지게 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LizardWarrior))]
public sealed partial class LizardWarriorChargeRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private LizardWarrior owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<LizardWarrior>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = CreateWarningStyle();
    }

    private void OnDestroy()
    {
        if (warningStyle != null)
            Destroy(warningStyle);
    }

    private void OnDisable()
    {
        Cancel();
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildChargeContext(system, spec, initialTarget, out LizardWarrior.ChargeContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            yield return RunStep(system, context, owner.FirstStep, spec);
            if (!cancelRequested && !owner.IsDead && !IsCancelled(spec))
                yield return RunStep(system, context, owner.SecondStep, spec);
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
    }

    public void CleanupPresentation()
    {
        HideWarning();
    }

    private IEnumerator RunStep(AbilitySystem system, LizardWarrior.ChargeContext context, LizardWarrior.ChargeStep step, AbilitySpec spec)
    {
        Vector2 direction = context.Target != null
            ? CommonMonsterCombatUtility.DirectionTo(gameObject, context.Target, false)
            : context.Direction;

        try
        {
            owner.LockFacingForChargeStep(context.Target);
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.AttackReady);
            float warningSeconds = CombatTimingService.ScaleSeconds(this, step.warningSeconds, CombatTimingSlot.AttackWarning);
            ShowWarning(transform.position, direction, step, warningSeconds);
            if (warningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, warningSeconds);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            yield return Dash(direction, step, context.HitPayload, spec);
        }
        finally
        {
            owner.ReleaseChargeFacingLock();
        }
    }

    private IEnumerator Dash(Vector2 direction, LizardWarrior.ChargeStep step, CombatHitPayload payload, AbilitySpec spec)
    {
        Vector2 safeDirection = direction.normalized;
        float speed = step.dashDistance / Mathf.Max(0.01f, step.dashSeconds);
        float duration = Mathf.Max(0.01f, step.dashSeconds);
        float elapsed = 0f;
        bool hitTarget = false;

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            float deltaTime = Mathf.Min(Time.deltaTime, duration - elapsed);
            Vector2 desiredDelta = safeDirection * (speed * deltaTime);
            Vector2 resolvedDelta = CommonMonsterCombatUtility.ResolveDashWallSlideDelta(
                transform.position,
                desiredDelta,
                owner.ChargeLogic.DashCastRadius,
                owner.ChargeLogic.DashObstacleLayers,
                owner.ChargeLogic.DashWallSkinWidth);
            transform.position += (Vector3)resolvedDelta;
            if (!hitTarget)
                hitTarget = CommonMonsterCombatUtility.TryApplyCircleDamage(transform.position, step.warningWidth, owner.ChargeLogic.TargetLayers, gameObject, payload);
            elapsed += deltaTime;
            yield return null;
        }

        if (!hitTarget)
            CommonMonsterCombatUtility.TryApplyCircleDamage(transform.position, step.warningWidth, owner.ChargeLogic.TargetLayers, gameObject, payload);
    }

    private void ShowWarning(Vector2 start, Vector2 direction, LizardWarrior.ChargeStep step, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        Vector3 center = (Vector3)start + (Vector3)(direction.normalized * step.dashDistance * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        telegraphService.Show(AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(step.dashDistance, step.warningWidth),
            angle,
            warningSeconds,
            warningStyle));
    }

    private void HideWarning()
    {
        telegraphService?.HideCurrent();
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private static AttackTelegraphStyle CreateWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerAreaColors(style);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.72f;
        style.blinkFrequency = 5f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
