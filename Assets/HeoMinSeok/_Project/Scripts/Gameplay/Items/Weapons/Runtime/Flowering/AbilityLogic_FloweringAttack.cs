using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_FloweringAttack", menuName = "GAS/Weapon/Flowering/Logic Attack")]
public sealed class AbilityLogic_FloweringAttack : AbilityLogic
{
    private const string LastBloomHitboxVariantKey = "Flowering.Bloom.LastHitboxVariant";

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null)
            yield break;

        if (spec.Definition.sourceObject is FloweringBaseAttackData)
        {
            yield return AbilityLogic_FloweringBaseAttack.ActivateBaseAttack(system, spec, initialTarget, this);
            yield break;
        }

        FloweringAttackData data = spec.Definition.sourceObject as FloweringAttackData;
        if (data == null)
        {
            Debug.LogError("[FloweringAttack] AbilityDefinition.sourceObject must be FloweringAttackData or FloweringBaseAttackData.");
            yield break;
        }

        if (!data.HasAnyHitboxPrefab || data.DamageEffect == null || system.AttributeSet == null)
            yield break;

        float finalAttackSpeed = AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system);
        float safeAttackSpeed = finalAttackSpeed > 0.0001f ? finalAttackSpeed : 1f;
        float activationDelay = Mathf.Max(0.02f, data.NextAttackDelay / safeAttackSpeed);

        spec.SetFloat("RecoveryOverride", activationDelay);
        system.SetNextActivationDelay(spec, activationDelay);

        ApplyWeaponVisualSideSign(system, data.SideSign);

        Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        if (dir.sqrMagnitude <= 0.0001f)
            dir = Vector2.right;
        dir.Normalize();

        if (IsCancelled(spec))
            yield break;

        int previousVariantIndex = spec.GetInt(LastBloomHitboxVariantKey, -1);
        MeleeHitboxActor hitboxPrefab = data.GetRandomHitboxPrefab(previousVariantIndex, out int selectedVariantIndex);
        if (hitboxPrefab == null)
            yield break;

        spec.SetInt(LastBloomHitboxVariantKey, selectedVariantIndex);

        TryPlayAnim(system, data.GetAnimationTriggerForVariant(selectedVariantIndex), spec.Definition);

        CombatHitPayload payload = BuildPayload(system, spec, data, 1f);
        if (payload == null)
            yield break;

        Vector2 center = ResolveHitboxCenter(system, data, dir);
        SpawnHitbox(system, spec, data, payload, hitboxPrefab, center, dir);
        SpawnBloomSlashParticle(data, center, dir);
        AbilityAudioRouter.PlayOneShotAtPosition(
            data.GetBloomSlashSoundForVariant(selectedVariantIndex),
            system,
            spec,
            center,
            data);
    }

    private static Vector2 ResolveHitboxCenter(AbilitySystem system, FloweringAttackData data, Vector2 dir)
    {
        Vector2 perp = new(-dir.y, dir.x);
        return (Vector2)system.transform.position
               + dir * data.ForwardOffset
               + perp * (data.SideOffset * data.SideSign);
    }

    private static void SpawnHitbox(
        AbilitySystem system,
        AbilitySpec spec,
        FloweringAttackData data,
        CombatHitPayload payload,
        MeleeHitboxActor hitboxPrefab,
        Vector2 center,
        Vector2 dir)
    {
        MeleeHitboxActor hitbox = Object.Instantiate(hitboxPrefab, center, Quaternion.identity);
        if (hitbox == null)
            return;

        hitbox.Setup(new MeleeHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = data.ActiveTime,
            wallLayers = data.WallLayers,
            damageLayers = data.HitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = data.HitboxSize,
            hitboxScaleMultiplier = data.HitboxScaleMultiplier,
            overrideSizingMode = data.OverrideSizingMode,
            sizingMode = data.SizingMode,
            hitOncePerTarget = true,
            destroyOnFirstHit = false,
            direction = dir,
            flipVisualX = dir.x < 0f,
            visualMirrorMode = MeleeHitboxVisualMirrorMode.FlipLocalYWhenRequested,
            overrideAttachToOwnerOnSetup = true,
            attachToOwnerOnSetup = false
        });
    }

    private static void SpawnBloomSlashParticle(
        FloweringAttackData data,
        Vector2 center,
        Vector2 direction)
    {
        if (data == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg);
        SpawnParticle(data.BloomSlashParticlePrefab, center, rotation, data.BloomSlashParticleLifetimeFallback);
    }

    private static void SpawnParticle(GameObject particlePrefab, Vector2 center, Quaternion rotation, float fallbackLifetime)
    {
        if (particlePrefab == null)
            return;

        GameObject particle = Object.Instantiate(particlePrefab, center, rotation);
        if (particle != null)
            Object.Destroy(particle, Mathf.Max(0.01f, fallbackLifetime));
    }

    private static void TryPlayAnim(AbilitySystem system, string animationTrigger, AbilityDefinition definition)
    {
        if (system == null || string.IsNullOrWhiteSpace(animationTrigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(animationTrigger), definition);
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec?.Token != null && spec.Token.IsCancelled;
    }

    internal static CombatHitPayload BuildPayload(
        AbilitySystem system,
        AbilitySpec spec,
        FloweringAttackData data,
        float damageScale)
    {
        if (system == null || system.AttributeSet == null || data == null || data.DamageEffect == null)
            return null;

        IStatProvider statProvider = AbilityStatProviderFactory.Create(system);
        float safeScale = Mathf.Max(0f, damageScale);

        float baseHp = data.DamageFormula != null
            ? data.DamageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: data.LegacyDamage)
            : data.LegacyDamage;
        baseHp *= safeScale;

        float baseKnockback = data.KnockbackFormula != null
            ? data.KnockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
            : 0f;
        baseKnockback *= safeScale;

        UnityGAS.DamagePayloadConfig config = data.DamageConfig;
        float baseStagger = config != null && config.includeStaggerBuildUp && config.staggerFormula != null
            ? config.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
            : data.LegacyStaggerDamage;
        baseStagger *= safeScale;

        CombatDamageSnapshot snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
            statProvider,
            config,
            baseHp,
            config != null && config.includeStaggerBuildUp ? baseStagger : 0f,
            baseKnockback,
            system.gameObject);

        if (snapshot.FinalHpDamage <= 0f)
            return null;

        return CombatHitPayload.FromSnapshot(
            system,
            spec,
            data.DamageEffect,
            data.KnockbackEffect,
            snapshot,
            data.HitConfirmedTag,
            system.gameObject,
            data.HitImpactCueKind);
    }

    private static void ApplyWeaponVisualSideSign(AbilitySystem system, int sideSign)
    {
        WeaponEquipController equipController = system != null
            ? system.GetComponentInChildren<WeaponEquipController>()
            : null;
        equipController?.SetAttackVisualSideSign(sideSign);
    }
}
