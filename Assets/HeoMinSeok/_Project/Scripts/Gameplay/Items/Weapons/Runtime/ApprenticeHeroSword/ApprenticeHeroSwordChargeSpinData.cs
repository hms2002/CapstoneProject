using CapstoneAudio;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "ALData_ApprenticeHeroSwordChargeSpin", menuName = "GAS/Weapon/Apprentice Hero Sword/Charge Spin Data")]
public sealed class ApprenticeHeroSwordChargeSpinData : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private string chargeAnimationTrigger = "Skill2Charge";
    [SerializeField] private string releaseAnimationTrigger = "Skill2";
    [SerializeField] private GameplayTag releaseHitEventTag;
    [SerializeField, Min(0f)] private float releaseHitEventTimeout = 0.6f;

    [Header("Charge")]
    [SerializeField, Min(0f)] private float minChargeSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float maxChargeSeconds = 1f;
    [SerializeField, Min(0.01f)] private float spinDuration = 0.35f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.2f;

    [Header("Hit")]
    [SerializeField, Min(1)] private int pulseCount = 4;
    [SerializeField, Min(0f)] private float minRadius = 1f;
    [SerializeField, Min(0f)] private float maxRadius = 1.8f;
    [SerializeField, Min(0f)] private float minDamageScale = 1f;
    [SerializeField, Min(0f)] private float maxDamageScale = 1.8f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private ApprenticeHeroSwordHitboxConfig hitbox = new();
    [SerializeField] private ApprenticeHeroSwordDamageConfig damage = new();

    [Header("Charge Presentation")]
    [SerializeField] private ParticleSystem chargeParticlePrefab;
    [SerializeField] private Vector3 chargeParticleLocalPosition;
    [SerializeField] private Vector3 chargeParticleLocalEulerAngles;
    [SerializeField] private Vector3 chargeParticleLocalScale = Vector3.one;
    [SerializeField] private bool chargeParticleUseLocalSimulation = true;
    [SerializeField, Min(0f)] private float chargeParticleStopDelay;

    [Header("Charge Reveal")]
    [SerializeField] private bool enableChargeReveal = true;
    [SerializeField] private Sprite chargeRevealSprite;
    [SerializeField] private Sprite fullChargeRevealSprite;
    [SerializeField] private Sprite chargeRevealMaskSprite;
    [SerializeField] private Color chargeRevealColor = new(0.35f, 0.85f, 1f, 0.85f);
    [SerializeField] private Color fullChargeRevealColor = new(0.75f, 1f, 1f, 1f);
    [SerializeField] private Vector3 chargeRevealLocalPosition;
    [SerializeField] private Vector3 chargeRevealLocalEulerAngles;
    [SerializeField] private Vector3 chargeRevealLocalScale = Vector3.one;
    [SerializeField] private Vector2 chargeRevealMaskStartOffset = new(0f, -0.35f);
    [SerializeField] private Vector2 chargeRevealMaskEndOffset = Vector2.zero;
    [SerializeField] private Vector2 chargeRevealMaskStartScale = new(1f, 0.02f);
    [SerializeField] private Vector2 chargeRevealMaskEndScale = Vector2.one;
    [SerializeField] private bool useDirectionalChargeRevealMask = true;
    [SerializeField] private Vector2 chargeRevealMaskLocalDirection = new(-1f, 1f);
    [SerializeField, Min(0f)] private float chargeRevealMaskPadding = 0.04f;
    [SerializeField, Min(0.01f)] private float chargeRevealMaskWidthMultiplier = 1.25f;
    [SerializeField, Range(0f, 1f)] private float fullChargeSpriteThreshold = 1f;
    [SerializeField] private int chargeRevealSortingOrderOffset = 1;
    [SerializeField, Range(0f, 1f)] private float chargeRevealMaskAlphaCutoff = 0.5f;

    [Header("Release Charge Presentation")]
    [SerializeField] private Color minChargeReleaseColor = Color.white;
    [SerializeField] private Color maxPartialChargeReleaseColor = new(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Color fullChargeReleaseColor = new(1f, 0.45f, 0.12f, 1f);
    [SerializeField] private Vector2 minChargeReleaseSizeMultiplier = Vector2.one;
    [SerializeField] private Vector2 maxPartialChargeReleaseSizeMultiplier = new(1.25f, 1.25f);
    [SerializeField] private Vector2 fullChargeReleaseSizeMultiplier = new(1.55f, 1.55f);

    [Header("Full Charge VFX")]
    [SerializeField] private ParticleSystem fullChargeVfxPrefab;
    [SerializeField] private Vector3 fullChargeVfxLocalPosition;
    [SerializeField] private Vector3 fullChargeVfxLocalEulerAngles;
    [SerializeField] private Vector3 fullChargeVfxLocalScale = Vector3.one;
    [SerializeField] private bool fullChargeVfxUseLocalSimulation = true;
    [SerializeField, Min(0.01f)] private float fullChargeVfxDestroyDelay = 2f;

    [Header("Audio")]
    [SerializeField] private SoundRef chargeStartSound;
    [SerializeField] private SoundRef releaseSound;
    [SerializeField] private SoundRef pulseSound;

    public string ChargeAnimationTrigger => chargeAnimationTrigger;
    public string ReleaseAnimationTrigger => releaseAnimationTrigger;
    public GameplayTag ReleaseHitEventTag => releaseHitEventTag;
    public float ReleaseHitEventTimeout => Mathf.Max(0f, releaseHitEventTimeout);
    public float MinChargeSeconds => Mathf.Max(0f, minChargeSeconds);
    public float MaxChargeSeconds => Mathf.Max(0.01f, Mathf.Max(maxChargeSeconds, minChargeSeconds));
    public float SpinDuration => Mathf.Max(0.01f, spinDuration);
    public float RecoveryDuration => Mathf.Max(0f, recoveryDuration);
    public int PulseCount => Mathf.Max(1, pulseCount);
    public float MinRadius => Mathf.Max(0f, minRadius);
    public float MaxRadius => Mathf.Max(MinRadius, maxRadius);
    public float MinDamageScale => Mathf.Max(0f, minDamageScale);
    public float MaxDamageScale => Mathf.Max(MinDamageScale, maxDamageScale);
    public LayerMask HitLayers => hitLayers;
    public ApprenticeHeroSwordHitboxConfig Hitbox => hitbox;
    public ApprenticeHeroSwordDamageConfig Damage => damage;
    public ParticleSystem ChargeParticlePrefab => chargeParticlePrefab;
    public Vector3 ChargeParticleLocalPosition => chargeParticleLocalPosition;
    public Vector3 ChargeParticleLocalEulerAngles => chargeParticleLocalEulerAngles;
    public Vector3 ChargeParticleLocalScale => SanitizeScale(chargeParticleLocalScale);
    public bool ChargeParticleUseLocalSimulation => chargeParticleUseLocalSimulation;
    public float ChargeParticleStopDelay => Mathf.Max(0f, chargeParticleStopDelay);
    public bool EnableChargeReveal => enableChargeReveal;
    public Sprite ChargeRevealSprite => chargeRevealSprite;
    public Sprite FullChargeRevealSprite => fullChargeRevealSprite;
    public Sprite ChargeRevealMaskSprite => chargeRevealMaskSprite;
    public Color ChargeRevealColor => chargeRevealColor;
    public Color FullChargeRevealColor => fullChargeRevealColor;
    public Vector3 ChargeRevealLocalPosition => chargeRevealLocalPosition;
    public Vector3 ChargeRevealLocalEulerAngles => chargeRevealLocalEulerAngles;
    public Vector3 ChargeRevealLocalScale => SanitizeScale(chargeRevealLocalScale);
    public Vector2 ChargeRevealMaskStartOffset => chargeRevealMaskStartOffset;
    public Vector2 ChargeRevealMaskEndOffset => chargeRevealMaskEndOffset;
    public Vector2 ChargeRevealMaskStartScale => SanitizeScale(chargeRevealMaskStartScale);
    public Vector2 ChargeRevealMaskEndScale => SanitizeScale(chargeRevealMaskEndScale);
    public bool UseDirectionalChargeRevealMask => useDirectionalChargeRevealMask;
    public Vector2 ChargeRevealMaskLocalDirection => SanitizeDirection(chargeRevealMaskLocalDirection);
    public float ChargeRevealMaskPadding => Mathf.Max(0f, chargeRevealMaskPadding);
    public float ChargeRevealMaskWidthMultiplier => Mathf.Max(0.01f, chargeRevealMaskWidthMultiplier);
    public float FullChargeSpriteThreshold => Mathf.Clamp01(fullChargeSpriteThreshold);
    public int ChargeRevealSortingOrderOffset => chargeRevealSortingOrderOffset;
    public float ChargeRevealMaskAlphaCutoff => Mathf.Clamp01(chargeRevealMaskAlphaCutoff);
    public Color MinChargeReleaseColor => minChargeReleaseColor;
    public Color MaxPartialChargeReleaseColor => maxPartialChargeReleaseColor;
    public Color FullChargeReleaseColor => fullChargeReleaseColor;
    public Vector2 MinChargeReleaseSizeMultiplier => SanitizeScale(minChargeReleaseSizeMultiplier);
    public Vector2 MaxPartialChargeReleaseSizeMultiplier => SanitizeScale(maxPartialChargeReleaseSizeMultiplier);
    public Vector2 FullChargeReleaseSizeMultiplier => SanitizeScale(fullChargeReleaseSizeMultiplier);
    public ParticleSystem FullChargeVfxPrefab => fullChargeVfxPrefab;
    public Vector3 FullChargeVfxLocalPosition => fullChargeVfxLocalPosition;
    public Vector3 FullChargeVfxLocalEulerAngles => fullChargeVfxLocalEulerAngles;
    public Vector3 FullChargeVfxLocalScale => SanitizeScale(fullChargeVfxLocalScale);
    public bool FullChargeVfxUseLocalSimulation => fullChargeVfxUseLocalSimulation;
    public float FullChargeVfxDestroyDelay => Mathf.Max(0.01f, fullChargeVfxDestroyDelay);
    public SoundRef ChargeStartSound => chargeStartSound;
    public SoundRef ReleaseSound => releaseSound;
    public SoundRef PulseSound => pulseSound;

    public Color ResolveChargeReleaseColor(float chargeRatio)
    {
        float ratio = Mathf.Clamp01(chargeRatio);
        if (ratio >= FullChargeSpriteThreshold)
            return fullChargeReleaseColor;

        return Color.Lerp(minChargeReleaseColor, maxPartialChargeReleaseColor, ResolvePartialChargeRatio(ratio));
    }

    public Vector2 ResolveChargeReleaseSizeMultiplier(float chargeRatio)
    {
        float ratio = Mathf.Clamp01(chargeRatio);
        if (ratio >= FullChargeSpriteThreshold)
            return FullChargeReleaseSizeMultiplier;

        return Vector2.Lerp(
            MinChargeReleaseSizeMultiplier,
            MaxPartialChargeReleaseSizeMultiplier,
            ResolvePartialChargeRatio(ratio));
    }

    private float ResolvePartialChargeRatio(float ratio)
    {
        float threshold = Mathf.Clamp(FullChargeSpriteThreshold, 0.0001f, 1f);
        float minRatio = MaxChargeSeconds > 0f
            ? Mathf.Clamp01(MinChargeSeconds / MaxChargeSeconds)
            : 0f;
        if (threshold <= minRatio + 0.0001f)
            return ratio >= threshold ? 1f : 0f;

        return Mathf.InverseLerp(minRatio, threshold, ratio);
    }

    private static Vector3 SanitizeScale(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x) > 0.0001f ? value.x : 1f,
            Mathf.Abs(value.y) > 0.0001f ? value.y : 1f,
            Mathf.Abs(value.z) > 0.0001f ? value.z : 1f);
    }

    private static Vector2 SanitizeScale(Vector2 value)
    {
        return new Vector2(
            Mathf.Abs(value.x) > 0.0001f ? value.x : 0.0001f,
            Mathf.Abs(value.y) > 0.0001f ? value.y : 0.0001f);
    }

    private static Vector2 SanitizeDirection(Vector2 value)
    {
        return value.sqrMagnitude > 0.0001f
            ? value.normalized
            : new Vector2(-1f, 1f).normalized;
    }
}
