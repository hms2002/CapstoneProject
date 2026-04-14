using UnityEngine;

namespace CapstoneAudio
{
    public static class SoundPlaybackUtility
    {
        public static AudioHandle Play(
            in SoundRef soundRef,
            GameObject instigator = null,
            GameObject causer = null,
            GameObject target = null,
            Vector3? position = null,
            Object sourceObject = null)
        {
            if (!soundRef.IsSet)
                return AudioHandle.Invalid;

            return SoundManager.EnsureInstance().Play(soundRef, BuildContext(
                instigator,
                causer,
                target,
                position,
                sourceObject));
        }

        public static void Stop(AudioHandle handle, float fadeOutDuration = 0f)
        {
            if (!handle.IsValid)
                return;

            SoundManager.EnsureInstance().Stop(handle, fadeOutDuration);
        }

        public static SoundPlaybackContext BuildContext(
            GameObject instigator = null,
            GameObject causer = null,
            GameObject target = null,
            Vector3? position = null,
            Object sourceObject = null)
        {
            return new SoundPlaybackContext
            {
                Instigator = instigator,
                Causer = causer,
                Target = target,
                Position = ResolvePosition(instigator, causer, target, position),
                SourceObject = sourceObject
            };
        }

        private static Vector3 ResolvePosition(
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Vector3? explicitPosition)
        {
            if (explicitPosition.HasValue)
                return explicitPosition.Value;

            if (target != null)
                return target.transform.position;

            if (causer != null)
                return causer.transform.position;

            if (instigator != null)
                return instigator.transform.position;

            return Vector3.zero;
        }
    }
}
