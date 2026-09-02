using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 책임:
/// 복합 방 오브젝트의 슬롯 자세 재정의가 프리팹 기본값과 다른 방 인스턴스를 변경하지 않는지 회귀 검증한다.
/// </summary>
public sealed class RoomCompositePoseOverridePlayModeTests
{
    [Test]
    public void PoseOverride_ChangesOnlySelectedInstanceAndChannels()
    {
        GameObject source = CreateCompositeSource();
        GameObject firstRoomInstance = Object.Instantiate(source);
        GameObject secondRoomInstance = Object.Instantiate(source);
        try
        {
            RoomCompositePoseAuthoring firstComposite =
                firstRoomInstance.GetComponent<RoomCompositePoseAuthoring>();
            RoomCompositePoseAuthoring secondComposite =
                secondRoomInstance.GetComponent<RoomCompositePoseAuthoring>();
            Assert.That(
                firstComposite.TryGetSlot("RewardDoor", out RoomCompositePoseSlotData firstSlot),
                Is.True);
            Assert.That(
                secondComposite.TryGetSlot("RewardDoor", out RoomCompositePoseSlotData secondSlot),
                Is.True);

            Quaternion prefabRotation = firstSlot.Target.localRotation;
            var overrides = new List<RoomObjectChildPoseOverrideData>
            {
                new()
                {
                    slotId = "RewardDoor",
                    overridePosition = true,
                    localPosition = new Vector3(7.25f, 3.5f, 0f),
                    overrideRotation = false,
                    localRotationDegrees = 90f,
                    overrideScale = true,
                    localScale = new Vector3(1.5f, 0.75f, 1f)
                }
            };

            Assert.That(
                firstComposite.TryApplyPoseOverrides(overrides, out string failureReason),
                Is.True,
                failureReason);

            Assert.That(firstSlot.Target.localPosition, Is.EqualTo(new Vector3(7.25f, 3.5f, 0f)));
            Assert.That(firstSlot.Target.localRotation, Is.EqualTo(prefabRotation));
            Assert.That(firstSlot.Target.localScale, Is.EqualTo(new Vector3(1.5f, 0.75f, 1f)));
            Assert.That(secondSlot.Target.localPosition, Is.EqualTo(new Vector3(2.5f, 2f, 0f)));
            Assert.That(secondSlot.Target.localScale, Is.EqualTo(Vector3.one));
        }
        finally
        {
            Object.DestroyImmediate(secondRoomInstance);
            Object.DestroyImmediate(firstRoomInstance);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void PoseOverride_WithUnknownSlotFailsWithoutChangingKnownTarget()
    {
        GameObject source = CreateCompositeSource();
        try
        {
            RoomCompositePoseAuthoring composite =
                source.GetComponent<RoomCompositePoseAuthoring>();
            Assert.That(
                composite.TryGetSlot("RewardDoor", out RoomCompositePoseSlotData knownSlot),
                Is.True);
            Vector3 initialPosition = knownSlot.Target.localPosition;
            var overrides = new List<RoomObjectChildPoseOverrideData>
            {
                new()
                {
                    slotId = "RemovedSlot",
                    overridePosition = true,
                    localPosition = Vector3.one
                }
            };

            Assert.That(
                composite.TryApplyPoseOverrides(overrides, out string failureReason),
                Is.False);
            StringAssert.Contains("RemovedSlot", failureReason);
            Assert.That(knownSlot.Target.localPosition, Is.EqualTo(initialPosition));
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void RoomObjectAuthoring_CapturesEnabledSlotPoseAndRestoresPrefabDefault()
    {
        GameObject source = CreateCompositeSource();
        GameObject roomRoot = new("RoomAuthoring");
        GameObject instance = null;
        try
        {
            Grid grid = roomRoot.AddComponent<Grid>();
            RoomPieceAuthoring room = roomRoot.AddComponent<RoomPieceAuthoring>();
            room.EditorAssignTilemaps(grid, null, null);

            instance = Object.Instantiate(source, grid.transform);
            RoomObjectAuthoring roomObject = instance.AddComponent<RoomObjectAuthoring>();
            roomObject.EditorConfigure(
                "Alcove",
                RoomObjectKind.Prop,
                source,
                RoomMonsterSpawnRole.Warrior,
                null);
            roomObject.EditorSetPlacement(new RoomObjectPlacementData
            {
                placementId = "Alcove",
                kind = RoomObjectKind.Prop,
                prefab = source,
                localCell = new Vector2Int(2, 3),
                localScale = Vector3.one,
                childPoseOverrides = new List<RoomObjectChildPoseOverrideData>
                {
                    new()
                    {
                        slotId = "RewardDoor",
                        overridePosition = true,
                        localPosition = new Vector3(5f, 4f, 0f)
                    }
                }
            });

            Assert.That(
                roomObject.TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring composite),
                Is.True);
            Assert.That(
                composite.TryGetSlot("RewardDoor", out RoomCompositePoseSlotData doorSlot),
                Is.True);
            Assert.That(doorSlot.Target.localPosition, Is.EqualTo(new Vector3(5f, 4f, 0f)));

            doorSlot.Target.localPosition = new Vector3(6.25f, 4.5f, 0f);
            Assert.That(roomObject.TryGetPlacementData(out RoomObjectPlacementData captured), Is.True);
            Assert.That(captured.childPoseOverrides, Has.Count.EqualTo(1));
            Assert.That(
                captured.childPoseOverrides[0].localPosition,
                Is.EqualTo(new Vector3(6.25f, 4.5f, 0f)));

            roomObject.EditorRemoveChildPoseOverride("RewardDoor", restorePrefabPose: true);
            Assert.That(doorSlot.Target.localPosition, Is.EqualTo(new Vector3(2.5f, 2f, 0f)));
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
            Object.DestroyImmediate(roomRoot);
            Object.DestroyImmediate(source);
        }
    }

    private static GameObject CreateCompositeSource()
    {
        GameObject root = new("CompositeSource");
        GameObject statue = new("OfferingStatue");
        GameObject door = new("RewardDoor");
        statue.transform.SetParent(root.transform, false);
        door.transform.SetParent(root.transform, false);
        door.transform.localPosition = new Vector3(2.5f, 2f, 0f);

        RoomCompositePoseAuthoring composite =
            root.AddComponent<RoomCompositePoseAuthoring>();
        composite.EditorSetPoseSlots(new[]
        {
            new RoomCompositePoseSlotData(
                "OfferingStatue",
                "Offering Statue",
                statue.transform),
            new RoomCompositePoseSlotData(
                "RewardDoor",
                "Reward Door",
                door.transform)
        });
        return root;
    }
}
