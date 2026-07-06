using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Ability 로직이 구체 오디오 backend를 직접 몰라도 되도록 공통 오디오 재생 진입점을 제공한다.
    /// - AbilitySystem / AbilitySpec / target 문맥을 SoundPlaybackContext로 변환해 일관된 방식으로 전달한다.
    /// </summary>
    public static class AbilityAudioRouter
    {
        /// <summary>
        /// 책임 :
        /// - Ability 로직/데이터가 제공한 SoundRef를 현재 문맥에 맞는 위치/대상 정보와 함께 1회 재생한다.
        /// - 공통 AbilityDefinition 외에 LogicData 전용 사운드도 같은 경로로 재생되게 한다.
        /// </summary>
        public static void PlayOneShot(
            in SoundRef soundRef,
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target = null,
            Object sourceObjectOverride = null,
            GameObject causerOverride = null)
        {
            if (!soundRef.IsSet)
                return;

            SoundPlaybackUtility.Play(soundRef, BuildContext(
                system,
                spec,
                target,
                sourceObjectOverride,
                causerOverride));
        }

        public static void PlayOneShotAtPosition(
            in SoundRef soundRef,
            AbilitySystem system,
            AbilitySpec spec,
            Vector3 position,
            Object sourceObjectOverride = null,
            GameObject causerOverride = null)
        {
            if (!soundRef.IsSet)
                return;

            SoundPlaybackUtility.Play(soundRef, BuildContextAtPosition(
                system,
                spec,
                position,
                sourceObjectOverride,
                causerOverride));
        }

        /// <summary>
        /// 책임 :
        /// - Ability 실행 문맥을 Core 오디오 계약이 이해하는 SoundPlaybackContext로 변환한다.
        /// - 별도 override가 없으면 AbilityDefinition을 source object로 사용한다.
        /// </summary>
        public static SoundPlaybackContext BuildContext(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target = null,
            Object sourceObjectOverride = null,
            GameObject causerOverride = null)
        {
            GameObject owner = system != null ? system.gameObject : null;
            Object sourceObject = sourceObjectOverride != null
                ? sourceObjectOverride
                : (spec != null ? spec.Definition : null);

            Vector3 position = target != null
                ? target.transform.position
                : (owner != null ? owner.transform.position : Vector3.zero);

            return new SoundPlaybackContext
            {
                Instigator = owner,
                Causer = causerOverride != null ? causerOverride : owner,
                Target = target,
                Position = position,
                SourceObject = sourceObject
            };
        }

        private static SoundPlaybackContext BuildContextAtPosition(
            AbilitySystem system,
            AbilitySpec spec,
            Vector3 position,
            Object sourceObjectOverride = null,
            GameObject causerOverride = null)
        {
            GameObject owner = system != null ? system.gameObject : null;
            Object sourceObject = sourceObjectOverride != null
                ? sourceObjectOverride
                : (spec != null ? spec.Definition : null);

            return new SoundPlaybackContext
            {
                Instigator = owner,
                Causer = causerOverride != null ? causerOverride : owner,
                Position = position,
                SourceObject = sourceObject
            };
        }
    }
}
