using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - Room Authoring Workspace가 기존에 열린 씬의 오브젝트와 dirty 상태를 변경하지 않는지 검증한다.
/// - 에디터 전용 룸 라이브러리 등록 계약이 동일 방의 중복 참조를 허용하지 않는지 검증한다.
/// - 실제 레이아웃 조립기 기반 시각 미리보기가 임시 데이터만 사용하고 안전하게 생성·정리되는지 검증한다.
/// </summary>
public static class RoomAuthoringToolValidator
{
    [MenuItem("Tools/Dungeon/Validate Room Authoring Tool Isolation")]
    public static void ValidateIsolation()
    {
        if (RoomAuthoringWorkspace.IsOpen)
        {
            throw new InvalidOperationException(
                "Close the current Room Authoring Workspace before running isolation validation.");
        }

        int originalSceneCount = SceneManager.sceneCount;
        Scene originalActiveScene = SceneManager.GetActiveScene();
        ulong originalActiveHandle = originalActiveScene.IsValid()
            ? originalActiveScene.handle.GetRawData()
            : 0UL;
        List<ulong> originalSceneHandles = new(originalSceneCount);
        List<int> originalRootCounts = new(originalSceneCount);
        List<bool> originalDirtyStates = new(originalSceneCount);
        for (int sceneIndex = 0; sceneIndex < originalSceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            originalSceneHandles.Add(scene.handle.GetRawData());
            originalRootCounts.Add(scene.rootCount);
            originalDirtyStates.Add(scene.isDirty);
        }

        RoomThemeLibrarySO testLibrary = null;
        RoomTemplateSO testRoom = null;
        RoomTemplateSO testCombatRoom = null;
        Tile testFloorTile = null;
        Tile testWallTile = null;
        try
        {
            Scene workspaceScene = RoomAuthoringWorkspace.Open();
            if (!workspaceScene.IsValid() || SceneManager.sceneCount != originalSceneCount + 1)
            {
                throw new InvalidOperationException(
                    "Room Authoring Workspace did not open as one isolated additive scene.");
            }

            GameObject authoringRoot = new("IsolationValidationRoom");
            authoringRoot.AddComponent<RoomPieceAuthoring>();
            Grid authoringGrid = authoringRoot.AddComponent<Grid>();
            RoomAuthoringWorkspace.MoveToWorkspace(authoringRoot);
            if (authoringRoot.scene != workspaceScene)
                throw new InvalidOperationException("Authoring root escaped the isolated workspace scene.");

            RoomAuthoringWorkspace.MarkDirty();
            if (!RoomAuthoringWorkspace.HasUnsavedChanges)
                throw new InvalidOperationException("Workspace did not track unsaved authoring changes.");

            RoomAuthoringWorkspace.MarkSaved();
            if (RoomAuthoringWorkspace.HasUnsavedChanges)
                throw new InvalidOperationException("Workspace did not clear its saved session state.");

            testLibrary = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
            testRoom = ScriptableObject.CreateInstance<RoomTemplateSO>();
            testCombatRoom = ScriptableObject.CreateInstance<RoomTemplateSO>();
            testFloorTile = ScriptableObject.CreateInstance<Tile>();
            testWallTile = ScriptableObject.CreateInstance<Tile>();
            testRoom.EditorSetData(
                CreateLinearRoomLayout("Preview_Start", RoomType.Start, includeLeftSocket: false),
                CreateClosedRoomBuildData(testFloorTile, testWallTile));
            testCombatRoom.EditorSetData(
                CreateLinearRoomLayout("Preview_Combat", RoomType.Combat, includeLeftSocket: true),
                CreateClosedRoomBuildData(testFloorTile, testWallTile));
            if (!testLibrary.EditorAddRoom(testRoom) ||
                testLibrary.EditorAddRoom(testRoom) ||
                !testLibrary.EditorAddRoom(testCombatRoom) ||
                testLibrary.Rooms.Count != 2)
            {
                throw new InvalidOperationException(
                    "Room library editor registration accepted a duplicate room reference.");
            }

            RoomLayoutData editedCombatLayout =
                CreateLinearRoomLayout("Preview_Combat_Edited", RoomType.Combat, includeLeftSocket: true);
            RoomBuildData editedCombatBuild =
                CreateClosedRoomBuildData(testFloorTile, testWallTile);
            RoomAuthoringDungeonPreviewResult previewResult =
                RoomAuthoringDungeonPreview.Generate(
                    new RoomAuthoringDungeonPreviewRequest(
                        testLibrary,
                        layoutPolicy: null,
                        includeCurrentRoom: true,
                        testCombatRoom,
                        editedCombatLayout,
                        editedCombatBuild,
                        authoringGrid,
                        new Vector2Int(4, 4),
                        testFloorTile,
                        testWallTile,
                        seed: 1942,
                        roomCount: 3,
                        includeBossRoom: false,
                        maxPlacementAttemptsPerRoom: 64,
                        minimumCorridorLength: 2,
                        corridorLengthPerRoomCell: 0f,
                        corridorLengthVariation: 0,
                        guaranteedRoomTemplates: null,
                        corridorDecorationProfile: null));
            if (!previewResult.WasBuilt ||
                !previewResult.IsComplete ||
                previewResult.RoomCount != 3 ||
                previewResult.CurrentRoomPlacementCount != 2 ||
                !RoomAuthoringDungeonPreview.HasPreview)
            {
                throw new InvalidOperationException(
                    $"Dynamic dungeon preview validation failed: {previewResult.Message}");
            }

            if (RoomAuthoringWorkspace.HasUnsavedChanges)
            {
                throw new InvalidOperationException(
                    "Dynamic preview incorrectly marked the authored room as unsaved.");
            }

            RoomAuthoringDungeonPreview.Clear();
            if (RoomAuthoringDungeonPreview.HasPreview ||
                RoomAuthoringWorkspace.HasUnsavedChanges ||
                testLibrary.Rooms.Count != 2)
            {
                throw new InvalidOperationException(
                    "Dynamic preview cleanup changed authoring state or the source room library.");
            }
        }
        finally
        {
            RoomAuthoringDungeonPreview.Clear();
            RoomAuthoringWorkspace.Close(confirmDiscard: false);
            if (testWallTile != null)
                UnityEngine.Object.DestroyImmediate(testWallTile);
            if (testFloorTile != null)
                UnityEngine.Object.DestroyImmediate(testFloorTile);
            if (testCombatRoom != null)
                UnityEngine.Object.DestroyImmediate(testCombatRoom);
            if (testRoom != null)
                UnityEngine.Object.DestroyImmediate(testRoom);
            if (testLibrary != null)
                UnityEngine.Object.DestroyImmediate(testLibrary);
        }

        if (SceneManager.sceneCount != originalSceneCount)
            throw new InvalidOperationException("Workspace close changed the original loaded scene count.");

        for (int sceneIndex = 0; sceneIndex < originalSceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (scene.handle.GetRawData() != originalSceneHandles[sceneIndex] ||
                scene.rootCount != originalRootCounts[sceneIndex] ||
                scene.isDirty != originalDirtyStates[sceneIndex])
            {
                throw new InvalidOperationException(
                    $"Original scene state changed during room authoring isolation validation: {scene.path}");
            }
        }

        Scene restoredActiveScene = SceneManager.GetActiveScene();
        if (originalActiveHandle != 0UL &&
            restoredActiveScene.handle.GetRawData() != originalActiveHandle)
        {
            throw new InvalidOperationException(
                "Workspace close did not restore the previously active scene.");
        }

        Debug.Log(
            $"Room Authoring Tool isolation validation passed. " +
            $"OriginalScenes={originalSceneCount}, LibraryDuplicateGuard=True, DynamicPreview=True");
    }

    private static RoomLayoutData CreateLinearRoomLayout(
        string roomId,
        RoomType roomType,
        bool includeLeftSocket)
    {
        List<RoomSocketData> sockets = new()
        {
            new RoomSocketData
            {
                socketId = "Right",
                localCell = new Vector2Int(3, 1),
                direction = RoomSocketDirection.Right,
                width = RoomSocketGeometry.RequiredWidth
            }
        };
        if (includeLeftSocket)
        {
            sockets.Add(new RoomSocketData
            {
                socketId = "Left",
                localCell = new Vector2Int(0, 1),
                direction = RoomSocketDirection.Left,
                width = RoomSocketGeometry.RequiredWidth
            });
        }

        return new RoomLayoutData
        {
            roomId = roomId,
            roomType = roomType,
            size = new Vector2Int(4, 4),
            localBounds = new RectInt(0, 0, 4, 4),
            sockets = sockets,
            difficultyTier = 0,
            selectionWeight = 1f
        };
    }

    private static RoomBuildData CreateClosedRoomBuildData(
        TileBase floorTile,
        TileBase wallTile)
    {
        List<RoomTileData> floorTiles = new();
        List<RoomTileData> wallTiles = new();
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                Vector2Int cell = new(x, y);
                floorTiles.Add(new RoomTileData
                {
                    localCell = cell,
                    tile = floorTile
                });
                if (x == 0 || x == 3 || y == 0 || y == 3)
                {
                    wallTiles.Add(new RoomTileData
                    {
                        localCell = cell,
                        tile = wallTile
                    });
                }
            }
        }

        return new RoomBuildData
        {
            floorTiles = floorTiles,
            wallTiles = wallTiles,
            objectPlacements = new List<RoomObjectPlacementData>()
        };
    }
}
