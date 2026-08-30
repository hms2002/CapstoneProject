using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryBigExplosion", menuName = "GAS/Weapon/Crimson Boundary/Big Explosion Logic")]
public sealed class AbilityLogic_CrimsonBoundaryBigExplosion : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        CrimsonBoundaryWeaponData data = spec?.Definition?.sourceObject as CrimsonBoundaryWeaponData;
        if (system == null || data == null || data.damageEffect == null)
            yield break;

        Vector2 impactPosition = CrimsonBoundaryUtility.ResolveCursor(system);
        CrimsonBoundaryRuntimeState runtime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
        GameObject warning = CrimsonBoundaryUtility.CreateSquare(
            "CrimsonBoundary_BigExplosionWarning", impactPosition,
            new Vector2(data.skill2Diameter, data.skill2Diameter * TopDownEllipseHitUtility2D.DefaultTopDownCircleYScale),
            new Color(0.8f, 0.05f, 0.01f, 0.24f),
            "AttackTelegraph",
            1);
        runtime?.Register(warning);

        yield return WaitForSecondsUnlessCancelled(data.skill2ImpactDelay, spec);
        if (IsAbilityCancelled(spec))
        {
            runtime?.Forget(warning);
            if (warning != null) Object.Destroy(warning);
            yield break;
        }

        if (warning != null)
        {
            SpriteRenderer renderer = warning.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(1f, 0.22f, 0.01f, 0.65f);
                renderer.sortingLayerName = "FloatingAOE";
                renderer.sortingOrder = 1;
            }
        }

        List<GameObject> targets = CrimsonBoundaryUtility.CollectTargets(impactPosition, data.skill2Diameter, data.damageLayers);
        for (int i = 0; i < targets.Count; i++)
        {
            GameObject target = targets[i];
            bool critical;
            float baseDamage = CrimsonBoundaryUtility.CalculateDirectDamage(system, data.skill2BaseMultiplier, out critical);
            BurnStatus2D burn = target.GetComponent<BurnStatus2D>();
            int consumed = burn != null ? burn.ConsumeAll() : 0;
            float totalDamage = baseDamage + CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, consumed);
            CrimsonBoundaryUtility.ApplyDamage(system, spec, data.damageEffect, target, totalDamage, critical, system.gameObject);
        }

        yield return WaitForSecondsUnlessCancelled(0.15f, spec);
        runtime?.Forget(warning);
        if (warning != null) Object.Destroy(warning);
    }
}
