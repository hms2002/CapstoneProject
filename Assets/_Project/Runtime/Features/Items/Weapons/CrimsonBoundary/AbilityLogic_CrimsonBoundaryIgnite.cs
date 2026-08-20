using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryIgnite", menuName = "GAS/Weapon/Crimson Boundary/Ignite Logic")]
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

        var explosions = new List<Explosion>(statuses.Count);
        for (int i = 0; i < statuses.Count; i++)
        {
            BurnStatus2D status = statuses[i];
            if (status == null) continue;
            int consumed = status.ConsumeUpTo(data.skill1MaxConsume);
            if (consumed > 0)
                explosions.Add(new Explosion(status.transform.position, CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(system, consumed)));
        }

        CrimsonBoundaryRuntimeState runtime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
        var flashes = new List<GameObject>(explosions.Count);
        for (int i = 0; i < explosions.Count; i++)
        {
            Explosion explosion = explosions[i];
            GameObject flash = CrimsonBoundaryUtility.CreateSquare(
                "CrimsonBoundary_IgniteSquare", explosion.Position,
                new Vector2(data.skill1Diameter, data.skill1Diameter * TopDownEllipseHitUtility2D.DefaultTopDownCircleYScale),
                new Color(1f, 0.18f, 0.01f, 0.35f),
                "FloatingAOE",
                0);
            runtime?.Register(flash);
            flashes.Add(flash);

            List<GameObject> targets = CrimsonBoundaryUtility.CollectTargets(explosion.Position, data.skill1Diameter, data.damageLayers);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                CrimsonBoundaryUtility.ApplyDamage(system, spec, data.damageEffect, targets[targetIndex], explosion.Damage, false, system.gameObject);
        }

        yield return WaitForSecondsUnlessCancelled(0.12f, spec);
        for (int i = 0; i < flashes.Count; i++)
        {
            runtime?.Forget(flashes[i]);
            if (flashes[i] != null) Object.Destroy(flashes[i]);
        }
    }
}
