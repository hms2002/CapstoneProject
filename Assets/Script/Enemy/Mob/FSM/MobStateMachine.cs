/// <summary>
/// 책임 :
/// - 일반 몬스터의 현재 상태를 하나만 유지하고 Enter / Tick / Exit 호출 순서를 보장한다.
/// - 상태 전이를 한 곳에서 처리해 Mob 본체가 상태 생명주기 세부를 직접 다루지 않게 한다.
/// </summary>
public sealed class MobStateMachine
{
    private IMobState currentState;

    public IMobState CurrentState => currentState;

    public void SetInitialState(IMobState nextState, MobAIContext context)
    {
        currentState = nextState;
        currentState?.Enter(this, context);
    }

    public void Tick(MobAIContext context)
    {
        currentState?.Tick(this, context);
    }

    public void ChangeState(IMobState nextState, MobAIContext context)
    {
        if (ReferenceEquals(currentState, nextState))
            return;

        currentState?.Exit(this, context);
        currentState = nextState;
        currentState?.Enter(this, context);
    }

    public void Shutdown(MobAIContext context)
    {
        currentState?.Exit(this, context);
        currentState = null;
    }
}

