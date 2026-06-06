using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporarily hides the player from enemy target acquisition while an authored presentation owns the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTargetabilityBlocker : MonoBehaviour
{
    private readonly Dictionary<object, int> ownerBlockCounts = new();
    private int activeBlockCount;

    public bool IsTargetable => activeBlockCount <= 0;

    private void OnDisable()
    {
        ForceReleaseAll();
    }

    public void Acquire(object ownerToken)
    {
        if (ownerToken == null)
            return;

        ownerBlockCounts.TryGetValue(ownerToken, out int currentCount);
        ownerBlockCounts[ownerToken] = currentCount + 1;
        activeBlockCount++;
    }

    public void Release(object ownerToken)
    {
        if (ownerToken == null || activeBlockCount <= 0)
            return;

        if (!ownerBlockCounts.TryGetValue(ownerToken, out int currentCount) || currentCount <= 0)
            return;

        if (currentCount == 1)
            ownerBlockCounts.Remove(ownerToken);
        else
            ownerBlockCounts[ownerToken] = currentCount - 1;

        activeBlockCount = Mathf.Max(0, activeBlockCount - 1);
    }

    public void ForceReleaseAll()
    {
        ownerBlockCounts.Clear();
        activeBlockCount = 0;
    }

    public static PlayerTargetabilityBlocker GetOrAdd(Transform playerTransform)
    {
        if (playerTransform == null)
            return null;

        PlayerTargetabilityBlocker blocker = playerTransform.GetComponent<PlayerTargetabilityBlocker>();
        if (blocker == null)
            blocker = playerTransform.gameObject.AddComponent<PlayerTargetabilityBlocker>();

        return blocker;
    }
}
