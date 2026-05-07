using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 빈 탄창 격발 피드백에 필요한 사운드와 선택 VFX authoring 값을 보관한다.
    /// - 투사체나 피해 없이 입력이 먹혔다는 감각만 제공하게 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "OddIronDryFireData", menuName = "GAS/Weapon/Odd Iron/Dry Fire Data")]
    public sealed class OddIronDryFireData : ScriptableObject
    {
        public SoundRef dryFireSound;
        public GameObject dryFireVfxPrefab;
        public Vector3 vfxOffset = new(0.5f, 0.1f, 0f);
    }
}
