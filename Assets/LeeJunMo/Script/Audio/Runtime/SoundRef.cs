using System;
using UnityEngine;

namespace CapstoneAudio
{
    public enum SoundAnchorPolicy
    {
        CatalogDefault,
        TwoD,
        CuePosition,
        Instigator,
        Causer,
        Target
    }

    [Serializable]
    public struct SoundRef
    {
        public string key;

        [Range(0f, 2f)]
        public float volumeMultiplier;

        public SoundAnchorPolicy anchorPolicy;
        public Vector3 localOffset;

        public bool IsSet => !string.IsNullOrWhiteSpace(key);

        public float EffectiveVolumeMultiplier =>
            Mathf.Approximately(volumeMultiplier, 0f) ? 1f : volumeMultiplier;

        public static SoundRef FromKey(string keyValue)
        {
            return new SoundRef
            {
                key = keyValue,
                volumeMultiplier = 1f,
                anchorPolicy = SoundAnchorPolicy.CatalogDefault,
                localOffset = Vector3.zero
            };
        }
    }
}
