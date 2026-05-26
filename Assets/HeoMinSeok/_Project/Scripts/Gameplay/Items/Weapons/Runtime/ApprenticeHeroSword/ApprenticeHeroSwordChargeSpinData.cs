using CapstoneAudio;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "ALData_ApprenticeHeroSwordChargeSpin", menuName = "GAS/Weapon/Apprentice Hero Sword/Charge Spin Data")]
public sealed class ApprenticeHeroSwordChargeSpinData : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private string chargeAnimationTrigger = "Skill2Charge";
    [SerializeField] private string releaseAnimationTrigger = "Skill2";

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

    [Header("Audio")]
    [SerializeField] private SoundRef chargeStartSound;
    [SerializeField] private SoundRef releaseSound;
    [SerializeField] private SoundRef pulseSound;

    public string ChargeAnimationTrigger => chargeAnimationTrigger;
    public string ReleaseAnimationTrigger => releaseAnimationTrigger;
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
    public SoundRef ChargeStartSound => chargeStartSound;
    public SoundRef ReleaseSound => releaseSound;
    public SoundRef PulseSound => pulseSound;
}
