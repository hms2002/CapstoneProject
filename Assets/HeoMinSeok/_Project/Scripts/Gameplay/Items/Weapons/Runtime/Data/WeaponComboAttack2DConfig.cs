using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [Serializable]
    public sealed class WeaponComboElementDamageGroup
    {
        public List<ElementDamageInput> elements = new();
    }

    [Serializable]
    public sealed class WeaponAttackPrefabConfig
    {
        [SerializeField] private MeleeHitboxActor hitboxPrefab;
        [SerializeField, Min(0.01f)] private float activeTime = 0.1f;
        [SerializeField] private Vector2 hitboxSize = Vector2.one;
        [SerializeField] private Vector2 hitboxScaleMultiplier = Vector2.one;
        [SerializeField] private bool overrideSizingMode = true;
        [SerializeField] private MeleeHitboxSizingMode sizingMode = MeleeHitboxSizingMode.UsePrefabAuthoredShape;
        [SerializeField] private LayerMask wallLayers;
        [SerializeField] private bool hitOncePerTarget = true;
        [SerializeField] private bool destroyOnFirstHit;

        public MeleeHitboxActor HitboxPrefab => hitboxPrefab;
        public float ActiveTime => Mathf.Max(0.01f, activeTime);
        public Vector2 HitboxSize => new Vector2(Mathf.Max(0.01f, hitboxSize.x), Mathf.Max(0.01f, hitboxSize.y));
        public Vector2 HitboxScaleMultiplier => new Vector2(
            Mathf.Abs(hitboxScaleMultiplier.x) > 0.0001f ? Mathf.Abs(hitboxScaleMultiplier.x) : 1f,
            Mathf.Abs(hitboxScaleMultiplier.y) > 0.0001f ? Mathf.Abs(hitboxScaleMultiplier.y) : 1f);
        public bool OverrideSizingMode => overrideSizingMode;
        public MeleeHitboxSizingMode SizingMode => sizingMode;
        public LayerMask WallLayers => wallLayers;
        public bool HitOncePerTarget => hitOncePerTarget;
        public bool DestroyOnFirstHit => destroyOnFirstHit;
    }

    [Serializable]
    public readonly struct RuntimeWeaponComboAttackStep2D
    {
        public readonly WeaponAttackPrefabConfig attackPrefab;
        public readonly SoundRef attackSound;
        public readonly string animationTrigger;
        public readonly float recoveryDuration;
        public readonly float nextAttackDelay;
        public readonly ScaledStatFormula damageFormula;
        public readonly ScaledStatFormula knockbackFormula;
        public readonly float legacyDamage;
        public readonly float legacyStaggerDamage;
        public readonly WeaponComboElementDamageGroup elementDamages;
        public readonly float forwardOffset;
        public readonly float sideOffset;
        public readonly int sideSign;
        public readonly float lungeDistance;
        public readonly float lungeDuration;

        public RuntimeWeaponComboAttackStep2D(
            WeaponAttackPrefabConfig attackPrefab,
            SoundRef attackSound,
            string animationTrigger,
            float recoveryDuration,
            float nextAttackDelay,
            ScaledStatFormula damageFormula,
            ScaledStatFormula knockbackFormula,
            float legacyDamage,
            float legacyStaggerDamage,
            WeaponComboElementDamageGroup elementDamages,
            float forwardOffset,
            float sideOffset,
            int sideSign,
            float lungeDistance,
            float lungeDuration)
        {
            this.attackPrefab = attackPrefab;
            this.attackSound = attackSound;
            this.animationTrigger = animationTrigger;
            this.recoveryDuration = recoveryDuration;
            this.nextAttackDelay = nextAttackDelay;
            this.damageFormula = damageFormula;
            this.knockbackFormula = knockbackFormula;
            this.legacyDamage = legacyDamage;
            this.legacyStaggerDamage = legacyStaggerDamage;
            this.elementDamages = elementDamages;
            this.forwardOffset = forwardOffset;
            this.sideOffset = sideOffset;
            this.sideSign = sideSign;
            this.lungeDistance = lungeDistance;
            this.lungeDuration = lungeDuration;
        }
    }

    [Serializable]
    public sealed class WeaponComboAttackStep2D : IAttackSpeedScaledStep<RuntimeWeaponComboAttackStep2D>
    {
        private const float MinAttackSpeedScaledDuration = 0.02f;

        public WeaponAttackPrefabConfig attackPrefab = new();
        public SoundRef attackSound;
        public string animationTrigger;
        public float recoveryDuration = 0.12f;
        public float nextAttackDelay;
        public ScaledStatFormula damageFormula;
        public ScaledStatFormula knockbackFormula;
        public float legacyDamage = 10f;
        public float legacyStaggerDamage;
        public WeaponComboElementDamageGroup elementDamages = new();
        public float forwardOffset = 1f;
        public float sideOffset;
        public int sideSign = 1;
        public float lungeDistance;
        public float lungeDuration;

        public float ResolveNextAttackDelay()
        {
            return nextAttackDelay > 0f ? nextAttackDelay : Mathf.Max(0f, recoveryDuration);
        }

        public RuntimeWeaponComboAttackStep2D CreateAttackSpeedScaled(float finalAttackSpeed)
        {
            float safeAttackSpeed = finalAttackSpeed > 0.0001f ? finalAttackSpeed : 1f;
            float scaledRecovery = Mathf.Max(MinAttackSpeedScaledDuration, recoveryDuration / safeAttackSpeed);
            float scaledNextAttackDelay = Mathf.Max(MinAttackSpeedScaledDuration, ResolveNextAttackDelay() / safeAttackSpeed);
            float scaledLungeDuration = lungeDuration > 0f
                ? Mathf.Max(MinAttackSpeedScaledDuration, lungeDuration / safeAttackSpeed)
                : 0f;

            return new RuntimeWeaponComboAttackStep2D(
                attackPrefab,
                attackSound,
                animationTrigger,
                scaledRecovery,
                scaledNextAttackDelay,
                damageFormula,
                knockbackFormula,
                legacyDamage,
                legacyStaggerDamage,
                elementDamages,
                forwardOffset,
                sideOffset,
                sideSign,
                lungeDistance,
                scaledLungeDuration);
        }
    }

    [Serializable]
    public sealed class WeaponComboAttack2DConfig
    {
        [Header("Fallbacks")]
        [SerializeField] private MeleeHitboxActor defaultHitboxPrefab;

        [Header("Combo Steps")]
        [SerializeField] private WeaponComboAttackStep2D[] steps = Array.Empty<WeaponComboAttackStep2D>();

        [Header("Damage Channels")]
        [SerializeField] private UnityGAS.DamagePayloadConfig damageConfig = new();

        [Header("Combo")]
        [SerializeField, Min(0f)] private float comboResetTime = 0.45f;

        [Header("Hit Timing")]
        [SerializeField] private GameplayTag hitEventTag;
        [SerializeField] private GameplayTag hitConfirmedTag;
        [SerializeField, Min(0f)] private float hitEventTimeout = 0.35f;
        [SerializeField] private LayerMask hitLayers;

        [Header("Damage Effect")]
        [SerializeField] private GameplayEffect damageEffect;
        [SerializeField] private GE_Knockback_Spec knockbackEffect;

        public MeleeHitboxActor DefaultHitboxPrefab => defaultHitboxPrefab;
        public UnityGAS.DamagePayloadConfig DamageConfig => damageConfig;
        public float ComboResetTime => Mathf.Max(0f, comboResetTime);
        public GameplayTag HitEventTag => hitEventTag;
        public GameplayTag HitConfirmedTag => hitConfirmedTag;
        public float HitEventTimeout => Mathf.Max(0f, hitEventTimeout);
        public LayerMask HitLayers => hitLayers;
        public GameplayEffect DamageEffect => damageEffect;
        public GE_Knockback_Spec KnockbackEffect => knockbackEffect;

        public int GetStepCount()
        {
            return steps != null && steps.Length > 0 ? steps.Length : 1;
        }

        public WeaponComboAttackStep2D GetStep(int comboIndex)
        {
            if (steps == null || steps.Length == 0)
                return null;

            comboIndex = Mathf.Clamp(comboIndex, 0, steps.Length - 1);
            return steps[comboIndex];
        }

        public RuntimeWeaponComboAttackStep2D GetRuntimeStep(int comboIndex, float finalAttackSpeed)
        {
            WeaponComboAttackStep2D step = GetStep(comboIndex);
            return step != null
                ? step.CreateAttackSpeedScaled(finalAttackSpeed)
                : default;
        }
    }
}
