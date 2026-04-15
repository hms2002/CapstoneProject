using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public class ShadowServant : Mob
{
    private const float DefaultPresentationLifetimeSeconds = 1f;

    // 이 클래스의 책임:
    // ShadowServant의 공격 조건 판단과 공격 설정 데이터 제공을 담당하고, 실제 시퀀스 실행은 AD/runner에 위임한다.

    [Header("Fog")]
    [Tooltip("안개를 생성할 때 사용할 안개 프리팹입니다.")]
    [SerializeField] private GameObject fog;

    [Tooltip("안개에 사용할 데미지 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec explosionDamageEffect;

    [Tooltip("안개 피해량입니다.")]
    [SerializeField] private float explosionDamage = 1f;

    [Header("Attack Presentation")]
    [SerializeField] private GameObject attackEffectPrefab;
    [SerializeField] private Vector3 attackEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [SerializeField] [Min(0f)] private float attackEffectLifetimeSeconds = 0.35f;
    [SerializeField] private Vector3 attackEffectScaleMultiplier = Vector3.one;
    [SerializeField] private float attackEffectRotationOffsetZ;
    [SerializeField] private GameObject attackParticlePrefab;
    [SerializeField] private Vector3 attackParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [SerializeField] [Min(0f)] private float attackParticleLifetimeOverrideSeconds;
    [SerializeField] private bool useUnscaledAttackParticleTime;
    [SerializeField] private Vector3 attackParticleScaleMultiplier = Vector3.one;
    [SerializeField] private float attackParticleRotationOffsetZ;
    [SerializeField] private SoundRef attackSound;
    [SerializeField] private CameraShakeHook attackCameraShake = CameraShakeHook.Create(0.14f, 1f, 0.22f, 0.04f);
    [Header("Ability")]
    [SerializeField] private AbilityDefinition attackAbilityDefinition;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private ShadowServantAttackRunner attackRunner;

    private bool hasLoggedInvalidConfig;
    private bool ownsRuntimeAbilityDefinition;
    private AbilityLogic_ShadowServantAttack runtimeAttackLogic;

    protected override void Awake()
    {
        base.Awake();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator == null)
            abilityCoordinator = gameObject.AddComponent<MobAbilityCoordinator>();

        if (attackRunner == null)
            attackRunner = GetComponent<ShadowServantAttackRunner>();
        if (attackRunner == null)
            attackRunner = gameObject.AddComponent<ShadowServantAttackRunner>();
    }

    protected override void Start()
    {
        base.Start();
        EnsureAttackAbility();
    }

    public override bool CanUseChaseMovement()
    {
        return attackRunner == null || !attackRunner.IsRunning;
    }

    protected override void UpdateAttack()
    {
        if (abilityCoordinator == null || attackAbilityDefinition == null)
            return;

        if (abilityCoordinator.IsAbilityExecutionBusy)
            return;

        if (!CanAttack())
            return;

        abilityCoordinator.TryStartAbility(attackAbilityDefinition, target != null ? target.gameObject : null);
    }

    protected override void OnDeathStarted()
    {
        abilityCoordinator?.CancelActiveAbility(true);
        base.OnDeathStarted();
    }

    protected override bool CanDrawStopRangeGizmo()
    {
        return false;
    }

    protected override void DrawAttackGizmos()
    {
        float attackRadius = GetAttackRadius();
        if (attackRadius <= 0f)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (ownsRuntimeAbilityDefinition)
        {
            if (runtimeAttackLogic != null)
                Destroy(runtimeAttackLogic);
            if (attackAbilityDefinition != null)
                Destroy(attackAbilityDefinition);
        }
    }

    private bool CanAttack()
    {
        if (isDead)
            return false;

        if (!HasAttackData())
            return false;

        if (target == null)
            return false;

        return IsTargetInRange();
    }

    private bool HasAttackData()
    {
        bool isValid = fog != null &&
                       explosionDamageEffect != null &&
                       abilitySystem != null &&
                       GetFogRadius() > 0f &&
                       attackRunner != null;

        if (isValid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError(
                $"[{nameof(ShadowServant)}] 공격 설정이 비어 있습니다.",
                this);

            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    private bool IsTargetInRange()
    {
        if (target == null)
            return false;

        float attackRadius = GetAttackRadius();
        if (attackRadius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        return toTarget.sqrMagnitude <= attackRadius * attackRadius;
    }

    private void EnsureAttackAbility()
    {
        if (abilitySystem == null)
            return;

        if (attackAbilityDefinition != null)
        {
            if (abilitySystem.FindSpec(attackAbilityDefinition) == null)
                abilitySystem.GiveAbility(attackAbilityDefinition);

            return;
        }

        runtimeAttackLogic = ScriptableObject.CreateInstance<AbilityLogic_ShadowServantAttack>();
        attackAbilityDefinition = ScriptableObject.CreateInstance<AbilityDefinition>();
        attackAbilityDefinition.name = "AD_ShadowServant_Attack_Runtime";
        attackAbilityDefinition.abilityName = "AD_ShadowServant_Attack_Runtime";
        attackAbilityDefinition.castTime = 0f;
        attackAbilityDefinition.recoveryTime = 0f;
        attackAbilityDefinition.animationChannel = AbilityDefinition.AnimationChannel.Player;
        attackAbilityDefinition.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;
        attackAbilityDefinition.logic = runtimeAttackLogic;
        abilitySystem.GiveAbility(attackAbilityDefinition);
        ownsRuntimeAbilityDefinition = true;
    }

    public bool TryCreateAttackContext(GameObject explicitTarget, float delaySeconds, out ShadowServantAttackRunner.AttackContext context)
    {
        context = default;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (targetObject == null || !HasAttackData())
            return false;

        Vector3 targetPoint = targetObject.transform.position;
        Vector3 hitPoint = GetHitPoint(targetPoint);
        context = new ShadowServantAttackRunner.AttackContext(
            targetObject,
            targetPoint,
            hitPoint,
            GetFogDiameter(),
            Mathf.Max(0f, delaySeconds),
            GetDamageMask(targetObject));
        return true;
    }

    public void SpawnFog(Vector3 targetPoint)
    {
        Instantiate(fog, new Vector3(targetPoint.x, targetPoint.y, 0f), Quaternion.identity);
    }

    public void PlayAttackPresentation(Vector3 targetPoint)
    {
        if (animator != null)
            animator.SetTrigger("attack");

        SpawnPresentationPrefab(
            attackEffectPrefab,
            targetPoint + attackEffectLocalOffset,
            attackEffectRotationOffsetZ,
            attackEffectScaleMultiplier,
            attackEffectLifetimeSeconds,
            useUnscaledTime: false);
        SpawnPresentationPrefab(
            attackParticlePrefab,
            targetPoint + attackParticleLocalOffset,
            attackParticleRotationOffsetZ,
            attackParticleScaleMultiplier,
            attackParticleLifetimeOverrideSeconds,
            useUnscaledAttackParticleTime);

        SoundPlaybackUtility.Play(
            attackSound,
            instigator: gameObject,
            causer: gameObject,
            target: target != null ? target.gameObject : null,
            position: targetPoint,
            sourceObject: this);

        attackCameraShake.TryPlay(
            gameObject,
            targetPoint - transform.position,
            debugReason: "ShadowServant.Attack");
    }

    public LayerMask GetDamageMask(GameObject explicitTarget)
    {
        return explicitTarget != null
            ? (LayerMask)(1 << explicitTarget.layer)
            : (LayerMask)0;
    }

    public CombatHitPayload MakeHitPayload(AbilitySystem sourceSystem, AbilitySpec spec)
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: explosionDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: sourceSystem != null ? sourceSystem : abilitySystem,
            sourceSpec: spec,
            damageEffect: explosionDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    private float GetAttackRadius()
    {
        return ChaseIntent != null
            ? Mathf.Max(0f, ChaseIntent.StopRange)
            : 0f;
    }

    public float GetFogRadius()
    {
        if (fog == null)
            return 0f;

        CircleCollider2D fogCollider = fog.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return 0f;

        return Mathf.Max(0f, fogCollider.radius);
    }

    public float GetFogDiameter()
    {
        return GetFogRadius() * 2f;
    }

    private Vector3 GetHitPoint(Vector3 targetPoint)
    {
        Vector2 fogOffset = GetFogOffset();
        return targetPoint + new Vector3(fogOffset.x, fogOffset.y, 0f);
    }

    private Vector2 GetFogOffset()
    {
        if (fog == null)
            return Vector2.zero;

        CircleCollider2D fogCollider = fog.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return Vector2.zero;

        Vector3 scale = fog.transform.localScale;
        return new Vector2(
            fogCollider.offset.x * scale.x,
            fogCollider.offset.y * scale.y);
    }

    private static void SpawnPresentationPrefab(
        GameObject prefab,
        Vector3 position,
        float rotationOffsetZ,
        Vector3 scaleMultiplier,
        float lifetimeOverrideSeconds,
        bool useUnscaledTime)
    {
        if (prefab == null)
            return;

        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationOffsetZ);
        GameObject instance = Instantiate(prefab, position, rotation);
        if (instance == null)
            return;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
        ConfigureSpawnedPresentation(instance, useUnscaledTime);

        float lifetime = ResolvePresentationLifetime(instance, lifetimeOverrideSeconds);
        if (lifetime > 0f)
            Destroy(instance, lifetime);
    }

    private static void ConfigureSpawnedPresentation(GameObject instance, bool useUnscaledTime)
    {
        if (instance == null)
            return;

        instance.SetActive(true);

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (useUnscaledTime)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            particleSystem.Play(withChildren: true);
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            animationComponent.Play();
        }
    }

    private static float ResolvePresentationLifetime(GameObject instance, float lifetimeOverrideSeconds)
    {
        if (lifetimeOverrideSeconds > 0f)
            return lifetimeOverrideSeconds;

        float particleLifetime = ResolveParticleLifetime(instance);
        if (particleLifetime > 0f)
            return particleLifetime;

        float animationLifetime = ResolveAnimatorLifetime(instance);
        if (animationLifetime > 0f)
            return animationLifetime;

        return DefaultPresentationLifetimeSeconds;
    }

    private static float ResolveParticleLifetime(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        if (particleSystems == null || particleSystems.Length == 0)
            return 0f;

        float maxLifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            if (main.loop)
                return DefaultPresentationLifetimeSeconds;

            float startDelay = ResolveCurveMax(main.startDelay);
            float startLifetime = ResolveCurveMax(main.startLifetime);
            maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
        }

        return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
    }

    private static float ResolveAnimatorLifetime(GameObject instance)
    {
        float maxLifetime = 0f;

        Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                if (clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, clip.length);
            }
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            foreach (AnimationState state in animationComponent)
            {
                if (state?.clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, state.clip.length);
            }
        }

        return maxLifetime > 0f ? maxLifetime + 0.05f : 0f;
    }

    private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
            _ => Mathf.Max(curve.constant, curve.constantMax)
        };
    }
}
