using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryCharge", menuName = "GAS/Weapon/Crimson Boundary/Charge Logic")]
public sealed class AbilityLogic_CrimsonBoundaryCharge : AbilityLogic
{
    private const float CursorFollowSeconds = 0.06f;
    private const float MaximumCursorLag = 0.75f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null) yield break;
        // Holding, cancellation and normal completion never start an end cooldown.
        // The one explicit commit below starts it at a valid release instead.
        spec.SkipCooldownOnEnd = true;
        var data = spec.Definition.sourceObject as CrimsonBoundaryChargeData;
        var runtime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
        if (data == null || data.weaponData == null || data.weaponData.damageEffect == null ||
            data.presentationPrefab == null || runtime == null || !InputActionQuery.IsAvailable)
        {
            Debug.LogError("[CrimsonCharge] Missing authored data, weapon runtime, or input backend.");
            yield break;
        }

        GameObject instance = Object.Instantiate(data.presentationPrefab);
        runtime.TrackChargePresentation(instance);
        var presentation = instance.GetComponent<ICrimsonBoundaryChargePresentation>();
        bool handedOff = false;
        try
        {
            if (presentation == null)
            {
                Debug.LogError("[CrimsonCharge] Prefab must implement ICrimsonBoundaryChargePresentation.");
                yield break;
            }
            presentation.BindOwner(runtime);
            float charge = 0f;
            Vector3 impact = ResolveTarget(system, Camera.main);
            while (true)
            {
                if (Cancelled(system, spec, runtime, instance)) yield break;
                Camera camera = Camera.main;
                // One gameplay-owned center drives the warning, released meteor and damage query.
                impact = ClampTargetToViewport(DampChargeTarget(impact, ResolveTarget(system, camera), Time.deltaTime), camera);
                presentation.ShowCharge(system.transform.position, impact, data.ChargeRatio(charge),
                    data.ChargeDiameter(charge));
                if (InputActionQuery.WasReleasedThisFrame(InputActionId.Skill1) ||
                    !InputActionQuery.IsPressed(InputActionId.Skill1)) break;
                yield return null;
                charge = Mathf.Min(data.fullChargeSeconds, charge + Time.deltaTime);
            }

            if (!data.CanRelease(charge))
            {
                spec.Token?.Cancel();
                yield break;
            }
            if (!system.CommitDeferredCooldown(spec)) yield break;
            bool full = data.IsFull(charge);
            float diameter = data.ChargeDiameter(charge);
            Vector3 start = ResolveFlightStart(impact, Camera.main);
            presentation.BeginFlight(start, impact);
            var shared = data.weaponData;
            AbilityAudioRouter.PlayOneShotAtPosition(shared.lavaBallSummonSound, system, spec, impact, data);
            float flight = 0f;
            while (flight < data.flightSeconds)
            {
                if (Cancelled(system, spec, runtime, instance)) yield break;
                presentation.MoveMeteor(Vector3.Lerp(start, impact, flight / data.flightSeconds));
                yield return null;
                flight += Time.deltaTime;
            }
            if (Cancelled(system, spec, runtime, instance)) yield break;
            presentation.MoveMeteor(impact);
            presentation.PlayImpact(impact, diameter);
            handedOff = true;
            AbilityAudioRouter.PlayOneShotAtPosition(shared.lavaBallHitSound, system, spec, impact, data);
            shared.lavaBallHitShake.TryPlay(system.gameObject, Vector3.down, debugReason: "Crimson charged impact");
            ApplyImpact(system, spec, data, runtime, impact, diameter, full);
        }
        finally
        {
            if (runtime != null) runtime.ReleaseChargePresentation(instance);
            if (!handedOff && instance != null) Object.Destroy(instance);
        }
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        CrimsonBoundaryUtility.ResolveRuntimeState(system)?.CancelChargePresentation();
    }

    private static bool Cancelled(AbilitySystem system, AbilitySpec spec, CrimsonBoundaryRuntimeState runtime, GameObject instance)
        => system == null || !system.isActiveAndEnabled || IsAbilityCancelled(spec) ||
           runtime == null || !runtime.isActiveAndEnabled || instance == null;

    public static Vector3 ResolveTarget(AbilitySystem system, Camera camera)
        => ClampTargetToViewport(CrimsonBoundaryUtility.ResolveCursor(system), camera);

    public static Vector3 DampChargeTarget(Vector3 current, Vector3 target, float deltaTime)
    {
        if (deltaTime <= 0f) return current;
        float blend = 1f - Mathf.Exp(-deltaTime / CursorFollowSeconds);
        Vector3 following = Vector3.Lerp(current, target, blend);
        // Large cursor jumps catch up immediately to the allowed lag, then ease the remaining distance.
        return target + Vector3.ClampMagnitude(following - target, MaximumCursorLag);
    }

    private static Vector3 ClampTargetToViewport(Vector3 point, Camera camera)
    {
        if (camera == null) return point;
        Vector3 viewport = camera.WorldToViewportPoint(point);
        viewport.x = Mathf.Clamp01(viewport.x);
        viewport.y = Mathf.Clamp01(viewport.y);
        point = camera.ViewportToWorldPoint(viewport);
        point.z = 0f;
        return point;
    }

    public static Vector3 ResolveFlightStart(Vector3 target, Camera camera)
    {
        Vector3 approach = new(1f, 1f, 0f);
        if (camera == null) return target + approach * 12f;
        Vector3 viewport = camera.WorldToViewportPoint(target);
        Vector3 upperRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, viewport.z));
        // Follow the same 45-degree descent for every target. Extend beyond the
        // first screen edge reached, with enough margin to hide the whole meteor.
        float offset = Mathf.Max(3f, Mathf.Min(upperRight.x - target.x, upperRight.y - target.y) + 3f);
        return target + approach * offset;
    }

    private static void ApplyImpact(AbilitySystem system, AbilitySpec spec, CrimsonBoundaryChargeData data,
        CrimsonBoundaryRuntimeState runtime, Vector2 center, float diameter, bool full)
    {
        var shared = data.weaponData;
        // A real circle matching the warning. No wall raycast and no projectile hit callbacks.
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, diameter * 0.5f, shared.damageLayers);
        var seen = new HashSet<GameObject>();
        foreach (Collider2D hit in hits)
        {
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (target == null || target == system.gameObject || !seen.Add(target)) continue;
            Enemy enemy = target.GetComponent<Enemy>();
            bool wasAlive = enemy != null && !enemy.IsDead;
            Vector3 position = target.transform.position;
            float damage = CrimsonBoundaryUtility.CalculateDirectDamage(system,
                full ? data.fullDamageMultiplier : data.partialDamageMultiplier, out bool critical,
                out ElementDamageResult[] elementDamage);
            CrimsonBoundaryUtility.ApplyDamage(system, spec, shared.damageEffect, target, damage,
                critical, system.gameObject, elementDamage);
            BurnStatus2D.Apply(target, system, shared.damageEffect, system.gameObject,
                full ? data.fullBurnStacks : data.partialBurnStacks, shared.burnTickPrefab, shared.burnSustainPrefab);
            if (wasAlive && enemy != null && enemy.IsDead &&
                WeaponExclusiveRelics.Has(system.gameObject, WeaponExclusiveRelics.CrimsonKillShot))
                CrimsonBoundaryUtility.SpawnRelicKillShot(system, spec, shared, runtime, position, target);
        }
    }
}
