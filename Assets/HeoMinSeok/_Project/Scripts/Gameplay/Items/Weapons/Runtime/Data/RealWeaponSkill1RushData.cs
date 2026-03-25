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

        [Header("Cancel - Collision")]
        public float collisionCancelRadius = 0.35f;
        public LayerMask collisionCancelLayers;

        [Header("Cancel - Input")]
        public bool cancelOnAttackOrSkillInput = true;

        [Header("Cancel - Damaged")]
        [Tooltip("이 Attribute 값이 감소하면 피격으로 간주하고 Rush를 취소한다. 보통 Health.")]
        public AttributeDefinition cancelOnDamagedAttribute;

        [Header("Handoff")]
        [Min(0.01f)]
        public float handoffDurationSeconds = 0.1f;
    }
}