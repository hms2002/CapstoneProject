using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 책임 : 미니맵 발견 모델이 방문 방의 직접 이웃만 공개하고 현재 방과 복구 상태를 안정적으로 유지하는지 회귀 검증한다.
/// </summary>
public sealed class DungeonMapDiscoveryPlayModeTests
{
    [Test]
    public void RevealStartRoom_OnlyRevealsDirectNeighbor()
    {
        DungeonMapDiscoveryModel model = CreateLinearModel();

        Assert.That(model.RevealInitialStartRoom(), Is.True);

        Assert.That(model.GetVisibility(0), Is.EqualTo(DungeonMapRoomVisibility.Visited));
        Assert.That(model.GetVisibility(1), Is.EqualTo(DungeonMapRoomVisibility.Revealed));
        Assert.That(model.GetVisibility(2), Is.EqualTo(DungeonMapRoomVisibility.Unknown));
        Assert.That(model.GetVisibility(3), Is.EqualTo(DungeonMapRoomVisibility.Unknown));
        Assert.That(model.CurrentRoomPlacementId, Is.EqualTo(-1));
    }

    [Test]
    public void EnterRoom_VisitsTargetAndRevealsOnlyItsDirectNeighbor()
    {
        DungeonMapDiscoveryModel model = CreateLinearModel();
        model.RevealInitialStartRoom();

        Assert.That(model.EnterRoom(1), Is.True);

        Assert.That(model.GetVisibility(0), Is.EqualTo(DungeonMapRoomVisibility.Visited));
        Assert.That(model.GetVisibility(1), Is.EqualTo(DungeonMapRoomVisibility.Visited));
        Assert.That(model.GetVisibility(2), Is.EqualTo(DungeonMapRoomVisibility.Revealed));
        Assert.That(model.GetVisibility(3), Is.EqualTo(DungeonMapRoomVisibility.Unknown));
        Assert.That(model.CurrentRoomPlacementId, Is.EqualTo(1));
    }

    [Test]
    public void Restore_FiltersUnknownIdsAndRestoresVisitedAdjacency()
    {
        DungeonMapDiscoveryModel model = CreateLinearModel();

        model.Restore(
            new[] { 1, 999 },
            new[] { 1, 999 });

        Assert.That(model.GetVisibility(0), Is.EqualTo(DungeonMapRoomVisibility.Revealed));
        Assert.That(model.GetVisibility(1), Is.EqualTo(DungeonMapRoomVisibility.Visited));
        Assert.That(model.GetVisibility(2), Is.EqualTo(DungeonMapRoomVisibility.Revealed));
        Assert.That(model.GetVisibility(3), Is.EqualTo(DungeonMapRoomVisibility.Unknown));
        Assert.That(model.VisitedRoomPlacementIds, Is.EquivalentTo(new[] { 1 }));
        Assert.That(model.CurrentRoomPlacementId, Is.EqualTo(-1));
    }

    private static DungeonMapDiscoveryModel CreateLinearModel()
    {
        var rooms = new[]
        {
            new DungeonMapRoomNode(0, RoomType.Start, new Vector2(0f, 0f)),
            new DungeonMapRoomNode(1, RoomType.Combat, new Vector2(1f, 0f)),
            new DungeonMapRoomNode(2, RoomType.Treasure, new Vector2(2f, 0f)),
            new DungeonMapRoomNode(3, RoomType.Boss, new Vector2(3f, 0f))
        };
        var connections = new[]
        {
            new DungeonMapConnection(0, 1),
            new DungeonMapConnection(1, 2),
            new DungeonMapConnection(2, 3)
        };
        return new DungeonMapDiscoveryModel(
            new DungeonMapGraphSnapshot(rooms, connections));
    }
}
