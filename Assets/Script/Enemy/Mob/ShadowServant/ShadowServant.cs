using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public class ShadowServant : Mob
{
    private const float AttackDelay = 2f;

    [Header("Fog")]
    [Tooltip("안개를 생성할 때 사용할 안개 프리팹입니다.")]
    [SerializeField] private GameObject fog;

    [Tooltip("안개에 사용할 데미지 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec explosionDamageEffect;

    [Tooltip("안개 피해량입니다.")]
    [SerializeField] private float explosionDamage = 1f;

    [Header("Attack Presentation")]
    [SerializeField] private WorldPresentationHook attackPresentation;
    [HideInInspector, FormerlySerializedAs("attackEffectPrefab")]
    [SerializeField] private GameObject legacyAttackEffectPrefab;
    [HideInInspector, FormerlySerializedAs("attackEffectLocalOffset")]
    [SerializeField] private Vector3 legacyAttackEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [HideInInspector, FormerlySerializedAs("attackEffectLifetimeSeconds")]
    [SerializeField] private float legacyAttackEffectLifetimeSeconds = 0.35f;
    [HideInInspector, FormerlySerializedAs("attackEffectScaleMultiplier")]
    [SerializeField] private Vector3 legacyAttackEffectScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("attackEffectRotationOffsetZ")]
    [SerializeField] private float legacyAttackEffectRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("attackParticlePrefab")]
    [SerializeField] private GameObject legacyAttackParticlePrefab;
    [HideInInspector, FormerlySerializedAs("attackParticleLocalOffset")]
    [SerializeField] private Vector3 legacyAttackParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [HideInInspector, FormerlySerializedAs("attackParticleLifetimeOverrideSeconds")]
    [SerializeField] private float legacyAttackParticleLifetimeOverrideSeconds;
    [HideInInspector, FormerlySerializedAs("useUnscaledAttackParticleTime")]
    [SerializeField] private bool legacyUseUnscaledAttackParticleTime;
    [HideInInspector, FormerlySerializedAs("attackParticleScaleMultiplier")]
    [SerializeField] private Vector3 legacyAttackParticleScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("attackParticleRotationOffsetZ")]
    [SerializeField] private float legacyAttackParticleRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("attackSound")]
    [SerializeField] private SoundRef legacyAttackSound;
    [HideInInspector, FormerlySerializedAs("attackCameraShake")]
    [SerializeField] private CameraShakeHook legacyAttackCameraShake = CameraShakeHook.Create(0.14f, 1f, 0.22f, 0.04f);

    private readonly HashSet<GameObject> damagedTargets = new();

    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle warningStyle;
    private Coroutine attackRoutine;
    private bool isAttacking;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();
        MigrateLegacyAttackPresentation();
        telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = MakeWarningStyle();
    }

    private void OnValidate()
    {
        MigrateLegacyAttackPresentation();
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
        WorldPresentationRuntime.Play(
            attackPresentation,
            WorldPresentationContext.AtWorld(
                instigator: gameObject,
                position: targetPoint,
                fallbackDirection: targetPoint - transform.position,
                target: target != null ? target.gameObject : null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: gameObject));
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

    private void MigrateLegacyAttackPresentation()
    {
        if (legacyAttackEffectPrefab != null && !attackPresentation.effect.HasContent)
        {
            attackPresentation.effect.prefab = legacyAttackEffectPrefab;
            attackPresentation.effect.localOffset = legacyAttackEffectLocalOffset;
            attackPresentation.effect.rotationOffsetZ = legacyAttackEffectRotationOffsetZ;
            attackPresentation.effect.scaleMultiplier = legacyAttackEffectScaleMultiplier;
            attackPresentation.effect.lifetimeOverrideSeconds = legacyAttackEffectLifetimeSeconds;
        }

        if (legacyAttackParticlePrefab != null && !attackPresentation.particle.HasContent)
        {
            attackPresentation.particle.prefab = legacyAttackParticlePrefab;
            attackPresentation.particle.localOffset = legacyAttackParticleLocalOffset;
            attackPresentation.particle.rotationOffsetZ = legacyAttackParticleRotationOffsetZ;
            attackPresentation.particle.scaleMultiplier = legacyAttackParticleScaleMultiplier;
            attackPresentation.particle.lifetimeOverrideSeconds = legacyAttackParticleLifetimeOverrideSeconds;
            attackPresentation.particle.useUnscaledTime = legacyUseUnscaledAttackParticleTime;
        }

        if (!attackPresentation.HasSound && legacyAttackSound.IsSet)
            attackPresentation.sound = legacyAttackSound;

        if (!attackPresentation.HasShake && legacyAttackCameraShake.amplitude > 0f)
            attackPresentation.cameraShake = legacyAttackCameraShake;
    }

}
