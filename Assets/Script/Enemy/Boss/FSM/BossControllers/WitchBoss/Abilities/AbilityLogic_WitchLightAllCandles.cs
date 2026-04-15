using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public class AbilityLogic_WitchLightAllCandles : AbilityLogic
{
    private const float MoveToCenterDuration = 0.45f;
    private const float RelightDeadlineSeconds = 8f;
    private const float ShieldBreakWaitGraceSeconds = 1.5f;

    [Header("Charge Orb")]
    [SerializeField] private GameObject chargeOrbPrefab;
    [SerializeField] private Vector3 chargeOrbLocalOffset = new Vector3(0f, 1.35f, -0.05f);
    [SerializeField] private Vector3 chargeOrbStartScale = new Vector3(0.25f, 0.25f, 1f);
    [SerializeField] private Vector3 chargeOrbEndScale = new Vector3(1.35f, 1.35f, 1f);
    [SerializeField] private bool followWitchDuringCharge = true;
    [SerializeField] [Min(0.01f)] private float chargeOrbFailureDropDuration = 0.22f;
    [SerializeField] private Vector3 chargeOrbImpactLocalOffset = new Vector3(0f, 0.1f, -0.05f);

    [Header("Charge Presentation")]
    [SerializeField] private SoundRef chargeLoopSound;
    [SerializeField] [Min(0f)] private float chargeLoopFadeOutSeconds = 0.1f;
    [SerializeField] private WorldPresentationHook chargePulsePresentation;
    [SerializeField] private WorldPresentationHook orbLaunchPresentation;
    [HideInInspector, FormerlySerializedAs("orbLaunchSound")]
    [SerializeField] private SoundRef legacyOrbLaunchSound;
    [HideInInspector, FormerlySerializedAs("chargeLoopCameraShake")]
    [SerializeField] private CameraShakeHook legacyChargeLoopCameraShake = CameraShakeHook.Create(0.03f, 1f, 0.08f, 0.08f);

    [Header("Failure Presentation")]
    [SerializeField] private WorldPresentationHook failureImpactPresentation;
    [HideInInspector, FormerlySerializedAs("orbImpactSound")]
    [SerializeField] private SoundRef legacyOrbImpactSound;
    [HideInInspector, FormerlySerializedAs("failureEffectPrefab")]
    [SerializeField] private GameObject legacyFailureEffectPrefab;
    [HideInInspector, FormerlySerializedAs("failureEffectLocalOffset")]
    [SerializeField] private Vector3 legacyFailureEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [HideInInspector, FormerlySerializedAs("failureEffectLifetimeSeconds")]
    [SerializeField] private float legacyFailureEffectLifetimeSeconds = 0.35f;
    [HideInInspector, FormerlySerializedAs("failureEffectScaleMultiplier")]
    [SerializeField] private Vector3 legacyFailureEffectScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("failureEffectRotationOffsetZ")]
    [SerializeField] private float legacyFailureEffectRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("failureParticlePrefab")]
    [SerializeField] private GameObject legacyFailureParticlePrefab;
    [HideInInspector, FormerlySerializedAs("failureParticleLocalOffset")]
    [SerializeField] private Vector3 legacyFailureParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [HideInInspector, FormerlySerializedAs("failureParticleLifetimeOverrideSeconds")]
    [SerializeField] private float legacyFailureParticleLifetimeOverrideSeconds;
    [HideInInspector, FormerlySerializedAs("useUnscaledFailureParticleTime")]
    [SerializeField] private bool legacyUseUnscaledFailureParticleTime;
    [HideInInspector, FormerlySerializedAs("failureParticleScaleMultiplier")]
    [SerializeField] private Vector3 legacyFailureParticleScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("failureParticleRotationOffsetZ")]
    [SerializeField] private float legacyFailureParticleRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("failureCameraShake")]
    [SerializeField] private CameraShakeHook legacyFailureCameraShake = CameraShakeHook.Create(0.24f, 1f, 0.36f, 0.05f);

    private readonly Dictionary<int, AudioHandle> activeChargeLoopHandles = new();

    private void OnValidate()
    {
        MigrateLegacyChargePresentations();
        MigrateLegacyFailurePresentation();
    }

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        MigrateLegacyChargePresentations();
        MigrateLegacyFailurePresentation();

        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        if (witch == null)
            yield break;

        witch.PlayPatternAttackMotion();
        witch.MoveToPhaseTransitionCenter(MoveToCenterDuration);
        witch.ActivateShield();
        witch.EnableStaggerImmuneDuringPhaseTransition();
        CameraPresentationDirector phaseCameraDirector = witch.GetCameraPresentationDirector();
        bool shouldReturnPhaseCamera = false;
        if (phaseCameraDirector != null)
        {
            phaseCameraDirector.BeginBossFocusWithPhaseLens();
            shouldReturnPhaseCamera = true;
        }

        yield return new WaitForSeconds(MoveToCenterDuration);

        Vector3 center = witch.GetPhaseTransitionCenter();
        witch.SpeakSituation(BossSpeechSituationEnum.UltimateWarning);
        witch.ShowMapWideWarning(center, RelightDeadlineSeconds);
        witch.SealAllCandles();

        GameObject chargeOrbInstance = SpawnChargeOrb(witch);
        BeginChargeLoopSound(witch);
        float chargeStartTime = Time.time;
        float deadlineTime = chargeStartTime + RelightDeadlineSeconds;
        bool allCandlesRelit = false;

        while (Time.time < deadlineTime)
        {
            float chargeProgress = Mathf.InverseLerp(chargeStartTime, deadlineTime, Time.time);
            UpdateChargeOrb(witch, chargeOrbInstance, chargeProgress);
            WorldPresentationRuntime.PlaySignalOnly(
                chargePulsePresentation,
                WorldPresentationContext.AtWorld(
                    instigator: witch.gameObject,
                    position: witch.transform.position,
                    fallbackDirection: Vector3.up,
                    target: null,
                    sourceObject: this,
                    rotation: Quaternion.identity,
                    causer: witch.gameObject));

            if (!witch.HasAnySealedCandles())
            {
                allCandlesRelit = true;
                break;
            }

            yield return null;
        }

        if (allCandlesRelit)
        {
            StopChargeLoopSound(witch);
            CleanupChargeOrb(chargeOrbInstance);

            float shieldBreakDeadline = Time.time + ShieldBreakWaitGraceSeconds;

            while (witch.ShieldController != null && witch.ShieldController.HasShield && Time.time < shieldBreakDeadline)
                yield return null;

            if (witch.ShieldController != null && witch.ShieldController.HasShield)
                witch.BreakShield();

            witch.DisableStaggerImmuneDuringPhaseTransition();
            witch.HideMapWideWarning();
            witch.ApplyGroggyStatus();

            if (shouldReturnPhaseCamera)
                yield return phaseCameraDirector.ReturnToPlayerRoutine();

            yield break;
        }

        if (witch.HasAnySealedCandles())
        {
            StopChargeLoopSound(witch);
            yield return PlayFailurePresentation(witch, chargeOrbInstance);
            witch.DisableStaggerImmuneDuringPhaseTransition();
            witch.ClearShield();
            witch.HideMapWideWarning();
            witch.ApplyMapWideDamage(initialTarget);

            if (shouldReturnPhaseCamera)
                yield return phaseCameraDirector.ReturnToPlayerRoutine();

            yield break;
        }

        StopChargeLoopSound(witch);
        CleanupChargeOrb(chargeOrbInstance);
        witch.HideMapWideWarning();

        if (shouldReturnPhaseCamera)
            yield return phaseCameraDirector.ReturnToPlayerRoutine();
    }

    private GameObject SpawnChargeOrb(Witch witch)
    {
        if (witch == null || chargeOrbPrefab == null)
            return null;

        Vector3 spawnPosition = ResolveChargeOrbPosition(witch);
        GameObject instance = Object.Instantiate(chargeOrbPrefab, spawnPosition, chargeOrbPrefab.transform.rotation);
        if (instance == null)
            return null;

        instance.transform.localScale = chargeOrbStartScale;
        WorldPresentationRuntime.InitializeSpawnedPresentation(instance, useUnscaledTime: false);
        return instance;
    }

    private void UpdateChargeOrb(Witch witch, GameObject chargeOrbInstance, float chargeProgress)
    {
        if (witch == null || chargeOrbInstance == null)
            return;

        if (followWitchDuringCharge)
            chargeOrbInstance.transform.position = ResolveChargeOrbPosition(witch);

        chargeOrbInstance.transform.localScale = Vector3.LerpUnclamped(
            chargeOrbStartScale,
            chargeOrbEndScale,
            Mathf.Clamp01(chargeProgress));
    }

    private IEnumerator PlayFailurePresentation(Witch witch, GameObject chargeOrbInstance)
    {
        Vector3 impactPosition = ResolveChargeOrbImpactPosition(witch);

        WorldPresentationRuntime.PlaySignalOnly(
            orbLaunchPresentation,
            WorldPresentationContext.AtWorld(
                instigator: witch != null ? witch.gameObject : null,
                position: chargeOrbInstance != null ? chargeOrbInstance.transform.position : impactPosition,
                fallbackDirection: Vector3.down,
                target: null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: witch != null ? witch.gameObject : null));

        if (chargeOrbInstance != null)
        {
            Vector3 startPosition = chargeOrbInstance.transform.position;
            Vector3 startScale = chargeOrbInstance.transform.localScale;
            float elapsed = 0f;

            while (elapsed < chargeOrbFailureDropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / chargeOrbFailureDropDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                chargeOrbInstance.transform.position = Vector3.Lerp(startPosition, impactPosition, eased);
                chargeOrbInstance.transform.localScale = Vector3.LerpUnclamped(startScale, chargeOrbEndScale, eased);
                yield return null;
            }
        }

        CleanupChargeOrb(chargeOrbInstance);

        WorldPresentationRuntime.Play(
            failureImpactPresentation,
            WorldPresentationContext.AtWorld(
                instigator: witch != null ? witch.gameObject : null,
                position: impactPosition,
                fallbackDirection: Vector3.down,
                target: null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: witch != null ? witch.gameObject : null));
    }

    public void StopChargeLoopFor(Witch witch)
    {
        StopChargeLoopSound(witch);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        base.CleanupForSceneTransition(system, spec, target);

        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        StopChargeLoopSound(witch);
        witch?.HideMapWideWarning();
    }

    private Vector3 ResolveChargeOrbPosition(Witch witch)
    {
        return witch != null
            ? witch.transform.TransformPoint(chargeOrbLocalOffset)
            : chargeOrbLocalOffset;
    }

    private Vector3 ResolveChargeOrbImpactPosition(Witch witch)
    {
        return witch != null
            ? witch.transform.TransformPoint(chargeOrbImpactLocalOffset)
            : chargeOrbImpactLocalOffset;
    }

    private static void CleanupChargeOrb(GameObject chargeOrbInstance)
    {
        if (chargeOrbInstance != null)
            Object.Destroy(chargeOrbInstance);
    }

    private void BeginChargeLoopSound(Witch witch)
    {
        if (witch == null || !chargeLoopSound.IsSet)
            return;

        int key = witch.GetInstanceID();
        StopChargeLoopSound(witch);
        activeChargeLoopHandles[key] = SoundPlaybackUtility.Play(
            chargeLoopSound,
            instigator: witch.gameObject,
            causer: witch.gameObject,
            position: witch.transform.position,
            sourceObject: this);
    }

    private void StopChargeLoopSound(Witch witch)
    {
        if (witch == null)
            return;

        int key = witch.GetInstanceID();
        if (!activeChargeLoopHandles.TryGetValue(key, out AudioHandle handle))
            return;

        activeChargeLoopHandles.Remove(key);
        SoundPlaybackUtility.Stop(handle, chargeLoopFadeOutSeconds);
    }

    private void MigrateLegacyFailurePresentation()
    {
        if (legacyFailureEffectPrefab != null && !failureImpactPresentation.effect.HasContent)
        {
            failureImpactPresentation.effect.prefab = legacyFailureEffectPrefab;
            failureImpactPresentation.effect.localOffset = legacyFailureEffectLocalOffset;
            failureImpactPresentation.effect.rotationOffsetZ = legacyFailureEffectRotationOffsetZ;
            failureImpactPresentation.effect.scaleMultiplier = legacyFailureEffectScaleMultiplier;
            failureImpactPresentation.effect.lifetimeOverrideSeconds = legacyFailureEffectLifetimeSeconds;
        }

        if (legacyFailureParticlePrefab != null && !failureImpactPresentation.particle.HasContent)
        {
            failureImpactPresentation.particle.prefab = legacyFailureParticlePrefab;
            failureImpactPresentation.particle.localOffset = legacyFailureParticleLocalOffset;
            failureImpactPresentation.particle.rotationOffsetZ = legacyFailureParticleRotationOffsetZ;
            failureImpactPresentation.particle.scaleMultiplier = legacyFailureParticleScaleMultiplier;
            failureImpactPresentation.particle.lifetimeOverrideSeconds = legacyFailureParticleLifetimeOverrideSeconds;
            failureImpactPresentation.particle.useUnscaledTime = legacyUseUnscaledFailureParticleTime;
        }

        if (!failureImpactPresentation.HasSound && legacyOrbImpactSound.IsSet)
            failureImpactPresentation.sound = legacyOrbImpactSound;

        if (!failureImpactPresentation.HasShake && legacyFailureCameraShake.amplitude > 0f)
            failureImpactPresentation.cameraShake = legacyFailureCameraShake;
    }

    private void MigrateLegacyChargePresentations()
    {
        if (!orbLaunchPresentation.HasSound && legacyOrbLaunchSound.IsSet)
            orbLaunchPresentation.sound = legacyOrbLaunchSound;

        if (!chargePulsePresentation.HasShake && legacyChargeLoopCameraShake.amplitude > 0f)
            chargePulsePresentation.cameraShake = legacyChargeLoopCameraShake;
    }

}
