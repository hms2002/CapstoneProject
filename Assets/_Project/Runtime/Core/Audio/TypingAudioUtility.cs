using UnityEngine;

namespace CapstoneAudio
{
    /// <summary>
    /// 책임 : 대화/엔딩 타이핑 계열 사운드를 Core 오디오 계약으로 재생하는 작은 편의 진입점이다.
    /// </summary>
    public static class TypingAudioUtility
    {
        private static readonly SoundRef BossTalkingSound = new SoundRef
        {
            key = "boss.talking",
            volumeMultiplier = 1f,
            anchorPolicy = SoundAnchorPolicy.TwoD,
            localOffset = Vector3.zero
        };

        private static readonly SoundRef IntroOutroPencilSound = new SoundRef
        {
            key = "ui.inoutro.pencil",
            volumeMultiplier = 1f,
            anchorPolicy = SoundAnchorPolicy.TwoD,
            localOffset = Vector3.zero
        };

        public static void PlayBossTalking(Object sourceObject, GameObject speakerObject = null)
        {
            SoundPlaybackUtility.Play(BossTalkingSound, new SoundPlaybackContext
            {
                SourceObject = sourceObject,
                Causer = speakerObject,
                Position = speakerObject != null ? speakerObject.transform.position : Vector3.zero
            });
        }

        public static AudioHandle PlayIntroOutroPencil(Object sourceObject, GameObject speakerObject = null)
        {
            return SoundPlaybackUtility.PlayTrackedOneShot(IntroOutroPencilSound, new SoundPlaybackContext
            {
                SourceObject = sourceObject,
                Causer = speakerObject,
                Position = speakerObject != null ? speakerObject.transform.position : Vector3.zero
            });
        }
    }
}
