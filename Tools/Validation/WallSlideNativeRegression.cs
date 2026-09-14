// Copy to an isolated Unity project's Assets/Editor with current Core.dll and DOTween.dll
// in Assets/Plugins; enable ugui, physics2d, animation and audio packages.
// Run with -batchmode -nographics -executeMethod WallSlideNativeRegression.Run.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class WallSlideNativeRegression
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        try
        {
            foreach (float speed in new[] { 3f, 20f, 80f })
            {
                foreach (float gap in new[] { 0f, 0.001f })
                {
                    CheckWall(speed, false, gap);
                    CheckWall(speed, true, gap);
                }
            }
            Debug.Log("WALL_SLIDE_REGRESSION_PASS: 12 native physics cases; contact/near-contact, tangent, corner, head-on, escape, legacy policy.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckWall(float speed, bool corner, float gap)
    {
        var actor = new GameObject("Slide probe");
        actor.SetActive(false);
        var body = actor.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var shape = actor.AddComponent<BoxCollider2D>();
        shape.size = Vector2.one * 0.5f;
        var motor = actor.AddComponent<MovementMotor2D>();
        motor.enabled = false;
        Set(motor, "body", body);
        Set(motor, "wallCollisionLayers", (LayerMask)1);
        actor.SetActive(true);
        Call(motor, "CacheBodyColliders");
        Call(motor, "ConfigureWallContactFilter");
        Set(motor, "slideCurrentMovement", true);
        var wall = new GameObject("Vertical wall");
        wall.transform.position = new Vector3(1f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 100f);
        GameObject ceiling = null;
        if (corner)
        {
            ceiling = new GameObject("Corner wall");
            ceiling.transform.position = new Vector3(0f, 1f);
            ceiling.AddComponent<BoxCollider2D>().size = new Vector2(100f, 1f);
        }
        try
        {
            body.position = new Vector2(0.25f - gap, corner ? 0.25f - gap : 0f);
            Physics2D.SyncTransforms();
            var input = new Vector2(speed, speed);
            var result = (Vector2)Call(motor, "ResolveWallSafeVelocity", input);
            Check(Mathf.Abs(result.x) * Time.fixedDeltaTime < 0.031f, "Wall-normal movement was not clipped: " + result);
            Check(corner ? Mathf.Abs(result.y) * Time.fixedDeltaTime < 0.031f : Mathf.Abs(result.y - speed) < 0.01f,
                "Tangential remainder/corner failed: " + result);
            var headOn = (Vector2)Call(motor, "ResolveWallSafeVelocity", Vector2.right * speed);
            Check(headOn.magnitude * Time.fixedDeltaTime < 0.031f, "Head-on movement must stop within the contact skin.");
            var away = (Vector2)Call(motor, "ResolveWallSafeVelocity", Vector2.left * speed);
            Check(Mathf.Abs(away.x + speed) < 0.01f, "Movement away from the wall was blocked.");

            // Start away from contact, spend the approach first, then slide the remainder.
            if (!corner && speed >= 20f)
            {
                body.position = Vector2.zero;
                Physics2D.SyncTransforms();
                result = (Vector2)Call(motor, "ResolveWallSafeVelocity", input);
                Check(result.x > 0f && result.x * Time.fixedDeltaTime <= 0.251f,
                    "Approach crossed the wall: " + result);
                Check(Mathf.Abs(result.y - speed) < 0.01f, "Approach discarded tangential distance.");
            }
            body.position = new Vector2(0.249f, 0f);
            Physics2D.SyncTransforms();
            Set(motor, "slideCurrentMovement", false);
            result = (Vector2)Call(motor, "ResolveWallSafeVelocity", input);
            Check(speed < 8f ? result == input : result.magnitude * Time.fixedDeltaTime < 0.031f,
                "Non-player legacy policy changed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(wall);
            if (ceiling != null) UnityEngine.Object.DestroyImmediate(ceiling);
        }
    }
}
