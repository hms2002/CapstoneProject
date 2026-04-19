using System.Collections;
using CapstoneAudio;
using UnityEngine;

namespace CapstonePresentation
{
    public static class WorldPresentationRuntime
    {
        public static void Play(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            if (!hook.HasAnyContent)
                return;

            PlaySound(hook.sound, context);
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

            PlaySound(hook.sound, context);
            PlayShake(hook.cameraShake, context);
        }

        public static void PlayMerged(
            in WorldPresentationHook hook,
            SoundRef legacySound,
            CameraShakeHook legacyShake,
            in WorldPresentationContext context)
        {
            if (hook.HasSound)
                PlaySound(hook.sound, context);
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
