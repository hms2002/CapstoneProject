using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_ApprenticeHeroSwordDashStab", menuName = "GAS/Weapon/Apprentice Hero Sword/Logic Dash Stab")]
public sealed class AbilityLogic_ApprenticeHeroSwordDashStab : AbilityLogic
{
    public override bool RequestsParallelExecution(AbilitySystem system, AbilitySpec spec) =>
        system != null && WeaponExclusiveRelics.Has(system.gameObject, WeaponExclusiveRelics.ApprenticeChargeLink);

    private readonly Dictionary<AbilitySpec, List<MeleeHitboxActor>> activeHitboxesBySpec = new();

    private readonly Dictionary<AbilitySpec, (WeaponPresentationRig2D rig, int token)> aimLocks = new();

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null)
            yield break;

        ApprenticeHeroSwordDashStabData data = spec.Definition.sourceObject as ApprenticeHeroSwordDashStabData;
        if (data == null)
        {
            Debug.LogError("[ApprenticeHeroSwordDashStab] AbilityDefinition.sourceObject must be ApprenticeHeroSwordDashStabData.");
            yield break;
        }

        AbilityMotionController2D motion = system.GetComponent<AbilityMotionController2D>();
        if (motion == null)
        {
            Debug.LogError("[ApprenticeHeroSwordDashStab] AbilityMotionController2D is required.");
            yield break;
        }

        Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;
        direction.Normalize();

        float damageScale = 1f;
        Vector2 sizeScale = Vector2.one;
        Color chargeColor = Color.white;
        bool linkedCharge = false;
        AbilitySpec charge = system.CurrentExecSpec;
        if (RequestsParallelExecution(system, spec) && charge?.Definition?.logic is AbilityLogic_ApprenticeHeroSwordChargeSpin &&
            charge.GetInt(AbilityLogic_ApprenticeHeroSwordChargeSpin.HoldingChargeKey, 0) != 0 &&
            charge.Definition.sourceObject is ApprenticeHeroSwordChargeSpinData chargeData)
        {
            float seconds = Mathf.Clamp(charge.GetFloat(AbilityLogic_ApprenticeHeroSwordChargeSpin.ChargeSecondsKey, 0f),
                chargeData.MinChargeSeconds, chargeData.MaxChargeSeconds);
            float ratio = chargeData.MaxChargeSeconds > 0f ? Mathf.Clamp01(seconds / chargeData.MaxChargeSeconds) : 1f;
            // Transfer charge growth, not the charge attack's own base damage coefficient.
            damageScale = chargeData.MinDamageScale > 0f
                ? chargeData.ResolveDamageScale(seconds) / chargeData.MinDamageScale
                : 1f;
            sizeScale = chargeData.ResolveChargeReleaseSizeMultiplier(ratio);
            chargeColor = chargeData.ResolveChargeReleaseColor(ratio);
            linkedCharge = true;
        }

        try
        {
            float holdTime = Mathf.Max(data.DashDuration, data.Hitbox != null ? data.Hitbox.ActiveTime : 0f) + data.RecoveryDuration;
            BeginSkillAimLock(system, spec, direction, holdTime);
            TryPlayAnim(system, data.AnimationTrigger, spec.Definition);
            AbilityAudioRouter.PlayOneShot(data.DashStartSound, system, spec, sourceObjectOverride: data);
            if (IsAbilityCancelled(spec))
            {
                motion.CancelMotion();
                DestroyTrackedHitboxes(spec);
                yield break;
            }

            CombatHitPayload payload = ApprenticeHeroSwordHitUtility.BuildPayload(system, spec, data.Damage, damageScale);
            if (payload != null)
            {
                Vector2 center = (Vector2)system.transform.position + direction * data.ForwardOffset;
                MeleeHitboxActor hitbox = ApprenticeHeroSwordHitUtility.SpawnHitbox(
                    system,
                    spec,
                    data.Hitbox,
                    data.HitLayers,
                    payload,
                    center,
                    direction,
                    direction.x < 0f,
                    sizeScale, sizeScale, linkedCharge, chargeColor);

                TrackHitbox(spec, hitbox);
                AbilityAudioRouter.PlayOneShotAtPosition(data.StabSound, system, spec, center, data);
            }

            float dashSpeed = data.DashDistance / data.DashDuration;
            if (dashSpeed > 0f)
                motion.StartDash(direction, dashSpeed, data.DashDuration);

            float activeDuration = Mathf.Max(data.DashDuration, data.Hitbox != null ? data.Hitbox.ActiveTime : 0f);
            float elapsed = 0f;
            while (elapsed < activeDuration)
            {
                if (IsAbilityCancelled(spec))
                {
                    motion.CancelMotion();
                    DestroyTrackedHitboxes(spec);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (data.RecoveryDuration > 0f)
                spec.SetFloat("RecoveryOverride", data.RecoveryDuration);
        }
        finally
        {
            EndSkillAimLock(spec, IsAbilityCancelled(spec));
            if (IsAbilityCancelled(spec))
                DestroyTrackedHitboxes(spec);
            else
                ForgetTrackedHitboxes(spec);
        }
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        EndSkillAimLock(spec, cancelled: true);
        DestroyTrackedHitboxes(spec);
        AbilityMotionController2D motion = system != null ? system.GetComponent<AbilityMotionController2D>() : null;
        motion?.CancelMotion();
    }

    private void BeginSkillAimLock(AbilitySystem system, AbilitySpec spec, Vector2 direction, float holdTime)
    {
        EndSkillAimLock(spec, cancelled: true);
        var rig = system.GetComponentInChildren<WeaponPresentationRig2D>(true);
        if (rig == null) return;
        int token = rig.BeginAimPresentationOverride(WeaponAimPresentationMode.LockedAtCast, direction, holdTime);
        aimLocks[spec] = (rig, token);
    }

    private void EndSkillAimLock(AbilitySpec spec, bool cancelled)
    {
        if (spec == null || !aimLocks.TryGetValue(spec, out var entry)) return;
        aimLocks.Remove(spec);
        if (entry.rig == null) return;
        if (cancelled) entry.rig.CancelAimPresentationOverride(entry.token);
        else entry.rig.EndAimPresentationOverride(entry.token);
    }

    private void TrackHitbox(AbilitySpec spec, MeleeHitboxActor hitbox)
    {
        if (spec == null || hitbox == null)
            return;

        if (!activeHitboxesBySpec.TryGetValue(spec, out List<MeleeHitboxActor> hitboxes) || hitboxes == null)
        {
            hitboxes = new List<MeleeHitboxActor>();
            activeHitboxesBySpec[spec] = hitboxes;
        }

        hitboxes.Add(hitbox);
    }

    private void DestroyTrackedHitboxes(AbilitySpec spec)
    {
        if (spec == null || !activeHitboxesBySpec.TryGetValue(spec, out List<MeleeHitboxActor> hitboxes))
            return;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            MeleeHitboxActor hitbox = hitboxes[i];
            if (hitbox != null)
                Destroy(hitbox.gameObject);
        }

        activeHitboxesBySpec.Remove(spec);
    }

    private void ForgetTrackedHitboxes(AbilitySpec spec)
    {
        if (spec != null)
            activeHitboxesBySpec.Remove(spec);
    }

    private static void TryPlayAnim(AbilitySystem system, string animationTrigger, AbilityDefinition definition)
    {
        if (system == null || string.IsNullOrWhiteSpace(animationTrigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(animationTrigger), definition);
    }

    private static IEnumerator WaitForHitEvent(
        AbilitySystem system,
        AbilitySpec spec,
        ApprenticeHeroSwordDashStabData data)
    {
        if (system == null || spec == null || data == null || data.HitEventTag == null)
            yield break;

        float timeout = data.HitEventTimeout > 0f
            ? data.HitEventTimeout
            : data.DashDuration;

        yield return AbilityTasks.WaitGameplayEvent(
            system,
            spec,
            data.HitEventTag,
            onReceived: null,
            timeout: timeout,
            predicate: eventData => eventData.Spec == spec);
    }
}
