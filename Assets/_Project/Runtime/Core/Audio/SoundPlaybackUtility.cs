using UnityEngine;

namespace CapstoneAudio
{
    /// <summary>
    /// 책임: Core 오디오 요청과 음악/믹스 제어를 실제 오디오 구현으로 넘기는 최소 backend 계약이다.
    /// </summary>
    public interface ISoundPlaybackBackend
    {
        AudioHandle Play(in SoundRef soundRef, in SoundPlaybackContext context);
        AudioHandle PlayTrackedOneShot(in SoundRef soundRef, in SoundPlaybackContext context);
        void PlayMusic(in SoundRef soundRef);
        void StopMusic();
        void DuckCombatSfx(float targetVolume, float fadeSeconds);
        bool IsPlaying(AudioHandle handle);
        void Stop(AudioHandle handle, float fadeOutDuration = 0f);
        void SetPitch(AudioHandle handle, float pitch);
    }

    /// <summary>
    /// 책임: Core/Gameplay 호출자가 구체 SoundManager 구현 없이 사운드 재생, 정지, 런타임 제어를 요청하게 한다.
    /// </summary>
    public static class SoundPlaybackUtility
    {
        private static ISoundPlaybackBackend backend;

        public static void RegisterBackend(ISoundPlaybackBackend playbackBackend)
        {
            backend = playbackBackend;
        }

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

            return Play(soundRef, BuildContext(
                instigator,
                causer,
                target,
                position,
                sourceObject));
        }

        public static AudioHandle Play(in SoundRef soundRef, in SoundPlaybackContext context)
        {
            if (!soundRef.IsSet)
                return AudioHandle.Invalid;

            return backend != null
                ? backend.Play(soundRef, context)
                : AudioHandle.Invalid;
        }

        public static AudioHandle PlayTrackedOneShot(in SoundRef soundRef, in SoundPlaybackContext context)
        {
            if (!soundRef.IsSet)
                return AudioHandle.Invalid;

            return backend != null
                ? backend.PlayTrackedOneShot(soundRef, context)
                : AudioHandle.Invalid;
        }

        public static void PlayMusic(in SoundRef soundRef)
        {
            if (!soundRef.IsSet)
                return;

            backend?.PlayMusic(soundRef);
        }

        public static void StopMusic()
        {
            backend?.StopMusic();
        }

        public static void DuckCombatSfx(float targetVolume, float fadeSeconds)
        {
            backend?.DuckCombatSfx(targetVolume, fadeSeconds);
        }

        public static void Stop(AudioHandle handle, float fadeOutDuration = 0f)
        {
            if (!handle.IsValid)
                return;

            backend?.Stop(handle, fadeOutDuration);
        }

        public static bool IsPlaying(AudioHandle handle)
        {
            return handle.IsValid &&
                   backend != null &&
                   backend.IsPlaying(handle);
        }

        public static void SetPitch(AudioHandle handle, float pitch)
        {
            if (!handle.IsValid)
                return;

            backend?.SetPitch(handle, pitch);
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
