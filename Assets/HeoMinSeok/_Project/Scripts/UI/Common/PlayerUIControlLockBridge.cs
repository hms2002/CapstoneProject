using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - UI가 열려 있는 동안 플레이어 TagSystem에 입력 차단용 tag set을 ref-count 방식으로 제공/회수한다.
/// - 여러 UI가 동시에 잠금을 요청해도 마지막 UI가 닫힐 때까지 block tag가 유지되도록 조율한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerUIControlLockBridge : MonoBehaviour
{
    [SerializeField] private TagSystem tagSystem;

    private readonly Dictionary<int, int> ownerLockCounts = new();
    private readonly HashSet<GameplayTag> workingTags = new();
    private int activeLockCount;

    private void Awake()
    {
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
    }

    public bool Acquire(Object owner, GameplayTagSet tagSet)
    {
        if (owner == null || tagSet == null)
            return false;

        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();

        if (tagSystem == null)
            return false;

        int ownerId = owner.GetInstanceID();
        ownerLockCounts.TryGetValue(ownerId, out int currentCount);
        ownerLockCounts[ownerId] = currentCount + 1;

        activeLockCount++;
        if (activeLockCount == 1)
            ApplyTagSet(tagSet, add: true);

        return true;
    }

    public bool Release(Object owner, GameplayTagSet tagSet)
    {
        if (owner == null || tagSet == null || activeLockCount <= 0)
            return false;

        int ownerId = owner.GetInstanceID();
        if (!ownerLockCounts.TryGetValue(ownerId, out int currentCount) || currentCount <= 0)
            return false;

        if (currentCount == 1) ownerLockCounts.Remove(ownerId);
        else ownerLockCounts[ownerId] = currentCount - 1;

        activeLockCount--;
        if (activeLockCount == 0)
            ApplyTagSet(tagSet, add: false);

        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 플레이어 Transform에서 UI 잠금 브리지를 찾아 반환하고, 없으면 즉시 생성해 UI 흐름이 끊기지 않게 한다.
    /// </summary>
    public static PlayerUIControlLockBridge GetOrAdd(Transform playerTransform)
    {
        if (playerTransform == null)
            return null;

        var bridge = playerTransform.GetComponent<PlayerUIControlLockBridge>();
        if (bridge == null)
            bridge = playerTransform.gameObject.AddComponent<PlayerUIControlLockBridge>();

        return bridge;
    }

    private void ApplyTagSet(GameplayTagSet tagSet, bool add)
    {
        workingTags.Clear();
        tagSet.CollectTags(workingTags);

        foreach (var tag in workingTags)
        {
            if (tag == null)
                continue;

            if (add) tagSystem.AddTag(tag);
            else tagSystem.RemoveTag(tag);
        }
    }
}
