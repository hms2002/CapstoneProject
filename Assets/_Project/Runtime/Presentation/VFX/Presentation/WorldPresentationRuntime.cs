using System.Collections;
using CapstoneAudio;
using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: Core presentation 요청을 사운드, 카메라 흔들림, prefab spawn 실행으로 변환하는 Infrastructure 런타임 실행기이다.
    /// </summary>
    public static class WorldPresentationRuntime
    {
        private static readonly IWorldPresentationBackend s_playbackBackend = new WorldPresentationRuntimeBackend();

        /// <summary>
        /// 책임: Core의 WorldPresentationPlayback 요청을 실제 WorldPresentationRuntime 실행으로 연결한다.
        /// </summary>
        private sealed class WorldPresentationRuntimeBackend : IWorldPresentationBackend
        {
            public void Play(in WorldPresentationHook hook, in WorldPresentationContext context)
            {
                WorldPresentationRuntime.Play(hook, context);
            }

            public void PlaySignalOnly(in WorldPresentationHook hook, in WorldPresentationContext context)
            {
                WorldPresentationRuntime.PlaySignalOnly(hook, context);
            }

            public void PlayMerged(
                in WorldPresentationHook hook,
                SoundRef legacySound,
                CameraShakeHook legacyShake,
                in WorldPresentationContext context)
            {
                WorldPresentationRuntime.PlayMerged(hook, legacySound, legacyShake, context);
            }

            public Coroutine PlayDeferredAsync(in WorldPresentationHook hook, in WorldPresentationContext context)
            {
                return WorldPresentationRuntime.PlayDeferredAsync(hook, context);
            }

            public GameObject SpawnOneShot(in SpawnedPresentationHook hook, in WorldPresentationContext context)
            {
                return PresentationSpawnService.SpawnOneShot(hook, context);
            }

            public Coroutine SpawnOneShotDeferredAsync(in SpawnedPresentationHook hook, in WorldPresentationContext context)
            {
                return WorldPresentationRuntime.SpawnVisualDeferredAsync(hook, context);
            }

            public GameObject SpawnPersistent(in SpawnedPresentationHook hook, in WorldPresentationContext context)
            {
                return PresentationSpawnService.SpawnPersistent(hook, context);
            }

            public void InitializeSpawnedPresentation(GameObject instance, bool useUnscaledTime)
            {
                WorldPresentationRuntime.InitializeSpawnedPresentation(instance, useUnscaledTime);
            }

            public void Release(GameObject instance)
            {
                PresentationSpawnService.Release(instance);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterPlaybackBackend()
        {
            WorldPresentationPlayback.RegisterBackend(s_playbackBackend);
        }

        public static void Play(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            if (!hook.HasAnyContent)
                return;

            PlaySounds(hook, context);
            PlayShake(hook.cameraShake, context);
            SpawnVisual(hook.effect, context);
            SpawnVisual(hook.particle, context);
        }

        public static void Play(in CueRef cueRef, in WorldPresentationContext context)
        {
            if (!TryResolveCue(cueRef, out WorldPresentationHook resolvedHook))
                return;

            Play(resolvedHook, context);
        }

        public static void Play(in PresentationReference reference, in WorldPresentationContext context)
        {
            if (!TryResolve(reference, out WorldPresentationHook resolvedHook))
                return;

            Play(resolvedHook, context);
        }

        public static void PlaySignalOnly(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            if (!hook.HasSound && !hook.HasShake)
                return;

            PlaySounds(hook, context);
            PlayShake(hook.cameraShake, context);
        }

        public static void PlayMerged(
            in WorldPresentationHook hook,
            SoundRef legacySound,
            CameraShakeHook legacyShake,
            in WorldPresentationContext context)
        {
            if (hook.HasSound)
                PlaySounds(hook, context);
            else
                PlaySound(legacySound, context);

            if (hook.HasShake)
                PlayShake(hook.cameraShake, context);
            else
                PlayShake(legacyShake, context);

            SpawnVisual(hook.effect, context);
            SpawnVisual(hook.particle, context);
        }

        public static IEnumerator PlayAsync(WorldPresentationHook hook, WorldPresentationContext context)
        {
            if (!hook.HasAnyContent)
                yield break;

            PlaySignalOnly(hook, context);
            yield return SpawnVisualAsync(hook.effect, context);
            yield return SpawnVisualAsync(hook.particle, context);
        }

        public static IEnumerator PlayAsync(CueRef cueRef, WorldPresentationContext context)
        {
            AssetResolveOperation<PresentationCueSO> cueOperation = CueCatalogService.ResolveAsync(cueRef);
            if (cueOperation == null)
                yield break;

            if (!cueOperation.IsDone)
                yield return cueOperation;

            PresentationCueSO cue = cueOperation.Asset;
            if (cue == null || !cue.HasAnyContent)
                yield break;

            yield return PlayAsync(cue.Presentation, context);
        }

        public static IEnumerator PlayAsync(PresentationReference reference, WorldPresentationContext context)
        {
            switch (reference.mode)
            {
                case PresentationReferenceMode.Cue:
                    yield return PlayAsync(reference.cue, context);
                    yield break;

                case PresentationReferenceMode.InlineThenCue:
                    if (reference.inlinePresentation.HasAnyContent)
                    {
                        yield return PlayAsync(reference.inlinePresentation, context);
                        yield break;
                    }

                    yield return PlayAsync(reference.cue, context);
                    yield break;

                case PresentationReferenceMode.Inline:
                default:
                    if (!reference.inlinePresentation.HasAnyContent)
                        yield break;

                    yield return PlayAsync(reference.inlinePresentation, context);
                    yield break;
            }
        }

        public static Coroutine PlayDeferredAsync(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            return PresentationRoutineRunner.Run(PlayAsync(hook, context));
        }

        public static Coroutine PlayDeferredAsync(in CueRef cueRef, in WorldPresentationContext context)
        {
            return PresentationRoutineRunner.Run(PlayAsync(cueRef, context));
        }

        public static Coroutine PlayDeferredAsync(in PresentationReference reference, in WorldPresentationContext context)
        {
            return PresentationRoutineRunner.Run(PlayAsync(reference, context));
        }

        public static bool TryResolveCue(in CueRef cueRef, out WorldPresentationHook presentation)
        {
            return CueCatalogService.TryResolve(cueRef, out presentation);
        }

        public static bool TryResolve(in PresentationReference reference, out WorldPresentationHook presentation)
        {
            presentation = default;

            switch (reference.mode)
            {
                case PresentationReferenceMode.Cue:
                    return TryResolveCue(reference.cue, out presentation);

                case PresentationReferenceMode.InlineThenCue:
                    if (reference.inlinePresentation.HasAnyContent)
                    {
                        presentation = reference.inlinePresentation;
                        return true;
                    }

                    return TryResolveCue(reference.cue, out presentation);

                case PresentationReferenceMode.Inline:
                default:
                    if (!reference.inlinePresentation.HasAnyContent)
                        return false;

                    presentation = reference.inlinePresentation;
                    return true;
            }
        }

        private static void PlaySound(SoundRef sound, in WorldPresentationContext context)
        {
            if (!sound.IsSet)
                return;

            SoundPlaybackUtility.Play(
                sound,
                instigator: context.Instigator,
                causer: context.Causer,
                target: context.Target,
                position: context.Position,
                sourceObject: context.SourceObject);
        }

        /// <summary>
        /// 책임:
        /// - WorldPresentationHook의 메인 사운드 1개와 동시 재생 보조 사운드들을 같은 문맥으로 재생한다.
        /// - 랜덤 사운드는 메인 사운드 선택 정책으로만 사용하고, additionalSounds는 전부 병렬 재생한다.
        /// </summary>
        private static void PlaySounds(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            PlaySound(hook.ResolveSound(), context);

            SoundRef[] additionalSounds = hook.AdditionalSounds;
            if (additionalSounds == null || additionalSounds.Length == 0)
                return;

            for (int i = 0; i < additionalSounds.Length; i++)
                PlaySound(additionalSounds[i], context);
        }

        private static void PlayShake(CameraShakeHook shake, in WorldPresentationContext context)
        {
            if (shake.amplitude <= 0f)
                return;

            shake.TryPlay(
                source: context.Instigator,
                fallbackDirection: context.FallbackDirection,
                debugReason: nameof(WorldPresentationRuntime));
        }

        public static void SpawnVisual(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            PresentationSpawnService.SpawnOneShot(hook, context);
        }

        public static IEnumerator SpawnVisualAsync(SpawnedPresentationHook hook, WorldPresentationContext context)
        {
            if (!hook.HasContent)
                yield break;

            yield return PresentationSpawnService.SpawnOneShotAsync(hook, context);
        }

        public static Coroutine SpawnVisualDeferredAsync(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            return PresentationRoutineRunner.Run(SpawnVisualAsync(hook, context));
        }

        public static void InitializeSpawnedPresentation(GameObject instance, bool useUnscaledTime)
        {
            PresentationSpawnService.InitializeExternalInstance(instance, useUnscaledTime);
        }
    }
}
