using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 회전 패턴을 실행하며, 원형 경고, 범위 피해/넉백, 고정 수량의 분산/3발마다 조준 탄막을 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DragonRotation", menuName = "GAS/Ability Logic/Dragon/AL_DragonRotation")]
public sealed class AbilityLogic_DragonRotation : AbilityLogic
{
    private readonly Dictionary<GameObject, float> nextDamageAllowedTimes = new();

    [Header("Timing")]
    [SerializeField, Min(0f)] private float warningSeconds = 1.4f;
    [SerializeField, Min(0.01f)] private float spinSeconds = 2.5f;
    [SerializeField, Min(0.01f)] private float damageIntervalSeconds = 0.45f;
    [SerializeField, Min(0f)] private float visualSwayAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float visualSwayFrequency = 3.5f;
    [SerializeField] private bool debugVisualSway;

    [Header("Area Hit")]
    [SerializeField, Min(0.1f)] private float hitDiameter = 3.6f;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 8f;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GE_Damage_Spec projectileDamageEffect;
    [SerializeField, Min(0f)] private float projectileDamageAmount = 1f;
    [SerializeField, Min(0f)] private float projectileSpeed = 5f;
    [SerializeField, Min(0.01f)] private float projectileLifetimeSeconds = 4f;
    [SerializeField, Min(0.01f)] private float projectileSpawnIntervalSeconds = 0.4f;
    [Tooltip("기존 회전 시간/발사 간격으로 계산한 탄막 수의 배수입니다. 회전 시간은 유지하며 균등 분산 발사합니다.")]
    [SerializeField, Min(1)] private int projectileCountMultiplier = 1;
    [SerializeField, Min(0f)] private float projectileSpawnRadius = 0.65f;
    [SerializeField] private float projectileRotationOffsetDegrees;
    [SerializeField] private LayerMask projectileWallLayers;
    [SerializeField] private SoundRef projectileFireSound;

    [Header("Audio")]
    [SerializeField] private SoundRef spinLoopSound = new()
    {
        key = "sound_dragon_spinloop",
        volumeMultiplier = 1f,
        anchorPolicy = SoundAnchorPolicy.CatalogDefault
    };

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
        if (dragon == null)
            yield break;

        IAttackTelegraphPresenter telegraphService = AttackTelegraphPresenterResolver.Resolve(dragon);
        Vector2 center = dragon.transform.position;

        ShowWarningTelegraph(telegraphService, center);

        dragon.PushFaceTargetLock();
        try
        {
            yield return WaitForSecondsUnlessCancelled(warningSeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            telegraphService?.HideCurrent();
            dragon.PlayPatternTrigger(DragonAnimationKeys.Rotation);

            yield return RunSpin(dragon, spec);
        }
        finally
        {
            telegraphService?.HideCurrent();
            nextDamageAllowedTimes.Clear();
            dragon.PopFaceTargetLock();
            dragon.PlayPatternTrigger(DragonAnimationKeys.Idle);
        }
    }

    private IEnumerator RunSpin(DragonController dragon, AbilitySpec spec)
    {
        nextDamageAllowedTimes.Clear();

        float elapsed = 0f;
        int projectileCount = ResolveProjectileCount();
        int nextProjectileIndex = 0;
        List<GameObject> spawnedProjectiles = new();
        Transform visualRoot = dragon != null ? dragon.PatternMotionRoot : null;
        Transform shadowRoot = dragon != null ? dragon.PatternShadowMotionRoot : null;
        Vector3 visualBaseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        Vector3 shadowBaseLocalPosition = shadowRoot != null ? shadowRoot.localPosition : Vector3.zero;
        AudioHandle spinLoopHandle = AudioHandle.Invalid;
        LogVisualSwayStart(dragon, visualRoot, visualBaseLocalPosition);

        try
        {
            spinLoopHandle = SoundPlaybackUtility.Play(
                spinLoopSound,
                instigator: dragon.gameObject,
                causer: dragon.gameObject,
                target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                position: dragon.transform.position,
                sourceObject: this);

            while (elapsed < spinSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                ApplyVisualSway(visualRoot, visualBaseLocalPosition, shadowRoot, shadowBaseLocalPosition, elapsed);
                ApplyAreaHit(dragon);

                SpawnDueProjectiles(dragon, spawnedProjectiles, elapsed, projectileCount, ref nextProjectileIndex);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!IsAbilityCancelled(spec))
            {
                // A long final frame must not discard the remaining scheduled shots.
                SpawnDueProjectiles(dragon, spawnedProjectiles, spinSeconds, projectileCount, ref nextProjectileIndex);
                ApplyAreaHit(dragon);
            }
        }
        finally
        {
            if (visualRoot != null)
                visualRoot.localPosition = visualBaseLocalPosition;

            if (shadowRoot != null)
                shadowRoot.localPosition = shadowBaseLocalPosition;

            if (IsAbilityCancelled(spec))
                DestroySpawnedProjectiles(spawnedProjectiles);

            SoundPlaybackUtility.Stop(spinLoopHandle);
            LogVisualSwayEnd(visualRoot, visualBaseLocalPosition);
        }
    }

    private void ApplyVisualSway(
        Transform visualRoot,
        Vector3 visualBaseLocalPosition,
        Transform shadowRoot,
        Vector3 shadowBaseLocalPosition,
        float elapsed)
    {
        if (visualSwayAmplitude <= 0f || visualSwayFrequency <= 0f)
            return;

        float offset = Mathf.Sin(elapsed * Mathf.PI * 2f * visualSwayFrequency) * visualSwayAmplitude;
        Vector3 offsetVector = Vector3.right * offset;

        if (visualRoot != null)
            visualRoot.localPosition = visualBaseLocalPosition + offsetVector;

        if (shadowRoot != null)
            shadowRoot.localPosition = shadowBaseLocalPosition + offsetVector;

        LogVisualSwayTick(visualRoot, elapsed, offset);
    }

    private void LogVisualSwayStart(
        DragonController dragon,
        Transform visualRoot,
        Vector3 baseLocalPosition)
    {
        if (!debugVisualSway)
            return;

        string targetName = visualRoot != null ? visualRoot.name : "null";
        bool isRootFallback = dragon != null && visualRoot == dragon.transform;
        Debug.Log(
            $"[DragonRotation] Visual sway start. target={targetName}, rootFallback={isRootFallback}, baseLocal={baseLocalPosition}, amplitude={visualSwayAmplitude}, frequency={visualSwayFrequency}",
            dragon);
    }

    private void LogVisualSwayTick(Transform visualRoot, float elapsed, float offset)
    {
        if (!debugVisualSway || visualRoot == null)
            return;

        int frame = Time.frameCount;
        if (frame % 15 != 0)
            return;

        Debug.Log(
            $"[DragonRotation] Visual sway tick. elapsed={elapsed:F2}, offset={offset:F3}, local={visualRoot.localPosition}",
            visualRoot);
    }

    private void LogVisualSwayEnd(Transform visualRoot, Vector3 baseLocalPosition)
    {
        if (!debugVisualSway)
            return;

        Debug.Log(
            $"[DragonRotation] Visual sway end. restoredLocal={baseLocalPosition}",
            visualRoot);
    }

    private void ShowWarningTelegraph(IAttackTelegraphPresenter telegraphService, Vector2 center)
    {
        if (telegraphService == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            center,
            hitDiameter,
            warningSeconds,
            warningTelegraphStyle);

        spec = AttackTelegraphSpecUtility.WithThinWarningOutline(spec);
        telegraphService.Show(spec);
    }

    private void ApplyAreaHit(DragonController dragon)
    {
        if (dragon == null || damageEffect == null || damageAmount <= 0f)
            return;

        float radius = Mathf.Max(0.05f, hitDiameter * 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(dragon.transform.position, radius, ResolveTargetMask(dragon));
        CombatHitPayload payload = MakeAreaHitPayload(dragon);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == dragon.gameObject)
                continue;

            if (!CanDamageTargetNow(targetRoot))
                continue;

            Vector3 hitPoint = hits[i].ClosestPoint(dragon.transform.position);
            if (CombatHitPayloadApplier.Apply(targetRoot, payload, hitPoint))
                nextDamageAllowedTimes[targetRoot] = Time.time + damageIntervalSeconds;
        }
    }

    private bool CanDamageTargetNow(GameObject targetRoot)
    {
        if (targetRoot == null)
            return false;

        return !nextDamageAllowedTimes.TryGetValue(targetRoot, out float nextAllowedTime) ||
               Time.time >= nextAllowedTime;
    }

    private int ResolveProjectileCount()
    {
        int baseCount = Mathf.CeilToInt(Mathf.Max(0f, spinSeconds) / Mathf.Max(0.01f, projectileSpawnIntervalSeconds));
        return baseCount * Mathf.Max(1, projectileCountMultiplier);
    }

    private void SpawnDueProjectiles(
        DragonController dragon,
        List<GameObject> spawnedProjectiles,
        float elapsed,
        int projectileCount,
        ref int nextProjectileIndex)
    {
        while (nextProjectileIndex < projectileCount &&
               elapsed >= nextProjectileIndex * (spinSeconds / projectileCount))
        {
            SpawnProjectile(dragon, spawnedProjectiles, nextProjectileIndex);
            nextProjectileIndex++;
        }
    }

    private void SpawnProjectile(DragonController dragon, List<GameObject> spawnedProjectiles, int projectileIndex)
    {
        if (dragon == null || projectilePrefab == null || projectileDamageEffect == null)
            return;

        Vector3 center = dragon.transform.position;
        Vector2? targetPosition = (projectileIndex + 1) % 3 == 0 && dragon.CurrentTarget != null
            ? CommonMonsterCombatUtility.ResolveAimPoint(dragon.CurrentTarget.gameObject, CombatAimPointKind.ProjectileTarget)
            : null;
        Vector2 direction = ResolveProjectileDirection(center, targetPosition);
        float spawnRadius = Mathf.Max(0f, projectileSpawnRadius);
        if (targetPosition.HasValue)
        {
            // Do not spawn beyond a player standing inside the authored spawn radius.
            spawnRadius = Mathf.Min(spawnRadius, Vector2.Distance(center, targetPosition.Value) * 0.5f);
        }

        Vector3 origin = center + (Vector3)(direction * spawnRadius);
        GameObject projectileObject = Object.Instantiate(
            projectilePrefab,
            origin,
            ResolveProjectileRotation(direction));
        DragonSpinProjectile2D projectile = projectileObject.GetComponent<DragonSpinProjectile2D>();

        if (projectile == null)
        {
            Object.Destroy(projectileObject);
            return;
        }

        spawnedProjectiles?.Add(projectileObject);

        ProjectileAttackSpawnContext context = new()
        {
            ownerSystem = dragon.AbilitySystem,
            sourceSpec = null,
            causer = dragon.gameObject,
            ignoreTarget = dragon.gameObject,
            lifetime = projectileLifetimeSeconds,
            wallLayers = projectileWallLayers,
            damageLayers = ResolveTargetMask(dragon),
            hitPayload = MakeProjectileHitPayload(dragon),
            direction = direction,
            speed = projectileSpeed,
        };

        projectile.Setup(context);
        SoundPlaybackUtility.Play(
            projectileFireSound,
            instigator: dragon.gameObject,
            causer: dragon.gameObject,
            position: origin,
            sourceObject: this);
    }

    private static Vector2 ResolveProjectileDirection(Vector2 origin, Vector2? targetPosition)
    {
        Vector2 direction = targetPosition.HasValue ? targetPosition.Value - origin : Vector2.zero;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Random.insideUnitCircle;

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    /// <summary>
    /// 책임:
    /// 회전 패턴 취소 시 패턴이 직접 만든 탄막을 제거해 groggy/death 이후 잔여 공격 판정을 남기지 않는다.
    /// </summary>
    private static void DestroySpawnedProjectiles(List<GameObject> spawnedProjectiles)
    {
        if (spawnedProjectiles == null)
            return;

        for (int i = 0; i < spawnedProjectiles.Count; i++)
        {
            if (spawnedProjectiles[i] != null)
                Object.Destroy(spawnedProjectiles[i]);
        }

        spawnedProjectiles.Clear();
    }

    private Quaternion ResolveProjectileRotation(Vector2 direction)
    {
        Vector2 resolvedDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;

        float angleDegrees = Mathf.Atan2(resolvedDirection.y, resolvedDirection.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angleDegrees + projectileRotationOffsetDegrees);
    }

    private LayerMask ResolveTargetMask(DragonController dragon)
    {
        Transform target = dragon != null ? dragon.CurrentTarget : null;
        return target != null ? (LayerMask)(1 << target.gameObject.layer) : Physics2D.DefaultRaycastLayers;
    }

    private CombatHitPayload MakeAreaHitPayload(DragonController dragon)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: dragon.AbilitySystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);
    }

    private CombatHitPayload MakeProjectileHitPayload(DragonController dragon)
    {
        CombatDamageSnapshot snapshot = new(
            finalHpDamage: projectileDamageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: dragon.AbilitySystem,
            sourceSpec: null,
            damageEffect: projectileDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: dragon.gameObject);
    }
}
