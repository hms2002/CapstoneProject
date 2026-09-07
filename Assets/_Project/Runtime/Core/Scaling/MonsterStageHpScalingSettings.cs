using UnityEngine;

/// <summary>
/// 책임:
/// - 일반 몬스터의 스테이지 진행도 기반 HP/공격 템포 보정 정책을 중앙 설정 에셋으로 제공한다.
/// - 여러 씬의 MonsterSpawner가 같은 보정 수치를 공유하게 해 밸런스 조정을 한 곳에서 끝내게 한다.
/// </summary>
[CreateAssetMenu(fileName = "MonsterStageHpScalingSettings", menuName = "GAS/Monster Spawn/Stage HP Scaling Settings")]
public sealed class MonsterStageHpScalingSettings : ScriptableObject
{
    [SerializeField] private bool enabled = true;
    [SerializeField, Min(0f)] private float hpMultiplierPerClearedStage = 0.5f;
    [Tooltip("스테이지 0부터 모든 일반 몬스터에 적용할 기본 공격 템포 배율입니다.")]
    [SerializeField, Min(0f)] private float baseAttackSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float attackSpeedMultiplierPerClearedStage = 0.1f;

    [Header("Combat Timing Slots")]
    [SerializeField] private bool scaleAttackWarning = false;
    [SerializeField] private bool scaleAttackRecovery = true;
    [SerializeField] private bool scaleAttackInterval = true;
    [SerializeField] private bool scaleAbilityCast = false;
    [SerializeField] private bool scaleAbilityRecovery = true;
    [SerializeField] private bool scaleAbilityCooldown = true;
    [SerializeField, Min(0.01f)] private float minimumScaledSeconds = 0.08f;

    [Header("Combat Timing Slot Speed Multipliers")]
    [Tooltip("공격 준비/경고 시간이 기본 공격속도보다 얼마나 더 빨리 줄어들지 정합니다.")]
    [SerializeField, Min(0f)] private float attackWarningSlotSpeedMultiplier = 1f;
    [Tooltip("공격 후 후딜레이 시간이 기본 공격속도보다 얼마나 더 빨리 줄어들지 정합니다.")]
    [SerializeField, Min(0f)] private float attackRecoverySlotSpeedMultiplier = 1f;
    [Tooltip("연속 공격 간격이 기본 공격속도보다 얼마나 더 빨리 줄어들지 정합니다.")]
    [SerializeField, Min(0f)] private float attackIntervalSlotSpeedMultiplier = 1f;
    [Tooltip("어빌리티 캐스팅 시간이 기본 공격속도보다 얼마나 더 빨리 줄어들지 정합니다.")]
    [SerializeField, Min(0f)] private float abilityCastSlotSpeedMultiplier = 1f;
    [Tooltip("어빌리티 후딜레이 시간이 기본 공격속도보다 얼마나 더 빨리 줄어들지 정합니다.")]
    [SerializeField, Min(0f)] private float abilityRecoverySlotSpeedMultiplier = 1f;
    [Tooltip("어빌리티 쿨다운 시간이 기본 공격속도보다 얼마나 더 빨리 줄어들지 정합니다.")]
    [SerializeField, Min(0f)] private float abilityCooldownSlotSpeedMultiplier = 1f;

    [Header("Debug")]
    [SerializeField] private bool logStageScalingDebug;
    [SerializeField] private bool logCombatTimingDebug;

    public bool Enabled => enabled;
    public float HpMultiplierPerClearedStage => Mathf.Max(0f, hpMultiplierPerClearedStage);
    public float BaseAttackSpeedMultiplier => Mathf.Max(0f, baseAttackSpeedMultiplier);
    public float AttackSpeedMultiplierPerClearedStage => Mathf.Max(0f, attackSpeedMultiplierPerClearedStage);
    public float MinimumScaledSeconds => Mathf.Max(0.01f, minimumScaledSeconds);
    public bool LogStageScalingDebug => logStageScalingDebug;
    public bool LogCombatTimingDebug => logCombatTimingDebug;

    /// <summary>
    /// 책임:
    /// - 현재 stage index를 최종 HP 배율로 변환한다.
    /// - stage 0은 1배, 이후 스테이지는 설정된 증가량만큼 선형 누적된다.
    /// </summary>
    public float CalculateStageHpMultiplier(int stageIndex)
    {
        if (!enabled)
            return 1f;

        return 1f + HpMultiplierPerClearedStage * Mathf.Max(0, stageIndex);
    }

    /// <summary>
    /// 책임:
    /// - 현재 stage index를 몬스터 공격 템포 배율로 변환한다.
    /// - stage 0은 1배, 이후 스테이지는 설정된 증가량만큼 선형 누적된다.
    /// </summary>
    public float CalculateStageAttackSpeedMultiplier(int stageIndex)
    {
        if (!enabled)
            return 1f;

        return BaseAttackSpeedMultiplier *
               (1f + AttackSpeedMultiplierPerClearedStage * Mathf.Max(0, stageIndex));
    }

    /// <summary>
    /// 책임:
    /// - CombatTimingService가 각 전투 시간 슬롯을 공격속도 보정 대상으로 볼지 결정한다.
    /// - 사망/대사/VFX 같은 PresentationOnly 시간은 항상 보정하지 않는다.
    /// </summary>
    public bool ShouldScaleTimingSlot(UnityGAS.CombatTimingSlot slot)
    {
        if (!enabled)
            return false;

        return slot switch
        {
            UnityGAS.CombatTimingSlot.AttackWarning => scaleAttackWarning,
            UnityGAS.CombatTimingSlot.AttackRecovery => scaleAttackRecovery,
            UnityGAS.CombatTimingSlot.AttackInterval => scaleAttackInterval,
            UnityGAS.CombatTimingSlot.AbilityCast => scaleAbilityCast,
            UnityGAS.CombatTimingSlot.AbilityRecovery => scaleAbilityRecovery,
            UnityGAS.CombatTimingSlot.AbilityCooldown => scaleAbilityCooldown,
            _ => false
        };
    }

    /// <summary>
    /// 책임:
    /// - CombatTimingService가 같은 공격속도 안에서도 경고/후딜/공격 간격별 체감 감소폭을 다르게 적용하게 한다.
    /// - 몬스터 전체 공격 템포 실험을 중앙 설정 하나로 조절할 수 있게 한다.
    /// </summary>
    public float ResolveTimingSlotSpeedMultiplier(UnityGAS.CombatTimingSlot slot)
    {
        if (!enabled)
            return 1f;

        return slot switch
        {
            UnityGAS.CombatTimingSlot.AttackWarning => Mathf.Max(0f, attackWarningSlotSpeedMultiplier),
            UnityGAS.CombatTimingSlot.AttackRecovery => Mathf.Max(0f, attackRecoverySlotSpeedMultiplier),
            UnityGAS.CombatTimingSlot.AttackInterval => Mathf.Max(0f, attackIntervalSlotSpeedMultiplier),
            UnityGAS.CombatTimingSlot.AbilityCast => Mathf.Max(0f, abilityCastSlotSpeedMultiplier),
            UnityGAS.CombatTimingSlot.AbilityRecovery => Mathf.Max(0f, abilityRecoverySlotSpeedMultiplier),
            UnityGAS.CombatTimingSlot.AbilityCooldown => Mathf.Max(0f, abilityCooldownSlotSpeedMultiplier),
            _ => 1f
        };
    }
}
