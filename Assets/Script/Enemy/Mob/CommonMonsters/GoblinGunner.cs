using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 고블린 사수의 추적 조준선 공격 판단과 탄막 생성 문맥을 소유한다.
/// - 실제 경고 표시와 발사는 GoblinGunnerShotRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(GoblinGunnerShotRunner))]
public sealed class GoblinGunner : Mob, IMobAttackDecisionSource
{
    [SerializeField] private AbilityDefinition shotAbility;
    [SerializeField, Min(0f)] private float maxHealth = 5f;
    [Header("Presentation Sockets")]
    [SerializeField] private Transform muzzleEffectSocket;

    private GoblinGunnerShotRunner runner;
    private bool hasLoggedInvalidConfig;
    private AbilityLogic_GoblinGunnerShot Logic => shotAbility != null ? shotAbility.logic as AbilityLogic_GoblinGunnerShot : null;
    public AbilityLogic_GoblinGunnerShot ShotLogic => Logic;

    public readonly struct ShotContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly float WarningSeconds;
        public readonly float WarningWidth;
        public readonly float TelegraphRange;
        public readonly float ProjectileSpeed;
        public readonly float ProjectileLifetime;
        public readonly LayerMask WallLayers;
        public readonly LayerMask TargetLayers;
        public readonly CombatHitPayload HitPayload;

        public ShotContext(
            GameObject target,
            Vector2 origin,
            Vector2 direction,
            float warningSeconds,
            float warningWidth,
            float telegraphRange,
            float projectileSpeed,
            float projectileLifetime,
            LayerMask wallLayers,
            LayerMask targetLayers,
            CombatHitPayload hitPayload)
        {
            Target = target;
            Origin = origin;
            Direction = direction;
            WarningSeconds = warningSeconds;
            WarningWidth = warningWidth;
            TelegraphRange = telegraphRange;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetime = projectileLifetime;
            WallLayers = wallLayers;
            TargetLayers = targetLayers;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        runner = GetComponent<GoblinGunnerShotRunner>();
        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && shotAbility != null)
            abilitySystem.GiveAbility(shotAbility);
    }

    public override bool CanUseChaseMovement()
    {
        return base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    }

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        AbilityLogic_GoblinGunnerShot logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(shotAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 조준 준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태 종료 시 취소가 아니라면 사격 회복 애니메이션을 요청한다.</summary>
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

    public bool TryBuildShotContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ShotContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        AbilityLogic_GoblinGunnerShot logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        Vector2 direction = CommonMonsterCombatUtility.DirectionTo(gameObject, targetObject, sprite != null && sprite.flipX);
        float speed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(target) * logic.ProjectileSpeedMultiplier;
        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem,
            spec,
            logic.DamageEffect,
            null,
            gameObject,
            logic.DamageAmount,
            0f);

        context = new ShotContext(
            targetObject,
            transform.position,
            direction,
            logic.WarningSeconds,
            logic.WarningWidth,
            logic.AttackRange,
            speed,
            logic.ProjectileLifetime,
            logic.WallLayers,
            logic.TargetLayers,
            payload);
        return true;
    }

    public void FireProjectile(ShotContext context)
    {
        AbilityLogic_GoblinGunnerShot logic = Logic;
        if (logic == null || logic.ProjectilePrefab == null)
            return;

        SpawnMuzzleEffect(logic);

        GameObject projectileObject = Instantiate(logic.ProjectilePrefab, context.Origin, Quaternion.identity);
        if (projectileObject == null)
            return;

        LightBeadProjectile2D projectile = projectileObject.GetComponent<LightBeadProjectile2D>();
        if (projectile == null)
        {
            Debug.LogError($"[{nameof(GoblinGunner)}] AL projectilePrefab에 {nameof(LightBeadProjectile2D)}가 없습니다.", projectileObject);
            Destroy(projectileObject);
            return;
        }

        projectile.Setup(new ProjectileAttackSpawnContext
        {
            ownerSystem = abilitySystem,
            sourceSpec = null,
            causer = gameObject,
            ignoreTarget = gameObject,
            lifetime = context.ProjectileLifetime,
            wallLayers = context.WallLayers,
            damageLayers = context.TargetLayers,
            hitPayload = context.HitPayload,
            direction = context.Direction,
            speed = context.ProjectileSpeed
        });
    }

    private void SpawnMuzzleEffect(AbilityLogic_GoblinGunnerShot logic)
    {
        if (logic == null || logic.MuzzleEffectPrefab == null)
            return;

        Vector3 spawnPosition = ResolveMuzzleEffectPosition();
        Quaternion spawnRotation = logic.MuzzleEffectPrefab.transform.rotation;
        GameObject instance = Instantiate(logic.MuzzleEffectPrefab, spawnPosition, spawnRotation);
        if (instance == null)
            return;

        Vector3 effectScale = logic.MuzzleEffectPrefab.transform.localScale * logic.MuzzleEffectScale;
        if (sprite != null && sprite.flipX)
            effectScale.x *= -1f;
        instance.transform.localScale = effectScale;

        ParticleSystem particle = instance.GetComponentInChildren<ParticleSystem>();
        if (particle != null)
            Destroy(instance, particle.main.duration + particle.main.startLifetime.constantMax);
        else
            Destroy(instance, logic.MuzzleEffectFallbackLifetime);
    }

    private Vector3 ResolveMuzzleEffectPosition()
    {
        if (muzzleEffectSocket == null)
            return transform.position;

        if (sprite == null || !sprite.flipX || muzzleEffectSocket.parent == null)
            return muzzleEffectSocket.position;

        Vector3 mirroredLocalPosition = muzzleEffectSocket.localPosition;
        mirroredLocalPosition.x *= -1f;
        return muzzleEffectSocket.parent.TransformPoint(mirroredLocalPosition);
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
        AbilityLogic_GoblinGunnerShot logic = Logic;
        bool valid = abilitySystem != null && shotAbility != null && logic != null && logic.ProjectilePrefab != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(GoblinGunner)}] 사격 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 고블린 사수의 추적 실선 경고를 표시하고 발사 직전 방향으로 한 발을 발사한다.
/// - 경고 진행도는 유지하되, 발사 전까지 목표 위치에 맞춰 조준선 geometry를 갱신한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinGunner))]
public sealed partial class GoblinGunnerShotRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private GoblinGunner owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;
    private float nextWallClipProbeLogTime;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<GoblinGunner>();
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
        if (!owner.TryBuildShotContext(system, spec, initialTarget, out GoblinGunner.ShotContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            ShowWarning(context);
            if (context.WarningSeconds > 0f)
                yield return TrackWarningUntilFire(system, spec, initialTarget, context);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            if (owner.TryBuildShotContext(system, spec, initialTarget, out GoblinGunner.ShotContext finalContext))
            {
                context = finalContext;
                UpdateWarning(context);
            }

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            owner.FireProjectile(context);
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    private IEnumerator TrackWarningUntilFire(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject initialTarget,
        GoblinGunner.ShotContext context)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, context.WarningSeconds);

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            if (owner.TryBuildShotContext(system, spec, initialTarget, out GoblinGunner.ShotContext trackedContext))
            {
                context = trackedContext;
                UpdateWarning(context);
            }

            elapsed += Time.deltaTime;
            yield return null;
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

    private void ShowWarning(GoblinGunner.ShotContext context)
    {
        if (telegraphService == null)
            return;

        telegraphService.Show(CreateWarningSpec(context));
    }

    private void UpdateWarning(GoblinGunner.ShotContext context)
    {
        if (telegraphService == null)
            return;

        telegraphService.UpdateCurrentGeometry(CreateWarningSpec(context));
    }

    private AttackTelegraphSpec CreateWarningSpec(GoblinGunner.ShotContext context)
    {
        LogWallClipProbe(context);

        Vector3 center = (Vector3)context.Origin + (Vector3)(context.Direction.normalized * context.TelegraphRange * 0.5f);
        float angle = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(context.TelegraphRange, context.WarningWidth),
            angle,
            context.WarningSeconds,
            warningStyle);
        spec.origin = context.Origin;
        return spec.WithWallClipping(context.WallLayers, 48, 0.03f);
    }

    private void LogWallClipProbe(GoblinGunner.ShotContext context)
    {
        AbilityLogic_GoblinGunnerShot logic = ResolveLogic();
        if (logic == null || !logic.LogWallClipProbe || Time.time < nextWallClipProbeLogTime)
            return;

        nextWallClipProbeLogTime = Time.time + logic.WallClipProbeLogInterval;

        Vector2 direction = context.Direction.sqrMagnitude > 0.0001f ? context.Direction.normalized : Vector2.right;
        RaycastHit2D hit = Physics2D.Raycast(context.Origin, direction, context.TelegraphRange, context.WallLayers);
        if (hit.collider == null)
        {
            Debug.Log(
                $"[GoblinGunnerWallClipProbe] no wall hit. origin={context.Origin}, dir={direction}, range={context.TelegraphRange:0.00}, wallMask={context.WallLayers.value}",
                this);
            return;
        }

        Debug.Log(
            $"[GoblinGunnerWallClipProbe] wall hit. collider={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}({hit.collider.gameObject.layer}), trigger={hit.collider.isTrigger}, distance={hit.distance:0.00}, point={hit.point}, wallMask={context.WallLayers.value}",
            hit.collider);
    }

    private void HideWarning()
    {
        telegraphService?.HideCurrent();
    }

    /// <summary>Runner가 직접 데이터를 소유하지 않고 현재 AD에 연결된 사격 AL 데이터를 조회한다.</summary>
    private AbilityLogic_GoblinGunnerShot ResolveLogic()
    {
        return owner != null ? owner.ShotLogic : null;
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private static AttackTelegraphStyle CreateWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0.15f, 0.05f, 0.12f);
        style.fillColorEnd = new Color(1f, 0.15f, 0.05f, 0.3f);
        style.borderColorStart = new Color(1f, 0.65f, 0.25f, 1f);
        style.borderColorEnd = new Color(1f, 0.65f, 0.25f, 1f);
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
