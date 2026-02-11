using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 스킬1: "강제 이동 + 단계적 속도 증가"
    /// - 실행 동안 SampleTopDownPlayer는 forcedMoveTag에 의해 WASD를 무시하고 AimDirection으로 이동
    /// - 속도 증가는 moveSpeedMultiplierAttribute에 Flat(+) modifier로 누적 부여
    /// - 외부 요인(피격/충돌/공격 입력)으로 캔슬되면 즉시 해제
    /// </summary>
    [CreateAssetMenu(fileName = "RW_Skill1_Rush_Data", menuName = "GAS/Weapon/RealWeapon/Skill1 Rush Data")]
    public sealed class RealWeaponSkill1RushData : ScriptableObject
    {
        [Header("Move Speed Multiplier Attribute")]
        [Tooltip("이 Attribute에 Flat(+1, +1, +1) 형태로 누적됩니다. 기본값 1 권장.")]
        public AttributeDefinition moveSpeedMultiplierAttribute;

        [Header("Stacks")]
        [Tooltip("첫 적용 이후, 다음 스택까지 대기 시간(초)")]
        public float stepIntervalSeconds = 3f;

        [Tooltip("총 스택 수. 3이면 +1, +1, +1을 순서대로 적용(총 +3)")]
        public int stacks = 3;

        [Tooltip("스택 1개당 더해지는 배수(+1 = +100%를 의미하도록 설계)")]
        public float addPerStack = 1f;

        [Header("Cancel - Collision")]
        [Tooltip("이 반경 안에서 아래 레이어와 Overlap되면 스킬이 캔슬됩니다.")]
        public float collisionCancelRadius = 0.35f;

        public LayerMask collisionCancelLayers;

        [Header("Cancel - Input")]
        [Tooltip("true면 실행 중 마우스 클릭/스킬키 입력이 들어오면 캔슬합니다.")]
        public bool cancelOnAttackOrSkillInput = true;
    }
}
