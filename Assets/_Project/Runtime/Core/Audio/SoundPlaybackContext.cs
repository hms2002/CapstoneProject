using UnityEngine;

namespace CapstoneAudio
{
    /// <summary>
    /// 책임: 사운드 재생 요청을 어느 게임 오브젝트/월드 위치/소스 자산 기준으로 해석할지 전달하는 Core 계약 데이터이다.
    /// </summary>
    public struct SoundPlaybackContext
    {
        public GameObject Instigator;
        public GameObject Causer;
        public GameObject Target;
        public Vector3 Position;
        public Object SourceObject;
    }
}
