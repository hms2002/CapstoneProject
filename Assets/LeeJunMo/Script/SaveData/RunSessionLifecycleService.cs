using System;
using UnityEngine;

internal static class RunSessionLifecycleService
{
    public static void StartRun(GamePlayData data, Action clearPendingRunProgress)
    {
        if (data == null)
            return;

        clearPendingRunProgress?.Invoke();
        data.isRunActive = true;
        data.runElapsedSeconds = 0f;
        data.runRemainingSeconds = 0f;
    }

    public static void EndRun(
        GamePlayData data,
        RunEndReason reason,
        Action commitPendingRunProgress,
        Action clearRoutePlan,
        UnityEngine.Object saveRequester)
    {
        if (data == null)
            return;

        data.lastRunEndReason = reason;
        if (data.isRunActive)
        {
            commitPendingRunProgress?.Invoke();
            GameDataSaveCoordinator.FlushNow(saveRequester);
        }

        data.isRunActive = false;
        data.runRemainingSeconds = 0f;
        data.pendingTransition = null;
        data.pendingPlayerState = null;
        clearRoutePlan?.Invoke();
    }
}
