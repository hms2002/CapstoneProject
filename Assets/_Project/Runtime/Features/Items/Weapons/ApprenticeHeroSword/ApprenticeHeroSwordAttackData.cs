using System;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public readonly struct RuntimeApprenticeHeroSwordAttackStep
{
    public readonly ApprenticeHeroSwordHitboxConfig hitbox;
    public readonly ApprenticeHeroSwordDamageConfig damage;
    public readonly SoundRef attackSound;
    public readonly string animationTrigger;
    public readonly float recoveryDuration;
    public readonly float nextAttackDelay;
    public readonly float forwardOffset;
    public readonly float sideOffset;
    public readonly int sideSign;
    public readonly float lungeDistance;
    public readonly float lungeDuration;

    public RuntimeApprenticeHeroSwordAttackStep(
        ApprenticeHeroSwordHitboxConfig hitbox,
        ApprenticeHeroSwordDamageConfig damage,
        SoundRef attackSound,
        string animationTrigger,
        float recoveryDuration,
        float nextAttackDelay,
        float forwardOffset,
        float sideOffset,
        int sideSign,
        float lungeDistance,
        float lungeDuration)
    {
        this.hitbox = hitbox;
        this.damage = damage;
        this.attackSound = attackSound;
        this.animationTrigger = animationTrigger;
        this.recoveryDuration = recoveryDuration;
        this.nextAttackDelay = nextAttackDelay;
        this.forwardOffset = forwardOffset;
        this.sideOffset = sideOffset;
        this.sideSign = sideSign;
        this.lungeDistance = lungeDistance;
        this.lungeDuration = lungeDuration;
    }
}

[Serializable]
public sealed class ApprenticeHeroSwordAttackStep
{
    private const float MinAttackSpeedScaledDuration = 0.02f;

    public ApprenticeHeroSwordHitboxConfig hitbox = new();
    public ApprenticeHeroSwordDamageConfig damage = new();
    public SoundRef attackSound;
    public string animationTrigger;
    public float recoveryDuration = 0.16f;
    public float nextAttackDelay;
    public float forwardOffset = 0.9f;
    public float sideOffset;
    public int sideSign = 1;
    public float lungeDistance = 0.6f;
    public float lungeDuration = 0.12f;

    public RuntimeApprenticeHeroSwordAttackStep CreateAttackSpeedScaled(float finalAttackSpeed)
    {
        float safeAttackSpeed = finalAttackSpeed > 0.0001f ? finalAttackSpeed : 1f;
        float resolvedNextDelay = nextAttackDelay > 0f ? nextAttackDelay : Mathf.Max(0f, recoveryDuration);

        return new RuntimeApprenticeHeroSwordAttackStep(
            hitbox,
            damage,
            attackSound,
            animationTrigger,
            Mathf.Max(MinAttackSpeedScaledDuration, recoveryDuration / safeAttackSpeed),
            Mathf.Max(MinAttackSpeedScaledDuration, resolvedNextDelay / safeAttackSpeed),
            forwardOffset,
            sideOffset,
            sideSign,
            lungeDistance,
            lungeDuration > 0f ? Mathf.Max(MinAttackSpeedScaledDuration, lungeDuration / safeAttackSpeed) : 0f);
    }
}

[Serializable]
public sealed class ApprenticeHeroSwordAttackComboConfig
{
    [SerializeField] private MeleeHitboxActor defaultHitboxPrefab;
    [SerializeField] private ApprenticeHeroSwordAttackStep[] steps = Array.Empty<ApprenticeHeroSwordAttackStep>();
    [SerializeField, Min(0f)] private float comboResetTime = 0.75f;
    [SerializeField] private GameplayTag hitEventTag;
    [SerializeField, Min(0f)] private float hitEventTimeout = 0.25f;
    [SerializeField] private LayerMask hitLayers;

    public MeleeHitboxActor DefaultHitboxPrefab => defaultHitboxPrefab;
    public float ComboResetTime => Mathf.Max(0f, comboResetTime);
    public GameplayTag HitEventTag => hitEventTag;
    public float HitEventTimeout => Mathf.Max(0f, hitEventTimeout);
    public LayerMask HitLayers => hitLayers;

    public int GetStepCount()
    {
        return steps != null && steps.Length > 0 ? steps.Length : 1;
    }

    public RuntimeApprenticeHeroSwordAttackStep GetRuntimeStep(int comboIndex, float finalAttackSpeed)
    {
        if (steps == null || steps.Length == 0)
            return default;

        comboIndex = Mathf.Clamp(comboIndex, 0, steps.Length - 1);
        return steps[comboIndex].CreateAttackSpeedScaled(finalAttackSpeed);
    }
}

[CreateAssetMenu(fileName = "ALData_ApprenticeHeroSwordAttack", menuName = "GAS/Weapon/Apprentice Hero Sword/Attack Data")]
public sealed class ApprenticeHeroSwordAttackData : ScriptableObject
{
    [SerializeField] private ApprenticeHeroSwordAttackComboConfig combo = new();

    public ApprenticeHeroSwordAttackComboConfig Combo => combo;
}
