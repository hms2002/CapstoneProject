using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 책임 : 절차 복도의 PreserveDuringRun 정책이 같은 런에서 seed와 생성 오브젝트 상태를 유지하고 새 런에서 초기화하는지 회귀 검증한다.
/// </summary>
public sealed class DungeonReentryPolicyPlayModeTests
{
    private const string DungeonId = "test_procedural_corridor_preserve";

    private GamePlayDataManager manager;

    [SetUp]
    public void SetUp()
    {
        manager = GamePlayDataManager.EnsureInstance();
        Assert.That(manager, Is.Not.Null);
        manager.ResetForDevelopmentStart();
        manager.StartRun();
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null)
            manager.ResetForDevelopmentStart();
    }

    [Test]
    public void PreserveDuringRun_ReusesSeedAndGeneratedObjectState()
    {
        int firstSeed = manager.ResolveDungeonSeed(
            DungeonId,
            DungeonReentryPolicy.PreserveDuringRun,
            fallbackSeed: 17);
        manager.SaveDungeonObjectStates(
            DungeonId,
            new[]
            {
                new DungeonObjectRuntimeStateData
                {
                    stateId = "room:3/object:monster:1",
                    isPresent = false,
                    isActive = false
                },
                new DungeonObjectRuntimeStateData
                {
                    stateId = "room:5/object:chest:1",
                    isPresent = true,
                    isActive = true,
                    isChestOpened = true
                }
            });

        int reentrySeed = manager.ResolveDungeonSeed(
            DungeonId,
            DungeonReentryPolicy.PreserveDuringRun,
            fallbackSeed: 999);
        var restoredStates = new List<DungeonObjectRuntimeStateData>();

        Assert.That(reentrySeed, Is.EqualTo(firstSeed));
        Assert.That(
            manager.TryGetDungeonObjectStates(DungeonId, restoredStates),
            Is.True);
        Assert.That(restoredStates, Has.Count.EqualTo(2));
        Assert.That(restoredStates[0].isPresent, Is.False);
        Assert.That(restoredStates[1].isChestOpened, Is.True);
    }

    [Test]
    public void StartRun_ClearsPreservedDungeonStateForTheNewRun()
    {
        manager.ResolveDungeonSeed(
            DungeonId,
            DungeonReentryPolicy.PreserveDuringRun,
            fallbackSeed: 17);
        manager.SaveDungeonObjectStates(
            DungeonId,
            new[]
            {
                new DungeonObjectRuntimeStateData
                {
                    stateId = "room:3/object:monster:1",
                    isPresent = false,
                    isActive = false
                }
            });

        manager.StartRun();
        var restoredStates = new List<DungeonObjectRuntimeStateData>();

        Assert.That(manager.Data.dungeonRunStates, Is.Empty);
        Assert.That(
            manager.TryGetDungeonObjectStates(DungeonId, restoredStates),
            Is.False);
        Assert.That(restoredStates, Is.Empty);
    }
}
