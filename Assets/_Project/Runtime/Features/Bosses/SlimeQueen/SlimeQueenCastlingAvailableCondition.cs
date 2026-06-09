public sealed class SlimeQueenCastlingAvailableCondition : BossPatternCondition
{
    /// <summary>캐슬링은 2페이즈 두 슬라임이 모두 살아 있고 다른 패턴 실행 중이 아닐 때만 선택됩니다.</summary>
    public override BossPatternEvalResult Evaluate(BossPatternEvalContext context, BossPatternEntry ownerPattern)
    {
        SlimeQueenPhaseTwoBase slimeQueen = context.Boss as SlimeQueenPhaseTwoBase;
        if (slimeQueen == null)
            return BossPatternEvalResult.HardFail("슬라임퀸 2페이즈 전용 조건입니다.");

        return slimeQueen.CanStartCastlingPattern(out string reason)
            ? BossPatternEvalResult.Pass()
            : BossPatternEvalResult.HardFail(reason);
    }
}
