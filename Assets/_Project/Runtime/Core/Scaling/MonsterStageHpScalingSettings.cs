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
    [SerializeField, Min(0f)] private float attackSpeedMultiplierPerClearedStage = 0.1f;

    [Header("Combat Timing Slots")]
    [SerializeField] private bool scaleAttackWarning = false;
    [SerializeField] private bool scaleAttackRecovery = true;
    [SerializeField] private bool scaleAttackInterval = true;
    [SerializeField] private bool scaleAbilityCast = false;
    [SerializeField] private bool scaleAbilityRecovery = true;
    [SerializeField] private bool scaleAbilityCooldown = true;
    [SerializeField, Min(0.01f)] private float minimumScaledSeconds = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool logStageScalingDebug;
    [SerializeField] private bool logCombatTimingDebug;

    public bool Enabled => enabled;
    public float HpMultiplierPerClearedStage => Mathf.Max(0f, hpMultiplierPerClearedStage);
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

        return 1f + AttackSpeedMultiplierPerClearedStage * Mathf.Max(0, stageIndex);
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
}
