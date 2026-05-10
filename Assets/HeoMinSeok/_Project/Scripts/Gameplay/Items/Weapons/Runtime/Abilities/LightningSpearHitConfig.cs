using System;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

[Serializable]
public sealed class LightningSpearHitConfig
{
    [Header("Actor")]
    [SerializeField] private MeleeHitboxActor hitboxPrefab;
    [SerializeField, Min(0.01f)] private float activeTime = 0.1f;
    [SerializeField] private Vector2 hitboxSize = new Vector2(2f, 1f);
    [SerializeField] private float forwardOffset = 1f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private LayerMask wallLayers;

    [Header("Damage")]
    [SerializeField] private UnityGAS.DamagePayloadConfig damageConfig = new UnityGAS.DamagePayloadConfig();
    [SerializeField] private GameplayEffect damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private ScaledStatFormula damageFormula;
    [SerializeField] private ScaledStatFormula knockbackFormula;
    [SerializeField, Min(0f)] private float legacyDamage = 8f;
    [SerializeField, Min(0f)] private float legacyStaggerDamage;
    [SerializeField] private GameplayTag hitConfirmedTag;

    public MeleeHitboxActor HitboxPrefab => hitboxPrefab;
    public float ActiveTime => Mathf.Max(0.01f, activeTime);
    public Vector2 HitboxSize => new Vector2(Mathf.Max(0.01f, hitboxSize.x), Mathf.Max(0.01f, hitboxSize.y));
    public float ForwardOffset => forwardOffset;
    public LayerMask HitLayers => hitLayers;
    public LayerMask WallLayers => wallLayers;
    public bool HasHitbox => hitboxPrefab != null;

    public CombatHitPayload BuildPayload(AbilitySystem system, AbilitySpec spec, float damageScale = 1f)
    {
        return FragmentBladeDamageUtility.BuildPayload(
            system,
            spec,
            damageConfig,
            damageEffect,
            knockbackEffect,
            damageFormula,
            knockbackFormula,
            legacyDamage,
            legacyStaggerDamage,
            damageScale,
            hitConfirmedTag);
    }
}
