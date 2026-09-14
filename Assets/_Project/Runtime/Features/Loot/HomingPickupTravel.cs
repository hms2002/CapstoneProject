using UnityEngine;

/// <summary>Shared homing acceleration and synchronous pre-travel collection; owns no objects or currency.</summary>
public static class HomingPickupTravel
{
    public static float Speed(float initialSpeed, float trackingSeconds)
        => Mathf.Min(40f, Mathf.Max(0f, initialSpeed) + 20f * Mathf.Max(0f, trackingSeconds));

    public static bool TryCollectFollowing(Transform player)
    {
        if (player == null) return false;
        bool succeeded = true;
        foreach (var pickup in Object.FindObjectsByType<ExperiencePickup2D>())
            if (pickup != null && pickup.isActiveAndEnabled)
                succeeded &= pickup.TryCollectForTravel(player);
        foreach (var pickup in Object.FindObjectsByType<GoldPickup2D>())
            if (pickup != null && pickup.isActiveAndEnabled)
                succeeded &= pickup.TryCollectForTravel(player);
        foreach (var pickup in Object.FindObjectsByType<MagicStonePickup>())
            if (pickup != null && pickup.isActiveAndEnabled)
                succeeded &= pickup.TryCollectForTravel(player);
        if (!succeeded)
            WarningPopupPlayback.ShowMessage("획득물을 정산하지 못했습니다. 잠시 후 다시 이동해 주세요.");
        return succeeded;
    }
}
