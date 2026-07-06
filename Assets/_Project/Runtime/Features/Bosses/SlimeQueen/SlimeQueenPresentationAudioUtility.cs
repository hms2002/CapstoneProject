using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

/// <summary>
/// 책임:
/// - 슬라임 퀸 패턴 코드가 기존 오디오/프레젠테이션 인프라를 일관된 문맥으로 호출하게 돕는다.
/// - 패턴 host가 AudioSource를 직접 다루지 않도록 SoundRef와 WorldPresentationHook 실행만 캡슐화한다.
/// </summary>
internal static class SlimeQueenPresentationAudioUtility
{
    public static void PlaySound(
        in SoundRef sound,
        GameObject instigator,
        Vector3 position,
        Object sourceObject,
        GameObject target = null,
        GameObject causer = null)
    {
        if (!sound.IsSet)
            return;

        SoundPlaybackUtility.Play(
            sound,
            instigator,
            causer != null ? causer : instigator,
            target,
            position,
            sourceObject);
    }

    public static void PlayPresentation(
        in WorldPresentationHook presentation,
        GameObject instigator,
        Vector3 position,
        Object sourceObject,
        GameObject target = null,
        GameObject causer = null,
        Vector3? fallbackDirection = null)
    {
        if (!presentation.HasAnyContent)
            return;

        WorldPresentationContext context = WorldPresentationContext.AtWorld(
            instigator,
            position,
            fallbackDirection ?? Vector3.up,
            target,
            sourceObject,
            causer: causer);

        WorldPresentationPlayback.PlaySignalOnly(presentation, context);
        SpawnVisualWithFallback(presentation.effect, context);
        SpawnVisualWithFallback(presentation.particle, context);
    }

    private static void SpawnVisualWithFallback(in SpawnedPresentationHook hook, in WorldPresentationContext context)
    {
        if (!hook.HasContent)
            return;

        GameObject spawned = WorldPresentationPlayback.SpawnOneShot(hook, context);
        if (spawned != null)
            return;

        GameObject instance = Object.Instantiate(
            hook.prefab,
            context.Position + context.Rotation * hook.localOffset,
            context.Rotation * Quaternion.Euler(0f, 0f, hook.rotationOffsetZ));

        if (instance == null)
            return;

        Vector3 initialScale = instance.transform.localScale == Vector3.zero ? Vector3.one : instance.transform.localScale;
        instance.transform.localScale = Vector3.Scale(initialScale, hook.EffectiveScaleMultiplier);

        if (hook.attachToTarget && context.Target != null)
            instance.transform.SetParent(context.Target.transform, worldPositionStays: true);

        WorldPresentationPlayback.InitializeSpawnedPresentation(instance, hook.useUnscaledTime);

        float lifetimeSeconds = ResolveFallbackLifetime(instance, hook);
        if (lifetimeSeconds > 0f)
            Object.Destroy(instance, lifetimeSeconds);
    }

    private static float ResolveFallbackLifetime(GameObject instance, in SpawnedPresentationHook hook)
    {
        if (hook.lifetimeMode == PresentationLifetimeMode.ManualRelease)
            return 0f;

        if (hook.lifetimeOverrideSeconds > 0f)
            return hook.lifetimeOverrideSeconds;

        if (hook.lifetimeMode == PresentationLifetimeMode.FixedSeconds)
            return 0f;

        float lifetimeSeconds = 0f;
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            if (main.loop)
                lifetimeSeconds = Mathf.Max(lifetimeSeconds, 1f);
            else
                lifetimeSeconds = Mathf.Max(
                    lifetimeSeconds,
                    ResolveCurveMax(main.startDelay) + main.duration + ResolveCurveMax(main.startLifetime) + 0.25f);
        }

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
                if (clip != null)
                    lifetimeSeconds = Mathf.Max(lifetimeSeconds, clip.length + 0.05f);
            }
        }

        return lifetimeSeconds > 0f ? lifetimeSeconds : 1f;
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
