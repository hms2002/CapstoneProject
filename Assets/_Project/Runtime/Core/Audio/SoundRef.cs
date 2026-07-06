using System;
using UnityEngine;

namespace CapstoneAudio
{
    /// <summary>
    /// 책임: 사운드 요청이 런타임 문맥에서 어떤 위치 기준으로 해석될지 나타낸다.
    /// </summary>
    public enum SoundAnchorPolicy
    {
        CatalogDefault,
        TwoD,
        CuePosition,
        Instigator,
        Causer,
        Target
    }

    /// <summary>
    /// 책임: Core/Gameplay 데이터가 구체 오디오 재생 구현 없이 사운드 키와 위치 정책을 전달하는 직렬화 값 타입이다.
    /// </summary>
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
