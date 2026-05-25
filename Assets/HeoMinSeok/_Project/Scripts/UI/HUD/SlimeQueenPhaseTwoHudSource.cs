using UnityEngine;

/// <summary>
/// 슬라임 여왕 2페이즈의 근거리/원거리 두 몸을 하나의 보스 HUD snapshot으로 변환합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SlimeQueenPhaseTwoHudSource : MonoBehaviour, IBossHudSource
{
    [SerializeField] private string displayName = "Slime Queen";
    [SerializeField] private string shortLabel = "Short";
    [SerializeField] private string longLabel = "Long";
    [SerializeField] private SlimeQueenP2Short shortBoss;
    [SerializeField] private SlimeQueenP2Long longBoss;

    public int Priority => 100;

    private void Awake()
    {
        AssignKnownBoss(GetComponent<SlimeQueenPhaseTwoBase>());
        ResolveBosses();
    }

    public static SlimeQueenPhaseTwoHudSource EnsureFor(SlimeQueenPhaseTwoBase owner)
    {
        if (owner == null)
            return null;

        SlimeQueenPhaseTwoHudSource source = owner.GetComponent<SlimeQueenPhaseTwoHudSource>();
        if (source == null)
            source = owner.gameObject.AddComponent<SlimeQueenPhaseTwoHudSource>();

        source.AssignKnownBoss(owner);
        source.ResolveBosses();
        return source;
    }

    public bool OwnsBoss(BossControllerBase boss)
    {
        if (boss == null)
            return false;

        return boss == shortBoss ||
               boss == longBoss ||
               boss is SlimeQueenP2Short ||
               boss is SlimeQueenP2Long;
    }

    public bool TryBuildSnapshot(out BossHudSnapshot snapshot)
    {
        snapshot = default;
        ResolveBosses();

        bool hasShort = IsLiveBoss(shortBoss);
        bool hasLong = IsLiveBoss(longBoss);
        if (!hasShort && !hasLong)
            return false;

        BossHudChannelSnapshot shortChannel = hasShort
            ? BossHudValueUtility.BuildBossChannel(shortBoss, shortLabel, false)
            : BossHudChannelSnapshot.Empty(shortLabel);

        BossHudChannelSnapshot longChannel = hasLong
            ? BossHudValueUtility.BuildBossChannel(longBoss, longLabel, false)
            : BossHudChannelSnapshot.Empty(longLabel);

        snapshot = BossHudSnapshot.Dual(
            string.IsNullOrWhiteSpace(displayName) ? "Slime Queen" : displayName,
            shortChannel,
            longChannel);
        return true;
    }

    private void AssignKnownBoss(SlimeQueenPhaseTwoBase boss)
    {
        if (boss is SlimeQueenP2Short shortQueen)
            shortBoss = shortQueen;
        else if (boss is SlimeQueenP2Long longQueen)
            longBoss = longQueen;
    }

    private void ResolveBosses()
    {
        if (!IsLiveBoss(shortBoss))
            shortBoss = FindLivePhaseTwo<SlimeQueenP2Short>();

        if (!IsLiveBoss(longBoss))
            longBoss = FindLivePhaseTwo<SlimeQueenP2Long>();
    }

    private static T FindLivePhaseTwo<T>() where T : SlimeQueenPhaseTwoBase
    {
        T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Exclude);
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (IsLiveBoss(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsLiveBoss(SlimeQueenPhaseTwoBase boss)
    {
        return boss != null &&
               boss.isActiveAndEnabled &&
               boss.gameObject.activeInHierarchy &&
               !boss.IsDead &&
               !boss.HasDeadTag() &&
               boss.CurrentHealthValue > 0f;
    }
}
