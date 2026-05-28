using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 마도 근접 골렘의 빠른 2연타 돌진 공격 판단과 공격 step 데이터를 소유한다.
/// - 실제 경고/돌진/피해 실행은 ArcaneMeleeGolemChargeRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(ArcaneMeleeGolemChargeRunner))]
public sealed class ArcaneMeleeGolem : Mob, IMobAttackDecisionSource
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
    [SerializeField, Min(0f)] private float maxHealth = 14f;

    private ArcaneMeleeGolemChargeRunner runner;
    private bool hasLoggedInvalidConfig;
    private bool hasChargeFacingLock;
    private AbilityLogic_ArcaneMeleeGolemCharge Logic => chargeAbility != null ? chargeAbility.logic as AbilityLogic_ArcaneMeleeGolemCharge : null;

    public AbilityLogic_ArcaneMeleeGolemCharge ChargeLogic => Logic;
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
        runner = GetComponent<ArcaneMeleeGolemChargeRunner>();
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
        AbilityLogic_ArcaneMeleeGolemCharge logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
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
    /// - 마도 근접 골렘의 각 공격 준비 시점마다 타깃 방향으로 flipX를 다시 맞추고 자동 갱신을 잠근다.
    /// - 리자드 워리어와 같은 2연타 구조를 유지해 2타 준비 때 방향을 다시 확정할 수 있게 한다.
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
    /// - 마도 근접 골렘의 step 단위 공격 방향 잠금을 해제한다.
    /// - 취소/사망/상태 종료에서 여러 번 호출되어도 안전하게 한 번만 해제한다.
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
        AbilityLogic_ArcaneMeleeGolemCharge logic = Logic;
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
        AbilityLogic_ArcaneMeleeGolemCharge logic = Logic;
        bool valid = abilitySystem != null && chargeAbility != null && logic != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(ArcaneMeleeGolem)}] 돌진 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 마도 근접 골렘의 빠른 2연타 돌진 시퀀스를 실행한다.
/// - 각 타격은 자기 경고 시간과 돌진 속도를 독립적으로 사용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ArcaneMeleeGolem))]
public sealed partial class ArcaneMeleeGolemChargeRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private ArcaneMeleeGolem owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    [Header("Telegraph Clipping")]
    [SerializeField] private LayerMask telegraphWallClipLayers = 1 << 30;
    [SerializeField, Min(3)] private int telegraphWallClipSampleCount = 48;
    [SerializeField, Min(0f)] private float telegraphWallClipSkinWidth = 0.03f;

    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<ArcaneMeleeGolem>();
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
        if (!owner.TryBuildChargeContext(system, spec, initialTarget, out ArcaneMeleeGolem.ChargeContext context)) yield break;
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

    /// <summary>
    /// 책임:
    /// - 마도 근접 골렘의 각 돌진 타격을 리자드 워리어와 같은 AttackReady -> Attack 흐름으로 실행한다.
    /// - 각 step 준비 시점마다 경고, 방향 고정, 공격 애니메이션을 새로 확정한다.
    /// </summary>
    private IEnumerator RunStep(AbilitySystem system, ArcaneMeleeGolem.ChargeContext context, ArcaneMeleeGolem.ChargeStep step, AbilitySpec spec)
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

    private IEnumerator Dash(Vector2 direction, ArcaneMeleeGolem.ChargeStep step, CombatHitPayload payload, AbilitySpec spec)
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

    private void ShowWarning(Vector2 start, Vector2 direction, ArcaneMeleeGolem.ChargeStep step, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        Vector3 center = (Vector3)start + (Vector3)(direction.normalized * step.dashDistance * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(step.dashDistance, step.warningWidth),
            angle,
            warningSeconds,
            warningStyle)
            .WithWallClipping(
                telegraphWallClipLayers,
                telegraphWallClipSampleCount,
                telegraphWallClipSkinWidth);

        telegraphService.Show(spec);
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
