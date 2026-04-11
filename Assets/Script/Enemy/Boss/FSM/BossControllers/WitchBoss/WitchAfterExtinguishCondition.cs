public class WitchAfterExtinguishCondition : WitchPatternCondition
{
    protected override BossPatternEvalResult EvaluateWitch(
        Witch witch,
        BossBlackboard blackboard,
        WitchRuntimeData runtimeData,
        BossPatternEntry ownerPattern)
    {
        BossPatternEntry lastPattern = witch.PatternRuntime.LastUsedPattern;
        if (lastPattern == null || lastPattern.Ability == null)
            return BossPatternEvalResult.HardFail("이전 패턴이 없습니다.");

        if (lastPattern.Ability.logic is UnityGAS.Sample.AbilityLogic_ExtinguishCandle)
            return BossPatternEvalResult.Pass();

        return BossPatternEvalResult.HardFail("촛불 끄기 패턴 뒤에만 사용할 수 있습니다.");
    }
}
