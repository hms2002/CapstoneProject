using UnityEngine;
using UnityGAS;

[System.Serializable]
public sealed class BossPatternEntry
{
    [Header("Ability")]
    [Tooltip("이 패턴이 실제로 실행할 GAS Ability입니다.")]
    [SerializeField] private AbilityDefinition ability;

    [Space(8)]
    [Header("AI Selection")]
    [Tooltip("가중치 기반 패턴 선택 시 사용합니다.")]
    [SerializeField] private int selectionWeight = 100;

    [Tooltip("같은 패턴 연속 사용 제한 횟수입니다. 1이면 연속 사용 금지입니다.")]
    [SerializeField] private int maxConsecutiveUseCount = 1;

    [Tooltip("GAS 쿨다운과 별개인 AI 선택 잠금 시간입니다.")]
    [SerializeField] private float aiSelectionLockTime = 0f;

    [Space(8)]
    [Header("Distance Preference")]
    [Tooltip("이 거리 이상일 때만 선택합니다.")]
    [SerializeField] private float minDistanceToTarget = 0f;

    [Tooltip("이 거리 이하일 때만 선택합니다.")]
    [SerializeField] private float maxDistanceToTarget = 999f;

    [Space(8)]
    [Header("HP Preference")]
    [Tooltip("보스 HP 비율이 이 값 이상일 때만 선택합니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float minHpRatio = 0f;

    [Tooltip("보스 HP 비율이 이 값 이하일 때만 선택합니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxHpRatio = 1f;

    public AbilityDefinition Ability => ability;
    public int SelectionWeight => Mathf.Max(1, selectionWeight);
    public int MaxConsecutiveUseCount => Mathf.Max(1, maxConsecutiveUseCount);
    public float AiSelectionLockTime => Mathf.Max(0f, aiSelectionLockTime);
    public float MinDistanceToTarget => minDistanceToTarget;
    public float MaxDistanceToTarget => maxDistanceToTarget;
    public float MinHpRatio => minHpRatio;
    public float MaxHpRatio => maxHpRatio;

    public bool IsSelectable(BossControllerBase boss, BossBlackboard blackboard)
    {
        if (boss == null || blackboard == null)
            return false;

        if (ability == null)
            return false;

        if (blackboard.CurrentHpRatio < minHpRatio || blackboard.CurrentHpRatio > maxHpRatio)
            return false;

        if (blackboard.DistanceToTarget < minDistanceToTarget || blackboard.DistanceToTarget > maxDistanceToTarget)
            return false;

        if (!blackboard.IsPatternSelectionReady(this))
            return false;

        if (blackboard.LastUsedPattern == this &&
            blackboard.ConsecutivePatternUseCount >= MaxConsecutiveUseCount)
        {
            return false;
        }

        GameObject targetObject = blackboard.CurrentTarget != null ? blackboard.CurrentTarget.gameObject : null;

        return ability.CanActivate(boss.gameObject, targetObject);
    }
}