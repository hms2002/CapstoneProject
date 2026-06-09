using UnityEngine;

internal static class PlayerSceneRestoreConfirmationService
{
    public static bool TryConfirm(
        GamePlayDataManager gameplay,
        PlayerRuntimeState pendingState,
        GameObject player,
        Object logContext)
    {
        if (gameplay == null || pendingState == null || player == null)
            return false;

        if (!PlayerSceneRestorePlanner.TryGatherPlayerComponents(player, logContext, out var ctx))
            return false;

        if (!PlayerSceneRestorePlanner.MatchesPendingEquipmentState(pendingState, ctx))
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] Restored equipment state does not match pending state.", logContext);
            return false;
        }

        gameplay.ConsumePendingPlayerState();
        return true;
    }
}
