using CapstoneAudio;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "ALData_ApprenticeHeroSwordDashStab", menuName = "GAS/Weapon/Apprentice Hero Sword/Dash Stab Data")]
public sealed class ApprenticeHeroSwordDashStabData : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private string animationTrigger = "Skill1";
    [SerializeField] private GameplayTag hitEventTag;
    [SerializeField, Min(0f)] private float hitEventTimeout = 0.25f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float dashDistance = 3f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.18f;

    [Header("Hit")]
    [SerializeField, Min(0f)] private float forwardOffset = 0.95f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private ApprenticeHeroSwordHitboxConfig hitbox = new();
    [SerializeField] private ApprenticeHeroSwordDamageConfig damage = new();

    [Header("Audio")]
    [SerializeField] private SoundRef dashStartSound;
    [SerializeField] private SoundRef stabSound;

    public string AnimationTrigger => animationTrigger;
    public GameplayTag HitEventTag => hitEventTag;
    public float HitEventTimeout => Mathf.Max(0f, hitEventTimeout);
    public float DashDistance => Mathf.Max(0f, dashDistance);
    public float DashDuration => Mathf.Max(0.01f, dashDuration);
    public float RecoveryDuration => Mathf.Max(0f, recoveryDuration);
    public float ForwardOffset => Mathf.Max(0f, forwardOffset);
    public LayerMask HitLayers => hitLayers;
    public ApprenticeHeroSwordHitboxConfig Hitbox => hitbox;
    public ApprenticeHeroSwordDamageConfig Damage => damage;
    public SoundRef DashStartSound => dashStartSound;
    public SoundRef StabSound => stabSound;
}
