using System.Collections;
using UnityEngine;
using UnityGAS;
using CapstoneAudio;

/// <summary>
/// 책임:
/// - 고블린 탱커의 원형 내려치기 공격 판단과 피해 문맥을 소유한다.
/// - 실제 경고 표시, 대기, 범위 피해, cleanup은 GoblinTankSlamRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(GoblinTankSlamRunner))]
public sealed class GoblinTank : Mob, IMobAttackDecisionSource
{
    [SerializeField] private AbilityDefinition slamAbility;
    [SerializeField, Min(0f)] private float maxHealth = 18f;
    [SerializeField, Range(0.1f, 2f)] private float chaseSpeedScale = 0.7f;

    private GoblinTankSlamRunner runner;
    private bool hasLoggedInvalidConfig;
    private AbilityLogic_GoblinTankSlam Logic => slamAbility != null ? slamAbility.logic as AbilityLogic_GoblinTankSlam : null;

    // 책임: 고블린 탱크 범위 내려찍기의 중심, 경고 시간, 피해 정보를 보관한다.
    public readonly struct SlamContext
    {
        public readonly Vector2 Center;
        public readonly float WarningSeconds;
        public readonly float ImpactDiameter;
        public readonly LayerMask TargetLayers;
        public readonly CombatHitPayload HitPayload;

        public SlamContext(
            Vector2 center,
            float warningSeconds,
            float impactDiameter,
            LayerMask targetLayers,
            CombatHitPayload hitPayload)
        {
            Center = center;
            WarningSeconds = warningSeconds;
            ImpactDiameter = impactDiameter;
            TargetLayers = targetLayers;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        runner = GetComponent<GoblinTankSlamRunner>();
        ApplyStats();
        ChaseIntent?.SetSpeedScale(chaseSpeedScale);
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && slamAbility != null)
            abilitySystem.GiveAbility(slamAbility);
    }

    public override bool CanUseChaseMovement()
    {
        return base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    }

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        AbilityLogic_GoblinTankSlam logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(slamAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 내려치기 준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태 종료 시 취소가 아니라면 회복 애니메이션을 요청한다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        if (!wasCancelled && !IsDead)
            CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Recover);
    }

    protected override void OnDeathStarted()
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        base.OnDeathStarted();
    }

    public bool TryBuildSlamContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out SlamContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        AbilityLogic_GoblinTankSlam logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem,
            spec,
            logic.DamageEffect,
            logic.KnockbackEffect,
            gameObject,
            logic.DamageAmount,
            logic.KnockbackImpulse);

        context = new SlamContext(
            transform.position,
            logic.WarningSeconds,
            logic.ImpactDiameter,
            logic.TargetLayers,
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
        AbilityLogic_GoblinTankSlam logic = Logic;
        bool valid = abilitySystem != null && slamAbility != null && logic != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(GoblinTank)}] 내려치기 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 고블린 탱커의 원형 경고를 표시하고 경고 종료 시점에 한 번 범위 피해를 적용한다.
/// - 그로기/사망/비활성화 시 남은 경고 표시를 제거한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinTank))]
public sealed partial class GoblinTankSlamRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    private static readonly SoundRef SlamSound = SoundRef.FromKey("sound_goblinTank_Slam");

    [SerializeField] private GoblinTank owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private MonoBehaviour telegraphService;

    private AttackTelegraphStyle warningStyle;
    private IAttackTelegraphPresenter telegraphPresenter;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<GoblinTank>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        telegraphPresenter = AttackTelegraphPresenterResolver.Resolve(telegraphService, this);
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
        if (!owner.TryBuildSlamContext(system, spec, initialTarget, out GoblinTank.SlamContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            float warningSeconds = CombatTimingService.ScaleSeconds(system, context.WarningSeconds, CombatTimingSlot.AttackWarning);
            ShowWarning(context, warningSeconds);
            if (warningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, warningSeconds);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            AbilityLogic_GoblinTankSlam logic = ResolveLogic(spec);
            if (logic != null && logic.PostWarningImpactDelay > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, logic.PostWarningImpactDelay);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            PlaySlamSound(context.Center);
            SpawnImpactEffect(context.Center, context.ImpactDiameter, logic);
            CommonMonsterCombatUtility.TryApplyCircleDamage(
                context.Center,
                context.ImpactDiameter,
                context.TargetLayers,
                owner.gameObject,
                context.HitPayload);
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

    private void ShowWarning(GoblinTank.SlamContext context, float warningSeconds)
    {
        telegraphPresenter?.Show(AttackTelegraphSpecUtility.WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
            context.Center,
            context.ImpactDiameter,
            warningSeconds,
            warningStyle)));
    }

    private void HideWarning()
    {
        telegraphPresenter?.HideCurrent();
    }

    /// <summary>고블린 탱커 내려찍기 피해 타이밍에 임팩트 사운드를 재생합니다.</summary>
    private void PlaySlamSound(Vector2 center)
    {
        SoundPlaybackUtility.Play(
            SlamSound,
            instigator: owner != null ? owner.gameObject : gameObject,
            causer: owner != null ? owner.gameObject : gameObject,
            target: null,
            position: center,
            sourceObject: this);
    }

    /// <summary>
    /// 책임:
    /// - 고블린 탱커가 실제 내려찍기 피해를 발생시키는 순간의 VFX를 생성한다.
    /// - 생성된 VFX는 파티클 수명 또는 fallback 수명 이후 제거해 씬에 잔류하지 않게 한다.
    /// - VFX authoring 데이터는 Runner가 아니라 AL_GoblinTankSlam 에셋에서 읽는다.
    /// </summary>
    private void SpawnImpactEffect(Vector2 center, float impactDiameter, AbilityLogic_GoblinTankSlam logic)
    {
        if (logic == null || logic.ImpactEffectPrefab == null)
            return;

        Vector3 spawnPosition = new(center.x, center.y, transform.position.z);
        spawnPosition += logic.ImpactEffectOffset;

        GameObject instance = Instantiate(logic.ImpactEffectPrefab, spawnPosition, Quaternion.identity);
        float rangeScale = impactDiameter / Mathf.Max(0.01f, logic.ImpactEffectReferenceDiameter);
        instance.transform.localScale *= logic.ImpactEffectScale * rangeScale;
        PlayImpactParticles(instance);
        Destroy(instance, ResolveImpactEffectLifetime(instance, logic.ImpactEffectFallbackLifetime));
    }

    /// <summary>비활성 파티클이 포함된 프리팹도 생성 직후 재생되도록 보장한다.</summary>
    private static void PlayImpactParticles(GameObject instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.gameObject.SetActive(true);
            particleSystem.Play(withChildren: true);
        }
    }

    /// <summary>파티클 duration/startLifetime을 읽어 자동 제거 시간을 계산하고, 없으면 fallback을 사용한다.</summary>
    private static float ResolveImpactEffectLifetime(GameObject instance, float fallbackLifetime)
    {
        if (instance == null)
            return fallbackLifetime;

        float lifetime = 0f;
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
        }

        return Mathf.Max(fallbackLifetime, lifetime);
    }

    /// <summary>실행 중인 AbilitySpec에서 고블린 탱커 내려치기 AL 데이터를 가져온다.</summary>
    private static AbilityLogic_GoblinTankSlam ResolveLogic(AbilitySpec spec)
    {
        return spec != null && spec.Definition != null
            ? spec.Definition.logic as AbilityLogic_GoblinTankSlam
            : null;
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
        style.blinkStartNormalized = 0.7f;
        style.blinkFrequency = 5f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
