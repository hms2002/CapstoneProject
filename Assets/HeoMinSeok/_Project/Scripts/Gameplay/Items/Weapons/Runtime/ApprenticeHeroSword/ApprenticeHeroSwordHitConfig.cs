using System;
using UnityEngine;
using UnityGAS;

[Serializable]
public sealed class ApprenticeHeroSwordHitboxConfig
{
    [SerializeField] private MeleeHitboxActor hitboxPrefab;
    [SerializeField, Min(0.01f)] private float activeTime = 0.12f;
    [SerializeField] private Vector2 hitboxSize = new(1.6f, 0.9f);
    [SerializeField] private Vector2 hitboxScaleMultiplier = Vector2.one;
    [SerializeField] private bool overrideSizingMode = true;
    [SerializeField] private MeleeHitboxSizingMode sizingMode = MeleeHitboxSizingMode.OverrideColliderWorldSizeKeepVisualScale;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private bool hitOncePerTarget = true;
    [SerializeField] private bool destroyOnFirstHit;
    [SerializeField] private bool attachToOwnerOnSetup = true;
    [SerializeField] private MeleeHitboxVisualMirrorMode visualMirrorMode = MeleeHitboxVisualMirrorMode.PreserveWorldUpWhenFacingLeft;

    public MeleeHitboxActor HitboxPrefab => hitboxPrefab;
    public float ActiveTime => Mathf.Max(0.01f, activeTime);
    public Vector2 HitboxSize => new(Mathf.Max(0.01f, hitboxSize.x), Mathf.Max(0.01f, hitboxSize.y));
    public Vector2 HitboxScaleMultiplier => new(
        Mathf.Abs(hitboxScaleMultiplier.x) > 0.0001f ? Mathf.Abs(hitboxScaleMultiplier.x) : 1f,
        Mathf.Abs(hitboxScaleMultiplier.y) > 0.0001f ? Mathf.Abs(hitboxScaleMultiplier.y) : 1f);
    public bool OverrideSizingMode => overrideSizingMode;
    public MeleeHitboxSizingMode SizingMode => sizingMode;
    public LayerMask WallLayers => wallLayers;
    public bool HitOncePerTarget => hitOncePerTarget;
    public bool DestroyOnFirstHit => destroyOnFirstHit;
    public bool AttachToOwnerOnSetup => attachToOwnerOnSetup;
    public MeleeHitboxVisualMirrorMode VisualMirrorMode => visualMirrorMode;
}

[Serializable]
public sealed class ApprenticeHeroSwordDamageConfig
{
    [SerializeField] private UnityGAS.DamagePayloadConfig damageConfig = new();
    [SerializeField] private GameplayEffect damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private ScaledStatFormula damageFormula;
    [SerializeField] private ScaledStatFormula knockbackFormula;
    [SerializeField] private float legacyDamage = 5f;
    [SerializeField] private float legacyStaggerDamage;
    [SerializeField] private GameplayTag hitConfirmedTag;
    [SerializeField] private HitImpactCueKind hitImpactCueKind = HitImpactCueKind.Slash;

    public UnityGAS.DamagePayloadConfig DamageConfig => damageConfig;
    public GameplayEffect DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public ScaledStatFormula DamageFormula => damageFormula;
    public ScaledStatFormula KnockbackFormula => knockbackFormula;
    public float LegacyDamage => legacyDamage;
    public float LegacyStaggerDamage => legacyStaggerDamage;
    public GameplayTag HitConfirmedTag => hitConfirmedTag;
    public HitImpactCueKind HitImpactCueKind => hitImpactCueKind;
}

internal static class ApprenticeHeroSwordHitUtility
{
    public static CombatHitPayload BuildPayload(
        AbilitySystem system,
        AbilitySpec spec,
        ApprenticeHeroSwordDamageConfig data,
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

    public static MeleeHitboxActor SpawnHitbox(
        AbilitySystem system,
        AbilitySpec spec,
        ApprenticeHeroSwordHitboxConfig hitboxConfig,
        LayerMask hitLayers,
        CombatHitPayload payload,
        Vector2 center,
        Vector2 direction,
        bool flipVisualX,
        System.Collections.Generic.HashSet<int> sharedHitTargetIds = null)
    {
        if (system == null || hitboxConfig == null || hitboxConfig.HitboxPrefab == null || payload == null)
            return null;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        MeleeHitboxActor hitbox = UnityEngine.Object.Instantiate(
            hitboxConfig.HitboxPrefab,
            center,
            Quaternion.identity);

        if (hitbox == null)
            return null;

        hitbox.Setup(new MeleeHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = hitboxConfig.ActiveTime,
            wallLayers = hitboxConfig.WallLayers,
            damageLayers = hitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = hitboxConfig.HitboxSize,
            hitboxScaleMultiplier = hitboxConfig.HitboxScaleMultiplier,
            overrideSizingMode = hitboxConfig.OverrideSizingMode,
            sizingMode = hitboxConfig.SizingMode,
            hitOncePerTarget = hitboxConfig.HitOncePerTarget,
            destroyOnFirstHit = hitboxConfig.DestroyOnFirstHit,
            direction = safeDirection,
            flipVisualX = flipVisualX,
            visualMirrorMode = hitboxConfig.VisualMirrorMode,
            overrideAttachToOwnerOnSetup = true,
            attachToOwnerOnSetup = hitboxConfig.AttachToOwnerOnSetup,
            sharedHitTargetIds = sharedHitTargetIds
        });

        return hitbox;
    }
}
