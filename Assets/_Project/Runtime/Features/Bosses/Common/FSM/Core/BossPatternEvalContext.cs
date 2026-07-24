public readonly struct BossPatternEvalContext
{
    // 이 구조체의 책임:
    // 패턴 평가에 필요한 공통 보스, 전투 문맥, 패턴 런타임 상태를 한 번에 전달한다.

    public BossControllerBase Boss { get; }
    public BossBlackboard Blackboard { get; }
    public BossPatternRuntimeState PatternRuntime { get; }

    public BossPatternEvalContext(BossControllerBase boss, BossBlackboard blackboard, BossPatternRuntimeState patternRuntime)
    {
        Boss = boss;
        Blackboard = blackboard;
        PatternRuntime = patternRuntime;
    }
}
