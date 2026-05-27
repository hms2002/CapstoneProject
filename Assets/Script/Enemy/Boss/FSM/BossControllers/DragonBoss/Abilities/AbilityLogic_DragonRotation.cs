using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 회전 패턴을 실행하며, 원형 경고, 회전 중 범위 피해/넉백, 랜덤 탄막 발사를 처리한다.
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
    [SerializeField, Min(0f)] private float projectileSpawnRadius = 0.65f;
    [SerializeField] private float projectileRotationOffsetDegrees;
    [SerializeField] private LayerMask projectileWallLayers;

    [Header("Telegraph")]
    [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
        if (dragon == null)
            yield break;

        AttackTelegraphService telegraphService = dragon.GetComponent<AttackTelegraphService>();
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
        float nextProjectileTime = 0f;
        List<GameObject> spawnedProjectiles = new();
        Transform visualRoot = dragon != null ? dragon.PatternMotionRoot : null;
        Transform shadowRoot = dragon != null ? dragon.PatternShadowMotionRoot : null;
        Vector3 visualBaseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        Vector3 shadowBaseLocalPosition = shadowRoot != null ? shadowRoot.localPosition : Vector3.zero;
        LogVisualSwayStart(dragon, visualRoot, visualBaseLocalPosition);

        try
        {
            while (elapsed < spinSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                ApplyVisualSway(visualRoot, visualBaseLocalPosition, shadowRoot, shadowBaseLocalPosition, elapsed);
                ApplyAreaHit(dragon);

                if (elapsed >= nextProjectileTime)
                {
                    SpawnRandomProjectile(dragon, spawnedProjectiles);
                    nextProjectileTime += projectileSpawnIntervalSeconds;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!IsAbilityCancelled(spec))
                ApplyAreaHit(dragon);
        }
        finally
        {
            if (visualRoot != null)
                visualRoot.localPosition = visualBaseLocalPosition;

            if (shadowRoot != null)
                shadowRoot.localPosition = shadowBaseLocalPosition;

            if (IsAbilityCancelled(spec))
                DestroySpawnedProjectiles(spawnedProjectiles);

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

    private void ShowWarningTelegraph(AttackTelegraphService telegraphService, Vector2 center)
    {
        if (telegraphService == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            center,
            hitDiameter,
            warningSeconds,
            warningTelegraphStyle);

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
            CombatHitPayloadApplier.Apply(targetRoot, payload, hitPoint);
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

    private void SpawnRandomProjectile(DragonController dragon, List<GameObject> spawnedProjectiles)
    {
        if (dragon == null || projectilePrefab == null || projectileDamageEffect == null)
            return;

        Vector2 direction = Random.insideUnitCircle;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        direction.Normalize();

        Vector3 origin = dragon.transform.position + (Vector3)(direction * projectileSpawnRadius);
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
