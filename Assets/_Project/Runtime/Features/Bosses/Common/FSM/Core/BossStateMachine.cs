public class BossStateMachine
{
    private readonly BossBlackboard blackboard;

    public BossStateMachine(BossBlackboard blackboard)
    {
        this.blackboard = blackboard;
    }

    public BossState CurrentState { get; private set; }

    public void ChangeState(BossState nextState)
    {
        if (nextState == null || CurrentState == nextState)
            return;

        CurrentState?.OnExit();
        CurrentState = nextState;
        blackboard.NotifyStateChanged(CurrentState.StateName);
        CurrentState.OnEnter();
    }

    public void Update()
    {
        CurrentState?.OnUpdate();
    }
}