using System;
using System.Collections.Generic;

[Serializable]
public sealed class GamePlayData
{
    public bool isRunActive;
    public int runCount;
    public float runElapsedSeconds;
    public RunEndReason lastRunEndReason;
    public int lastDefeatedBossId = -1;
    public string lastDefeatReason;

    public SceneTransitionContext pendingTransition;

    public bool HasPendingTransition => pendingTransition != null;

    public void ResetForRunStart()
    {
        isRunActive = true;
        runElapsedSeconds = 0f;
        lastRunEndReason = RunEndReason.None;
        lastDefeatedBossId = -1;
        lastDefeatReason = null;
        pendingTransition = null;
    }

    public void ClearRunState()
    {
        isRunActive = false;
        runElapsedSeconds = 0f;
        pendingTransition = null;
    }
}
