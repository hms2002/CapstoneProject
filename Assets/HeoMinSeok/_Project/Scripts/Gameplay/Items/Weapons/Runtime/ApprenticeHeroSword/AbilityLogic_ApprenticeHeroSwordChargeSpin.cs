using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_ApprenticeHeroSwordChargeSpin", menuName = "GAS/Weapon/Apprentice Hero Sword/Logic Charge Spin")]
public sealed class AbilityLogic_ApprenticeHeroSwordChargeSpin : AbilityLogic
{
    private readonly Dictionary<AbilitySpec, List<MeleeHitboxActor>> activeHitboxesBySpec = new();

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null)
            yield break;

        ApprenticeHeroSwordChargeSpinData data = spec.Definition.sourceObject as ApprenticeHeroSwordChargeSpinData;
        if (data == null)
        {
            Debug.LogError("[ApprenticeHeroSwordChargeSpin] AbilityDefinition.sourceObject must be ApprenticeHeroSwordChargeSpinData.");
            yield break;
        }

        TryPlayAnim(system, data.ChargeAnimationTrigger, spec.Definition);
        AbilityAudioRouter.PlayOneShot(data.ChargeStartSound, system, spec, sourceObjectOverride: data);

        InputBindingService input = InputBindingService.EnsureInstance();
        if (input == null)
        {
            Debug.LogError("[ApprenticeHeroSwordChargeSpin] InputBindingService is required for hold-release timing.");
            yield break;
        }

        float chargeElapsed = 0f;
        while (true)
        {
            if (IsAbilityCancelled(spec))
            {
                DestroyTrackedHitboxes(spec);
                yield break;
            }

            bool released = input.WasReleasedThisFrame(InputActionId.Skill2) || !input.IsPressed(InputActionId.Skill2);
            if (released)
                break;

            chargeElapsed += Time.deltaTime;
            yield return null;
        }

        float effectiveChargeSeconds = Mathf.Clamp(chargeElapsed, data.MinChargeSeconds, data.MaxChargeSeconds);
        float chargeRatio = data.MaxChargeSeconds > 0f
            ? Mathf.Clamp01(effectiveChargeSeconds / data.MaxChargeSeconds)
            : 1f;
        float damageScale = Mathf.Lerp(data.MinDamageScale, data.MaxDamageScale, chargeRatio);

        TryPlayAnim(system, data.ReleaseAnimationTrigger, spec.Definition);
        AbilityAudioRouter.PlayOneShot(data.ReleaseSound, system, spec, sourceObjectOverride: data);

        Vector2 baseDirection = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        if (baseDirection.sqrMagnitude <= 0.0001f)
            baseDirection = Vector2.right;
        baseDirection.Normalize();

        try
        {
            if (IsAbilityCancelled(spec))
            {
                DestroyTrackedHitboxes(spec);
                yield break;
            }

            Vector2 center = system.transform.position;
            CombatHitPayload payload = ApprenticeHeroSwordHitUtility.BuildPayload(system, spec, data.Damage, damageScale);
            MeleeHitboxActor hitbox = ApprenticeHeroSwordHitUtility.SpawnHitbox(
                system,
                spec,
                data.Hitbox,
                data.HitLayers,
                payload,
                center,
                baseDirection,
                baseDirection.x < 0f);

            TrackHitbox(spec, hitbox);
            AbilityAudioRouter.PlayOneShotAtPosition(data.PulseSound, system, spec, center, data);

            float elapsed = 0f;
            while (elapsed < data.SpinDuration)
            {
                if (IsAbilityCancelled(spec))
                {
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
}
