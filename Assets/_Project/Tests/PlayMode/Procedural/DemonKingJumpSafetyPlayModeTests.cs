#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Checks body-sized jump planning, airborne physics and idempotent completion/cancellation restoration.</summary>
public sealed class DemonKingJumpSafetyPlayModeTests
{
    private readonly List<GameObject> objects = new();
    private GameObject owner;
    private Rigidbody2D body;
    private BoxCollider2D shape;

    [SetUp]
    public void Setup()
    {
        owner = Create("JumpOwner");
        body = owner.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        shape = owner.AddComponent<BoxCollider2D>();
        shape.size = new Vector2(2, 1);
    }

    [TearDown]
    public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    private GameObject Create(string name)
    {
        var go = new GameObject(name);
        objects.Add(go);
        return go;
    }

    private BoxCollider2D Wall(Vector2 center, Vector2 size)
    {
        var go = Create("Wall");
        go.layer = 30;
        go.transform.position = center;
        var wall = go.AddComponent<BoxCollider2D>();
        wall.size = size;
        Physics2D.SyncTransforms();
        return wall;
    }

    [Test]
    public void WallNearPlayerClampsWholeBodyAndPreservesPlanningPosition()
    {
        var wall = Wall(new Vector2(3, 0), new Vector2(1, 8));
        using var jump = new DemonKingJumpSafety2D(owner, 1 << 30);
        Assert.IsTrue(jump.TryResolve(new Vector2(2.4f, 0), out var start, out var target));
        Assert.Less(target.x, 1.5f);
        Assert.AreEqual(Vector3.zero, owner.transform.position);
        jump.Begin();
        Assert.IsFalse(body.simulated);
        jump.SetPose(target, 3);
        Assert.IsTrue(jump.Complete());
        Assert.IsTrue(body.simulated);
        Assert.That(Vector2.Distance(owner.transform.position, target), Is.LessThan(0.001f));
        Assert.Greater(shape.Distance(wall).distance, 0.02f);
    }

    [Test]
    public void OpenSpaceKeepsRequestedDestination()
    {
        Physics2D.SyncTransforms();
        using var jump = new DemonKingJumpSafety2D(owner, 1 << 30);
        Assert.IsTrue(jump.TryResolve(Vector2.right * 3, out _, out var target));
        Assert.AreEqual(Vector2.right * 3, target);
    }

    [Test]
    public void InitialWallOverlapIsRepairedBeforeJump()
    {
        var wall = Wall(new Vector2(-1.25f, 0), new Vector2(1, 8));
        using var jump = new DemonKingJumpSafety2D(owner, 1 << 30);
        Assert.IsTrue(jump.TryResolve(Vector2.right * 2, out var start, out _));
        Assert.Greater(start.x, 0.25f);
        jump.Begin();
        jump.SetPose(Vector2.one, 4);
        jump.Dispose();
        Assert.IsTrue(body.simulated);
        Assert.That(Vector2.Distance(owner.transform.position, start), Is.LessThan(0.001f));
        Assert.Greater(shape.Distance(wall).distance, 0.02f);
    }

    [Test]
    public void CancellationRestoresGroundAndIsIdempotent()
    {
        Physics2D.SyncTransforms();
        using var jump = new DemonKingJumpSafety2D(owner, 1 << 30);
        Assert.IsTrue(jump.TryResolve(Vector2.right, out var start, out _));
        jump.Begin();
        jump.SetPose(Vector2.right, 5);
        jump.Dispose();
        jump.Dispose();
        Assert.IsTrue(body.simulated);
        Assert.AreEqual((Vector3)start, owner.transform.position);
        Assert.AreEqual(Vector2.zero, body.linearVelocity);
    }

    [Test]
    public void NewWallAtLandingRejectsImpactAndRestoresStart()
    {
        Physics2D.SyncTransforms();
        using var jump = new DemonKingJumpSafety2D(owner, 1 << 30);
        Assert.IsTrue(jump.TryResolve(Vector2.right * 3, out var start, out var target));
        jump.Begin();
        jump.SetPose(target, 2);
        Wall(target, Vector2.one);
        Assert.IsFalse(jump.Complete());
        Assert.IsTrue(body.simulated);
        Assert.AreEqual((Vector3)start, owner.transform.position);
    }

    [Test]
    public void ImpossiblePlacementRejectsJumpWithoutMovingOwner()
    {
        Wall(Vector2.zero, Vector2.one * 20);
        using var jump = new DemonKingJumpSafety2D(owner, 1 << 30);
        Assert.IsFalse(jump.TryResolve(Vector2.right, out _, out _));
        Assert.IsTrue(body.simulated);
        Assert.AreEqual(Vector3.zero, owner.transform.position);
    }
}
#endif
