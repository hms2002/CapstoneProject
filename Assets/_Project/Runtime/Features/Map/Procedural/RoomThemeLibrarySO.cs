using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 하나의 보스/던전 테마에서 사용할 RoomTemplateSO 후보 목록을 보관한다.
/// - 레이아웃 조립기가 방 역할별 후보를 조회할 수 있는 가벼운 룸 라이브러리 역할을 한다.
/// - Editor 제작 툴이 검증 완료된 방을 중복 없이 명시적으로 등록할 수 있게 한다.
/// </summary>
[CreateAssetMenu(fileName = "RoomThemeLibrary", menuName = "Gameplay/Dungeon/Room Theme Library")]
public sealed class RoomThemeLibrarySO : ScriptableObject
{
    [SerializeField] private string themeId = "Theme_New";
    [SerializeField] private List<RoomTemplateSO> rooms = new();

    public string ThemeId => themeId;
    public IReadOnlyList<RoomTemplateSO> Rooms => rooms;

#if UNITY_EDITOR
    public bool EditorAddRoom(RoomTemplateSO room)
    {
        if (room == null)
            return false;

        rooms ??= new List<RoomTemplateSO>();
        if (rooms.Contains(room))
            return false;

        rooms.Add(room);
        return true;
    }
#endif

    public void CollectRooms(RoomType roomType, List<RoomTemplateSO> results)
    {
        if (results == null || rooms == null)
            return;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomTemplateSO room = rooms[i];
            if (room != null && room.LayoutData.roomType == roomType)
                results.Add(room);
        }
    }

    public void CollectExpansionRooms(List<RoomTemplateSO> results)
    {
        if (results == null || rooms == null)
            return;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomTemplateSO room = rooms[i];
            if (room != null && IsExpansionType(room.LayoutData.roomType))
                results.Add(room);
        }
    }

    private static bool IsExpansionType(RoomType roomType)
    {
        return roomType != RoomType.Start &&
               roomType != RoomType.Boss &&
               roomType != RoomType.Exit;
    }
}
