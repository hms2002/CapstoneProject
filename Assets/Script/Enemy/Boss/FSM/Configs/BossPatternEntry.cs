using UnityEngine;
using UnityGAS;

public enum BossPatternEvalState
{
    Pass,
    SoftFail,
    HardFail
}

public readonly struct BossPatternEvalResult
{
    public BossPatternEvalState State { get; }
    public float WeightMultiplier { get; }
    public string Reason { get; }
    public bool CanUse => State != BossPatternEvalState.HardFail;

    public BossPatternEvalResult(BossPatternEvalState state, float weightMultiplier, string reason)
    {
        State = state;
        WeightMultiplier = Mathf.Max(0f, weightMultiplier);
        Reason = reason;
    }

    /// <summary>최종 가중치를 계산합니다.</summary>
    public int GetWeight(int baseWeight)
    {
        if (!CanUse) return 0;

        float finalWeight = Mathf.Max(0.01f, WeightMultiplier) * Mathf.Max(1, baseWeight);
        return Mathf.Max(1, Mathf.RoundToInt(finalWeight));
    }

    /// <summary>통과 결과를 만듭니다.</summary>
    public static BossPatternEvalResult Pass(string reason = null, float weightMultiplier = 1f)
    {
        return new BossPatternEvalResult(BossPatternEvalState.Pass, weightMultiplier, reason);
    }

    /// <summary>비선호 결과를 만듭니다.</summary>
    public static BossPatternEvalResult SoftFail(string reason = null, float weightMultiplier = 0.35f)
    {
        return new BossPatternEvalResult(BossPatternEvalState.SoftFail, weightMultiplier, reason);
    }

    /// <summary>탈락 결과를 만듭니다.</summary>
    public static BossPatternEvalResult HardFail(string reason = null)
    {
        return new BossPatternEvalResult(BossPatternEvalState.HardFail, 0f, reason);
    }
}

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

    [Tooltip("전체 전투 중 최대 사용 횟수입니다. 0이면 제한이 없습니다.")]
    [SerializeField] private int maxUseCount;

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
    public int MaxUseCount => Mathf.Max(0, maxUseCount);
    public float AiSelectionLockTime => Mathf.Max(0f, aiSelectionLockTime);
    public float MinDistanceToTarget => minDistanceToTarget;
    public float MaxDistanceToTarget => maxDistanceToTarget;
    public float MinHpRatio => minHpRatio;
    public float MaxHpRatio => maxHpRatio;

    /// <summary>공통 패턴 평가 결과를 계산합니다.</summary>
    public BossPatternEvalResult Evaluate(BossControllerBase boss, BossBlackboard blackboard)
    {
        if (boss == null || blackboard == null) return BossPatternEvalResult.HardFail("평가 대상이 없습니다.");

        if (ability == null) return BossPatternEvalResult.HardFail("어빌리티가 없습니다.");

        if (blackboard.CurrentHpRatio < minHpRatio || blackboard.CurrentHpRatio > maxHpRatio)
            return BossPatternEvalResult.HardFail("체력 조건이 맞지 않습니다.");

        if (blackboard.DistanceToTarget < minDistanceToTarget || blackboard.DistanceToTarget > maxDistanceToTarget)
            return BossPatternEvalResult.HardFail("거리 조건이 맞지 않습니다.");

        if (!blackboard.IsPatternSelectionReady(this))
            return BossPatternEvalResult.HardFail("선택 잠금 중입니다.");

        if (blackboard.LastUsedPattern == this &&
            blackboard.ConsecutivePatternUseCount >= MaxConsecutiveUseCount)
        {
            return BossPatternEvalResult.HardFail("연속 사용 제한에 걸렸습니다.");
        }

        if (MaxUseCount > 0 && blackboard.GetUseCount(this) >= MaxUseCount)
            return BossPatternEvalResult.HardFail("사용 횟수를 모두 소모했습니다.");

        GameObject targetObject = blackboard.CurrentTarget != null ? blackboard.CurrentTarget.gameObject : null;
        if (!ability.CanActivate(boss.gameObject, targetObject))
            return BossPatternEvalResult.HardFail("GAS 활성화 조건을 만족하지 않습니다.");

        return BossPatternEvalResult.Pass();
    }
}
