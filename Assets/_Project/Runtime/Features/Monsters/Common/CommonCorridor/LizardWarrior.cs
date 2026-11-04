using System.Collections;
using UnityEngine;
using UnityGAS;
using CapstoneAudio;

/// <summary>Owns frenzy eligibility, damage context and actor-local movement/presentation.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(LizardWarriorChargeRunner))]
public sealed class LizardWarrior : Mob, IMobAttackDecisionSource
{
    [SerializeField] private AbilityDefinition chargeAbility;

    private LizardWarriorChargeRunner runner;
    private bool hasLoggedInvalidConfig;
    private float nextFrenzyTime;
    private SpriteRenderer frenzyRenderer;
    private Color originalColor;
    private bool hasFrenzyPresentation;

    public AbilityLogic_LizardWarriorCharge ChargeLogic => chargeAbility != null
        ? chargeAbility.logic as AbilityLogic_LizardWarriorCharge : null;
    public Vector2 ContactCenter => collision != null ? (Vector2)collision.bounds.center : (Vector2)transform.position;

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
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && chargeAbility != null)
            abilitySystem.GiveAbility(chargeAbility);
    }

    public override bool CanUseChaseMovement() => base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    public override bool CanUsePostAttackRecoveryRetreat() => false;

    // The exhausted opening must survive attack-speed bonuses.
    public override float ResolvePostAttackRecoverSeconds(float scaledRecoverSeconds) =>
        Mathf.Max(ChargeLogic != null ? ChargeLogic.RecoverSeconds : 0f, scaledRecoverSeconds);

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        AbilityLogic_LizardWarriorCharge logic = ChargeLogic;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        if (!HasRequiredData() || CombatHitPause2D.GetActionTime(gameObject) < nextFrenzyTime ||
            !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(chargeAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    public void OnAttackStateEntered(MobAttackRequest request) { }

    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        StopFrenzyMovement();
        CleanupFrenzyPresentation();
        if (ChargeLogic != null)
            nextFrenzyTime = CombatHitPause2D.GetActionTime(gameObject) + ChargeLogic.RecoverSeconds + ChargeLogic.RearmSeconds;
    }

    protected override void OnDeathStarted()
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        base.OnDeathStarted();
    }

    public void BeginFrenzyPresentation()
    {
        if (hasFrenzyPresentation) return;
        hasFrenzyPresentation = true;
        frenzyRenderer = sprite;
        if (frenzyRenderer != null)
        {
            originalColor = frenzyRenderer.color;
            frenzyRenderer.color = originalColor * ChargeLogic.FrenzyTint;
        }
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    public void CleanupFrenzyPresentation()
    {
        if (!hasFrenzyPresentation) return;
        hasFrenzyPresentation = false;
        if (frenzyRenderer != null) frenzyRenderer.color = originalColor;
        frenzyRenderer = null;
        // The authored frenzy state holds until explicit completion/cancellation.
        if (animator != null && !IsDead)
        {
            animator.ResetTrigger("LizardWarrior_AttackReady");
            animator.ResetTrigger("LizardWarrior_Attack");
            animator.Play("Base Layer.Idle", 0, 0f);
        }
    }

    public void SetFrenzyMovement(Vector2 direction, float speed)
    {
        StopFrenzyMovement();
        if (externalMovement == null) return;
        float duration = Mathf.Max(0.08f, Time.fixedDeltaTime * 2f);
        // Use the existing body sweep so frenzy cannot walk into walls or holes.
        if (IsRecoveryRetreatDirectionBlocked(direction, speed * duration + 0.03f)) return;
        externalMovement.AddTimedVelocity(direction * speed, duration, 0f, this);
    }

    public void StopFrenzyMovement() => externalMovement?.RemoveTimedVelocitiesFromSource(this);

    public bool TryBuildChargeContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ChargeContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        if (!HasRequiredData() || targetObject == null) return false;
        AbilityLogic_LizardWarriorCharge logic = ChargeLogic;
        Vector2 direction = CommonMonsterCombatUtility.DirectionTo(gameObject, targetObject, sprite != null && sprite.flipX);
        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem, spec, logic.DamageEffect, logic.KnockbackEffect,
            gameObject, logic.DamageAmount, logic.KnockbackImpulse);
        context = new ChargeContext(targetObject, direction, payload);
        return true;
    }

    private bool HasRequiredData()
    {
        bool valid = abilitySystem != null && chargeAbility != null && ChargeLogic != null &&
            ChargeLogic.DamageEffect != null && runner != null && externalMovement != null && collision != null;
        if (!valid && !hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(LizardWarrior)}] 광폭 추격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }
        return valid;
    }
}

/// <summary>Runs a finite, turn-limited pursuit. Contact consumes the attack even when damage is evaded.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LizardWarrior))]
public sealed partial class LizardWarriorChargeRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    private static readonly SoundRef FrenzySound = SoundRef.FromKey("sound_LizardWarrior_dash1");
    [SerializeField] private LizardWarrior owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    private bool isRunning;
    private bool cancelRequested;
    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null) owner = GetComponent<LizardWarrior>();
        if (abilityCoordinator == null) abilityCoordinator = GetComponent<MobAbilityCoordinator>();
    }

    private void OnDisable()
    {
        Cancel();
        isRunning = false;
        abilityCoordinator?.EndRunner(this);
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null || !owner.TryBuildChargeContext(system, spec, initialTarget, out LizardWarrior.ChargeContext context))
            yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;
        isRunning = true;
        cancelRequested = false;
        AbilityLogic_LizardWarriorCharge logic = owner.ChargeLogic;
        try
        {
            owner.BeginFrenzyPresentation();
            float playerBaseSpeed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(context.Target.transform);
            float warningSeconds = Mathf.Max(0.6f,
                CombatTimingService.ScaleSeconds(system, logic.WarningSeconds, CombatTimingSlot.AttackWarning));
            yield return CommonMonsterCombatUtility.WaitAttackWarning(
                warningSeconds, () => ShouldStop(spec, context.Target),
                () => owner.SetFrenzyMovement(CommonMonsterCombatUtility.DirectionTo(gameObject, context.Target, false),
                    playerBaseSpeed * logic.WarningSpeedMultiplier), owner.gameObject);
            if (ShouldStop(spec, context.Target)) yield break;

            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            SoundPlaybackUtility.Play(FrenzySound, instigator: gameObject, causer: gameObject,
                target: context.Target, position: transform.position, sourceObject: this);
            Vector2 direction = CommonMonsterCombatUtility.DirectionTo(gameObject, context.Target, false);
            float speed = playerBaseSpeed * logic.ChaseSpeedMultiplier;
            float elapsed = 0f;
            while (elapsed < logic.ChaseSeconds && !ShouldStop(spec, context.Target))
            {
                if (CombatHitPause2D.IsParalyzedOn(owner.gameObject))
                {
                    owner.StopFrenzyMovement();
                    yield return null;
                    continue;
                }
                float dt = CombatHitPause2D.GetActionDeltaTime(owner.gameObject);
                if (CommonMonsterCombatUtility.TryApplyCircleDamage(owner.ContactCenter, logic.ContactDiameter,
                    logic.TargetLayers, gameObject, context.HitPayload)) yield break;
                Vector2 desired = CommonMonsterCombatUtility.DirectionTo(gameObject, context.Target, false);
                float angle = Mathf.Clamp(Vector2.SignedAngle(direction, desired),
                    -logic.TurnDegreesPerSecond * dt, logic.TurnDegreesPerSecond * dt);
                direction = (Quaternion.Euler(0f, 0f, angle) * direction).normalized;
                owner.SetFrenzyMovement(direction, speed);
                elapsed += dt;
                yield return null;
            }
        }
        finally
        {
            owner.StopFrenzyMovement();
            CleanupPresentation();
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        owner?.StopFrenzyMovement();
        CleanupPresentation();
    }

    public void CleanupPresentation() => owner?.CleanupFrenzyPresentation();

    private bool ShouldStop(AbilitySpec spec, GameObject targetObject) =>
        cancelRequested || owner == null || owner.IsDead || targetObject == null ||
        (abilityCoordinator != null && abilityCoordinator.IsAbilityExecutionSuppressed) ||
        (spec != null && spec.Token != null && spec.Token.IsCancelled);
}
