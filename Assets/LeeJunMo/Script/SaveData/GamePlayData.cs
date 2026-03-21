[System.Serializable]
public sealed class GamePlayData
{
    public bool isRunActive;
    public float runElapsedSeconds;
    public RunEndReason lastRunEndReason = RunEndReason.None;

    public SceneTransitionContext pendingTransition;
    public object pendingPlayerState; // 나중에 PlayerRuntimeState로 교체
}