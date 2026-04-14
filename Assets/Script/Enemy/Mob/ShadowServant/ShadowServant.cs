using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public class ShadowServant : Mob
{
    private const float AttackDelay = 2f;
    private const float DefaultPresentationLifetimeSeconds = 1f;

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

    private readonly HashSet<GameObject> damagedTargets = new();

    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle warningStyle;
    private Coroutine attackRoutine;
    private bool isAttacking;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = MakeWarningStyle();
    }

    public override bool CanUseChaseMovement()
    {
        return !isAttacking;
    }

    protected override void UpdateAttack()
    {
        if (attackRoutine != null)
            return;

        if (!CanAttack())
            return;

        attackRoutine = StartCoroutine(RunAttack());
    }

    protected override void OnDeathStarted()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        ClearAttack();
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

        if (warningStyle != null)
            Destroy(warningStyle);
    }

    private bool CanAttack()
    {
        if (isDead || isAttacking)
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
                       GetFogRadius() > 0f;

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

    private IEnumerator RunAttack()
    {
        isAttacking = true;

        Vector3 targetPoint = target != null ? target.position : transform.position;
        Vector3 hitPoint = GetHitPoint(targetPoint);
        ShowWarning(hitPoint);

        yield return new WaitForSeconds(AttackDelay);

        if (isDead)
        {
            ClearAttack();
            yield break;
        }

        if (animator != null)
            animator.SetTrigger("attack");

        PlayAttackPresentation(hitPoint);
        Explode(hitPoint);
        SpawnFog(targetPoint);
        ClearAttack();
    }

    private void ShowWarning(Vector3 targetPoint)
    {
        if (telegraphService == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            targetPoint,
            GetFogDiameter(),
            AttackDelay,
            warningStyle);

        telegraphService.Show(spec);
    }

    private void ClearAttack()
    {
        attackRoutine = null;
        isAttacking = false;

        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    private void Explode(Vector3 targetPoint)
    {
        CombatHitPayload payload = MakeHitPayload();
        if (payload == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            targetPoint,
            GetFogRadius(),
            GetDamageMask());

        damagedTargets.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            GameObject hitTarget = CombatTargetResolver2D.ResolveDamageTarget(hit);

            if (hitTarget == null || hitTarget == gameObject)
                continue;

            if (!damagedTargets.Add(hitTarget))
                continue;

            CombatHitPayloadApplier.Apply(hitTarget, payload, hit.ClosestPoint(targetPoint));
        }
    }

    private void SpawnFog(Vector3 targetPoint)
    {
        Instantiate(fog, new Vector3(targetPoint.x, targetPoint.y, 0f), Quaternion.identity);
    }

    private void PlayAttackPresentation(Vector3 targetPoint)
    {
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

    private LayerMask GetDamageMask()
    {
        return target != null
            ? (LayerMask)(1 << target.gameObject.layer)
            : (LayerMask)0;
    }

    private CombatHitPayload MakeHitPayload()
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: explosionDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
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

    private float GetFogRadius()
    {
        if (fog == null)
            return 0f;

        CircleCollider2D fogCollider = fog.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return 0f;

        return Mathf.Max(0f, fogCollider.radius);
    }

    private float GetFogDiameter()
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

    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.35f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.35f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
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
