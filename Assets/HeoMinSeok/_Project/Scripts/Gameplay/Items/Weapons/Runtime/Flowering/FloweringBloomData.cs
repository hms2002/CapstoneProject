using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "ALData_FloweringBloom", menuName = "GAS/Weapon/Flowering/Bloom Data")]
public sealed class FloweringBloomData : ScriptableObject
{
    [Header("Bloom")]
    [SerializeField, Min(0.1f)] private float durationSeconds = 15f;
    [SerializeField, Min(0f)] private float killExtensionSeconds = 1f;
    [SerializeField] private AbilityDefinition dashAbility;

    [Header("Attributes")]
    [SerializeField] private AttributeDefinition normalDamageAddAttribute;
    [SerializeField] private AttributeDefinition attackSpeedBaseAttribute;
    [SerializeField] private AttributeDefinition moveSpeedMulAttribute;
    [SerializeField] private float normalDamageAdd = 25f;
    [SerializeField] private float attackSpeedBaseAdd = 2.5f;
    [SerializeField] private float moveSpeedPercent = 0.6f;

    [Header("Cut-in")]
    [SerializeField] private Color bloomColor = new(0.933f, 0.251f, 0.286f, 1f);

    [Header("Cut-in Animation")]
    [SerializeField] private string cutInAnimationTrigger;
    [SerializeField] private WeaponAimPresentationSettings cutInAimPresentation = new();
    [SerializeField] private GameplayTag weaponRevealEventTag;
    [SerializeField, Min(0f)] private float weaponRevealEventTimeout = 1f;
    [SerializeField, Min(0f)] private float weaponRevealFallbackDelay;

    [SerializeField] private Material screenBorderMaterial;
    [SerializeField, Range(0f, 1f)] private float dimTargetAlpha = 0.28f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.18f;
    [SerializeField, Min(0f)] private float holdSeconds = 0.22f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.16f;
    [SerializeField, Min(0f)] private float screenBorderRevealSeconds = 0.35f;
    [SerializeField, Min(0f)] private float screenBorderThicknessRatio = 0.22f;
    [SerializeField, Min(0f)] private float openingShakeAmplitude = 0.55f;
    [SerializeField, Min(0f)] private float finalShakeAmplitude = 1.25f;
    [SerializeField, Min(0f)] private float zoomShakeAmplitude = 0.22f;
    [SerializeField, Min(0.01f)] private float zoomShakeIntervalSeconds = 0.055f;

    [Header("Cut-in World Dim")]
    [SerializeField] private string worldDimSortingLayerName = "Entity";
    [SerializeField] private int worldDimSortingOrder = -1;
    [SerializeField, Min(1f)] private float worldDimCameraPadding = 1.2f;
    [SerializeField] private float worldDimZ = -0.05f;

    [Header("Cut-in Camera Zoom")]
    [SerializeField, Range(0.1f, 1f)] private float cutInZoomScale = 0.65f;
    [SerializeField, Min(0f)] private float zoomInSeconds = 0.3f;
    [SerializeField, Min(0f)] private float zoomOutSeconds = 0.3f;

    [Header("Cut-in Eye Flash")]
    [SerializeField] private Sprite[] eyeFlashFrames;
    [SerializeField, Min(1f)] private float eyeFlashFps = 18f;
    [SerializeField] private Vector2 eyeFlashLocalOffset = new(0f, 0.42f);
    [SerializeField, Min(0.01f)] private float eyeFlashScale = 1f;
    [SerializeField] private string eyeFlashSortingLayerName = "Entity";
    [SerializeField] private int eyeFlashSortingOrder = 20;
    [SerializeField] private Color eyeFlashPlayerTint = Color.black;

    [Header("Cut-in Player Silhouette")]
    [SerializeField] private Sprite playerCutInBaseSprite;
    [SerializeField] private Vector2 playerCutInLocalOffset;
    [SerializeField, Min(0.01f)] private float playerCutInScale = 1f;
    [SerializeField, Range(0f, 1f)] private float playerTintInStartRatio = 0.15f;
    [SerializeField, Range(0f, 1f)] private float playerTintInEndRatio = 0.75f;
    [SerializeField, Range(0f, 1f)] private float playerTintOutStartRatio = 0f;
    [SerializeField, Range(0f, 1f)] private float playerTintOutEndRatio = 1f;

    [Header("Weapon Visual State")]
    [SerializeField] private Sprite weaponInactiveSprite;
    [SerializeField] private Sprite weaponBloomSprite;
    [SerializeField] private Material weaponRevealMaterial;
    [SerializeField, Min(0f)] private float weaponRevealSeconds = 0.22f;
    [SerializeField, Min(0f)] private float weaponRevealOutSeconds = 0.22f;
    [SerializeField] private Vector2 weaponRevealUvDirection = new(-1f, 1f);
    [SerializeField, Range(0f, 0.25f)] private float weaponRevealFeather = 0.08f;
    [SerializeField] private Vector2 weaponRevealMaskLocalDirection = new(-1f, 1f);
    [SerializeField, Range(0f, 1f)] private float weaponRevealMaskAlphaCutoff = 0.5f;
    [SerializeField, Min(0f)] private float weaponRevealMaskPadding = 0.1f;
    [SerializeField, Min(0.1f)] private float weaponRevealMaskWidthMultiplier = 1.25f;

    [Header("Bloom HUD")]
    [SerializeField] private StatusHudDefinition bloomStatusDefinition;
    [SerializeField] private Sprite bloomStatusIcon;
    [SerializeField] private GameplayTag killExtensionRequiredTag;

    [Header("Player Outline")]
    [SerializeField, Min(0.1f)] private float outlinePixels = 1.25f;
    [SerializeField, Min(0f)] private float outlineTopWavePixels = 1.2f;
    [SerializeField, Min(0f)] private float outlineWaveSpeed = 16f;

    [Header("Dash Boost")]
    [SerializeField, Min(0.01f)] private float dashDistanceMultiplier = 1.45f;
    [SerializeField, Min(0.01f)] private float dashDurationMultiplier = 0.45f;

    [Header("Dash Slash")]
    [SerializeField, Min(1)] private int dashSlashCount = 3;
    [SerializeField, Min(0f)] private float dashSlashInitialDelaySeconds = 0.02f;
    [SerializeField, Min(0f)] private float dashSlashIntervalSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float dashSlashActiveTime = 0.08f;
    [SerializeField] private Vector2 dashSlashHitboxSize = new(2.4f, 0.6f);
    [SerializeField, Min(0f)] private float dashSlashAngleJitter = 35f;
    [SerializeField, Min(0f)] private float dashSlashDamageScale = 1f;
    [SerializeField] private LayerMask dashSlashHitLayers;
    [SerializeField] private LayerMask dashSlashWallLayers;
    [SerializeField] private UnityGAS.DamagePayloadConfig dashSlashDamageConfig = new();
    [SerializeField] private GameplayEffect dashSlashDamageEffect;
    [SerializeField] private GE_Knockback_Spec dashSlashKnockbackEffect;
    [SerializeField] private ScaledStatFormula dashSlashDamageFormula;
    [SerializeField] private ScaledStatFormula dashSlashKnockbackFormula;
    [SerializeField] private float dashSlashLegacyDamage = 5f;
    [SerializeField] private float dashSlashLegacyStaggerDamage;
    [SerializeField] private GameplayTag dashSlashHitConfirmedTag;
    [SerializeField] private HitImpactCueKind dashSlashHitImpactCueKind = HitImpactCueKind.Slash;

    [Header("Dash Slash Effect")]
    [SerializeField] private GameObject slashEffectPrefab;
    [SerializeField, Min(0.01f)] private float slashEffectLifetime = 0.35f;
    [SerializeField, Min(0.01f)] private float slashEffectScale = 1.35f;
    [SerializeField] private int slashEffectSortingOrderOffset = 40;
    [SerializeField] private GameObject dashSlashParticlePrefab;
    [SerializeField] private GameObject weaponRevealParticlePrefab;
    [SerializeField] private GameObject finalShakeParticlePrefab;
    [SerializeField, Min(0.01f)] private float particleLifetimeFallback = 1f;
    [SerializeField] private int particleSortingOrderOffset = 40;

    public float DurationSeconds => Mathf.Max(0.1f, durationSeconds);
    public float KillExtensionSeconds => Mathf.Max(0f, killExtensionSeconds);
    public AbilityDefinition DashAbility => dashAbility;
    public AttributeDefinition NormalDamageAddAttribute => normalDamageAddAttribute;
    public AttributeDefinition AttackSpeedBaseAttribute => attackSpeedBaseAttribute;
    public AttributeDefinition MoveSpeedMulAttribute => moveSpeedMulAttribute;
    public float NormalDamageAdd => normalDamageAdd;
    public float AttackSpeedBaseAdd => attackSpeedBaseAdd;
    public float MoveSpeedPercent => moveSpeedPercent;
    public Color BloomColor => bloomColor;
    public string CutInAnimationTrigger => cutInAnimationTrigger;
    public WeaponAimPresentationSettings CutInAimPresentation => cutInAimPresentation;
    public GameplayTag WeaponRevealEventTag => weaponRevealEventTag;
    public float WeaponRevealEventTimeout => Mathf.Max(0f, weaponRevealEventTimeout);
    public float WeaponRevealFallbackDelay => Mathf.Max(0f, weaponRevealFallbackDelay);
    public Material ScreenBorderMaterial => screenBorderMaterial;
    public float DimTargetAlpha => Mathf.Clamp01(dimTargetAlpha);
    public float FadeInSeconds => Mathf.Max(0f, fadeInSeconds);
    public float HoldSeconds => Mathf.Max(0f, holdSeconds);
    public float FadeOutSeconds => Mathf.Max(0f, fadeOutSeconds);
    public float ScreenBorderRevealSeconds => Mathf.Max(0f, screenBorderRevealSeconds);
    public float ScreenBorderThicknessRatio => Mathf.Max(0f, screenBorderThicknessRatio);
    public float OpeningShakeAmplitude => Mathf.Max(0f, openingShakeAmplitude);
    public float FinalShakeAmplitude => Mathf.Max(0f, finalShakeAmplitude);
    public float ZoomShakeAmplitude => Mathf.Max(0f, zoomShakeAmplitude);
    public float ZoomShakeIntervalSeconds => Mathf.Max(0.01f, zoomShakeIntervalSeconds);
    public string WorldDimSortingLayerName => string.IsNullOrWhiteSpace(worldDimSortingLayerName) ? "Entity" : worldDimSortingLayerName;
    public int WorldDimSortingOrder => worldDimSortingOrder;
    public float WorldDimCameraPadding => Mathf.Max(1f, worldDimCameraPadding);
    public float WorldDimZ => worldDimZ;
    public float CutInZoomScale => Mathf.Clamp(cutInZoomScale, 0.1f, 1f);
    public float ZoomInSeconds => Mathf.Max(0f, zoomInSeconds);
    public float ZoomOutSeconds => Mathf.Max(0f, zoomOutSeconds);
    public Sprite[] EyeFlashFrames => eyeFlashFrames;
    public float EyeFlashFps => Mathf.Max(1f, eyeFlashFps);
    public Vector2 EyeFlashLocalOffset => eyeFlashLocalOffset;
    public float EyeFlashScale => Mathf.Max(0.01f, eyeFlashScale);
    public string EyeFlashSortingLayerName => string.IsNullOrWhiteSpace(eyeFlashSortingLayerName) ? "Entity" : eyeFlashSortingLayerName;
    public int EyeFlashSortingOrder => eyeFlashSortingOrder;
    public Color EyeFlashPlayerTint => eyeFlashPlayerTint;
    public Sprite PlayerCutInBaseSprite => playerCutInBaseSprite;
    public Vector2 PlayerCutInLocalOffset => playerCutInLocalOffset;
    public float PlayerCutInScale => Mathf.Max(0.01f, playerCutInScale);
    public float PlayerTintInStartRatio => Mathf.Clamp01(playerTintInStartRatio);
    public float PlayerTintInEndRatio => Mathf.Clamp01(playerTintInEndRatio);
    public float PlayerTintOutStartRatio => Mathf.Clamp01(playerTintOutStartRatio);
    public float PlayerTintOutEndRatio => Mathf.Clamp01(playerTintOutEndRatio);
    public Sprite WeaponInactiveSprite => weaponInactiveSprite;
    public Sprite WeaponBloomSprite => weaponBloomSprite;
    public Material WeaponRevealMaterial => weaponRevealMaterial;
    public float WeaponRevealSeconds => Mathf.Max(0f, weaponRevealSeconds);
    public float WeaponRevealInSeconds => Mathf.Max(0f, weaponRevealSeconds);
    public float WeaponRevealOutSeconds => Mathf.Max(0f, weaponRevealOutSeconds);
    public Vector2 WeaponRevealUvDirection => weaponRevealUvDirection.sqrMagnitude > 0.0001f ? weaponRevealUvDirection.normalized : new Vector2(-1f, 1f).normalized;
    public float WeaponRevealFeather => Mathf.Clamp(weaponRevealFeather, 0f, 0.25f);
    public Vector2 WeaponRevealMaskLocalDirection => weaponRevealMaskLocalDirection.sqrMagnitude > 0.0001f ? weaponRevealMaskLocalDirection.normalized : new Vector2(-1f, 1f).normalized;
    public float WeaponRevealMaskAlphaCutoff => Mathf.Clamp01(weaponRevealMaskAlphaCutoff);
    public float WeaponRevealMaskPadding => Mathf.Max(0f, weaponRevealMaskPadding);
    public float WeaponRevealMaskWidthMultiplier => Mathf.Max(0.1f, weaponRevealMaskWidthMultiplier);
    public StatusHudDefinition BloomStatusDefinition => bloomStatusDefinition;
    public Sprite BloomStatusIcon => bloomStatusIcon;
    public GameplayTag KillExtensionRequiredTag => killExtensionRequiredTag;
    public float OutlinePixels => Mathf.Max(0.1f, outlinePixels);
    public float OutlineTopWavePixels => Mathf.Max(0f, outlineTopWavePixels);
    public float OutlineWaveSpeed => Mathf.Max(0f, outlineWaveSpeed);
    public float DashDistanceMultiplier => Mathf.Max(0.01f, dashDistanceMultiplier);
    public float DashDurationMultiplier => Mathf.Max(0.01f, dashDurationMultiplier);
    public int DashSlashCount => Mathf.Max(1, dashSlashCount);
    public float DashSlashInitialDelaySeconds => Mathf.Max(0f, dashSlashInitialDelaySeconds);
    public float DashSlashIntervalSeconds => Mathf.Max(0f, dashSlashIntervalSeconds);
    public float DashSlashActiveTime => Mathf.Max(0.01f, dashSlashActiveTime);
    public Vector2 DashSlashHitboxSize => new(Mathf.Max(0.01f, dashSlashHitboxSize.x), Mathf.Max(0.01f, dashSlashHitboxSize.y));
    public float DashSlashAngleJitter => Mathf.Max(0f, dashSlashAngleJitter);
    public float DashSlashDamageScale => Mathf.Max(0f, dashSlashDamageScale);
    public LayerMask DashSlashHitLayers => dashSlashHitLayers;
    public LayerMask DashSlashWallLayers => dashSlashWallLayers;
    public UnityGAS.DamagePayloadConfig DashSlashDamageConfig => dashSlashDamageConfig;
    public GameplayEffect DashSlashDamageEffect => dashSlashDamageEffect;
    public GE_Knockback_Spec DashSlashKnockbackEffect => dashSlashKnockbackEffect;
    public ScaledStatFormula DashSlashDamageFormula => dashSlashDamageFormula;
    public ScaledStatFormula DashSlashKnockbackFormula => dashSlashKnockbackFormula;
    public float DashSlashLegacyDamage => dashSlashLegacyDamage;
    public float DashSlashLegacyStaggerDamage => dashSlashLegacyStaggerDamage;
    public GameplayTag DashSlashHitConfirmedTag => dashSlashHitConfirmedTag;
    public HitImpactCueKind DashSlashHitImpactCueKind => dashSlashHitImpactCueKind;
    public GameObject SlashEffectPrefab => slashEffectPrefab;
    public float SlashEffectLifetime => Mathf.Max(0.01f, slashEffectLifetime);
    public float SlashEffectScale => Mathf.Max(0.01f, slashEffectScale);
    public int SlashEffectSortingOrderOffset => slashEffectSortingOrderOffset;
    public GameObject DashSlashParticlePrefab => dashSlashParticlePrefab;
    public GameObject WeaponRevealParticlePrefab => weaponRevealParticlePrefab;
    public GameObject FinalShakeParticlePrefab => finalShakeParticlePrefab;
    public float ParticleLifetimeFallback => Mathf.Max(0.01f, particleLifetimeFallback);
    public int ParticleSortingOrderOffset => particleSortingOrderOffset;
}
