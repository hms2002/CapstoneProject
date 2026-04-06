using UnityEngine;

public abstract class BossState
{
    protected readonly BossControllerBase boss;

    protected BossState(BossControllerBase boss)
    {
        this.boss = boss;
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
