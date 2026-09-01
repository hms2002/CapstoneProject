using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 : 기존 SlimeCorridor의 런 특수 NPC 구성을 분석하고 절차 생성용 NPC 프리팹과 방 데이터를 반복 생성한다.
/// </summary>
public static class ProceduralSlimeNpcRoomInstaller
{
    private const string SourceScenePath = "Assets/_Project/Scenes/SlimeCorridor.unity";
    private const string ProceduralScenePath = "Assets/_Project/Scenes/ProceduralSlimeCorridor.unity";
    private const string PrefabFolder = "Assets/_Project/Prefabs/Map/Procedural/Npc";
    private const string ConstructionPrefabPath = PrefabFolder + "/SlimeConstructionNpcModule.prefab";
    private const string TeleportPrefabPath = PrefabFolder + "/SlimeTeleportNpc.prefab";
    private const string TeleportDestinationPrefabPath = PrefabFolder + "/SlimeTeleportDestination.prefab";
    private const string RoomFolder = "Assets/_Project/Data/Dungeon/Rooms/BossThemes/Slime";
    private const string LegacyNpcRoomPath = RoomFolder + "/Slime_Event_Npc.asset";
    private const string ConstructionRoomPath = RoomFolder + "/Slime_Event_ConstructionShortcut.asset";
    private const string TeleportRoomPath = RoomFolder + "/Slime_Event_TeleportRemote.asset";
    private const string NpcRoomSourcePath = RoomFolder + "/Slime_Treasure_Sacrifice.asset";
    private const string StartRoomPath = RoomFolder + "/Slime_Start.asset";
    private const string SlimeLibraryPath = "Assets/_Project/Data/Dungeon/Libraries/ProceduralSlimeLibrary.asset";
    private const string SlimeProfilePath = "Assets/_Project/Data/Dungeon/GenerationProfiles/ProceduralSlimeGenerationProfile.asset";
    private const string SpeechBubblePrefabPath = "Assets/_Project/Prefabs/UI/SpeechBubble/SpeechBubblePrefab.prefab";
    private const string TeleportAppearanceSlot = "SlimeTeleportAppearance";
    private const string TeleportLandingSlot = "SlimeTeleportArrival";
    private const string ConstructionRoomId = "Slime_Event_ConstructionShortcut";
    private const string TeleportRoomId = "Slime_Event_TeleportRemote";
    private const int TeleportMinimumGraphDistance = 4;
    private static readonly Vector2Int ConstructionPivotCell = new(8, 5);
    private static readonly Vector2 ConstructionPivotOffset = new(0.5f, 0.5f);
    private static readonly Vector3Int ConstructionSiteMinimumCell = new(-6, -2, 0);

    [MenuItem("Tools/Dungeon/Slime NPC Room/Install Procedural Content")]
    public static void Install()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(PrefabFolder);
        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        SpeechBubble speechBubblePrefab = LoadRequiredComponent<SpeechBubble>(SpeechBubblePrefabPath);

        GameObject constructionPrefab = CreateConstructionNpcPrefab(
            sourceScene,
            speechBubblePrefab);
        GameObject teleportPrefab = CreateTeleportNpcPrefab(
            sourceScene,
            speechBubblePrefab);
        GameObject destinationPrefab = CreateTeleportDestinationPrefab();

        RoomTemplateSO constructionRoom = CreateOrUpdateConstructionRoom(
            constructionPrefab);
        RoomTemplateSO teleportRoom = CreateOrUpdateNpcRoom(
            TeleportRoomPath,
            TeleportRoomId,
            teleportPrefab,
            new Vector2Int(9, 4),
            new RoomTopologyPlacementData
            {
                mode = RoomTopologyPlacementMode.FarthestFromStart,
                minimumGraphDistanceFromStart = TeleportMinimumGraphDistance,
                requireDeadEnd = true
            });
        RemoveDestinationFromStartRoom();
        SealNpcRooms(constructionRoom, teleportRoom);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateSealedContent(constructionRoom, teleportRoom);
        Debug.Log(
            "[ProceduralSlimeNpcRoomInstaller] Refreshed self-contained Slime NPC assets but kept " +
            "the shortcut/teleport rooms sealed outside procedural generation.");
    }

    /// <summary>
    /// 책임:
    /// - 현재 제작된 Slime 특수 NPC 방과 순간이동 도착 프롭을 절차 생성 등록에서 제거한다.
    /// - 구현 에셋은 삭제하지 않아 기획 승인 뒤 다시 연결할 수 있게 보존한다.
    /// </summary>
    [MenuItem("Tools/Dungeon/Slime NPC Room/Seal Procedural Content")]
    public static void SealInstalledContent()
    {
        RoomTemplateSO constructionRoom =
            AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(ConstructionRoomPath);
        RoomTemplateSO teleportRoom =
            AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(TeleportRoomPath);
        RemoveDestinationFromStartRoom();
        SealNpcRooms(constructionRoom, teleportRoom);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateSealedContent(constructionRoom, teleportRoom);
        Debug.Log(
            "[ProceduralSlimeNpcRoomInstaller] Sealed Slime special NPC rooms and removed their start-room destination.");
    }

    public static void ValidateRoomAuthoringIntegration()
    {
        EditorSceneManager.OpenScene(ProceduralScenePath, OpenSceneMode.Single);
        RoomAuthoringToolValidator.ValidateIsolation();
    }

    private static GameObject CreateConstructionNpcPrefab(
        Scene sourceScene,
        SpeechBubble speechBubblePrefab)
    {
        GameObject sourceNpc = FindRequiredNamedObject(sourceScene, "ConstructionNpc");
        GameObject sourceSite = FindRequiredNamedObject(sourceScene, "ConstructionSite");
        GameObject npcClone = Object.Instantiate(sourceNpc);
        GameObject siteClone = Object.Instantiate(sourceSite);
        try
        {
            npcClone.name = "SlimeConstructionNpcModule";
            siteClone.name = "ConstructionSite";
            npcClone.transform.SetParent(null, worldPositionStays: true);

            Grid sourceGrid = sourceSite.GetComponentInParent<Grid>();
            Grid prefabGrid = siteClone.GetComponent<Grid>();
            if (prefabGrid == null)
                prefabGrid = siteClone.AddComponent<Grid>();
            if (sourceGrid != null)
                EditorUtility.CopySerialized(sourceGrid, prefabGrid);

            siteClone.transform.SetParent(npcClone.transform, worldPositionStays: false);
            siteClone.transform.localPosition = Vector3.zero;
            siteClone.transform.localRotation = Quaternion.identity;
            siteClone.transform.localScale = Vector3.one;
            NormalizeConstructionSiteTileCells(
                siteClone,
                ConstructionSiteMinimumCell);

            RemoveOptionalChild(npcClone.transform, "TestTeleportPoint");
            ConfigureSpeechAndPresenter(npcClone, speechBubblePrefab);

            ConstructionSiteTilemapModule siteModule =
                siteClone.GetComponent<ConstructionSiteTilemapModule>();
            RunConstructionNpcFeature feature =
                npcClone.GetComponentInChildren<RunConstructionNpcFeature>(includeInactive: true);
            if (siteModule == null || feature == null)
                throw new InvalidOperationException("Construction NPC source contract is incomplete.");

            ProceduralConstructionShortcutBinder shortcutBinder =
                npcClone.GetComponent<ProceduralConstructionShortcutBinder>();
            if (shortcutBinder == null)
                shortcutBinder = npcClone.AddComponent<ProceduralConstructionShortcutBinder>();
            shortcutBinder.EditorConfigure(
                siteClone.transform,
                RoomSocketDirection.Left);

            SetObjectReference(feature, "constructionSiteModule", siteModule);
            SetObjectReference(
                feature,
                "blockedStateRoot",
                FindRequiredChild(siteClone.transform, "ConstructionYet").gameObject);
            SetObjectReference(
                feature,
                "openStateRoot",
                FindRequiredChild(siteClone.transform, "ConstructionComplete").gameObject);

            NormalizePrefabRoot(npcClone.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(npcClone, ConstructionPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Could not save prefab: {ConstructionPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(siteClone);
            Object.DestroyImmediate(npcClone);
        }
    }

    private static GameObject CreateTeleportNpcPrefab(
        Scene sourceScene,
        SpeechBubble speechBubblePrefab)
    {
        GameObject sourceNpc = FindRequiredNamedObject(sourceScene, "TeleportNPC");
        GameObject npcClone = Object.Instantiate(sourceNpc);
        try
        {
            npcClone.name = "SlimeTeleportNpc";
            npcClone.transform.SetParent(null, worldPositionStays: true);
            RemoveOptionalChild(npcClone.transform, "TestTeleportPoint");
            ConfigureSpeechAndPresenter(npcClone, speechBubblePrefab);

            RunSameSceneTeleportNpcFeature feature =
                npcClone.GetComponentInChildren<RunSameSceneTeleportNpcFeature>(includeInactive: true);
            if (feature == null)
                throw new InvalidOperationException("Teleport NPC source feature is missing.");

            SetObjectReference(feature, "appearancePoint", null);
            SetObjectReference(feature, "landingPoint", null);
            SetAnchorReference(
                feature,
                "proceduralAppearancePoint",
                TeleportAppearanceSlot,
                ProceduralRoomAnchorScope.Dungeon);
            SetAnchorReference(
                feature,
                "proceduralLandingPoint",
                TeleportLandingSlot,
                ProceduralRoomAnchorScope.Dungeon);

            NormalizePrefabRoot(npcClone.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(npcClone, TeleportPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Could not save prefab: {TeleportPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(npcClone);
        }
    }

    private static GameObject CreateTeleportDestinationPrefab()
    {
        var root = new GameObject("SlimeTeleportDestination");
        try
        {
            CreateAnchorChild(
                root.transform,
                "Appearance",
                new Vector3(0f, 1.5f, 0f),
                TeleportAppearanceSlot);
            CreateAnchorChild(
                root.transform,
                "Landing",
                new Vector3(0f, 0f, 0f),
                TeleportLandingSlot);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                TeleportDestinationPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException(
                    $"Could not save prefab: {TeleportDestinationPrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateAnchorChild(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        string slotId)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(parent, worldPositionStays: false);
        child.transform.localPosition = localPosition;
        ProceduralRoomAnchor anchor = child.AddComponent<ProceduralRoomAnchor>();
        anchor.EditorConfigure(slotId, ProceduralRoomAnchorScope.Dungeon);
    }

    private static RoomTemplateSO CreateOrUpdateConstructionRoom(GameObject npcPrefab)
    {
        RoomTemplateSO source = LoadRequiredAsset<RoomTemplateSO>(NpcRoomSourcePath);
        RoomTemplateSO room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(ConstructionRoomPath);
        if (room == null)
        {
            room = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(room, ConstructionRoomPath);
        }

        RoomLayoutData layout = source.LayoutData;
        layout.roomId = ConstructionRoomId;
        layout.roomType = RoomType.Event;
        layout.difficultyTier = 0;
        layout.selectionWeight = 1f;
        layout.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.CycleDetour
        };
        layout.sockets = CreateFourWaySockets(layout.localBounds);

        Vector2Int expectedPivotCell = new(
            layout.localBounds.xMin + layout.localBounds.width / 2 - 1,
            layout.localBounds.yMin + layout.localBounds.height / 2 - 1);
        if (expectedPivotCell != ConstructionPivotCell)
        {
            throw new InvalidOperationException(
                $"Construction shortcut source bounds changed. Expected pivot cell " +
                $"{ConstructionPivotCell}, resolved {expectedPivotCell} from {layout.localBounds}.");
        }

        RoomBuildData sourceBuild = source.BuildData;
        List<RoomTileData> floorTiles = CreateShortcutFloorTiles(
            sourceBuild.floorTiles,
            layout.localBounds);
        EnsureSocketFloorTiles(floorTiles, layout.sockets, layout.localBounds);
        RoomBuildData build = new()
        {
            floorTiles = floorTiles,
            wallTiles = CreateShortcutWallTiles(
                sourceBuild.wallTiles,
                layout.localBounds,
                layout.sockets),
            objectPlacements = new List<RoomObjectPlacementData>
            {
                CreatePropPlacement(
                    npcPrefab.name,
                    npcPrefab,
                    ConstructionPivotCell,
                    ConstructionPivotOffset)
            },
            travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>()
        };

        room.EditorSetData(layout, build);
        EditorUtility.SetDirty(room);
        return room;
    }

    private static List<RoomTileData> CreateShortcutFloorTiles(
        IReadOnlyList<RoomTileData> sourceTiles,
        RectInt bounds)
    {
        var results = new List<RoomTileData>();
        foreach (Vector2Int cell in EnumerateCells(bounds))
        {
            if (!IsShortcutWalkableCell(cell, bounds))
                continue;

            TileBase tile = FindTileForCell(sourceTiles, cell);
            if (tile == null)
            {
                throw new InvalidOperationException(
                    $"Could not resolve a Slime floor tile for shortcut cell {cell}.");
            }

            results.Add(new RoomTileData
            {
                localCell = cell,
                tile = tile
            });
        }

        return results;
    }

    private static List<RoomTileData> CreateShortcutWallTiles(
        IReadOnlyList<RoomTileData> sourceTiles,
        RectInt bounds,
        IReadOnlyList<RoomSocketData> sockets)
    {
        TileBase fillTile = FindMostFrequentTile(sourceTiles);
        if (fillTile == null)
            throw new InvalidOperationException("The Slime source room has no wall tile to build a shortcut boundary.");

        var socketCells = new HashSet<Vector2Int>();
        for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
        {
            RoomSocketData socket = sockets[socketIndex];
            for (int cellIndex = 0;
                 cellIndex < RoomSocketGeometry.ResolveWidth(socket);
                 cellIndex++)
            {
                socketCells.Add(RoomSocketGeometry.GetLocalCell(socket, cellIndex));
            }
        }

        var results = new List<RoomTileData>();
        foreach (Vector2Int cell in EnumerateCells(bounds))
        {
            if (IsShortcutWalkableCell(cell, bounds) && !socketCells.Contains(cell))
                continue;

            results.Add(new RoomTileData
            {
                localCell = cell,
                tile = FindExactTile(sourceTiles, cell) ?? fillTile
            });
        }

        return results;
    }

    private static IEnumerable<Vector2Int> EnumerateCells(RectInt bounds)
    {
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                yield return new Vector2Int(x, y);
        }
    }

    private static bool IsShortcutWalkableCell(Vector2Int cell, RectInt bounds)
    {
        int pivotX = bounds.xMin + bounds.width / 2;
        int pivotY = bounds.yMin + bounds.height / 2;
        bool horizontalArm = cell.y >= pivotY - 1 &&
                             cell.y <= pivotY;
        bool verticalArm = cell.x >= pivotX - 1 &&
                           cell.x <= pivotX;
        bool centralNpcArea = cell.x >= pivotX - 2 &&
                              cell.x <= pivotX + 1 &&
                              cell.y >= pivotY - 2 &&
                              cell.y <= pivotY + 1;
        return horizontalArm || verticalArm || centralNpcArea;
    }

    private static TileBase FindTileForCell(
        IReadOnlyList<RoomTileData> sourceTiles,
        Vector2Int cell)
    {
        TileBase exactTile = FindExactTile(sourceTiles, cell);
        if (exactTile != null)
            return exactTile;

        TileBase nearestTile = null;
        int nearestDistance = int.MaxValue;
        if (sourceTiles == null)
            return null;

        for (int tileIndex = 0; tileIndex < sourceTiles.Count; tileIndex++)
        {
            RoomTileData candidate = sourceTiles[tileIndex];
            if (candidate.tile == null)
                continue;

            int distance = Mathf.Abs(candidate.localCell.x - cell.x) +
                           Mathf.Abs(candidate.localCell.y - cell.y);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestTile = candidate.tile;
        }

        return nearestTile;
    }

    private static TileBase FindExactTile(
        IReadOnlyList<RoomTileData> sourceTiles,
        Vector2Int cell)
    {
        if (sourceTiles == null)
            return null;

        for (int tileIndex = 0; tileIndex < sourceTiles.Count; tileIndex++)
        {
            RoomTileData candidate = sourceTiles[tileIndex];
            if (candidate.localCell == cell && candidate.tile != null)
                return candidate.tile;
        }

        return null;
    }

    private static TileBase FindMostFrequentTile(IReadOnlyList<RoomTileData> sourceTiles)
    {
        if (sourceTiles == null)
            return null;

        var counts = new Dictionary<TileBase, int>();
        TileBase mostFrequentTile = null;
        int highestCount = 0;
        for (int tileIndex = 0; tileIndex < sourceTiles.Count; tileIndex++)
        {
            TileBase tile = sourceTiles[tileIndex].tile;
            if (tile == null)
                continue;

            counts.TryGetValue(tile, out int count);
            count++;
            counts[tile] = count;
            if (count <= highestCount)
                continue;

            highestCount = count;
            mostFrequentTile = tile;
        }

        return mostFrequentTile;
    }

    private static void NormalizeConstructionSiteTileCells(
        GameObject siteRoot,
        Vector3Int targetMinimumCell)
    {
        Tilemap[] tilemaps = siteRoot.GetComponentsInChildren<Tilemap>(includeInactive: true);
        bool foundTile = false;
        Vector3Int occupiedMinimum = new(int.MaxValue, int.MaxValue, 0);
        for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
        {
            Tilemap tilemap = tilemaps[tilemapIndex];
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;

                foundTile = true;
                occupiedMinimum.x = Mathf.Min(occupiedMinimum.x, cell.x);
                occupiedMinimum.y = Mathf.Min(occupiedMinimum.y, cell.y);
            }
        }

        if (!foundTile)
            throw new InvalidOperationException("The source construction site has no authored tiles.");

        Vector3Int offset = targetMinimumCell - occupiedMinimum;
        for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
        {
            Tilemap tilemap = tilemaps[tilemapIndex];
            var cells = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            var colors = new List<Color>();
            var transforms = new List<Matrix4x4>();
            var flags = new List<TileFlags>();
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(cell);
                if (tile == null)
                    continue;

                cells.Add(cell + offset);
                tiles.Add(tile);
                colors.Add(tilemap.GetColor(cell));
                transforms.Add(tilemap.GetTransformMatrix(cell));
                flags.Add(tilemap.GetTileFlags(cell));
            }

            tilemap.ClearAllTiles();
            for (int tileIndex = 0; tileIndex < cells.Count; tileIndex++)
            {
                Vector3Int cell = cells[tileIndex];
                tilemap.SetTile(cell, tiles[tileIndex]);
                tilemap.SetTileFlags(cell, TileFlags.None);
                tilemap.SetColor(cell, colors[tileIndex]);
                tilemap.SetTransformMatrix(cell, transforms[tileIndex]);
                tilemap.SetTileFlags(cell, flags[tileIndex]);
            }

            tilemap.CompressBounds();
        }
    }

    private static RoomTemplateSO CreateOrUpdateNpcRoom(
        string roomPath,
        string roomId,
        GameObject npcPrefab,
        Vector2Int localCell,
        RoomTopologyPlacementData topologyPlacement)
    {
        RoomTemplateSO source = LoadRequiredAsset<RoomTemplateSO>(NpcRoomSourcePath);
        RoomTemplateSO room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(roomPath);
        if (room == null)
        {
            room = ScriptableObject.CreateInstance<RoomTemplateSO>();
            AssetDatabase.CreateAsset(room, roomPath);
        }

        RoomLayoutData layout = source.LayoutData;
        layout.roomId = roomId;
        layout.roomType = RoomType.Event;
        layout.difficultyTier = 0;
        layout.selectionWeight = 1f;
        layout.topologyPlacement = topologyPlacement;
        layout.sockets = CreateFourWaySockets(layout.localBounds);

        RoomBuildData sourceBuild = source.BuildData;
        var floorTiles = sourceBuild.floorTiles != null
            ? new List<RoomTileData>(sourceBuild.floorTiles)
            : new List<RoomTileData>();
        EnsureSocketFloorTiles(floorTiles, layout.sockets, layout.localBounds);
        RoomBuildData build = new()
        {
            floorTiles = floorTiles,
            wallTiles = sourceBuild.wallTiles != null
                ? new List<RoomTileData>(sourceBuild.wallTiles)
                : new List<RoomTileData>(),
            objectPlacements = new List<RoomObjectPlacementData>
            {
                CreatePropPlacement(npcPrefab.name, npcPrefab, localCell)
            },
            travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>()
        };

        room.EditorSetData(layout, build);
        EditorUtility.SetDirty(room);
        return room;
    }

    private static List<RoomSocketData> CreateFourWaySockets(RectInt bounds)
    {
        if (bounds.width < RoomSocketGeometry.RequiredWidth + 2 ||
            bounds.height < RoomSocketGeometry.RequiredWidth + 2)
        {
            throw new InvalidOperationException(
                $"NPC room bounds are too small for four 2-cell sockets: {bounds}.");
        }

        int horizontalStart = bounds.xMin + (bounds.width - RoomSocketGeometry.RequiredWidth) / 2;
        int verticalStart = bounds.yMin + (bounds.height - RoomSocketGeometry.RequiredWidth) / 2;
        return new List<RoomSocketData>
        {
            CreateSocket("Up", new Vector2Int(horizontalStart, bounds.yMax - 1), RoomSocketDirection.Up),
            CreateSocket("Right", new Vector2Int(bounds.xMax - 1, verticalStart), RoomSocketDirection.Right),
            CreateSocket("Down", new Vector2Int(horizontalStart, bounds.yMin), RoomSocketDirection.Down),
            CreateSocket("Left", new Vector2Int(bounds.xMin, verticalStart), RoomSocketDirection.Left)
        };
    }

    private static RoomSocketData CreateSocket(
        string socketId,
        Vector2Int localCell,
        RoomSocketDirection direction)
    {
        return new RoomSocketData
        {
            socketId = socketId,
            localCell = localCell,
            direction = direction,
            width = RoomSocketGeometry.RequiredWidth
        };
    }

    private static void EnsureSocketFloorTiles(
        List<RoomTileData> floorTiles,
        IReadOnlyList<RoomSocketData> sockets,
        RectInt bounds)
    {
        var occupiedCells = new HashSet<Vector2Int>();
        for (int tileIndex = 0; tileIndex < floorTiles.Count; tileIndex++)
            occupiedCells.Add(floorTiles[tileIndex].localCell);

        for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
        {
            RoomSocketData socket = sockets[socketIndex];
            Vector2Int inward = GetInwardDirection(socket.direction);
            for (int cellIndex = 0; cellIndex < RoomSocketGeometry.ResolveWidth(socket); cellIndex++)
            {
                Vector2Int socketCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                if (occupiedCells.Contains(socketCell))
                    continue;

                TileBase floorTile = FindNearestInwardFloorTile(
                    floorTiles,
                    socketCell,
                    inward,
                    bounds);
                if (floorTile == null)
                {
                    throw new InvalidOperationException(
                        $"Could not extend floor data to socket '{socket.socketId}' cell {socketCell}.");
                }

                floorTiles.Add(new RoomTileData
                {
                    localCell = socketCell,
                    tile = floorTile
                });
                occupiedCells.Add(socketCell);
            }
        }
    }

    private static TileBase FindNearestInwardFloorTile(
        IReadOnlyList<RoomTileData> floorTiles,
        Vector2Int socketCell,
        Vector2Int inward,
        RectInt bounds)
    {
        int maximumDistance = Mathf.Max(bounds.width, bounds.height);
        for (int distance = 1; distance < maximumDistance; distance++)
        {
            Vector2Int candidateCell = socketCell + inward * distance;
            if (!bounds.Contains(candidateCell))
                break;

            for (int tileIndex = 0; tileIndex < floorTiles.Count; tileIndex++)
            {
                RoomTileData tile = floorTiles[tileIndex];
                if (tile.localCell == candidateCell && tile.tile != null)
                    return tile.tile;
            }
        }

        return null;
    }

    private static Vector2Int GetInwardDirection(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2Int.down,
            RoomSocketDirection.Right => Vector2Int.left,
            RoomSocketDirection.Down => Vector2Int.up,
            RoomSocketDirection.Left => Vector2Int.right,
            _ => Vector2Int.zero
        };
    }

    private static void AddDestinationToStartRoom(GameObject destinationPrefab)
    {
        RoomTemplateSO room = LoadRequiredAsset<RoomTemplateSO>(StartRoomPath);
        RoomLayoutData layout = room.LayoutData;
        layout.sockets = layout.sockets != null
            ? new List<RoomSocketData>(layout.sockets)
            : new List<RoomSocketData>();

        RoomBuildData sourceBuild = room.BuildData;
        var objectPlacements = sourceBuild.objectPlacements != null
            ? new List<RoomObjectPlacementData>(sourceBuild.objectPlacements)
            : new List<RoomObjectPlacementData>();
        for (int placementIndex = objectPlacements.Count - 1; placementIndex >= 0; placementIndex--)
        {
            if (objectPlacements[placementIndex].placementId == "SlimeTeleportDestination")
                objectPlacements.RemoveAt(placementIndex);
        }

        objectPlacements.Add(CreatePropPlacement(
            "SlimeTeleportDestination",
            destinationPrefab,
            new Vector2Int(6, 3)));
        RoomBuildData build = new()
        {
            floorTiles = sourceBuild.floorTiles != null
                ? new List<RoomTileData>(sourceBuild.floorTiles)
                : new List<RoomTileData>(),
            wallTiles = sourceBuild.wallTiles != null
                ? new List<RoomTileData>(sourceBuild.wallTiles)
                : new List<RoomTileData>(),
            objectPlacements = objectPlacements,
            travelEndpointPlacements = sourceBuild.travelEndpointPlacements != null
                ? new List<RoomTravelEndpointPlacementData>(sourceBuild.travelEndpointPlacements)
                : new List<RoomTravelEndpointPlacementData>()
        };
        room.EditorSetData(layout, build);
        EditorUtility.SetDirty(room);
    }

    private static RoomObjectPlacementData CreatePropPlacement(
        string placementId,
        GameObject prefab,
        Vector2Int localCell,
        Vector2 localOffset = default)
    {
        return new RoomObjectPlacementData
        {
            placementId = placementId,
            kind = RoomObjectKind.Prop,
            prefab = prefab,
            localCell = localCell,
            localOffset = localOffset,
            localRotationDegrees = 0f,
            localScale = Vector3.one,
            linkedChestLockPlacementId = string.Empty
        };
    }

    private static void SealNpcRooms(
        RoomTemplateSO constructionRoom,
        RoomTemplateSO teleportRoom)
    {
        RoomThemeLibrarySO library = LoadRequiredAsset<RoomThemeLibrarySO>(SlimeLibraryPath);
        RoomTemplateSO legacyRoom =
            AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(LegacyNpcRoomPath);
        library.EditorRemoveRoom(legacyRoom);
        library.EditorRemoveRoom(constructionRoom);
        library.EditorRemoveRoom(teleportRoom);
        EditorUtility.SetDirty(library);

        DungeonGenerationProfileSO profile =
            LoadRequiredAsset<DungeonGenerationProfileSO>(SlimeProfilePath);
        var guaranteedRooms = new List<RoomTemplateSO>();
        IReadOnlyList<RoomTemplateSO> existingRooms = profile.GuaranteedRoomTemplates;
        if (existingRooms != null)
        {
            for (int roomIndex = 0; roomIndex < existingRooms.Count; roomIndex++)
            {
                RoomTemplateSO room = existingRooms[roomIndex];
                if (room != null &&
                    room != legacyRoom &&
                    room != constructionRoom &&
                    room != teleportRoom &&
                    !guaranteedRooms.Contains(room))
                {
                    guaranteedRooms.Add(room);
                }
            }
        }

        profile.EditorSetGuaranteedRooms(guaranteedRooms);
        EditorUtility.SetDirty(profile);
    }

    /// <summary>
    /// 책임:
    /// - 봉인된 순간이동 NPC만 사용하던 Slime 시작 방 도착 프롭을 제거한다.
    /// </summary>
    private static void RemoveDestinationFromStartRoom()
    {
        RoomTemplateSO startRoom =
            AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(StartRoomPath);
        if (startRoom == null)
            return;

        RoomBuildData build = startRoom.BuildData;
        build.objectPlacements ??= new List<RoomObjectPlacementData>();
        build.objectPlacements.RemoveAll(
            placement => string.Equals(
                placement.placementId,
                "SlimeTeleportDestination",
                StringComparison.Ordinal));
        startRoom.EditorSetData(startRoom.LayoutData, build);
        EditorUtility.SetDirty(startRoom);
    }

    /// <summary>
    /// 책임:
    /// - 특수 NPC 방 에셋은 남아 있지만 현재 라이브러리/필수 방/시작 방에는 등록되지 않았는지 검증한다.
    /// </summary>
    private static void ValidateSealedContent(
        RoomTemplateSO constructionRoom,
        RoomTemplateSO teleportRoom)
    {
        RoomThemeLibrarySO library = LoadRequiredAsset<RoomThemeLibrarySO>(SlimeLibraryPath);
        DungeonGenerationProfileSO profile =
            LoadRequiredAsset<DungeonGenerationProfileSO>(SlimeProfilePath);
        if (library.ContainsRoom(constructionRoom) ||
            library.ContainsRoom(teleportRoom) ||
            ContainsRoom(profile.GuaranteedRoomTemplates, constructionRoom) ||
            ContainsRoom(profile.GuaranteedRoomTemplates, teleportRoom))
        {
            throw new InvalidOperationException(
                "Slime special NPC rooms remain registered after sealing.");
        }

        RoomTemplateSO startRoom = LoadRequiredAsset<RoomTemplateSO>(StartRoomPath);
        IReadOnlyList<RoomObjectPlacementData> placements =
            startRoom.BuildData.objectPlacements;
        for (int i = 0; placements != null && i < placements.Count; i++)
        {
            if (string.Equals(
                    placements[i].placementId,
                    "SlimeTeleportDestination",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Slime start-room teleport destination remains after NPC sealing.");
            }
        }
    }

    private static void ValidateInstalledContent(
        RoomTemplateSO constructionRoom,
        RoomTemplateSO teleportRoom)
    {
        ValidatePrefabAssetReferences(ConstructionPrefabPath);
        ValidatePrefabAssetReferences(TeleportPrefabPath);
        ValidatePrefabAssetReferences(TeleportDestinationPrefabPath);
        ValidateConstructionShortcutAuthoring(constructionRoom);

        RoomThemeLibrarySO library = LoadRequiredAsset<RoomThemeLibrarySO>(SlimeLibraryPath);
        DungeonGenerationProfileSO profile =
            LoadRequiredAsset<DungeonGenerationProfileSO>(SlimeProfilePath);
        RoomTemplateSO legacyRoom =
            AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(LegacyNpcRoomPath);
        if (!library.ContainsRoom(constructionRoom) ||
            !library.ContainsRoom(teleportRoom) ||
            !ContainsRoom(profile.GuaranteedRoomTemplates, constructionRoom) ||
            !ContainsRoom(profile.GuaranteedRoomTemplates, teleportRoom) ||
            library.ContainsRoom(legacyRoom) ||
            ContainsRoom(profile.GuaranteedRoomTemplates, legacyRoom))
        {
            throw new InvalidOperationException(
                "Separated Slime NPC rooms are not registered as the only guaranteed NPC content.");
        }

        DungeonLayoutResult layout = new DungeonGraphLayoutAssembler().Assemble(
            library,
            profile.LayoutPolicy,
            profile.Seed,
            profile.RoomCount,
            profile.MaxPlacementAttemptsPerRoom,
            profile.MinimumCorridorLength,
            profile.CorridorLengthPerRoomCell,
            profile.CorridorLengthVariation,
            profile.GuaranteedRoomTemplates);
        if (!layout.IsComplete)
            throw new InvalidOperationException($"NPC room layout failed: {layout.FailureReason}");

        ValidateNpcRoomPlacements(layout, constructionRoom, teleportRoom);
        ValidateGuaranteedNpcRoomsAcrossSeeds(
            library,
            profile,
            constructionRoom,
            teleportRoom);
        ValidateProceduralSceneGeneration();
    }

    private static void ValidateGuaranteedNpcRoomsAcrossSeeds(
        RoomThemeLibrarySO library,
        DungeonGenerationProfileSO profile,
        RoomTemplateSO constructionRoom,
        RoomTemplateSO teleportRoom)
    {
        const int seedCount = 64;
        for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
        {
            int seed = unchecked(profile.Seed + seedIndex * 7919);
            DungeonLayoutResult result = new DungeonGraphLayoutAssembler().Assemble(
                library,
                profile.LayoutPolicy,
                seed,
                profile.RoomCount,
                profile.MaxPlacementAttemptsPerRoom,
                profile.MinimumCorridorLength,
                profile.CorridorLengthPerRoomCell,
                profile.CorridorLengthVariation,
                profile.GuaranteedRoomTemplates);
            if (!result.IsComplete)
            {
                throw new InvalidOperationException(
                    $"Guaranteed NPC rooms seed sweep failed. Seed={seed}, " +
                    $"Reason={result.FailureReason}");
            }

            ValidateNpcRoomPlacements(result, constructionRoom, teleportRoom);
        }
    }

    private static void ValidateConstructionShortcutAuthoring(RoomTemplateSO room)
    {
        if (room == null)
            throw new InvalidOperationException("The construction shortcut room asset is missing.");

        RoomLayoutData layout = room.LayoutData;
        if (layout.sockets == null || layout.sockets.Count != 4)
        {
            throw new InvalidOperationException(
                "The construction shortcut room must expose one socket in every direction so any cycle corner can use it.");
        }

        var socketsByDirection = new Dictionary<RoomSocketDirection, RoomSocketData>();
        var socketCells = new HashSet<Vector2Int>();
        for (int socketIndex = 0; socketIndex < layout.sockets.Count; socketIndex++)
        {
            RoomSocketData socket = layout.sockets[socketIndex];
            if (!RoomSocketGeometry.IsValid(socket, layout.localBounds) ||
                !socketsByDirection.TryAdd(socket.direction, socket))
            {
                throw new InvalidOperationException(
                    $"Construction shortcut socket '{socket.socketId}' is invalid or duplicates {socket.direction}.");
            }

            for (int cellIndex = 0;
                 cellIndex < RoomSocketGeometry.ResolveWidth(socket);
                 cellIndex++)
            {
                socketCells.Add(RoomSocketGeometry.GetLocalCell(socket, cellIndex));
            }
        }

        var walkableCells = new HashSet<Vector2Int>();
        IReadOnlyList<RoomTileData> floorTiles = room.BuildData.floorTiles;
        IReadOnlyList<RoomTileData> wallTiles = room.BuildData.wallTiles;
        for (int tileIndex = 0; tileIndex < floorTiles.Count; tileIndex++)
        {
            if (floorTiles[tileIndex].tile != null)
                walkableCells.Add(floorTiles[tileIndex].localCell);
        }

        for (int tileIndex = 0; tileIndex < wallTiles.Count; tileIndex++)
        {
            if (wallTiles[tileIndex].tile != null &&
                !socketCells.Contains(wallTiles[tileIndex].localCell))
            {
                walkableCells.Remove(wallTiles[tileIndex].localCell);
            }
        }

        RoomSocketDirection[] directions =
        {
            RoomSocketDirection.Up,
            RoomSocketDirection.Right,
            RoomSocketDirection.Down,
            RoomSocketDirection.Left
        };
        for (int firstIndex = 0; firstIndex < directions.Length; firstIndex++)
        {
            for (int secondIndex = 0; secondIndex < directions.Length; secondIndex++)
            {
                RoomSocketDirection firstDirection = directions[firstIndex];
                RoomSocketDirection secondDirection = directions[secondIndex];
                int difference = Mathf.Abs((int)firstDirection - (int)secondDirection);
                if (difference != 1 && difference != 3)
                    continue;

                RoomSocketData firstSocket = socketsByDirection[firstDirection];
                RoomSocketData secondSocket = socketsByDirection[secondDirection];
                if (!CanTraverseShortcut(
                        walkableCells,
                        firstSocket,
                        secondSocket,
                        blockedCells: null))
                {
                    throw new InvalidOperationException(
                        $"Completed construction shortcut cannot connect {firstDirection} to {secondDirection}.");
                }

                HashSet<Vector2Int> barrierCells = ResolveShortcutBarrierCells(
                    layout.localBounds,
                    firstDirection);
                if (CanTraverseShortcut(
                        walkableCells,
                        firstSocket,
                        secondSocket,
                        barrierCells))
                {
                    throw new InvalidOperationException(
                        $"Incomplete construction shortcut can bypass the {firstDirection} gate toward {secondDirection}.");
                }
            }
        }

        IReadOnlyList<RoomObjectPlacementData> placements = room.BuildData.objectPlacements;
        if (placements == null || placements.Count != 1 ||
            placements[0].localCell != ConstructionPivotCell ||
            placements[0].localOffset != ConstructionPivotOffset)
        {
            throw new InvalidOperationException(
                "The construction NPC module must be placed at the shortcut room's grid-vertex pivot.");
        }

        GameObject prefab = placements[0].prefab;
        ProceduralConstructionShortcutBinder binder =
            prefab != null
                ? prefab.GetComponent<ProceduralConstructionShortcutBinder>()
                : null;
        ConstructionSiteTilemapModule site =
            prefab != null
                ? prefab.GetComponentInChildren<ConstructionSiteTilemapModule>(includeInactive: true)
                : null;
        if (binder == null || binder.OrientationRoot == null || site == null ||
            site.BlockedStateRoot == null || site.OpenStateRoot == null ||
            !binder.OrientationRoot.IsChildOf(prefab.transform))
        {
            throw new InvalidOperationException(
                "The construction NPC prefab is missing its rotatable local site/state-root contract.");
        }

        if (!TryCalculateOccupiedCellBounds(site.gameObject, out BoundsInt tileBounds) ||
            tileBounds.min != ConstructionSiteMinimumCell ||
            tileBounds.size != new Vector3Int(6, 4, 1))
        {
            throw new InvalidOperationException(
                $"Construction site tiles must occupy normalized cells " +
                $"{ConstructionSiteMinimumCell} with size (6, 4, 1); found {tileBounds}.");
        }
    }

    private static bool CanTraverseShortcut(
        HashSet<Vector2Int> walkableCells,
        RoomSocketData startSocket,
        RoomSocketData targetSocket,
        HashSet<Vector2Int> blockedCells)
    {
        var targets = new HashSet<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        for (int cellIndex = 0; cellIndex < RoomSocketGeometry.ResolveWidth(targetSocket); cellIndex++)
            targets.Add(RoomSocketGeometry.GetLocalCell(targetSocket, cellIndex));
        for (int cellIndex = 0; cellIndex < RoomSocketGeometry.ResolveWidth(startSocket); cellIndex++)
        {
            Vector2Int cell = RoomSocketGeometry.GetLocalCell(startSocket, cellIndex);
            if (walkableCells.Contains(cell) &&
                (blockedCells == null || !blockedCells.Contains(cell)) &&
                visited.Add(cell))
            {
                queue.Enqueue(cell);
            }
        }

        Vector2Int[] steps =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (targets.Contains(current))
                return true;

            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                Vector2Int next = current + steps[stepIndex];
                if (!walkableCells.Contains(next) ||
                    (blockedCells != null && blockedCells.Contains(next)) ||
                    !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static HashSet<Vector2Int> ResolveShortcutBarrierCells(
        RectInt bounds,
        RoomSocketDirection direction)
    {
        Vector2Int pivot = new(
            bounds.xMin + bounds.width / 2,
            bounds.yMin + bounds.height / 2);
        return direction switch
        {
            RoomSocketDirection.Up => new HashSet<Vector2Int>
            {
                new(pivot.x - 1, pivot.y + 2),
                new(pivot.x, pivot.y + 2)
            },
            RoomSocketDirection.Right => new HashSet<Vector2Int>
            {
                new(pivot.x + 2, pivot.y - 1),
                new(pivot.x + 2, pivot.y)
            },
            RoomSocketDirection.Down => new HashSet<Vector2Int>
            {
                new(pivot.x - 1, pivot.y - 3),
                new(pivot.x, pivot.y - 3)
            },
            RoomSocketDirection.Left => new HashSet<Vector2Int>
            {
                new(pivot.x - 3, pivot.y - 1),
                new(pivot.x - 3, pivot.y)
            },
            _ => new HashSet<Vector2Int>()
        };
    }

    private static void ValidateNpcRoomPlacements(
        DungeonLayoutResult layout,
        RoomTemplateSO constructionRoom,
        RoomTemplateSO teleportRoom)
    {
        DungeonRoomPlacement constructionPlacement = null;
        DungeonRoomPlacement teleportPlacement = null;
        int constructionCount = 0;
        int teleportCount = 0;
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement room = layout.Rooms[roomIndex];
            if (room.Template == constructionRoom)
            {
                constructionCount++;
                constructionPlacement = room;
            }
            else if (room.Template == teleportRoom)
            {
                teleportCount++;
                teleportPlacement = room;
            }
        }

        if (constructionCount != 1 || teleportCount != 1 ||
            constructionPlacement == null || teleportPlacement == null)
        {
            throw new InvalidOperationException(
                $"Each separated NPC room must be placed once. " +
                $"Construction={constructionCount}, Teleport={teleportCount}.");
        }

        if (!constructionPlacement.IsCycleDetour)
        {
            throw new InvalidOperationException(
                "Construction NPC room was not placed on a non-critical cycle detour.");
        }

        List<RoomSocketDirection> constructionDirections = CollectConnectedDirections(
            layout,
            constructionPlacement);
        int directionDifference = constructionDirections.Count == 2
            ? Mathf.Abs((int)constructionDirections[0] - (int)constructionDirections[1])
            : 0;
        if (constructionDirections.Count != 2 ||
            (directionDifference != 1 && directionDifference != 3))
        {
            throw new InvalidOperationException(
                $"Construction NPC room must occupy a two-arm cycle corner; found " +
                $"[{string.Join(", ", constructionDirections)}].");
        }

        if (!teleportPlacement.IsDeadEnd ||
            teleportPlacement.GraphDistanceFromStart < TeleportMinimumGraphDistance)
        {
            throw new InvalidOperationException(
                $"Teleport NPC room must be a remote dead end. " +
                $"Distance={teleportPlacement.GraphDistanceFromStart}, " +
                $"DeadEnd={teleportPlacement.IsDeadEnd}.");
        }

        int farthestEligibleDistance = 0;
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement room = layout.Rooms[roomIndex];
            if (room.Template != null &&
                room.Template.LayoutData.roomType != RoomType.Boss &&
                room.IsDeadEnd)
            {
                farthestEligibleDistance = Mathf.Max(
                    farthestEligibleDistance,
                    room.GraphDistanceFromStart);
            }
        }

        if (teleportPlacement.GraphDistanceFromStart != farthestEligibleDistance)
        {
            throw new InvalidOperationException(
                $"Teleport NPC room is not on the farthest eligible dead end. " +
                $"Actual={teleportPlacement.GraphDistanceFromStart}, " +
                $"Farthest={farthestEligibleDistance}.");
        }
    }

    private static void ValidateProceduralSceneGeneration()
    {
        Scene scene = EditorSceneManager.OpenScene(ProceduralScenePath, OpenSceneMode.Single);
        List<DungeonGenerator> generators = FindComponentsInScene<DungeonGenerator>(scene);
        if (generators.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one DungeonGenerator in '{ProceduralScenePath}', found {generators.Count}.");
        }

        DungeonGenerator generator = generators[0];
        if (!generator.Generate())
        {
            throw new InvalidOperationException(
                $"Procedural Slime scene generation failed: {generator.LastLayout?.FailureReason}");
        }

        List<RunSameSceneTeleportNpcFeature> teleportFeatures =
            FindComponentsInScene<RunSameSceneTeleportNpcFeature>(scene);
        List<RunConstructionNpcFeature> constructionFeatures =
            FindComponentsInScene<RunConstructionNpcFeature>(scene);
        List<ConstructionSiteTilemapModule> constructionSites =
            FindComponentsInScene<ConstructionSiteTilemapModule>(scene);
        List<ProceduralConstructionShortcutBinder> constructionBinders =
            FindComponentsInScene<ProceduralConstructionShortcutBinder>(scene);
        if (teleportFeatures.Count != 1 ||
            !teleportFeatures[0].HasDestination ||
            !teleportFeatures[0].HasLandingStartPresentation)
        {
            throw new InvalidOperationException(
                "Generated remote NPC room must contain one bound teleport NPC with its water arrival presentation.");
        }

        if (constructionFeatures.Count != 1 ||
            constructionSites.Count != 1 ||
            constructionBinders.Count != 1 ||
            !constructionBinders[0].IsBound)
        {
            throw new InvalidOperationException(
                "Generated Slime NPC room must contain one construction NPC and one bound local shortcut module.");
        }

        DungeonRoomPlacement constructionRoomPlacement = null;
        DungeonRoomPlacement teleportRoomPlacement = null;
        for (int roomIndex = 0; roomIndex < generator.LastLayout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement room = generator.LastLayout.Rooms[roomIndex];
            string roomId = room.Template != null ? room.Template.LayoutData.roomId : string.Empty;
            if (roomId == ConstructionRoomId)
                constructionRoomPlacement = room;
            else if (roomId == TeleportRoomId)
                teleportRoomPlacement = room;
        }

        Tilemap floorTilemap = generator.RoomBuilder.FloorTilemap;
        if (constructionRoomPlacement == null ||
            teleportRoomPlacement == null ||
            constructionRoomPlacement.PlacementId == teleportRoomPlacement.PlacementId ||
            floorTilemap == null ||
            !TryCalculateOccupiedWorldBounds(constructionSites[0].gameObject, out Bounds siteBounds))
        {
            throw new InvalidOperationException(
                "Could not validate the generated construction site's room footprint.");
        }

        RectInt roomBounds = constructionRoomPlacement.WorldBounds;
        Vector3 roomWorldMin = floorTilemap.CellToWorld(
            new Vector3Int(roomBounds.xMin, roomBounds.yMin, 0));
        Vector3 roomWorldMax = floorTilemap.CellToWorld(
            new Vector3Int(roomBounds.xMax, roomBounds.yMax, 0));
        const float boundsTolerance = 0.05f;
        if (siteBounds.min.x < roomWorldMin.x - boundsTolerance ||
            siteBounds.min.y < roomWorldMin.y - boundsTolerance ||
            siteBounds.max.x > roomWorldMax.x + boundsTolerance ||
            siteBounds.max.y > roomWorldMax.y + boundsTolerance)
        {
            throw new InvalidOperationException(
                $"Construction site escaped NPC room bounds. Site={siteBounds}, " +
                $"Room={roomWorldMin}..{roomWorldMax}.");
        }

        List<RoomSocketDirection> connectedDirections = CollectConnectedDirections(
            generator.LastLayout,
            constructionRoomPlacement);
        ProceduralConstructionShortcutBinder binder = constructionBinders[0];
        if (connectedDirections.Count != 2 ||
            !connectedDirections.Contains(binder.BoundGateDirection))
        {
            throw new InvalidOperationException(
                $"Generated construction gate did not bind to a connected cycle arm. " +
                $"Bound={binder.BoundGateDirection}, Connected={string.Join(", ", connectedDirections)}.");
        }

        ConstructionSiteTilemapModule site = constructionSites[0];
        site.ApplyIncompleteState();
        if (!site.BlockedStateRoot.activeSelf || site.OpenStateRoot.activeSelf)
        {
            throw new InvalidOperationException(
                "Incomplete construction state must enable the blocking tiles and hide the open passage.");
        }

        HashSet<Vector2Int> expectedLocalBarrier = ResolveShortcutBarrierCells(
            constructionRoomPlacement.Template.LayoutData.localBounds,
            binder.BoundGateDirection);
        HashSet<Vector2Int> actualBlockedWorldCells = CollectColliderTileCells(
            site.BlockedStateRoot,
            floorTilemap);
        foreach (Vector2Int localCell in expectedLocalBarrier)
        {
            Vector2Int worldCell = constructionRoomPlacement.Origin + localCell;
            if (!actualBlockedWorldCells.Contains(worldCell))
            {
                throw new InvalidOperationException(
                    $"Generated construction gate leaves shortcut cell {worldCell} unblocked " +
                    $"for direction {binder.BoundGateDirection}.");
            }
        }

        site.ApplyCompletedState(null);
        if (site.BlockedStateRoot.activeSelf ||
            !site.OpenStateRoot.activeSelf ||
            !site.IsCompletedStateApplied)
        {
            throw new InvalidOperationException(
                "Completed construction state must remove the blocker and enable the authored open passage.");
        }

        generator.RoomBuilder.ClearGeneratedContent();
    }

    private static List<RoomSocketDirection> CollectConnectedDirections(
        DungeonLayoutResult layout,
        DungeonRoomPlacement roomPlacement)
    {
        var directions = new List<RoomSocketDirection>();
        for (int connectionIndex = 0; connectionIndex < layout.Connections.Count; connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            int socketIndex = connection.FirstRoomPlacementId == roomPlacement.PlacementId
                ? connection.FirstSocketIndex
                : connection.SecondRoomPlacementId == roomPlacement.PlacementId
                    ? connection.SecondSocketIndex
                    : -1;
            if (socketIndex < 0)
                continue;

            RoomSocketDirection direction =
                roomPlacement.Template.LayoutData.sockets[socketIndex].direction;
            if (!directions.Contains(direction))
                directions.Add(direction);
        }

        return directions;
    }

    private static HashSet<Vector2Int> CollectColliderTileCells(
        GameObject stateRoot,
        Tilemap floorTilemap)
    {
        var results = new HashSet<Vector2Int>();
        Tilemap[] tilemaps = stateRoot.GetComponentsInChildren<Tilemap>(includeInactive: false);
        for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
        {
            Tilemap tilemap = tilemaps[tilemapIndex];
            if (tilemap.GetComponent<TilemapCollider2D>() == null)
                continue;

            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;

                Vector3 worldCenter = tilemap.GetCellCenterWorld(cell);
                Vector3Int floorCell = floorTilemap.WorldToCell(worldCenter);
                results.Add(new Vector2Int(floorCell.x, floorCell.y));
            }
        }

        return results;
    }

    private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            T[] components = roots[rootIndex].GetComponentsInChildren<T>(includeInactive: true);
            results.AddRange(components);
        }

        return results;
    }

    private static bool ContainsRoom(
        IReadOnlyList<RoomTemplateSO> rooms,
        RoomTemplateSO target)
    {
        if (rooms == null || target == null)
            return false;

        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            if (rooms[roomIndex] == target)
                return true;
        }

        return false;
    }

    [MenuItem("Tools/Dungeon/Slime NPC Room/Report Source Contracts")]
    public static void ReportSourceContracts()
    {
        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        ReportNamedObject(scene, "ConstructionNpc");
        ReportNamedObject(scene, "TeleportNPC");
        ReportNamedObject(scene, "ConstructionSite");
    }

    private static void ReportNamedObject(Scene scene, string objectName)
    {
        GameObject target = FindNamedObject(scene, objectName);
        if (target == null)
        {
            Debug.LogError($"[ProceduralSlimeNpcRoomInstaller] Could not find '{objectName}'.");
            return;
        }

        Transform root = target.transform;
        var report = new StringBuilder();
        report.AppendLine($"[ProceduralSlimeNpcRoomInstaller] SOURCE {GetHierarchyPath(root)}");
        Transform[] transforms = target.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
        {
            Transform current = transforms[transformIndex];
            report.AppendLine(
                $"  OBJECT {GetHierarchyPath(current)} active={current.gameObject.activeSelf} " +
                $"world={current.position} local={current.localPosition}");
            Component[] components = current.GetComponents<Component>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component == null)
                    continue;

                report.AppendLine($"    COMPONENT {component.GetType().FullName}");
                if (component is Tilemap tilemap)
                    report.AppendLine($"      TILE_BOUNDS {tilemap.cellBounds}");
                var serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue == null)
                    {
                        continue;
                    }

                    Object referencedObject = property.objectReferenceValue;
                    GameObject referencedGameObject = referencedObject switch
                    {
                        GameObject gameObject => gameObject,
                        Component referencedComponent => referencedComponent.gameObject,
                        _ => null
                    };
                    string scope = referencedGameObject == null
                        ? "ASSET"
                        : referencedGameObject.transform == root ||
                          referencedGameObject.transform.IsChildOf(root)
                            ? "LOCAL"
                            : "EXTERNAL";
                    string referenceName = referencedGameObject != null
                        ? GetHierarchyPath(referencedGameObject.transform)
                        : AssetDatabase.GetAssetPath(referencedObject);
                    report.AppendLine(
                        $"      REF {property.propertyPath} [{scope}] {referencedObject.GetType().Name} {referenceName}");
                }
            }
        }

        Debug.Log(report.ToString(), target);
    }

    private static void ConfigureSpeechAndPresenter(
        GameObject root,
        SpeechBubble speechBubblePrefab)
    {
        SpeechBubbleComponent[] speechComponents =
            root.GetComponentsInChildren<SpeechBubbleComponent>(includeInactive: true);
        for (int componentIndex = 0; componentIndex < speechComponents.Length; componentIndex++)
            SetObjectReference(speechComponents[componentIndex], "bubblePrefab", speechBubblePrefab);

        RunSpecialNpcInteractor[] interactors =
            root.GetComponentsInChildren<RunSpecialNpcInteractor>(includeInactive: true);
        for (int interactorIndex = 0; interactorIndex < interactors.Length; interactorIndex++)
            SetObjectReference(interactors[interactorIndex], "choicePresenter", null);
    }

    private static void SetAnchorReference(
        Object target,
        string propertyName,
        string slotId,
        ProceduralRoomAnchorScope scope)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty slotProperty = serializedObject.FindProperty(propertyName + ".slotId");
        SerializedProperty scopeProperty = serializedObject.FindProperty(propertyName + ".scope");
        if (slotProperty == null || scopeProperty == null)
        {
            throw new InvalidOperationException(
                $"Missing procedural anchor reference property '{propertyName}' on {target.name}.");
        }

        slotProperty.stringValue = slotId;
        scopeProperty.enumValueIndex = (int)scope;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReference(
        Object target,
        string propertyName,
        Object value)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            throw new InvalidOperationException(
                $"Missing object reference property '{propertyName}' on {target.name}.");
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void NormalizePrefabRoot(Transform root)
    {
        root.position = Vector3.zero;
        root.rotation = Quaternion.identity;
        root.localScale = Vector3.one;
    }

    private static void RemoveOptionalChild(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    private static Transform FindRequiredChild(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        if (child == null)
            throw new InvalidOperationException($"Missing child '{childName}' under '{root.name}'.");
        return child;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            if (children[childIndex].name == childName)
                return children[childIndex];
        }

        return null;
    }

    private static bool TryCalculateOccupiedWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
        {
            Tilemap tilemap = tilemaps[tilemapIndex];
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;

                Vector3 center = tilemap.GetCellCenterWorld(cell);
                if (!hasBounds)
                {
                    bounds = new Bounds(center, Vector3.one);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(center - Vector3.one * 0.5f);
                    bounds.Encapsulate(center + Vector3.one * 0.5f);
                }
            }
        }

        return hasBounds;
    }

    private static bool TryCalculateOccupiedCellBounds(GameObject root, out BoundsInt bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Vector3Int minimum = default;
        Vector3Int maximumExclusive = default;
        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int tilemapIndex = 0; tilemapIndex < tilemaps.Length; tilemapIndex++)
        {
            Tilemap tilemap = tilemaps[tilemapIndex];
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;

                if (!hasBounds)
                {
                    minimum = cell;
                    maximumExclusive = cell + Vector3Int.one;
                    hasBounds = true;
                }
                else
                {
                    minimum = Vector3Int.Min(minimum, cell);
                    maximumExclusive = Vector3Int.Max(
                        maximumExclusive,
                        cell + Vector3Int.one);
                }
            }
        }

        if (hasBounds)
            bounds = new BoundsInt(minimum, maximumExclusive - minimum);
        return hasBounds;
    }

    private static void ValidatePrefabAssetReferences(string prefabPath)
    {
        GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
        Component[] components = prefab.GetComponentsInChildren<Component>(includeInactive: true);
        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            Component component = components[componentIndex];
            if (component == null)
                throw new InvalidOperationException($"Missing script in prefab '{prefabPath}'.");

            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue == null)
                {
                    continue;
                }

                if (!EditorUtility.IsPersistent(property.objectReferenceValue))
                {
                    throw new InvalidOperationException(
                        $"Prefab '{prefabPath}' keeps a scene reference at " +
                        $"{component.GetType().Name}.{property.propertyPath}.");
                }
            }
        }
    }

    private static T LoadRequiredAsset<T>(string assetPath) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
            throw new InvalidOperationException($"Missing required asset: {assetPath}");
        return asset;
    }

    private static T LoadRequiredComponent<T>(string prefabPath) where T : Component
    {
        GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
        T component = prefab.GetComponentInChildren<T>(includeInactive: true);
        if (component == null)
            throw new InvalidOperationException(
                $"Prefab '{prefabPath}' does not contain {typeof(T).Name}.");
        return component;
    }

    private static GameObject FindRequiredNamedObject(Scene scene, string objectName)
    {
        GameObject gameObject = FindNamedObject(scene, objectName);
        if (gameObject == null)
            throw new InvalidOperationException(
                $"Could not find '{objectName}' in '{scene.path}'.");
        return gameObject;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separatorIndex = folderPath.LastIndexOf('/');
        if (separatorIndex <= 0 || separatorIndex >= folderPath.Length - 1)
            throw new InvalidOperationException($"Invalid asset folder path: {folderPath}");

        string parentPath = folderPath.Substring(0, separatorIndex);
        string folderName = folderPath.Substring(separatorIndex + 1);
        EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static GameObject FindNamedObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex]
                .GetComponentsInChildren<Transform>(includeInactive: true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                if (transforms[transformIndex].name == objectName)
                    return transforms[transformIndex].gameObject;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
