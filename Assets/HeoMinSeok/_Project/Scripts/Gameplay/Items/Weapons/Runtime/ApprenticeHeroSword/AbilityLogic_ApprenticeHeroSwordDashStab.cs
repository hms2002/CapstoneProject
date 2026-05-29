using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_ApprenticeHeroSwordDashStab", menuName = "GAS/Weapon/Apprentice Hero Sword/Logic Dash Stab")]
public sealed class AbilityLogic_ApprenticeHeroSwordDashStab : AbilityLogic
{
    private readonly Dictionary<AbilitySpec, List<MeleeHitboxActor>> activeHitboxesBySpec = new();

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

        TryPlayAnim(system, data.AnimationTrigger, spec.Definition);
        AbilityAudioRouter.PlayOneShot(data.DashStartSound, system, spec, sourceObjectOverride: data);

        float dashSpeed = data.DashDistance / data.DashDuration;
        if (dashSpeed > 0f)
            motion.StartDash(direction, dashSpeed, data.DashDuration);

        try
        {
            float hitEventWaitStart = Time.time;
            yield return WaitForHitEvent(system, spec, data);
            float hitEventWaitElapsed = Mathf.Max(0f, Time.time - hitEventWaitStart);

            if (IsAbilityCancelled(spec))
            {
                motion.CancelMotion();
                DestroyTrackedHitboxes(spec);
                yield break;
            }

            CombatHitPayload payload = ApprenticeHeroSwordHitUtility.BuildPayload(system, spec, data.Damage, 1f);
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
                    direction.x < 0f);

                TrackHitbox(spec, hitbox);
                AbilityAudioRouter.PlayOneShotAtPosition(data.StabSound, system, spec, center, data);
            }

            float remainingDashDuration = Mathf.Max(0f, data.DashDuration - hitEventWaitElapsed);
            float activeDuration = Mathf.Max(remainingDashDuration, data.Hitbox != null ? data.Hitbox.ActiveTime : 0f);
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
            if (IsAbilityCancelled(spec))
                DestroyTrackedHitboxes(spec);
            else
                ForgetTrackedHitboxes(spec);
        }
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        DestroyTrackedHitboxes(spec);
        AbilityMotionController2D motion = system != null ? system.GetComponent<AbilityMotionController2D>() : null;
        motion?.CancelMotion();
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
