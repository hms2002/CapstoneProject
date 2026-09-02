using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 방 역할 하나의 방문 완료 아이콘과 인접 공개 실루엣 아이콘 및 각 표시 색상을 묶는다.
/// </summary>
[Serializable]
public struct DungeonMinimapRoomIconData
{
    [SerializeField] private RoomType roomType;
    [SerializeField] private Sprite normalIcon;
    [SerializeField] private Sprite silhouetteIcon;
    [SerializeField] private Color normalIconTint;
    [SerializeField] private Color silhouetteIconTint;

    public RoomType RoomType => roomType;
    public Sprite NormalIcon => normalIcon;
    public Sprite SilhouetteIcon => silhouetteIcon;
    public Color NormalIconTint => normalIconTint;
    public Color SilhouetteIconTint => silhouetteIconTint;

    public DungeonMinimapRoomIconData(RoomType type)
    {
        roomType = type;
        normalIcon = null;
        silhouetteIcon = null;
        normalIconTint = Color.white;
        silhouetteIconTint = new Color(1f, 1f, 1f, 0.55f);
    }
}

/// <summary>
/// 책임 : 미니맵의 RoomType별 정상/실루엣 아이콘과 노드, 연결선, 패널의 공통 표시 규칙을 단일 자산으로 제공한다.
/// </summary>
[CreateAssetMenu(
    fileName = "DungeonMinimapIconSet",
    menuName = "Project/Procedural Dungeon/Minimap Icon Set")]
public sealed class DungeonMinimapIconSetSO : ScriptableObject
{
    [Header("Room Icons")]
    [SerializeField] private List<DungeonMinimapRoomIconData> roomIcons = new();

    [Header("Colors")]
    [SerializeField] private Color visitedNodeColor = new(0.75f, 0.78f, 0.84f, 0.95f);
    [SerializeField] private Color revealedNodeColor = new(0.25f, 0.28f, 0.34f, 0.72f);
    [SerializeField] private Color currentNodeColor = new(1f, 0.72f, 0.2f, 1f);
    [SerializeField] private Color visitedConnectionColor = new(0.65f, 0.68f, 0.74f, 0.9f);
    [SerializeField] private Color revealedConnectionColor = new(0.25f, 0.28f, 0.34f, 0.68f);

    public Color VisitedNodeColor => visitedNodeColor;
    public Color RevealedNodeColor => revealedNodeColor;
    public Color CurrentNodeColor => currentNodeColor;
    public Color VisitedConnectionColor => visitedConnectionColor;
    public Color RevealedConnectionColor => revealedConnectionColor;

    public bool TryGetRoomIcon(RoomType roomType, out DungeonMinimapRoomIconData iconData)
    {
        for (int iconIndex = 0; iconIndex < roomIcons.Count; iconIndex++)
        {
            if (roomIcons[iconIndex].RoomType != roomType)
                continue;

            iconData = roomIcons[iconIndex];
            return true;
        }

        iconData = new DungeonMinimapRoomIconData(roomType);
        return false;
    }

}
