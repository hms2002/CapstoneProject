using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 책임 : 절차 생성 방의 앵커 바인딩, 이동 도착 경계 보호와 필수 방의 그래프 위치 제약을 회귀 검증한다.
/// </summary>
public sealed class ProceduralRoomRuntimeBindingPlayModeTests
{
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
