using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - Skill1 Rush 로직이 사용할 설정값을 보관한다.
    /// - Rush 스택, 충돌/입력/피격 취소 조건, handoff 유지 시간을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RW_Skill1_Rush_Data", menuName = "GAS/Weapon/RealWeapon/Skill1 Rush Data")]
    public sealed class RealWeaponSkill1RushData : ScriptableObject
    {
        [Header("Move Speed Multiplier Attribute")]
        public AttributeDefinition moveSpeedMultiplierAttribute;

        [Header("Stacks")]
        public float stepIntervalSeconds = 3f;
        public int stacks = 3;
        public float addPerStack = 1f;
        [Tooltip("이 태그가 있으면 stack별 이속 증가량을 taggedAddPerStackOverrides 값으로 대체한다.")]
        public GameplayTag stackAddOverrideTag;
        [Tooltip("태그가 있을 때 stack index별로 적용할 이속 증가량. 비어 있으면 기본 addPerStack을 사용한다.")]
        public float[] taggedAddPerStackOverrides;

        [Header("Cancel - Collision")]
        public float collisionCancelRadius = 0.35f;
        public LayerMask collisionCancelLayers;

        [Header("Cancel - Input")]
        public bool cancelOnAttackOrSkillInput = true;
        [Tooltip("이 태그가 있으면 E 입력 취소를 즉시 처리하지 않고 Skill2 킬 확인 window를 연다.")]
        public GameplayTag deferSkill2CancelTag;
        [Tooltip("Skill2 처치 여부를 기다리는 최대 시간(초).")]
        [Min(0.01f)]
        public float skill2KillConfirmWindowSeconds = 0.35f;

        [Header("Cancel - Damaged")]
        [Tooltip("이 Attribute 값이 감소하면 피격으로 간주하고 Rush를 취소한다. 보통 Health.")]
        public AttributeDefinition cancelOnDamagedAttribute;

        [Header("Handoff")]
        [Min(0.01f)]
        public float handoffDurationSeconds = 0.1f;
    }
}
