using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryBigExplosion", menuName = "GAS/Weapon/Crimson Boundary/Big Explosion Logic")]
// Responsibility: execute the delayed area strike and add damage from each target's consumed burn stacks.
public sealed class AbilityLogic_CrimsonBoundaryBigExplosion : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        CrimsonBoundaryWeaponData data = spec?.Definition?.sourceObject as CrimsonBoundaryWeaponData;
        if (system == null || data == null || data.damageEffect == null)
            yield break;

        if (WeaponExclusiveRelics.Has(system.gameObject, WeaponExclusiveRelics.CrimsonLavaBall))
        {
            CrimsonBoundaryRuntimeState relicRuntime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
            var ball = CrimsonBoundaryVisual2D.Spawn(data.relicLavaBallPrefab, system.transform.position, Quaternion.identity, relicRuntime);
            if (ball != null)
                ball.GetComponent<CrimsonBoundaryLavaProjectile2D>().Setup(system, spec, data, relicRuntime,
                    AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right));
            yield break;
        }

        Vector2 impactPosition = CrimsonBoundaryUtility.ResolveCursor(system);
        CrimsonBoundaryRuntimeState runtime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
        bool hadRuntime = runtime != null;
        float startY = impactPosition.y + 8f;
        Camera camera = Camera.main;
        if (camera != null)
        {
            float depth = camera.WorldToViewportPoint(impactPosition).z;
            startY = Mathf.Max(startY, camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y + 2f);
        }
        Vector3 start = new Vector3(impactPosition.x, startY, 0f);
        var meteor = CrimsonBoundaryVisual2D.Spawn(data.meteorPrefab, start, Quaternion.identity, runtime);
        try
        {
            float elapsed = 0f;
            while (elapsed < data.skill2ImpactDelay)
            {
                if ((spec.Token != null && spec.Token.IsCancelled) || (hadRuntime && (runtime == null || !runtime.isActiveAndEnabled))) yield break;
                if (meteor != null) meteor.transform.position = Vector3.Lerp(start, impactPosition, elapsed / Mathf.Max(0.01f, data.skill2ImpactDelay));
                yield return null;
                elapsed += Time.deltaTime;
            }
            if ((spec.Token != null && spec.Token.IsCancelled) || (hadRuntime && (runtime == null || !runtime.isActiveAndEnabled))) yield break;
        }
        finally
        {
            if (meteor != null) Object.Destroy(meteor.gameObject);
        }
        CrimsonBoundaryVisual2D.Spawn(data.meteorHitPrefab, impactPosition, Quaternion.identity, runtime);

        List<GameObject> targets = CrimsonBoundaryUtility.CollectTargets(impactPosition, data.skill2Diameter, data.damageLayers);
        for (int i = 0; i < targets.Count; i++)
        {
            GameObject target = targets[i];
            bool critical;
            float baseDamage = CrimsonBoundaryUtility.CalculateDirectDamage(system, data.skill2BaseMultiplier, out critical, data.skillFireFormula);
            BurnStatus2D burn = target.GetComponent<BurnStatus2D>();
            int consumed = burn != null ? burn.ConsumeAll() : 0;
            float totalDamage = baseDamage +
                CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, consumed, data.burnConsumptionMultiplier, data.skillFireFormula);
            CrimsonBoundaryUtility.ApplyDamage(system, spec, data.damageEffect, target, totalDamage, critical, system.gameObject);
        }

    }
}
