public abstract class WitchPatternCondition : BossPatternCondition
{
    // 이 클래스의 책임:
    // 마녀 보스 전용 패턴 조건이 공통 컨텍스트에서 Witch와 WitchRuntimeData를 안전하게 꺼내도록 돕는다.

    public sealed override BossPatternEvalResult Evaluate(BossPatternEvalContext context, BossPatternEntry ownerPattern)
    {
        Witch witch = context.Boss as Witch;
        if (witch == null)
            return BossPatternEvalResult.HardFail("마녀 보스 컨텍스트가 아닙니다.");

        return EvaluateWitch(witch, context.Blackboard, witch.RuntimeData, ownerPattern);
    }

    protected abstract BossPatternEvalResult EvaluateWitch(
        Witch witch,
        BossBlackboard blackboard,
        WitchRuntimeData runtimeData,
        BossPatternEntry ownerPattern);
}
