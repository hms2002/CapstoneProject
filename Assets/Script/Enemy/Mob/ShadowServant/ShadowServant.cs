using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public class ShadowServant : Mob, IMobAttackDecisionSource
{
    private const float DefaultPresentationLifetimeSeconds = 1f;

    // 이 클래스의 책임:
    // ShadowServant의 공격 조건 판단과 공격 설정 데이터 제공을 담당하고, 실제 시퀀스 실행은 AD/runner에 위임한다.

    [Header("Ability")]
    [SerializeField] private AbilityDefinition attackAbilityDefinition;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private ShadowServantAttackRunner attackRunner;
    [Header("FSM")]
    [Tooltip("공격이 끝난 뒤 다음 상태 전이를 잠깐 늦춰 전투 리듬을 만드는 AI 후딜 시간입니다.")]
    [SerializeField] [Min(0f)] private float postAttackRecoverSeconds = 0.35f;

    private bool hasLoggedInvalidConfig;
    private IMobAbilityHelperAccess helperAccess;

    protected override void Awake()
    {
        base.Awake();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator == null)
            abilityCoordinator = gameObject.AddComponent<MobAbilityCoordinator>();
        helperAccess = abilityCoordinator as IMobAbilityHelperAccess;

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

    /// <summary>
    /// 책임 :
    /// - ShadowServant 공격 패턴 데이터의 현재 공식 소유자를 AL로 통일해 helper와 runner가 같은 설정을 보게 한다.
    /// - AL asset이 없거나 잘못 연결된 경우를 바로 드러내고, 패턴 실행 데이터가 owner fallback로 되돌아가지 않게 한다.
    /// </summary>
    public AbilityLogic_ShadowServantAttack.PatternData GetAttackPatternData()
    {
        AbilityLogic_ShadowServantAttack logic = GetAttackLogic();
        return logic != null ? logic.Data : default;
    }

    private AbilityLogic_ShadowServantAttack GetAttackLogic()
    {
        return attackAbilityDefinition != null
            ? attackAbilityDefinition.logic as AbilityLogic_ShadowServantAttack
            : null;
    }

    public override bool CanUseChaseMovement()
    {
        return attackRunner == null || !attackRunner.IsRunning;
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
    }

    public bool CanTryAttack(GameObject explicitTarget = null)
    {
        if (isDead)
            return false;

        if (!HasAttackData())
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (targetObject == null)
            return false;

        if (ChaseIntent != null && !ChaseIntent.IsTargetWithinDetectionRange())
            return false;

        return IsTargetInRange(targetObject.transform);
    }

    private bool HasAttackData()
    {
        AbilityLogic_ShadowServantAttack.PatternData data = GetAttackPatternData();
        bool isValid = data.fogPrefab != null &&
                       data.damageEffect != null &&
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

    private bool IsTargetInRange(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        float attackRadius = GetAttackRadius();
        if (attackRadius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(targetTransform.position - transform.position);
        return toTarget.sqrMagnitude <= attackRadius * attackRadius;
    }

    private void EnsureAttackAbility()
    {
        if (abilitySystem == null || attackAbilityDefinition == null)
            return;

        if (abilitySystem.FindSpec(attackAbilityDefinition) == null)
            abilitySystem.GiveAbility(attackAbilityDefinition);
    }

    public bool TryBuildAttackContext(GameObject explicitTarget, float delaySeconds, out ShadowServantAttackRunner.AttackContext context)
    {
        context = default;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (!CanTryAttack(targetObject))
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

    public bool TryRequestAttack(GameObject explicitTarget)
    {
        if (abilityCoordinator == null || attackAbilityDefinition == null)
            return false;

        if (helperAccess != null && helperAccess.GetCooldownRemaining(attackAbilityDefinition) > 0f)
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (!CanTryAttack(targetObject))
            return false;

        if (!TryBuildAttackContext(targetObject, Mathf.Max(0f, GetAttackPatternData().warningDuration), out _))
            return false;

        return abilityCoordinator.TryStartAbility(attackAbilityDefinition, targetObject);
    }

    /// <summary>FSM AttackState가 사용할 공격 요청을 구성합니다.</summary>
    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!TryBuildAttackContext(targetObject, Mathf.Max(0f, GetAttackPatternData().warningDuration), out _))
            return false;

        request = new MobAttackRequest(attackAbilityDefinition, targetObject, postAttackRecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 ShadowServant가 추가로 처리할 것이 없어 비워 둡니다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
    }

    /// <summary>공격 상태 종료 시 ShadowServant가 추가로 정리할 것이 없어 비워 둡니다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
    }

    public bool TryCreateAttackContext(GameObject explicitTarget, float delaySeconds, out ShadowServantAttackRunner.AttackContext context)
    {
        return TryBuildAttackContext(explicitTarget, delaySeconds, out context);
    }

    public void SpawnFog(Vector3 targetPoint)
    {
        GameObject fogPrefab = GetAttackPatternData().fogPrefab;
        if (fogPrefab == null)
            return;

        Instantiate(fogPrefab, new Vector3(targetPoint.x, targetPoint.y, 0f), Quaternion.identity);
    }

    public void PlayAttackPresentation(Vector3 targetPoint)
    {
        AbilityLogic_ShadowServantAttack.PatternData data = GetAttackPatternData();

        if (animator != null)
            animator.SetTrigger("attack");

        SpawnPresentationPrefab(
            data.attackEffectPrefab,
            targetPoint + data.attackEffectLocalOffset,
            data.attackEffectRotationOffsetZ,
            data.attackEffectScaleMultiplier,
            data.attackEffectLifetimeSeconds,
            useUnscaledTime: false);
        SpawnPresentationPrefab(
            data.attackParticlePrefab,
            targetPoint + data.attackParticleLocalOffset,
            data.attackParticleRotationOffsetZ,
            data.attackParticleScaleMultiplier,
            data.attackParticleLifetimeOverrideSeconds,
            data.useUnscaledAttackParticleTime);

        SoundPlaybackUtility.Play(
            data.attackSound,
            instigator: gameObject,
            causer: gameObject,
            target: target != null ? target.gameObject : null,
            position: targetPoint,
            sourceObject: this);

        data.attackCameraShake.TryPlay(
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
            finalHpDamage: GetAttackPatternData().damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: sourceSystem != null ? sourceSystem : abilitySystem,
            sourceSpec: spec,
            damageEffect: GetAttackPatternData().damageEffect,
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
        GameObject fogPrefab = GetAttackPatternData().fogPrefab;
        if (fogPrefab == null)
            return 0f;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
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
        GameObject fogPrefab = GetAttackPatternData().fogPrefab;
        if (fogPrefab == null)
            return Vector2.zero;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return Vector2.zero;

        Vector3 scale = fogPrefab.transform.localScale;
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
