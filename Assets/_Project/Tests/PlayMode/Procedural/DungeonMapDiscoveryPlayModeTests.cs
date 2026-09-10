using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임 : 미니맵 발견 모델이 방문 방의 직접 이웃만 공개하고 현재 방과 복구 상태를 안정적으로 유지하는지 회귀 검증한다.
/// </summary>
public sealed class DungeonMapDiscoveryPlayModeTests
{
    [Test]
    public void InteriorAnchor_RectangleChoosesCenter()
    {
        Vector2 anchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(
            new[] { new RectInt(0, 0, 9, 7) }, new Vector2Int(9, 7));
        Assert.That(anchor, Is.EqualTo(new Vector2(0.5f, 0.5f)));
    }

    [Test]
    public void InteriorAnchor_ConcaveRoomMovesAwayFromInnerEdge()
    {
        var rectangles = new[] { new RectInt(0, 6, 9, 3), new RectInt(6, 0, 3, 6) };
        Vector2 anchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(rectangles, new Vector2Int(9, 9));
        Vector2 cell = anchor * 9f;
        Assert.That(IsCovered(rectangles, Vector2Int.FloorToInt(cell)), Is.True);
        Assert.That(Mathf.Max(cell.x, cell.y), Is.EqualTo(7.5f));
        Assert.That(Mathf.Min(cell.x, cell.y), Is.EqualTo(4.5f));
    }

    [Test]
    public void InteriorAnchor_RectangleSeamsAndOverlapsDoNotChangeResult()
    {
        var rectangles = new[]
        {
            new RectInt(0, 0, 4, 9), new RectInt(4, 0, 5, 9), new RectInt(2, 2, 5, 5)
        };
        Vector2 anchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(rectangles, new Vector2Int(9, 9));
        Assert.That(anchor, Is.EqualTo(new Vector2(0.5f, 0.5f)));
    }

    [Test]
    public void InteriorAnchor_HoleIsNotChosen()
    {
        var rectangles = new[]
        {
            new RectInt(0, 0, 11, 3), new RectInt(0, 8, 11, 3),
            new RectInt(0, 3, 3, 5), new RectInt(8, 3, 3, 5)
        };
        Vector2 anchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(rectangles, new Vector2Int(11, 11));
        Assert.That(IsCovered(rectangles, Vector2Int.FloorToInt(anchor * 11f)), Is.True);
        Assert.That(anchor, Is.Not.EqualTo(new Vector2(0.5f, 0.5f)));
    }

    [Test]
    public void InteriorAnchor_EmptyAndInvalidShapesUseCenter()
    {
        Assert.That(DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(null, Vector2Int.one),
            Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(
            new[] { new RectInt(-3, -3, 1, 1) }, new Vector2Int(9, 9)),
            Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(
            new[] { new RectInt(0, 0, 1, 1) }, Vector2Int.zero),
            Is.EqualTo(new Vector2(0.5f, 0.5f)));
    }

    [Test]
    public void InteriorAnchor_ThinRoomStaysInsideAndIsDeterministic()
    {
        var rectangles = new[] { new RectInt(3, 0, 1, 9) };
        Vector2 anchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(rectangles, new Vector2Int(9, 9));
        Assert.That(anchor.x * 9f, Is.EqualTo(3.5f).Within(0.0001f));
        Assert.That(anchor.y, Is.EqualTo(0.5f));
        Assert.That(DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(rectangles, new Vector2Int(9, 9)),
            Is.EqualTo(anchor));
    }

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

    [Test]
    public void RoomNode_PreservesWorldBoundsAndDerivesCenter()
    {
        Rect bounds = new(-12f, 8f, 40f, 20f);

        var room = new DungeonMapRoomNode(7, RoomType.Combat, bounds);

        Assert.That(room.WorldBounds, Is.EqualTo(bounds));
        Assert.That(room.WorldCenter, Is.EqualTo(new Vector2(8f, 18f)));
    }

    [Test]
    public void Connection_WithSocketEndpointsPreservesDoorwayGeometry()
    {
        Vector2 firstSocket = new(10f, 4.5f);
        Vector2 secondSocket = new(32f, 4.5f);

        var connection = new DungeonMapConnection(
            1,
            2,
            firstSocket,
            secondSocket);

        Assert.That(connection.HasSocketEndpoints, Is.True);
        Assert.That(connection.FirstWorldSocketCenter, Is.EqualTo(firstSocket));
        Assert.That(connection.SecondWorldSocketCenter, Is.EqualTo(secondSocket));
        Assert.That(
            connection.FirstWorldSocketCenter.y,
            Is.EqualTo(connection.SecondWorldSocketCenter.y));
    }

    [Test]
    public void ShapeBuilder_MergesFloorAndWallWhilePreservingEmptyCells()
    {
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            RoomBuildData buildData = new()
            {
                floorTiles = new System.Collections.Generic.List<RoomTileData>
                {
                    CreateTileData(new Vector2Int(0, 0), tile),
                    CreateTileData(new Vector2Int(1, 0), tile),
                    CreateTileData(new Vector2Int(0, 1), tile)
                },
                wallTiles = new System.Collections.Generic.List<RoomTileData>
                {
                    CreateTileData(new Vector2Int(0, 2), tile)
                }
            };

            RectInt[] rectangles = DungeonMapRoomShapeBuilder.Build(
                buildData,
                new RectInt(0, 0, 3, 3));

            Assert.That(CountCoveredCells(rectangles), Is.EqualTo(4));
            Assert.That(IsCovered(rectangles, new Vector2Int(0, 2)), Is.True);
            Assert.That(IsCovered(rectangles, new Vector2Int(1, 1)), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(tile);
        }
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

    private static RoomTileData CreateTileData(Vector2Int cell, TileBase tile)
    {
        return new RoomTileData
        {
            localCell = cell,
            tile = tile
        };
    }

    private static int CountCoveredCells(RectInt[] rectangles)
    {
        int count = 0;
        for (int index = 0; index < rectangles.Length; index++)
            count += rectangles[index].width * rectangles[index].height;
        return count;
    }

    private static bool IsCovered(RectInt[] rectangles, Vector2Int cell)
    {
        for (int index = 0; index < rectangles.Length; index++)
        {
            if (rectangles[index].Contains(cell))
                return true;
        }

        return false;
    }
}
