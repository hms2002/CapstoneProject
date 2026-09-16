using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryIgnite", menuName = "GAS/Weapon/Crimson Boundary/Ignite Logic")]
// Responsibility: consume visible burn stacks and execute the resulting overlapping skill explosions.
public sealed class AbilityLogic_CrimsonBoundaryIgnite : AbilityLogic
{
    private readonly struct Explosion
    {
        public readonly Vector2 Position;
        public readonly float Damage;
        public Explosion(Vector2 position, float damage) { Position = position; Damage = damage; }
    }

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        CrimsonBoundaryWeaponData data = spec?.Definition?.sourceObject as CrimsonBoundaryWeaponData;
        if (system == null || data == null || data.damageEffect == null)
            yield break;

        List<BurnStatus2D> statuses = CrimsonBoundaryUtility.CollectBurnTargetsInViewport();
        if (statuses.Count == 0)
            yield break;

        CrimsonBoundaryRuntimeState runtime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
        bool hadRuntime = runtime != null;
        var charges = new List<CrimsonBoundaryVisual2D>();
        try
        {
            foreach (var status in statuses)
            {
                if (status == null) continue;
                var charge = CrimsonBoundaryVisual2D.Spawn(data.igniteChargePrefab, status.transform.position, Quaternion.identity, runtime);
                if (charge != null)
                {
                    charge.transform.SetParent(status.transform, true);
                    charges.Add(charge);
                }
            }
            yield return WaitForSecondsUnlessCancelled(data.igniteChargeSeconds, spec);
            if ((spec.Token != null && spec.Token.IsCancelled) || (hadRuntime && (runtime == null || !runtime.isActiveAndEnabled))) yield break;

            // Snapshot every source before area damage can kill another source.
            var explosions = new List<Explosion>();
            foreach (var status in statuses)
            {
                if (status == null || !status.isActiveAndEnabled) continue;
                int consumed = status.ConsumeUpTo(data.skill1MaxConsume);
                if (consumed > 0)
                    explosions.Add(new Explosion(status.transform.position,
                        CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, consumed, data.burnConsumptionMultiplier, data.skillFireFormula)));
            }
            foreach (var explosion in explosions)
            {
                CrimsonBoundaryVisual2D.Spawn(data.igniteExplosionPrefab, explosion.Position, Quaternion.identity, runtime);
                List<GameObject> targets = CrimsonBoundaryUtility.CollectTargets(explosion.Position, data.skill1Diameter, data.damageLayers);
                foreach (var target in targets)
                {
                    Enemy enemy = target != null ? target.GetComponent<Enemy>() : null;
                    bool wasAlive = enemy != null && !enemy.IsDead;
                    Vector3 deathPosition = target != null ? target.transform.position : explosion.Position;
                    CrimsonBoundaryUtility.ApplyDamage(system, spec, data.damageEffect, target, explosion.Damage, false, system.gameObject);
                    if (wasAlive && enemy != null && enemy.IsDead &&
                        WeaponExclusiveRelics.Has(system.gameObject, WeaponExclusiveRelics.CrimsonKillShot))
                        CrimsonBoundaryUtility.SpawnRelicKillShot(system, spec, data, runtime, deathPosition, target);
                }
            }
        }
        finally
        {
            foreach (var charge in charges)
                if (charge != null) Object.Destroy(charge.gameObject);
        }
    }
}
