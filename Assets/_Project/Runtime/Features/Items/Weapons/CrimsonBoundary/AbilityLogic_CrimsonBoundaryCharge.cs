using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryCharge", menuName = "GAS/Weapon/Crimson Boundary/Charge Logic")]
public sealed class AbilityLogic_CrimsonBoundaryCharge : AbilityLogic
{
    private const float CursorFollowSeconds = 0.06f;
    private const float MaximumCursorLag = 0.75f;
    // Equilateral triangle with one radius between centers, expressed in units of circle radius.
    private static readonly float TriangleHeight = Mathf.Sqrt(3f) * 0.5f;
    private static readonly Vector3[] RelicOffsets =
    {
        Vector3.up * (2f * TriangleHeight / 3f),
        new(-0.5f, -TriangleHeight / 3f, 0f),
        new(0.5f, -TriangleHeight / 3f, 0f)
    };

    private struct ChargeCircle
    {
        public GameObject Instance;
        public ICrimsonBoundaryChargePresentation View;
        public Vector3 Target;
        public Vector3 FlightStart;
    }

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

        bool triple = WeaponExclusiveRelics.Has(system.gameObject, WeaponExclusiveRelics.CrimsonKillShot);
        var circles = new ChargeCircle[triple ? RelicOffsets.Length : 1];
        bool handedOff = false;
        runtime.CancelChargePresentation();
        try
        {
            for (int i = 0; i < circles.Length; i++)
            {
                var instance = Object.Instantiate(data.presentationPrefab);
                circles[i].Instance = instance;
                runtime.TrackChargePresentation(instance);
                var view = instance.GetComponent<ICrimsonBoundaryChargePresentation>();
                if (view == null)
                {
                    Debug.LogError("[CrimsonCharge] Prefab must implement ICrimsonBoundaryChargePresentation.");
                    yield break;
                }
                view.BindOwner(runtime, i == 0, triple ? (Vector2)RelicOffsets[i] : Vector2.zero);
                circles[i].View = view;
            }
            float charge = 0f;
            float diameter;
            Vector3 aim = ResolveTarget(system, Camera.main);
            while (true)
            {
                if (Cancelled(system, spec, runtime, circles)) yield break;
                Camera camera = Camera.main;
                aim = ClampTargetToViewport(DampChargeTarget(aim, ResolveTarget(system, camera), Time.deltaTime), camera);
                diameter = data.ChargeDiameter(charge);
                float ratio = data.ChargeRatio(charge);
                for (int i = 0; i < circles.Length; i++)
                {
                    Vector3 center = triple ? aim + RelicOffsets[i] * (diameter * 0.5f) : aim;
                    circles[i].Target = center;
                    circles[i].View.ShowCharge(system.transform.position, center, ratio, diameter);
                }
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
            Camera releaseCamera = Camera.main;
            for (int i = 0; i < circles.Length; i++)
            {
                circles[i].FlightStart = ResolveFlightStart(circles[i].Target, releaseCamera);
                circles[i].View.BeginFlight(circles[i].FlightStart, circles[i].Target);
            }
            var shared = data.weaponData;
            AbilityAudioRouter.PlayOneShotAtPosition(shared.lavaBallSummonSound, system, spec, aim, data);
            float flight = 0f;
            while (flight < data.flightSeconds)
            {
                if (Cancelled(system, spec, runtime, circles)) yield break;
                float progress = flight / data.flightSeconds;
                foreach (var circle in circles)
                    circle.View.MoveMeteor(Vector3.Lerp(circle.FlightStart, circle.Target, progress));
                yield return null;
                flight += Time.deltaTime;
            }
            if (Cancelled(system, spec, runtime, circles)) yield break;
            foreach (var circle in circles)
            {
                circle.View.MoveMeteor(circle.Target);
                circle.View.PlayImpact(circle.Target, diameter);
            }
            handedOff = true;
            AbilityAudioRouter.PlayOneShotAtPosition(shared.lavaBallHitSound, system, spec, aim, data);
            shared.lavaBallHitShake.TryPlay(system.gameObject, Vector3.down, debugReason: "Crimson charged impact");
            foreach (var circle in circles)
                ApplyImpact(system, spec, data, circle.Target, diameter, full);
        }
        finally
        {
            foreach (var circle in circles)
            {
                if (runtime != null) runtime.ReleaseChargePresentation(circle.Instance);
                if (!handedOff && circle.Instance != null) Object.Destroy(circle.Instance);
            }
        }
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        CrimsonBoundaryUtility.ResolveRuntimeState(system)?.CancelChargePresentation();
    }

    private static bool Cancelled(AbilitySystem system, AbilitySpec spec, CrimsonBoundaryRuntimeState runtime, ChargeCircle[] circles)
    {
        if (system == null || !system.isActiveAndEnabled || IsAbilityCancelled(spec) ||
            runtime == null || !runtime.isActiveAndEnabled) return true;
        foreach (var circle in circles)
            if (circle.Instance == null || !circle.Instance.activeSelf) return true;
        return false;
    }

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
        Vector2 center, float diameter, bool full)
    {
        var shared = data.weaponData;
        // A real circle matching the warning. No wall raycast and no projectile hit callbacks.
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, diameter * 0.5f, shared.damageLayers);
        var seen = new HashSet<GameObject>();
        foreach (Collider2D hit in hits)
        {
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (target == null || target == system.gameObject || !seen.Add(target)) continue;
            if (target.TryGetComponent(out Enemy enemy) && enemy.IsDead) continue;
            float damage = CrimsonBoundaryUtility.CalculateDirectDamage(system,
                full ? data.fullDamageMultiplier : data.partialDamageMultiplier, out bool critical,
                out ElementDamageResult[] elementDamage);
            CrimsonBoundaryUtility.ApplyDamage(system, spec, shared.damageEffect, target, damage,
                critical, system.gameObject, elementDamage);
            BurnStatus2D.Apply(target, system, shared.damageEffect, system.gameObject,
                full ? data.fullBurnStacks : data.partialBurnStacks, shared.burnTickPrefab, shared.burnSustainPrefab);
        }
    }
}
