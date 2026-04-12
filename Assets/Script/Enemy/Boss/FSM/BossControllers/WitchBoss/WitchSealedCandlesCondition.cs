using UnityEngine;

public class WitchSealedCandlesCondition : WitchPatternCondition
{
    // 이 클래스의 책임:
    // 마녀 패턴이 현재 봉인된 촛대가 존재할 때만 선택되도록 제한한다.

    [SerializeField] private int minimumSealedCandles = 1;

    public static WitchSealedCandlesCondition CreateRuntime(int runtimeMinimumSealedCandles = 1)
    {
        WitchSealedCandlesCondition condition = CreateInstance<WitchSealedCandlesCondition>();
        condition.minimumSealedCandles = runtimeMinimumSealedCandles;
        return condition;
    }

    protected override BossPatternEvalResult EvaluateWitch(
        Witch witch,
        BossBlackboard blackboard,
        WitchRuntimeData runtimeData,
        BossPatternEntry ownerPattern)
    {
        if (witch == null)
            return BossPatternEvalResult.HardFail("마녀 보스가 없습니다.");

        return witch.GetSealedCandleCount() >= Mathf.Max(1, minimumSealedCandles)
            ? BossPatternEvalResult.Pass()
            : BossPatternEvalResult.HardFail("봉인된 촛대가 부족합니다.");
    }
}
