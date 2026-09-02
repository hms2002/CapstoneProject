using UnityEngine;

/// <summary>
/// 책임:
/// - 탐색형 절차 던전의 권장 방 수, 보스 거리, 분기/순환 수와 필수 방 역할 수를 데이터로 보관한다.
/// - DungeonGenerator와 제작 툴이 구체적인 그래프 생성 규칙을 하드코딩하지 않고 같은 기획 정책을 공유하게 한다.
/// </summary>
[CreateAssetMenu(fileName = "DungeonLayoutPolicy", menuName = "Gameplay/Dungeon/Layout Policy")]
public sealed class DungeonLayoutPolicySO : ScriptableObject
{
    [Header("Map Scale")]
    [SerializeField, Min(2)] private int recommendedMinimumRoomCount = 12;
    [SerializeField, Min(2)] private int recommendedMaximumRoomCount = 18;

    [Header("Critical Path")]
    [SerializeField, Min(1)] private int minimumBossGraphDistance = 6;
    [SerializeField, Min(1)] private int maximumBossGraphDistance = 8;

    [Header("Exploration Topology")]
    [SerializeField, Min(0)] private int minimumMeaningfulBranches = 2;
    [SerializeField, Min(0)] private int maximumMeaningfulBranches = 4;
    [SerializeField, Min(0)] private int minimumCycleConnections = 1;
    [SerializeField, Min(0)] private int maximumCycleConnections = 2;
    [SerializeField, Min(1)] private int maximumTopologyAttempts = 256;

    [Header("Guaranteed Room Roles")]
    [SerializeField, Min(0)] private int treasureRoomCount = 1;
    [SerializeField, Min(0)] private int eventRoomCount;
    [SerializeField, Min(0)] private int shopRoomCount;
    [SerializeField, Min(0)] private int minimumCombatRoomCount = 4;
    [SerializeField] private bool preferSpecialRoomsAtDeadEnds = true;

    public int RecommendedMinimumRoomCount => Mathf.Max(2, recommendedMinimumRoomCount);
    public int RecommendedMaximumRoomCount => Mathf.Max(RecommendedMinimumRoomCount, recommendedMaximumRoomCount);
    public int MinimumBossGraphDistance => Mathf.Max(1, minimumBossGraphDistance);
    public int MaximumBossGraphDistance => Mathf.Max(MinimumBossGraphDistance, maximumBossGraphDistance);
    public int MinimumMeaningfulBranches => Mathf.Max(0, minimumMeaningfulBranches);
    public int MaximumMeaningfulBranches => Mathf.Max(MinimumMeaningfulBranches, maximumMeaningfulBranches);
    public int MinimumCycleConnections => Mathf.Max(0, minimumCycleConnections);
    public int MaximumCycleConnections => Mathf.Max(MinimumCycleConnections, maximumCycleConnections);
    public int MaximumTopologyAttempts => Mathf.Max(1, maximumTopologyAttempts);
    public int TreasureRoomCount => Mathf.Max(0, treasureRoomCount);
    public int EventRoomCount => Mathf.Max(0, eventRoomCount);
    public int ShopRoomCount => Mathf.Max(0, shopRoomCount);
    public int MinimumCombatRoomCount => Mathf.Max(0, minimumCombatRoomCount);
    public bool PreferSpecialRoomsAtDeadEnds => preferSpecialRoomsAtDeadEnds;

    public bool IsWithinRecommendedRoomCount(int roomCount)
    {
        return roomCount >= RecommendedMinimumRoomCount &&
               roomCount <= RecommendedMaximumRoomCount;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 책임:
    /// - 설치기가 공유 탐색 정책 에셋을 재현 가능하게 구성하고 Inspector 수동 입력과 같은 정규화 규칙을 적용하게 한다.
    /// </summary>
    public void EditorConfigure(
        int recommendedMinimumRooms,
        int recommendedMaximumRooms,
        int minimumBossDistance,
        int maximumBossDistance,
        int minimumBranches,
        int maximumBranches,
        int minimumCycles,
        int maximumCycles,
        int topologyAttempts,
        int requiredTreasureRooms,
        int requiredEventRooms,
        int requiredShopRooms,
        int requiredMinimumCombatRooms,
        bool shouldPreferSpecialRoomsAtDeadEnds)
    {
        recommendedMinimumRoomCount = Mathf.Max(2, recommendedMinimumRooms);
        recommendedMaximumRoomCount = Mathf.Max(recommendedMinimumRoomCount, recommendedMaximumRooms);
        minimumBossGraphDistance = Mathf.Max(1, minimumBossDistance);
        maximumBossGraphDistance = Mathf.Max(minimumBossGraphDistance, maximumBossDistance);
        minimumMeaningfulBranches = Mathf.Max(0, minimumBranches);
        maximumMeaningfulBranches = Mathf.Max(minimumMeaningfulBranches, maximumBranches);
        minimumCycleConnections = Mathf.Max(0, minimumCycles);
        maximumCycleConnections = Mathf.Max(minimumCycleConnections, maximumCycles);
        maximumTopologyAttempts = Mathf.Max(1, topologyAttempts);
        treasureRoomCount = Mathf.Max(0, requiredTreasureRooms);
        eventRoomCount = Mathf.Max(0, requiredEventRooms);
        shopRoomCount = Mathf.Max(0, requiredShopRooms);
        minimumCombatRoomCount = Mathf.Max(0, requiredMinimumCombatRooms);
        preferSpecialRoomsAtDeadEnds = shouldPreferSpecialRoomsAtDeadEnds;
    }

    private void OnValidate()
    {
        recommendedMinimumRoomCount = Mathf.Max(2, recommendedMinimumRoomCount);
        recommendedMaximumRoomCount = Mathf.Max(recommendedMinimumRoomCount, recommendedMaximumRoomCount);
        minimumBossGraphDistance = Mathf.Max(1, minimumBossGraphDistance);
        maximumBossGraphDistance = Mathf.Max(minimumBossGraphDistance, maximumBossGraphDistance);
        minimumMeaningfulBranches = Mathf.Max(0, minimumMeaningfulBranches);
        maximumMeaningfulBranches = Mathf.Max(minimumMeaningfulBranches, maximumMeaningfulBranches);
        minimumCycleConnections = Mathf.Max(0, minimumCycleConnections);
        maximumCycleConnections = Mathf.Max(minimumCycleConnections, maximumCycleConnections);
        maximumTopologyAttempts = Mathf.Max(1, maximumTopologyAttempts);
        treasureRoomCount = Mathf.Max(0, treasureRoomCount);
        eventRoomCount = Mathf.Max(0, eventRoomCount);
        shopRoomCount = Mathf.Max(0, shopRoomCount);
        minimumCombatRoomCount = Mathf.Max(0, minimumCombatRoomCount);
    }
#endif
}
