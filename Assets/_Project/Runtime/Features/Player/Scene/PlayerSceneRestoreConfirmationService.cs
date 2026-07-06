using UnityEngine;

// 책임: 복원된 플레이어 장비/인벤토리 상태가 pending 상태와 일치하는지 확인하고 소비 처리한다.
internal static class PlayerSceneRestoreConfirmationService
{
    public static bool TryConfirm(
        PlayerRuntimeState pendingState,
        GameObject player,
        Object logContext)
    {
        if (!RunSessionStore.IsAvailable || pendingState == null || player == null)
            return false;

        if (!PlayerSceneRestorePlanner.TryGatherPlayerComponents(player, logContext, out var ctx))
            return false;

        if (!PlayerSceneRestorePlanner.MatchesPendingEquipmentState(pendingState, ctx))
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] Restored equipment state does not match pending state.", logContext);
            return false;
        }

        RunSessionStore.ConsumePendingPlayerState();
        return true;
    }
}
