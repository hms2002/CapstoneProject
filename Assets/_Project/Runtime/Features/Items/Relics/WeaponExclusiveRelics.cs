using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Weapon-specific relic gates. Persistent levels stay in RelicInventory.</summary>
public static class WeaponExclusiveRelics
{
    public const string SpearThirdStrike = "Relic.LightningSpear.ThirdStrike";
    public const string SpearTargetedRain = "Relic.LightningSpear.TargetedRain";
    public const string OddIronMagazine = "Relic.OddIron.Magazine";
    public const string CrimsonKillShot = "Relic.CrimsonBoundary.KillShot";
    public const string CrimsonLavaBall = "Relic.CrimsonBoundary.LavaBall";
    public const string ApprenticeChargeLink = "Relic.ApprenticeHeroSword.ChargeLink";

    public static bool Has(GameObject owner, string id) => owner != null &&
        owner.TryGetComponent(out RelicInventory inventory) && inventory.TryGetRelicLevelById(id, out int level) && level > 0;

    public static bool TryReload(GameObject owner, OddIronRuntimeData data)
    {
        if (data == null || data.HasAmmo || data.MaxAmmo <= 0 || owner == null) return false;
        if (!owner.TryGetComponent(out RelicInventory inventory) || !inventory.TryConsumeLevel(OddIronMagazine)) return false;
        data.RefillAmmo();
        return true;
    }

    public static List<Enemy> FindEnemies(Vector2 center, float radius, GameObject excluded = null)
    {
        var results = new List<Enemy>();
        var seen = new HashSet<Enemy>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || enemy.gameObject == excluded || !seen.Add(enemy)) continue;
            results.Add(enemy);
        }
        results.Sort((a, b) => ((Vector2)a.transform.position - center).sqrMagnitude.CompareTo(
            ((Vector2)b.transform.position - center).sqrMagnitude));
        return results;
    }
}
