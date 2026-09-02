using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 절차 생성 방 앵커를 현재 방 내부에서만 찾을지, 생성된 던전 전체에서 찾을지 구분한다.
/// </summary>
public enum ProceduralRoomAnchorScope
{
    LocalRoom = 0,
    Dungeon = 1
}

/// <summary>
/// 책임 : NPC 같은 방 기능이 직접 Transform을 저장하지 않고 안정적인 slot Id와 조회 범위를 저장하게 한다.
/// </summary>
[Serializable]
public struct ProceduralRoomAnchorReference
{
    [SerializeField] private string slotId;
    [SerializeField] private ProceduralRoomAnchorScope scope;

    public string SlotId => slotId;
    public ProceduralRoomAnchorScope Scope => scope;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(slotId);

    public bool TryResolve(ProceduralRoomRuntimeContext context, out Transform anchor)
    {
        anchor = null;
        return context != null &&
               IsConfigured &&
               context.TryResolveAnchor(slotId, scope, out anchor);
    }
}

/// <summary>
/// 책임 : 생성이 끝난 방 기능이 자신의 방 정보와 앵커 조회기를 전달받는 공통 초기화 계약을 정의한다.
/// </summary>
public interface IProceduralRoomRuntimeFeature
{
    bool TryBindProceduralRoom(
        ProceduralRoomRuntimeContext context,
        out string failureReason);
}

/// <summary>
/// 책임 : 생성된 방 하나의 안정 Id, 실제 연결 소켓 방향과 로컬/던전 앵커 조회 기능을 방 기능 프리팹에 제공한다.
/// </summary>
public sealed class ProceduralRoomRuntimeContext
{
    private readonly IReadOnlyDictionary<string, Transform> localAnchors;
    private readonly IReadOnlyDictionary<string, Transform> dungeonAnchors;
    private readonly IReadOnlyList<RoomSocketDirection> connectedSocketDirections;

    public int RoomPlacementId { get; }
    public string RoomId { get; }
    public RoomTemplateSO RoomTemplate { get; }
    public IReadOnlyList<RoomSocketDirection> ConnectedSocketDirections =>
        connectedSocketDirections;

    public ProceduralRoomRuntimeContext(
        int roomPlacementId,
        RoomTemplateSO roomTemplate,
        IReadOnlyDictionary<string, Transform> localAnchors,
        IReadOnlyDictionary<string, Transform> dungeonAnchors,
        IReadOnlyList<RoomSocketDirection> connectedSocketDirections = null)
    {
        RoomPlacementId = roomPlacementId;
        RoomTemplate = roomTemplate;
        RoomId = roomTemplate != null
            ? roomTemplate.LayoutData.roomId
            : string.Empty;
        this.localAnchors = localAnchors;
        this.dungeonAnchors = dungeonAnchors;
        this.connectedSocketDirections = connectedSocketDirections ??
            Array.Empty<RoomSocketDirection>();
    }

    public bool IsConnected(RoomSocketDirection direction)
    {
        for (int directionIndex = 0;
             directionIndex < connectedSocketDirections.Count;
             directionIndex++)
        {
            if (connectedSocketDirections[directionIndex] == direction)
                return true;
        }

        return false;
    }

    public bool TryResolveAnchor(
        string slotId,
        ProceduralRoomAnchorScope scope,
        out Transform anchor)
    {
        anchor = null;
        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        IReadOnlyDictionary<string, Transform> source =
            scope == ProceduralRoomAnchorScope.Dungeon
                ? dungeonAnchors
                : localAnchors;
        return source != null &&
               source.TryGetValue(slotId, out anchor) &&
               anchor != null;
    }
}
