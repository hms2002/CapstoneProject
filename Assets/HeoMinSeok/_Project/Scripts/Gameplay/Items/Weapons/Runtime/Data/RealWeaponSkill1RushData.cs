using UnityEngine;
using CapstoneAudio;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - Skill1 Rush 로직이 사용할 설정값을 보관한다.
    /// - Rush 스택, 충돌/입력/피격 취소 조건, handoff 유지 시간을 정의한다.
    /// - Rush 전용 단계별 사운드와 잔상 비주얼 authoring 값을 함께 제공한다.
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

        [Header("Audio (Optional)")]
        [Tooltip("Rush 스택 단계가 상승할 때 stack index 순서대로 재생할 사운드입니다. 길이가 부족하면 해당 단계는 무음으로 둡니다.")]
        public SoundRef[] stackAdvanceSounds;

        [Tooltip("Rush가 입력 취소로 handoff 종료될 때 재생할 선택 사운드입니다.")]
        public SoundRef cancelByInputSound;

        [Header("Visual - Afterimage (Optional)")]
        [Tooltip("켜져 있으면 Rush 실행 중 단계별 잔상을 생성합니다.")]
        public bool enableAfterimage = true;

        [Tooltip("Rush stack index 순서대로 적용할 잔상 생성 간격(초)입니다. 길이가 부족하면 마지막 값을 재사용합니다.")]
        public float[] afterimageIntervalsByStack = { 0.08f, 0.05f, 0.03f };

        [Tooltip("개별 잔상이 자연스럽게 사라질 때까지 유지되는 시간(초)입니다.")]
        [Min(0.01f)]
        public float afterimageLifetimeSeconds = 0.18f;

        [Tooltip("Rush 잔상에 적용할 공통 색과 투명도입니다.")]
        public Color afterimageColor = new(1f, 1f, 1f, 0.45f);

        [Header("Visual - Wind Particles (Optional)")]
        [Tooltip("Rush 중 이동 방향으로 바람을 가르는 파티클 프리팹입니다.")]
        public GameObject windParticlePrefab;

        [Tooltip("이동 방향 기준으로 파티클 시스템을 얼마나 옮겨 붙일지 결정합니다.")]
        public Vector3 windParticleLocalOffset = Vector3.zero;

        [Tooltip("이동 방향 각도에 추가로 더할 파티클 로컬 회전 오프셋입니다.")]
        public float windParticleAngleOffset = 0f;

        [Tooltip("켜져 있으면 MovementMotor2D의 최종 이동 방향에 맞춰 파티클이 회전합니다.")]
        public bool alignWindParticleToMovementDirection = true;

        [Tooltip("Rush stack index 순서대로 적용할 파티클 emission multiplier입니다. 길이가 부족하면 마지막 값을 재사용합니다.")]
        public float[] windParticleEmissionMultipliersByStack = { 1f, 1.35f, 1.7f };

        /// <summary>
        /// 책임 :
        /// - 현재 Rush stack index에 맞는 잔상 emission 간격을 안전하게 해석한다.
        /// - 배열이 비었거나 길이가 부족해도 마지막 유효 값을 재사용해 authoring 누락을 허용한다.
        /// </summary>
        public float ResolveAfterimageInterval(int stackIndex)
        {
            if (afterimageIntervalsByStack == null || afterimageIntervalsByStack.Length == 0)
                return 0.08f;

            int clampedIndex = Mathf.Clamp(stackIndex, 0, afterimageIntervalsByStack.Length - 1);
            return Mathf.Max(0.01f, afterimageIntervalsByStack[clampedIndex]);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 Rush stack index에 맞는 바람 파티클 emission multiplier를 해석한다.
        /// - 배열이 비었거나 길이가 부족해도 기본 1배 또는 마지막 유효 값을 재사용한다.
        /// </summary>
        public float ResolveWindParticleEmissionMultiplier(int stackIndex)
        {
            if (windParticleEmissionMultipliersByStack == null || windParticleEmissionMultipliersByStack.Length == 0)
                return 1f;

            int clampedIndex = Mathf.Clamp(stackIndex, 0, windParticleEmissionMultipliersByStack.Length - 1);
            return Mathf.Max(0f, windParticleEmissionMultipliersByStack[clampedIndex]);
        }
    }
}
