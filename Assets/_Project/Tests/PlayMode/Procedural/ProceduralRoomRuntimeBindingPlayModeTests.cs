using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임 : 절차 생성 방의 고정 시각 레이어, 몬스터 소스·테마 카탈로그, 앵커 바인딩, 이동 도착 경계 보호와 필수 방의 그래프 위치 제약을 회귀 검증한다.
/// </summary>
public sealed class ProceduralRoomRuntimeBindingPlayModeTests
{
    [Test]
    public void MonsterSpawnContainer_StageFixedPrefabStaysDeferredAndReportsSpawn()
    {
        GameObject spawnPoint = new("DeferredSpawnPoint");
        GameObject stageMonsterPrefab = new("StageMonsterPrefab");
        GameObject spawnedMonster = new("SpawnedMonster");
        try
        {
            MonsterSpawnContainer container =
                spawnPoint.AddComponent<MonsterSpawnContainer>();
            GameObject reportedMonster = null;
            container.ConfigureRuntime(
                stageMonsterPrefab,
                configuredRoomArea: null,
                configuredRoomGroup: null,
                configuredChestKillLock: null,
                onRuntimeSpawned: result => reportedMonster = result);

            Assert.That(container.TryCreateRequest(0, out MonsterSpawnRequest request), Is.True);
            Assert.That(container.SourceKind, Is.EqualTo(MonsterSpawnSourceKind.FixedPrefab));
            Assert.That(request.MonsterPrefab, Is.SameAs(stageMonsterPrefab));
            Assert.That(request.SourceContainer, Is.SameAs(container));
            Assert.That(reportedMonster, Is.Null);

            container.NotifyRuntimeSpawned(spawnedMonster);

            Assert.That(reportedMonster, Is.SameAs(spawnedMonster));
        }
        finally
        {
            Object.DestroyImmediate(spawnedMonster);
            Object.DestroyImmediate(stageMonsterPrefab);
            Object.DestroyImmediate(spawnPoint);
        }
    }

    [Test]
    public void MonsterSpawnContainer_RoleStageSetResolvesBossProgressionAtItsAuthoredPosition()
    {
        GameObject spawnPoint = new("WarriorSpawnPoint");
        GameObject stageZero = new("GoblinWarrior");
        GameObject stageOne = new("LizardWarrior");
        GameObject stageTwo = new("ArcaneMeleeGolem");
        StageMonsterSetSO warriorSet = ScriptableObject.CreateInstance<StageMonsterSetSO>();
        try
        {
            warriorSet.EditorSetStagePrefabs(new[] { stageZero, stageOne, stageTwo });
            MonsterSpawnContainer container =
                spawnPoint.AddComponent<MonsterSpawnContainer>();
            container.ConfigureRuntime(
                warriorSet,
                configuredMonsterPrefab: null,
                configuredRoomArea: null,
                configuredRoomGroup: null,
                configuredChestKillLock: null);

            Assert.That(container.TryCreateRequest(0, out MonsterSpawnRequest first), Is.True);
            Assert.That(container.TryCreateRequest(1, out MonsterSpawnRequest second), Is.True);
            Assert.That(container.TryCreateRequest(2, out MonsterSpawnRequest third), Is.True);
            Assert.That(first.MonsterPrefab, Is.SameAs(stageZero));
            Assert.That(second.MonsterPrefab, Is.SameAs(stageOne));
            Assert.That(third.MonsterPrefab, Is.SameAs(stageTwo));
            Assert.That(first.Position, Is.EqualTo(spawnPoint.transform.position));
        }
        finally
        {
            Object.DestroyImmediate(warriorSet);
            Object.DestroyImmediate(stageTwo);
            Object.DestroyImmediate(stageOne);
            Object.DestroyImmediate(stageZero);
            Object.DestroyImmediate(spawnPoint);
        }
    }

    [Test]
    public void RoomThemeLibrary_StageMonsterCatalogRemovesNullsAndDuplicates()
    {
        RoomThemeLibrarySO library = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
        GameObject first = new("ThemeMonsterA");
        GameObject second = new("ThemeMonsterB");
        try
        {
            library.EditorSetStageMonsterPrefabs(
                new GameObject[] { first, null, first, second });

            Assert.That(library.StageMonsterPrefabs, Has.Count.EqualTo(2));
            Assert.That(library.StageMonsterPrefabs[0], Is.SameAs(first));
            Assert.That(library.StageMonsterPrefabs[1], Is.SameAs(second));
        }
        finally
        {
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(library);
        }
    }

    [Test]
    public void DoorObject_ExternalEncounterBlockerSuppressesInteractionAndOpenDisablesObstacleImmediately()
    {
        GameObject doorObject = new("Door");
        GameObject playerObject = new("PlayerInteractor");
        GameObject lockOwner = new("EncounterLock");
        try
        {
            BoxCollider2D obstacle = doorObject.AddComponent<BoxCollider2D>();
            LogAssert.Expect(
                LogType.Error,
                "[DoorObject] 치명적 에러: 'Door'의 Door ID가 없습니다!");
            DoorObject door = doorObject.AddComponent<DoorObject>();
            door.obstacleCollider = obstacle;
            TestPlayerInteractor player = new(playerObject.transform);

            Assert.That(door.CanInteract(player), Is.True);

            door.SetExternalOpenBlocked(lockOwner, true);

            Assert.That(door.CanInteract(player), Is.False);

            door.SetExternalOpenBlocked(lockOwner, false);
            door.ForceOpen(immediate: false, playPresentation: false);

            Assert.That(door.IsOpen, Is.True);
            Assert.That(obstacle.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(lockOwner);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(doorObject);
        }
    }

    [Test]
    public void RoomTileLayerContract_SeparatesVisualLayersFromGroundPhysics()
    {
        Assert.That(RoomTileLayerContract.OrderedLayers.Count, Is.EqualTo(8));
        Assert.That(RoomTileLayerContract.UsesGroundPhysicsLayer(RoomTileLayerKind.Floor), Is.True);
        Assert.That(RoomTileLayerContract.UsesGroundPhysicsLayer(RoomTileLayerKind.Wall), Is.True);
        Assert.That(
            RoomTileLayerContract.UsesGroundPhysicsLayer(RoomTileLayerKind.GroundDecoration),
            Is.False);
        Assert.That(RoomTileLayerContract.RequiresCollider(RoomTileLayerKind.Wall), Is.True);
        Assert.That(RoomTileLayerContract.RequiresCollider(RoomTileLayerKind.WallDetail), Is.False);
        Assert.That(
            RoomTileLayerContract.GetSortingLayerName(RoomTileLayerKind.Foreground),
            Is.EqualTo("ForeGround"));
    }

    [Test]
    public void DungeonRoomBuilder_BuildsEveryFixedVisualTileLayer()
    {
        GameObject root = new("FixedRoomLayerBuildTest");
        RoomTemplateSO template = ScriptableObject.CreateInstance<RoomTemplateSO>();
        RoomThemeLibrarySO library = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            root.AddComponent<Grid>();
            Tilemap[] tilemaps = new Tilemap[RoomTileLayerContract.OrderedLayers.Count];
            for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
            {
                tilemaps[i] = CreateTestTilemap(
                    root.transform,
                    RoomTileLayerContract.OrderedLayers[i]);
            }

            DungeonRoomBuilder builder = root.AddComponent<DungeonRoomBuilder>();
            builder.EditorAssignTilemaps(
                tilemaps[0],
                tilemaps[1],
                tilemaps[2],
                tilemaps[3],
                tilemaps[4],
                tilemaps[5],
                tilemaps[6],
                tilemaps[7]);

            List<RoomTileData> socketBaseTiles = new()
            {
                new RoomTileData { localCell = new Vector2Int(0, 0), tile = tile },
                new RoomTileData { localCell = new Vector2Int(1, 0), tile = tile }
            };
            List<RoomTileData> decorationTile = new()
            {
                new RoomTileData { localCell = new Vector2Int(0, 1), tile = tile }
            };
            template.EditorSetData(
                new RoomLayoutData
                {
                    roomId = "FixedLayerStart",
                    roomType = RoomType.Start,
                    size = new Vector2Int(2, 2),
                    localBounds = new RectInt(0, 0, 2, 2),
                    sockets = new List<RoomSocketData>
                    {
                        new()
                        {
                            socketId = "Down",
                            localCell = Vector2Int.zero,
                            direction = RoomSocketDirection.Down,
                            width = 2
                        }
                    },
                    selectionWeight = 1f
                },
                new RoomBuildData
                {
                    underFloorTiles = new List<RoomTileData>(decorationTile),
                    floorTiles = socketBaseTiles,
                    floorDetailTiles = new List<RoomTileData>(decorationTile),
                    groundDecorationTiles = new List<RoomTileData>(decorationTile),
                    wallTiles = new List<RoomTileData>(socketBaseTiles),
                    wallDetailTiles = new List<RoomTileData>(decorationTile),
                    foregroundTiles = new List<RoomTileData>(decorationTile),
                    overlayFxTiles = new List<RoomTileData>(decorationTile),
                    objectPlacements = new List<RoomObjectPlacementData>(),
                    travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>()
                });
            library.EditorAddRoom(template);

            DungeonLayoutResult layout = new DungeonLayoutAssembler().Assemble(
                library,
                seed: 123,
                requestedRoomCount: 1,
                includeBossRoom: false,
                maxPlacementAttemptsPerRoom: 1,
                minimumCorridorLength: 0,
                corridorLengthPerRoomCell: 0f,
                corridorLengthVariation: 0);

            Assert.That(layout.IsComplete, Is.True, layout.FailureReason);
            Assert.That(builder.TryBuild(layout, DungeonBuildOptions.VisualOnly), Is.True);
            for (int i = 0; i < tilemaps.Length; i++)
                Assert.That(tilemaps[i].GetUsedTilesCount(), Is.GreaterThan(0));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tile);
            Object.DestroyImmediate(library);
            Object.DestroyImmediate(template);
        }
    }

    [Test]
    public void TravelEndpointGeometry_LegacyTriggerUsesAuthoredTransformScale()
    {
        var placement = new RoomTravelEndpointPlacementData
        {
            localScale = new Vector3(2f, 3f, 1f)
        };

        Assert.That(
            RoomTravelEndpointGeometry.ResolveTriggerSize(placement),
            Is.EqualTo(new Vector2(2f, 3f)));
    }

    [Test]
    public void TravelEndpointGeometry_ExplicitSizeIsIndependentFromTransformScale()
    {
        var placement = new RoomTravelEndpointPlacementData
        {
            localScale = new Vector3(2f, 3f, 1f),
            triggerSize = Vector2.one
        };

        Vector2 resolvedSize = RoomTravelEndpointGeometry.ResolveTriggerSize(placement);
        Vector2 colliderLocalSize = RoomTravelEndpointGeometry.ResolveLocalColliderSize(
            resolvedSize,
            placement.localScale);

        Assert.That(resolvedSize, Is.EqualTo(Vector2.one));
        Assert.That(colliderLocalSize.x * placement.localScale.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(colliderLocalSize.y * placement.localScale.y, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void SceneTravelEndpoint_RuntimeArrivalAnchorOverridesEndpointFallback()
    {
        GameObject endpointObject = new("TravelEndpoint");
        GameObject arrivalObject = new("ArrivalAnchor");
        try
        {
            arrivalObject.transform.SetParent(endpointObject.transform, worldPositionStays: false);
            SceneTravelEndpoint endpoint = endpointObject.AddComponent<SceneTravelEndpoint>();

            Assert.That(endpoint.ArrivalAnchor, Is.SameAs(endpointObject.transform));

            endpoint.ConfigureRuntimeArrivalAnchor(arrivalObject.transform);

            Assert.That(endpoint.ArrivalAnchor, Is.SameAs(arrivalObject.transform));
        }
        finally
        {
            Object.DestroyImmediate(endpointObject);
        }
    }

    [Test]
    public void SceneTravelTrigger_ArrivalSuppressionEnablesOutsideWallBlocker()
    {
        GameObject endpointObject = new("TravelTrigger");
        GameObject arrivalObject = new("ArrivalAnchor");
        GameObject playerObject = new("Player");
        try
        {
            BoxCollider2D triggerCollider = endpointObject.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(2f, 2f);

            arrivalObject.transform.SetParent(endpointObject.transform, worldPositionStays: false);
            arrivalObject.transform.localPosition = new Vector3(2f, 0f, 0f);
            SceneTravelEndpoint endpoint = endpointObject.AddComponent<SceneTravelEndpoint>();
            endpoint.ConfigureRuntimeArrivalAnchor(arrivalObject.transform);

            SceneTravelTrigger2D trigger = endpointObject.AddComponent<SceneTravelTrigger2D>();
            Transform blockerTransform = endpointObject.transform.Find("ArrivalSuppressionBlocker");

            Assert.That(blockerTransform, Is.Not.Null);
            BoxCollider2D blocker = blockerTransform.GetComponent<BoxCollider2D>();
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.isTrigger, Is.False);
            Assert.That(blocker.enabled, Is.False);
            Assert.That(blockerTransform.localPosition.x, Is.LessThan(0f));
            Assert.That(blocker.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Wall")));

            trigger.SuppressTravelUntilExit(playerObject.transform);

            Assert.That(blocker.enabled, Is.True);

            trigger.enabled = false;

            Assert.That(blocker.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(endpointObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void RuntimeContext_ResolvesOnlyRequestedAnchorScope()
    {
        GameObject localAnchorObject = new("LocalAnchor");
        GameObject dungeonAnchorObject = new("DungeonAnchor");
        try
        {
            var localAnchors = new Dictionary<string, Transform>
            {
                ["SharedSlot"] = localAnchorObject.transform
            };
            var dungeonAnchors = new Dictionary<string, Transform>
            {
                ["SharedSlot"] = dungeonAnchorObject.transform
            };
            var context = new ProceduralRoomRuntimeContext(
                3,
                null,
                localAnchors,
                dungeonAnchors);

            Assert.That(
                context.TryResolveAnchor(
                    "SharedSlot",
                    ProceduralRoomAnchorScope.LocalRoom,
                    out Transform resolvedLocal),
                Is.True);
            Assert.That(resolvedLocal, Is.SameAs(localAnchorObject.transform));

            Assert.That(
                context.TryResolveAnchor(
                    "SharedSlot",
                    ProceduralRoomAnchorScope.Dungeon,
                    out Transform resolvedDungeon),
                Is.True);
            Assert.That(resolvedDungeon, Is.SameAs(dungeonAnchorObject.transform));
        }
        finally
        {
            Object.DestroyImmediate(localAnchorObject);
            Object.DestroyImmediate(dungeonAnchorObject);
        }
    }

    [Test]
    public void ConstructionShortcutBinder_OrientsSiteTowardConnectedCycleArm()
    {
        GameObject root = new("ConstructionNpcModule");
        GameObject siteObject = new("ConstructionSite");
        try
        {
            siteObject.transform.SetParent(root.transform, worldPositionStays: false);
            ProceduralConstructionShortcutBinder binder =
                root.AddComponent<ProceduralConstructionShortcutBinder>();
            binder.EditorConfigure(siteObject.transform, RoomSocketDirection.Left);
            var context = new ProceduralRoomRuntimeContext(
                7,
                null,
                new Dictionary<string, Transform>(),
                new Dictionary<string, Transform>(),
                new[]
                {
                    RoomSocketDirection.Up,
                    RoomSocketDirection.Right
                });

            Assert.That(
                binder.TryBindProceduralRoom(context, out string failureReason),
                Is.True,
                failureReason);
            Assert.That(binder.IsBound, Is.True);
            Assert.That(binder.BoundGateDirection, Is.EqualTo(RoomSocketDirection.Up));
            Assert.That(
                Quaternion.Angle(
                    siteObject.transform.localRotation,
                    Quaternion.Euler(0f, 0f, -90f)),
                Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ConstructionShortcutBinder_RejectsNonCornerConnections()
    {
        GameObject root = new("ConstructionNpcModule");
        GameObject siteObject = new("ConstructionSite");
        try
        {
            siteObject.transform.SetParent(root.transform, worldPositionStays: false);
            ProceduralConstructionShortcutBinder binder =
                root.AddComponent<ProceduralConstructionShortcutBinder>();
            binder.EditorConfigure(siteObject.transform, RoomSocketDirection.Left);
            var context = new ProceduralRoomRuntimeContext(
                7,
                null,
                new Dictionary<string, Transform>(),
                new Dictionary<string, Transform>(),
                new[]
                {
                    RoomSocketDirection.Left,
                    RoomSocketDirection.Right
                });

            Assert.That(
                binder.TryBindProceduralRoom(context, out string failureReason),
                Is.False);
            Assert.That(failureReason, Does.Contain("corner connection"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void TeleportFeature_BindsConfiguredDungeonLandingAnchor()
    {
        GameObject featureObject = new("TeleportFeature");
        GameObject landingAnchorObject = new("LandingAnchor");
        try
        {
            RunSameSceneTeleportNpcFeature feature =
                featureObject.AddComponent<RunSameSceneTeleportNpcFeature>();
            JsonUtility.FromJsonOverwrite(
                "{\"proceduralLandingPoint\":{\"slotId\":\"SlimeTeleportArrival\",\"scope\":1}}",
                feature);

            var context = new ProceduralRoomRuntimeContext(
                1,
                null,
                new Dictionary<string, Transform>(),
                new Dictionary<string, Transform>
                {
                    ["SlimeTeleportArrival"] = landingAnchorObject.transform
                });

            Assert.That(
                feature.TryBindProceduralRoom(context, out string failureReason),
                Is.True,
                failureReason);
            Assert.That(feature.HasDestination, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(featureObject);
            Object.DestroyImmediate(landingAnchorObject);
        }
    }

    [Test]
    public void TeleportFeature_RejectsMissingConfiguredLandingAnchor()
    {
        GameObject featureObject = new("TeleportFeature");
        try
        {
            RunSameSceneTeleportNpcFeature feature =
                featureObject.AddComponent<RunSameSceneTeleportNpcFeature>();
            JsonUtility.FromJsonOverwrite(
                "{\"proceduralLandingPoint\":{\"slotId\":\"MissingArrival\",\"scope\":1}}",
                feature);

            var context = new ProceduralRoomRuntimeContext(
                1,
                null,
                new Dictionary<string, Transform>(),
                new Dictionary<string, Transform>());

            Assert.That(
                feature.TryBindProceduralRoom(context, out string failureReason),
                Is.False);
            Assert.That(failureReason, Does.Contain("MissingArrival"));
        }
        finally
        {
            Object.DestroyImmediate(featureObject);
        }
    }

    [Test]
    public void GraphAssembler_PlacesGuaranteedTemplateExactlyOnce()
    {
        RoomThemeLibrarySO library = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
        DungeonLayoutPolicySO policy = ScriptableObject.CreateInstance<DungeonLayoutPolicySO>();
        RoomTemplateSO start = CreateFourSocketTemplate("Start", RoomType.Start);
        RoomTemplateSO boss = CreateFourSocketTemplate("Boss", RoomType.Boss);
        RoomTemplateSO combat = CreateFourSocketTemplate("Combat", RoomType.Combat);
        RoomTemplateSO guaranteedEvent = CreateFourSocketTemplate("GuaranteedEvent", RoomType.Event);
        RoomTemplateSO randomEvent = CreateFourSocketTemplate("RandomEvent", RoomType.Event);
        try
        {
            library.EditorAddRoom(start);
            library.EditorAddRoom(boss);
            library.EditorAddRoom(combat);
            library.EditorAddRoom(guaranteedEvent);
            library.EditorAddRoom(randomEvent);
            policy.EditorConfigure(
                recommendedMinimumRooms: 8,
                recommendedMaximumRooms: 8,
                minimumBossDistance: 3,
                maximumBossDistance: 4,
                minimumBranches: 1,
                maximumBranches: 1,
                minimumCycles: 0,
                maximumCycles: 0,
                topologyAttempts: 512,
                requiredTreasureRooms: 0,
                requiredEventRooms: 0,
                requiredShopRooms: 0,
                requiredMinimumCombatRooms: 2,
                shouldPreferSpecialRoomsAtDeadEnds: true);

            DungeonLayoutResult result = new DungeonGraphLayoutAssembler().Assemble(
                library,
                policy,
                seed: 74123,
                requestedRoomCount: 8,
                maxPlacementAttemptsPerRoom: 512,
                minimumCorridorLength: 2,
                corridorLengthPerRoomCell: 0f,
                corridorLengthVariation: 0,
                guaranteedRoomTemplates: new[] { guaranteedEvent });

            Assert.That(result.IsComplete, Is.True, result.FailureReason);
            int guaranteedCount = 0;
            int randomEventCount = 0;
            for (int roomIndex = 0; roomIndex < result.Rooms.Count; roomIndex++)
            {
                if (result.Rooms[roomIndex].Template == guaranteedEvent)
                    guaranteedCount++;
                if (result.Rooms[roomIndex].Template == randomEvent)
                    randomEventCount++;
            }

            Assert.That(guaranteedCount, Is.EqualTo(1));
            Assert.That(randomEventCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(randomEvent);
            Object.DestroyImmediate(guaranteedEvent);
            Object.DestroyImmediate(combat);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(start);
            Object.DestroyImmediate(policy);
            Object.DestroyImmediate(library);
        }
    }

    [Test]
    public void GraphAssembler_RespectsShortcutAndRemoteRoomPlacementRules()
    {
        RoomThemeLibrarySO library = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
        DungeonLayoutPolicySO policy = ScriptableObject.CreateInstance<DungeonLayoutPolicySO>();
        RoomTemplateSO start = CreateFourSocketTemplate("Start", RoomType.Start);
        RoomTemplateSO boss = CreateFourSocketTemplate("Boss", RoomType.Boss);
        RoomTemplateSO combat = CreateFourSocketTemplate("Combat", RoomType.Combat);
        RoomTemplateSO shortcut = CreateFourSocketTemplate(
            "ConstructionShortcut",
            RoomType.Event,
            new RoomTopologyPlacementData
            {
                mode = RoomTopologyPlacementMode.CycleDetour
            });
        RoomTemplateSO remote = CreateFourSocketTemplate(
            "TeleportRemote",
            RoomType.Event,
            new RoomTopologyPlacementData
            {
                mode = RoomTopologyPlacementMode.FarthestFromStart,
                minimumGraphDistanceFromStart = 3,
                requireDeadEnd = true
            });
        try
        {
            library.EditorAddRoom(start);
            library.EditorAddRoom(boss);
            library.EditorAddRoom(combat);
            library.EditorAddRoom(shortcut);
            library.EditorAddRoom(remote);
            policy.EditorConfigure(
                recommendedMinimumRooms: 12,
                recommendedMaximumRooms: 12,
                minimumBossDistance: 5,
                maximumBossDistance: 5,
                minimumBranches: 2,
                maximumBranches: 2,
                minimumCycles: 1,
                maximumCycles: 1,
                topologyAttempts: 512,
                requiredTreasureRooms: 0,
                requiredEventRooms: 0,
                requiredShopRooms: 0,
                requiredMinimumCombatRooms: 2,
                shouldPreferSpecialRoomsAtDeadEnds: true);

            DungeonLayoutResult result = new DungeonGraphLayoutAssembler().Assemble(
                library,
                policy,
                seed: 48291,
                requestedRoomCount: 12,
                maxPlacementAttemptsPerRoom: 512,
                minimumCorridorLength: 2,
                corridorLengthPerRoomCell: 0f,
                corridorLengthVariation: 0,
                guaranteedRoomTemplates: new[] { shortcut, remote });

            Assert.That(result.IsComplete, Is.True, result.FailureReason);
            DungeonRoomPlacement shortcutPlacement = FindPlacement(result, shortcut);
            DungeonRoomPlacement remotePlacement = FindPlacement(result, remote);
            Assert.That(shortcutPlacement, Is.Not.Null);
            Assert.That(shortcutPlacement.IsCycleDetour, Is.True);
            Assert.That(remotePlacement, Is.Not.Null);
            Assert.That(remotePlacement.IsDeadEnd, Is.True);
            Assert.That(remotePlacement.GraphDistanceFromStart, Is.GreaterThanOrEqualTo(3));

            int farthestDeadEndDistance = 0;
            for (int roomIndex = 0; roomIndex < result.Rooms.Count; roomIndex++)
            {
                DungeonRoomPlacement room = result.Rooms[roomIndex];
                if (room.IsDeadEnd &&
                    room.Template != null &&
                    room.Template.LayoutData.roomType != RoomType.Boss)
                {
                    farthestDeadEndDistance = Mathf.Max(
                        farthestDeadEndDistance,
                        room.GraphDistanceFromStart);
                }
            }

            Assert.That(
                remotePlacement.GraphDistanceFromStart,
                Is.EqualTo(farthestDeadEndDistance));
        }
        finally
        {
            Object.DestroyImmediate(remote);
            Object.DestroyImmediate(shortcut);
            Object.DestroyImmediate(combat);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(start);
            Object.DestroyImmediate(policy);
            Object.DestroyImmediate(library);
        }
    }

    private static DungeonRoomPlacement FindPlacement(
        DungeonLayoutResult result,
        RoomTemplateSO template)
    {
        for (int roomIndex = 0; roomIndex < result.Rooms.Count; roomIndex++)
        {
            if (result.Rooms[roomIndex].Template == template)
                return result.Rooms[roomIndex];
        }

        return null;
    }

    private static Tilemap CreateTestTilemap(
        Transform parent,
        RoomTileLayerKind layer)
    {
        GameObject tilemapObject = new(RoomTileLayerContract.GetLayerName(layer));
        tilemapObject.transform.SetParent(parent, false);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        return tilemap;
    }

    private static RoomTemplateSO CreateFourSocketTemplate(
        string roomId,
        RoomType roomType,
        RoomTopologyPlacementData? topologyPlacement = null)
    {
        RoomTemplateSO template = ScriptableObject.CreateInstance<RoomTemplateSO>();
        template.EditorSetData(
            new RoomLayoutData
            {
                roomId = roomId,
                roomType = roomType,
                size = new Vector2Int(6, 6),
                localBounds = new RectInt(0, 0, 6, 6),
                sockets = new List<RoomSocketData>
                {
                    new()
                    {
                        socketId = "Up",
                        localCell = new Vector2Int(2, 5),
                        direction = RoomSocketDirection.Up,
                        width = 2
                    },
                    new()
                    {
                        socketId = "Right",
                        localCell = new Vector2Int(5, 2),
                        direction = RoomSocketDirection.Right,
                        width = 2
                    },
                    new()
                    {
                        socketId = "Down",
                        localCell = new Vector2Int(2, 0),
                        direction = RoomSocketDirection.Down,
                        width = 2
                    },
                    new()
                    {
                        socketId = "Left",
                        localCell = new Vector2Int(0, 2),
                        direction = RoomSocketDirection.Left,
                        width = 2
                    }
                },
                difficultyTier = 0,
                selectionWeight = 1f,
                topologyPlacement = topologyPlacement ?? default
            },
            new RoomBuildData
            {
                floorTiles = new List<RoomTileData>(),
                wallTiles = new List<RoomTileData>(),
                objectPlacements = new List<RoomObjectPlacementData>(),
                travelEndpointPlacements = new List<RoomTravelEndpointPlacementData>()
            });
        return template;
    }
}

/// <summary>
/// 책임 : 문 상호작용 회귀 테스트에 필요한 최소 플레이어 상호작용 상태를 제공한다.
/// </summary>
public sealed class TestPlayerInteractor : IPlayerInteractor
{
    public Transform Transform { get; }
    public InteractState CurrentState { get; private set; } = InteractState.Idle;

    public TestPlayerInteractor(Transform transform)
    {
        Transform = transform;
    }

    public void SetInteractState(InteractState state)
    {
        CurrentState = state;
    }
}
