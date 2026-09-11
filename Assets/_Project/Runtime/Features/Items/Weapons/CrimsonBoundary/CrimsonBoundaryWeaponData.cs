using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "CrimsonBoundaryWeaponData", menuName = "GAS/Weapon/Crimson Boundary/Data")]
// Responsibility: author Crimson Boundary attacks, skill geometry and fire-based damage coefficients.
public sealed class CrimsonBoundaryWeaponData : ScriptableObject
{
    [Header("Shared")]
    public GameplayEffect damageEffect;
    public LayerMask wallLayers;
    public LayerMask damageLayers;

    [Header("Attack")]
    public float projectileSpeed = 18f;
    public float projectileLifetime = 2f;
    public int attackBurnStacks = 3;

    [Header("Skill Damage")]
    [Tooltip("Optional skill-only fire scaling; unset keeps raw FireFinal scaling.")]
    public ScaledStatFormula skillFireFormula;
    [Min(0f)] public float burnConsumptionMultiplier = 0.5f;

    [Header("Skill 1")]
    public int skill1MaxConsume = 5;
    public float skill1Diameter = 5f;

    [Header("Skill 2")]
    public float skill2ImpactDelay = 0.6f;
    public float skill2Diameter = 5f;
    public float skill2BaseMultiplier = 2f;
}
