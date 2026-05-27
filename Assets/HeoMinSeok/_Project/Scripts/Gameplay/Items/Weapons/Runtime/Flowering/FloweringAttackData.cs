using CapstoneAudio;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "ALData_FloweringAttack", menuName = "GAS/Weapon/Flowering/Attack Data")]
public sealed class FloweringAttackData : ScriptableObject
{
    [Header("Hitbox")]
    [SerializeField] private MeleeHitboxActor hitboxPrefab;
    [SerializeField] private MeleeHitboxActor[] randomHitboxPrefabs = new MeleeHitboxActor[0];
    [SerializeField, Min(0.01f)] private float activeTime = 0.08f;
    [SerializeField] private Vector2 hitboxSize = new(1.4f, 0.8f);
    [SerializeField] private Vector2 hitboxScaleMultiplier = Vector2.one;
    [SerializeField] private bool overrideSizingMode = true;
    [SerializeField] private MeleeHitboxSizingMode sizingMode = MeleeHitboxSizingMode.LegacyContextSizeWithAuthoredScale;
    [SerializeField] private float forwardOffset = 0.9f;
    [SerializeField] private float sideOffset;
    [SerializeField] private int sideSign = 1;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private LayerMask wallLayers;

    [Header("Timing")]
    [SerializeField, Min(0.02f)] private float recoveryDuration = 0.16f;
    [SerializeField, Min(0f)] private float nextAttackDelay = 0.16f;
    [SerializeField] private string animationTrigger;
    [SerializeField] private string[] animationTriggersByHitboxVariant = new string[0];

    [Header("Bloom Attack Particle")]
    [SerializeField] private GameObject bloomSlashParticlePrefab;
    [SerializeField, Min(0.01f)] private float bloomSlashParticleLifetimeFallback = 1f;

    [Header("Bloom Attack Sound")]
    [SerializeField] private SoundRef bloomSlashSound;
    [SerializeField] private SoundRef[] bloomSlashSoundsByHitboxVariant = new SoundRef[0];

    [Header("Damage")]
    [SerializeField] private UnityGAS.DamagePayloadConfig damageConfig = new();
    [SerializeField] private GameplayEffect damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private ScaledStatFormula damageFormula;
    [SerializeField] private ScaledStatFormula knockbackFormula;
    [SerializeField] private float legacyDamage = 5f;
    [SerializeField] private float legacyStaggerDamage;
    [SerializeField] private GameplayTag hitConfirmedTag;
    [SerializeField] private HitImpactCueKind hitImpactCueKind = HitImpactCueKind.Slash;

    public MeleeHitboxActor HitboxPrefab => hitboxPrefab;
    public bool HasAnyHitboxPrefab => GetHitboxVariantCount() > 0;
    public float ActiveTime => Mathf.Max(0.01f, activeTime);
    public Vector2 HitboxSize => new(Mathf.Max(0.01f, hitboxSize.x), Mathf.Max(0.01f, hitboxSize.y));
    public Vector2 HitboxScaleMultiplier => new(
        Mathf.Abs(hitboxScaleMultiplier.x) > 0.0001f ? Mathf.Abs(hitboxScaleMultiplier.x) : 1f,
        Mathf.Abs(hitboxScaleMultiplier.y) > 0.0001f ? Mathf.Abs(hitboxScaleMultiplier.y) : 1f);
    public bool OverrideSizingMode => overrideSizingMode;
    public MeleeHitboxSizingMode SizingMode => sizingMode;
    public float ForwardOffset => forwardOffset;
    public float SideOffset => sideOffset;
    public int SideSign => sideSign < 0 ? -1 : 1;
    public LayerMask HitLayers => hitLayers;
    public LayerMask WallLayers => wallLayers;
    public float RecoveryDuration => Mathf.Max(0.02f, recoveryDuration);
    public float NextAttackDelay => nextAttackDelay > 0f ? nextAttackDelay : RecoveryDuration;
    public string AnimationTrigger => animationTrigger;
    public GameObject BloomSlashParticlePrefab => bloomSlashParticlePrefab;
    public float BloomSlashParticleLifetimeFallback => Mathf.Max(0.01f, bloomSlashParticleLifetimeFallback);
    public SoundRef BloomSlashSound => bloomSlashSound;
    public UnityGAS.DamagePayloadConfig DamageConfig => damageConfig;
    public GameplayEffect DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public ScaledStatFormula DamageFormula => damageFormula;
    public ScaledStatFormula KnockbackFormula => knockbackFormula;
    public float LegacyDamage => legacyDamage;
    public float LegacyStaggerDamage => legacyStaggerDamage;
    public GameplayTag HitConfirmedTag => hitConfirmedTag;
    public HitImpactCueKind HitImpactCueKind => hitImpactCueKind;

    public MeleeHitboxActor GetRandomHitboxPrefab(int previousVariantIndex, out int selectedVariantIndex)
    {
        selectedVariantIndex = -1;
        int variantCount = GetHitboxVariantCount();
        if (variantCount <= 0)
            return null;

        int chosenIndex = variantCount == 1 ? 0 : Random.Range(0, variantCount);
        if (variantCount > 1 && chosenIndex == previousVariantIndex)
            chosenIndex = (chosenIndex + 1) % variantCount;

        selectedVariantIndex = chosenIndex;
        return GetHitboxByVariantIndex(chosenIndex);
    }

    public string GetAnimationTriggerForVariant(int variantIndex)
    {
        if (variantIndex >= 0
            && animationTriggersByHitboxVariant != null
            && variantIndex < animationTriggersByHitboxVariant.Length
            && !string.IsNullOrWhiteSpace(animationTriggersByHitboxVariant[variantIndex]))
        {
            return animationTriggersByHitboxVariant[variantIndex];
        }

        return animationTrigger;
    }

    public SoundRef GetBloomSlashSoundForVariant(int variantIndex)
    {
        if (variantIndex >= 0
            && bloomSlashSoundsByHitboxVariant != null
            && variantIndex < bloomSlashSoundsByHitboxVariant.Length
            && bloomSlashSoundsByHitboxVariant[variantIndex].IsSet)
        {
            return bloomSlashSoundsByHitboxVariant[variantIndex];
        }

        return bloomSlashSound;
    }

    private int GetHitboxVariantCount()
    {
        int count = hitboxPrefab != null ? 1 : 0;
        if (randomHitboxPrefabs == null)
            return count;

        for (int i = 0; i < randomHitboxPrefabs.Length; i++)
        {
            if (randomHitboxPrefabs[i] != null)
                count++;
        }

        return count;
    }

    private MeleeHitboxActor GetHitboxByVariantIndex(int variantIndex)
    {
        int currentIndex = 0;
        if (hitboxPrefab != null)
        {
            if (variantIndex == currentIndex)
                return hitboxPrefab;

            currentIndex++;
        }

        if (randomHitboxPrefabs == null)
            return null;

        for (int i = 0; i < randomHitboxPrefabs.Length; i++)
        {
            MeleeHitboxActor prefab = randomHitboxPrefabs[i];
            if (prefab == null)
                continue;

            if (variantIndex == currentIndex)
                return prefab;

            currentIndex++;
        }

        return null;
    }
}
