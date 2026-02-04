using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "Dash2DData", menuName = "GAS/Samples/AbilityLogicData/Dash 2D Data")]
    public class Dash2DData : ScriptableObject
    {
        [Header("Dash")]
        [Tooltip("대쉬 총 지속시간(초)")]
        public float duration = 0.10f;

        [Tooltip("대쉬 이동 거리(유닛)")]
        public float distance = 3.0f;

        [Tooltip("입력 방향이 0일 때 조준 방향으로 대쉬할지")]
        public bool useAimWhenNoMoveInput = true;

        [Header("Tags (Optional)")]
        [Tooltip("대쉬 중 무적 태그. (GE_Damage_Spec.invulnerableTag 와 같은 태그를 사용하세요)")]
        public GameplayTag invulnerableTag;

        [Tooltip("대쉬 중 이동 입력을 막는 태그. (SampleTopDownPlayer.movementLockedTag 와 같은 태그 추천)")]
        public GameplayTag movementLockedTag;

        [Tooltip("대쉬 중 에임(Hand 회전)을 고정시키고 싶을 때(선택)")]
        public GameplayTag aimLockedTag;

        [Header("Tuning")]
        [Tooltip("대쉬 시작/끝에서 Rigidbody2D의 속도를 0으로 만들지")]
        public bool zeroVelocity = true;

        [Tooltip("대쉬가 끝난 뒤 잠깐 이동 입력을 막고 싶다면(초). 0이면 없음")]
        public float postLockTime = 0f;
    }
}
