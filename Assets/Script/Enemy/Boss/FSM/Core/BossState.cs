using UnityEngine;

public abstract class BossState
{
    // 이 클래스의 책임:
    // 보스 FSM 상태가 공통으로 사용할 보스 컨트롤러/능력 브리지 참조와 상태 로깅 도우미를 제공한다.

    protected readonly BossControllerBase boss;
    protected readonly IBossAbilityStateBridge abilityBridge;

    protected BossState(BossControllerBase boss)
    {
        this.boss = boss;
        abilityBridge = boss as IBossAbilityStateBridge;
    }

    public virtual string StateName => GetType().Name;

    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }

    protected void LogState(string message)
    {
        Debug.Log($"[BossFSM] {boss.name}: {message}", boss);
    }
}
