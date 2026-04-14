using UnityEngine;

namespace CapstoneAudio
{
    public static class TypingAudioUtility
    {
        private static readonly SoundRef BossTalkingSound = new SoundRef
        {
            key = "boss.talking",
            volumeMultiplier = 1f,
            anchorPolicy = SoundAnchorPolicy.TwoD,
            localOffset = Vector3.zero
        };

        public static void PlayBossTalking(Object sourceObject, GameObject speakerObject = null)
        {
            SoundManager.EnsureInstance().Play(BossTalkingSound, new SoundPlaybackContext
            {
                SourceObject = sourceObject,
                Causer = speakerObject,
                Position = speakerObject != null ? speakerObject.transform.position : Vector3.zero
            });
        }
    }
}
