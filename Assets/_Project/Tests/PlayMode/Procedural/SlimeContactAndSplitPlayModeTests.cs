#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Verifies lower-body charge contact and collision-safe split starts/landings and physics restoration.</summary>
public sealed class SlimeContactAndSplitPlayModeTests
{
    private readonly List<GameObject> objects = new();
    private GameObject child;
    private Rigidbody2D body;
    private CapsuleCollider2D capsule;

    [SetUp]
    public void SetUp()
    {
        child = Create("Child");
        body = child.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        var foot = Create("BodyCollision");
        foot.transform.SetParent(child.transform, false);
        foot.transform.localPosition = new Vector3(0, 0.2f, 0);
        capsule = foot.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Horizontal;
        capsule.size = new Vector2(0.8f, 0.4f);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    private GameObject Create(string name)
    {
        var result = new GameObject(name);
        objects.Add(result);
        return result;
    }

    private BoxCollider2D Wall(Vector2 center, Vector2 size)
    {
        GameObject wall = Create("Wall");
        wall.layer = 30;
        wall.transform.position = center;
        var collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        Physics2D.SyncTransforms();
        return collider;
    }

    private SlimeSplitPlacement2D Planner(MonsterRoomArea2D room = null) =>
        new(child, null, 1 << 30, 0.08f, 8, room);

    private void AssertSafe(Vector2 position, params Collider2D[] walls)
    {
        body.position = position;
        child.transform.position = position;
        Physics2D.SyncTransforms();
        foreach (Collider2D wall in walls)
        {
            var distance = capsule.Distance(wall);
            Assert.IsTrue(distance.isValid);
            Assert.GreaterOrEqual(distance.distance, 0.035f);
        }
    }

    [Test]
    public void WallOverlapRepairsStartAndLandingUsingChildBody()
    {
        var wall = Wall(new Vector2(-0.5f, 0), new Vector2(1, 8));
        Assert.IsTrue(Planner().TryResolve(new Vector2(0.2f, 0), Vector2.left, 0.7f, out var start, out var landing));
        AssertSafe(start, wall);
        AssertSafe(landing, wall);
        Assert.Greater(start.x, 0.4f);
    }

    [Test]
    public void CornerRepairsBothWallOverlaps()
    {
        var left = Wall(new Vector2(-0.5f, 0), new Vector2(1, 8));
        var bottom = Wall(new Vector2(0, -0.5f), new Vector2(8, 1));
        Assert.IsTrue(Planner().TryResolve(new Vector2(0.2f, -0.05f), Vector2.down, 0.7f, out var start, out var landing));
        AssertSafe(start, left, bottom);
        AssertSafe(landing, left, bottom);
    }

    [Test]
    public void ThinWallCannotBeCrossedByLandingCast()
    {
        var wall = Wall(new Vector2(1, 0), new Vector2(0.1f, 8));
        Assert.IsTrue(Planner().TryResolve(Vector2.zero, Vector2.right, 3f, out var start, out var landing));
        AssertSafe(landing, wall);
        Assert.Less(landing.x, 1f);
    }

    [Test]
    public void NoSpaceFailsInsteadOfReturningOverlappingParentPosition()
    {
        Wall(Vector2.zero, Vector2.one * 10);
        Assert.IsFalse(Planner().TryResolve(Vector2.zero, Vector2.right, 0.7f, out _, out _));
    }

    [Test]
    public void RoomBoundaryIsNotEscaped()
    {
        var areaObject = Create("Room");
        var area = areaObject.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = Vector2.one * 2;
        var room = areaObject.AddComponent<MonsterRoomArea2D>();
        room.Configure(area);
        Assert.IsTrue(Planner(room).TryResolve(Vector2.zero, Vector2.right, 3f, out _, out var landing));
        AssertSafe(landing);
        Assert.IsTrue(room.Contains(capsule.bounds));
    }

    [TestCase(1.3f, false)]
    [TestCase(0.2f, true)]
    public void ChargeUsesLowerBodyRatherThanUpperHurtbox(float playerY, bool expected)
    {
        var hurtbox = child.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.offset = Vector2.up * 0.9f;
        hurtbox.size = new Vector2(1.8f, 1.8f);
        var player = Create("Player").AddComponent<BoxCollider2D>();
        player.size = Vector2.one * 0.2f;
        player.transform.position = new Vector2(0, playerY);
        Physics2D.SyncTransforms();
        Assert.IsTrue(hurtbox.Distance(player).isOverlapped);
        var check = typeof(RookChargeRunner).GetMethod("HasPhysicalBodyContact", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.AreEqual(expected, check.Invoke(null, new object[] { capsule, player }));
        Assert.AreEqual(false, check.Invoke(null, new object[] { hurtbox, player }));
        Assert.IsTrue(hurtbox.enabled);
    }

    [UnityTest]
    public IEnumerator LandingDisablesPhysicsThenRestoresAtSafePosition()
    {
        var motion = child.AddComponent<SlimeSplitLandingMotion2D>();
        typeof(SlimeSplitLandingMotion2D).GetField("createRuntimeShadowWhenNoHeightPresentation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motion, false);
        motion.Begin(Vector2.zero, Vector2.right, 0.05f, 0.5f);
        Assert.IsFalse(body.simulated);
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(body.simulated);
        Assert.Less(Vector2.Distance(body.position, Vector2.right), 0.01f);
        Assert.IsFalse(motion.IsRunning);
        motion.Begin(Vector2.right, Vector2.zero, 1f, 0.5f);
        motion.enabled = false;
        Assert.IsTrue(body.simulated);
        Assert.IsFalse(motion.IsRunning);
    }
}
#endif
