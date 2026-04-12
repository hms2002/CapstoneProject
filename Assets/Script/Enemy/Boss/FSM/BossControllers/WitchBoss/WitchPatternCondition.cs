using UnityEngine;

public abstract class WitchPatternCondition : BossPatternCondition
{
    // 이 클래스의 책임:
    // 마녀 보스 전용 패턴 조건이 공통 컨텍스트에서 Witch와 WitchRuntimeData를 안전하게 꺼내도록 돕는다.

    private bool hasLoggedRuntimeDataBridge;

    public sealed override BossPatternEvalResult Evaluate(BossPatternEvalContext context, BossPatternEntry ownerPattern)
    {
        Witch witch = context.Boss as Witch;
        if (witch == null)
            return BossPatternEvalResult.HardFail("마녀 보스 컨텍스트가 아닙니다.");

        WitchRuntimeData runtimeData = witch.RuntimeData;
        if (runtimeData == null)
            return BossPatternEvalResult.HardFail("마녀 런타임 데이터가 없습니다.");

        if (!hasLoggedRuntimeDataBridge)
        {
            hasLoggedRuntimeDataBridge = true;
            string patternName = ownerPattern != null && ownerPattern.Ability != null ? ownerPattern.Ability.name : "None";
            Debug.Log(
                $"[BossFSM] {witch.name}: WitchPatternCondition가 runtime data를 정상 수신했습니다. pattern='{patternName}'",
                witch);
        }

        return EvaluateWitch(witch, context.Blackboard, runtimeData, ownerPattern);
    }

    protected abstract BossPatternEvalResult EvaluateWitch(
        Witch witch,
        BossBlackboard blackboard,
        WitchRuntimeData runtimeData,
        BossPatternEntry ownerPattern);
}
