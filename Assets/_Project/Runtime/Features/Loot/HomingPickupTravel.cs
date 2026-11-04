using UnityEngine;

/// <summary>Shared combat sampling, stone homing acceleration and pre-travel collection; owns no objects or currency.</summary>
public static class HomingPickupTravel
{
    private static int combatSampleFrame = -1;
    private static bool combatSample;

    // Sample once in the pickups' LateUpdate phase, after combat/wave updates.
    // Every EXP/gold pickup sees the same release decision, including the final kill's drops.
    internal static bool IsCombatActiveForPickupFrame
    {
        get
        {
            if (combatSampleFrame != Time.frameCount)
            {
                combatSampleFrame = Time.frameCount;
                combatSample = MonsterSpawnRoomGroup.IsPlayerInCombat;
            }
            return combatSample;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCombatSample()
    {
        combatSampleFrame = -1;
        combatSample = false;
    }

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
            WarningPopupPlayback.ShowMessage(GameText.Get("code.homingpickuptravel.4b200fb9cf", "획득물을 정산하지 못했습니다. 잠시 후 다시 이동해 주세요."));
        return succeeded;
    }
}

/// <summary>Per-pickup presentation/travel state; the pickup still owns its reward and lifetime.</summary>
internal sealed class CombatRewardPickupMotion
{
    private enum Phase { Grounded, Rising, Following }

    private Transform owner;
    private SpriteRenderer spriteRenderer;
    private float authoredAlpha;
    private Vector3 groundPosition;
    private Vector3 homingOrigin;
    private float riseSeconds;
    private float homingProgress;
    private Phase phase;

    public void Reset(Transform pickup, SpriteRenderer renderer, float groundAlpha)
    {
        Cancel();
        owner = pickup;
        spriteRenderer = renderer;
        authoredAlpha = renderer != null ? renderer.color.a : 1f;
        groundPosition = pickup.position;
        SetAlpha(groundAlpha);
    }

    public void Cancel()
    {
        if (owner != null && phase == Phase.Rising)
            owner.position = groundPosition;
        SetAlpha(1f);
        phase = Phase.Grounded;
        riseSeconds = 0f;
        homingProgress = 0f;
    }

    public bool Tick(Transform target, bool inCombat, float groundAlpha,
        float riseHeight, float riseDuration, float homingSpeed)
    {
        if (inCombat)
        {
            if (phase != Phase.Grounded)
                Cancel();
            groundPosition = owner.position;
            SetAlpha(groundAlpha);
            return false;
        }

        if (target == null || Time.deltaTime <= 0f)
            return false;

        if (phase == Phase.Grounded)
        {
            groundPosition = owner.position;
            phase = Phase.Rising;
        }

        if (phase == Phase.Rising)
        {
            riseSeconds += Time.deltaTime;
            float progress = riseDuration > 0f ? Mathf.Clamp01(riseSeconds / riseDuration) : 1f;
            float eased = 1f - (1f - progress) * (1f - progress);
            owner.position = groundPosition + Vector3.up * (Mathf.Max(0f, riseHeight) * eased);
            SetAlpha(Mathf.Lerp(groundAlpha, 1f, eased));
            if (progress >= 1f)
            {
                homingOrigin = owner.position;
                phase = Phase.Following;
            }
            return false;
        }

        // Normalized progress gives every pickup the same arrival time regardless of distance.
        homingProgress = Mathf.Clamp01(homingProgress + Mathf.Max(0.01f, homingSpeed) * Time.deltaTime);
        owner.position = Vector3.Lerp(homingOrigin, target.position, homingProgress * homingProgress);
        return homingProgress >= 1f;
    }

    private void SetAlpha(float multiplier)
    {
        if (spriteRenderer == null) return;
        Color color = spriteRenderer.color;
        float alpha = authoredAlpha * Mathf.Clamp01(multiplier);
        if (Mathf.Approximately(color.a, alpha)) return;
        color.a = alpha;
        spriteRenderer.color = color;
    }
}
