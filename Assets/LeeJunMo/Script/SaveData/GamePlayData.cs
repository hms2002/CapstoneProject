[System.Serializable]
public sealed class GamePlayData
{
    public bool isRunActive;
    public float runElapsedSeconds;
    public RunEndReason lastRunEndReason = RunEndReason.None;

    public SceneTransitionContext pendingTransition;
    public PlayerRuntimeState pendingPlayerState;
}