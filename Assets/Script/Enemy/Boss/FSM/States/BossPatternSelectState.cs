public class BossPatternSelectState : BossState
{
    public BossPatternSelectState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        BossPatternEntry nextPattern = boss.SelectNextPattern();

        if (nextPattern == null)
        {
            LogState("쓸 수 있는 패턴이 없어 다시 대기합니다.");
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        string selectedPatternName = nextPattern.Ability != null ? nextPattern.Ability.name : "None";
        LogState($"패턴 '{selectedPatternName}'을 선택합니다.");
        boss.Blackboard.ReservePattern(nextPattern);
        boss.ChangeState(boss.GetPatternExecuteState());
    }
}
