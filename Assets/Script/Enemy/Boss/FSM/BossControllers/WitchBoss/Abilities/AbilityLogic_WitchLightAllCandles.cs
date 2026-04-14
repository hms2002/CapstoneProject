using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public class AbilityLogic_WitchLightAllCandles : AbilityLogic
{
    private const float MoveToCenterDuration = 0.45f;
    private const float RelightDeadlineSeconds = 8f;
    private const float ShieldBreakWaitGraceSeconds = 1.5f;
    private const float DefaultPresentationLifetimeSeconds = 1f;

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
    [SerializeField] private SoundRef orbLaunchSound;
    [SerializeField] private SoundRef orbImpactSound;
    [SerializeField] private CameraShakeHook chargeLoopCameraShake = CameraShakeHook.Create(0.03f, 1f, 0.08f, 0.08f);

    [Header("Failure Presentation")]
    [SerializeField] private GameObject failureEffectPrefab;
    [SerializeField] private Vector3 failureEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [SerializeField] [Min(0f)] private float failureEffectLifetimeSeconds = 0.35f;
    [SerializeField] private Vector3 failureEffectScaleMultiplier = Vector3.one;
    [SerializeField] private float failureEffectRotationOffsetZ;
    [SerializeField] private GameObject failureParticlePrefab;
    [SerializeField] private Vector3 failureParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [SerializeField] [Min(0f)] private float failureParticleLifetimeOverrideSeconds = 0f;
    [SerializeField] private bool useUnscaledFailureParticleTime;
    [SerializeField] private Vector3 failureParticleScaleMultiplier = Vector3.one;
    [SerializeField] private float failureParticleRotationOffsetZ;
    [SerializeField] private CameraShakeHook failureCameraShake = CameraShakeHook.Create(0.24f, 1f, 0.36f, 0.05f);

    private readonly Dictionary<int, AudioHandle> activeChargeLoopHandles = new();

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
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
            chargeLoopCameraShake.TryPlay(
                source: witch.gameObject,
                fallbackDirection: Vector3.up,
                debugReason: "WitchLightAllCandles.Charging");

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
        ConfigureSpawnedPresentation(instance, useUnscaledTime: false);
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

        SoundPlaybackUtility.Play(
            orbLaunchSound,
            instigator: witch != null ? witch.gameObject : null,
            causer: witch != null ? witch.gameObject : null,
            position: chargeOrbInstance != null ? chargeOrbInstance.transform.position : impactPosition,
            sourceObject: this);

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

        SoundPlaybackUtility.Play(
            orbImpactSound,
            instigator: witch != null ? witch.gameObject : null,
            causer: witch != null ? witch.gameObject : null,
            position: impactPosition,
            sourceObject: this);

        SpawnPresentationPrefab(
            failureEffectPrefab,
            impactPosition + failureEffectLocalOffset,
            failureEffectRotationOffsetZ,
            failureEffectScaleMultiplier,
            failureEffectLifetimeSeconds,
            useUnscaledTime: false);
        SpawnPresentationPrefab(
            failureParticlePrefab,
            impactPosition + failureParticleLocalOffset,
            failureParticleRotationOffsetZ,
            failureParticleScaleMultiplier,
            failureParticleLifetimeOverrideSeconds,
            useUnscaledFailureParticleTime);

        failureCameraShake.TryPlay(
            source: witch != null ? witch.gameObject : null,
            fallbackDirection: Vector3.down,
            debugReason: "WitchLightAllCandles.FailImpact");
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

    private static void SpawnPresentationPrefab(
        GameObject prefab,
        Vector3 position,
        float rotationOffsetZ,
        Vector3 scaleMultiplier,
        float lifetimeOverrideSeconds,
        bool useUnscaledTime)
    {
        if (prefab == null)
            return;

        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationOffsetZ);
        GameObject instance = Object.Instantiate(prefab, position, rotation);
        if (instance == null)
            return;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
        ConfigureSpawnedPresentation(instance, useUnscaledTime);

        float lifetime = ResolvePresentationLifetime(instance, lifetimeOverrideSeconds);
        if (lifetime > 0f)
            Object.Destroy(instance, lifetime);
    }

    private static void ConfigureSpawnedPresentation(GameObject instance, bool useUnscaledTime)
    {
        if (instance == null)
            return;

        instance.SetActive(true);

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (useUnscaledTime)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            particleSystem.Play(withChildren: true);
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            animationComponent.Play();
        }
    }

    private static float ResolvePresentationLifetime(GameObject instance, float lifetimeOverrideSeconds)
    {
        if (lifetimeOverrideSeconds > 0f)
            return lifetimeOverrideSeconds;

        float particleLifetime = ResolveParticleLifetime(instance);
        if (particleLifetime > 0f)
            return particleLifetime;

        float animationLifetime = ResolveAnimatorLifetime(instance);
        if (animationLifetime > 0f)
            return animationLifetime;

        return DefaultPresentationLifetimeSeconds;
    }

    private static float ResolveParticleLifetime(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        if (particleSystems == null || particleSystems.Length == 0)
            return 0f;

        float maxLifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            if (main.loop)
                return DefaultPresentationLifetimeSeconds;

            float startDelay = ResolveCurveMax(main.startDelay);
            float startLifetime = ResolveCurveMax(main.startLifetime);
            maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
        }

        return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
    }

    private static float ResolveAnimatorLifetime(GameObject instance)
    {
        float maxLifetime = 0f;

        Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                if (clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, clip.length);
            }
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            foreach (AnimationState state in animationComponent)
            {
                if (state?.clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, state.clip.length);
            }
        }

        return maxLifetime > 0f ? maxLifetime + 0.05f : 0f;
    }

    private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
            _ => Mathf.Max(curve.constant, curve.constantMax)
        };
    }
}
