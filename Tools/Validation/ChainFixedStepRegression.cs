// Run in an isolated Unity project with SettingsPanelFakeChainPresentation.cs copied as source.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ChainFixedStepRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector2 Top = Vector2.zero;
    private static readonly Vector2 Bottom = new Vector2(120f, -480f);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        try
        {
            float[] constant = { 1f / 60f };
            float[] jitter = { .0165f, 1f / 30f - .0165f };
            float[] fast = { 1f / 120f, 1f / 40f };
            foreach (bool anchored in new[] { true, false })
            {
                Vector2 steady = Exercise(constant, anchored, false, out float steadyRange);
                Vector2 varied = Exercise(jitter, anchored, false, out float variedRange);
                Vector2 highFps = Exercise(fast, anchored, false, out float fastRange);
                Check(Vector2.Distance(steady, varied) < .01f, "Jitter changed settled pose");
                Check(Vector2.Distance(steady, highFps) < .01f, "Fast frames changed settled pose");
                Check(Mathf.Max(steadyRange, Mathf.Max(variedRange, fastRange)) < .005f,
                    "Fixed-step chain failed to settle");
                Debug.Log($"CHAIN_FIXED_STEP anchored={anchored} steady={steadyRange} jitter={variedRange} fast={fastRange}");
            }
            Exercise(jitter, true, true, out float legacyRange);
            Check(legacyRange > .02f, "Fixture did not reproduce legacy jitter");
            VerifyResetAndMotion();
            VerifySwingLimit();
            Debug.Log($"CHAIN_FIXED_STEP_PASS legacyJitter={legacyRange}; fixed/variable frames, anchored/free ends, reset and motion");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static SettingsPanelFakeChainPresentation Create(bool anchored)
    {
        var host = new GameObject("Chain regression", typeof(RectTransform));
        host.SetActive(false);
        var chain = host.AddComponent<SettingsPanelFakeChainPresentation>();
        var links = new RectTransform[11];
        var lengths = new float[11];
        for (int i = 0; i < links.Length; i++)
        {
            links[i] = new GameObject("Link", typeof(RectTransform)).GetComponent<RectTransform>();
            links[i].SetParent(host.transform, false);
            lengths[i] = 48.63689f;
        }
        Set(chain, "chainContainer", host.transform);
        Set(chain, "chainLinks", links);
        Set(chain, "segmentLengths", lengths);
        Set(chain, "totalChainLength", 48.63689f * 11);
        Set(chain, "jointPositions", new Vector2[12]);
        Set(chain, "previousJointPositions", new Vector2[12]);
        Set(chain, "bottomEndpointMode", anchored ? SettingsPanelChainBottomEndpointMode.Anchored : SettingsPanelChainBottomEndpointMode.FreeHanging);
        Set(chain, "simulationSubsteps", 1);
        Set(chain, "constraintIterations", 8);
        Set(chain, "enableSupportMotionResponse", false);
        Set(chain, "enableMouseBrushInteraction", false);
        Call(chain, "ResetSimulation", Top, Bottom);
        return chain;
    }

    private static Vector2 Exercise(float[] pattern, bool anchored, bool legacy, out float range)
    {
        var chain = Create(anchored);
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        try
        {
            for (int frame = 0; frame < 1800; frame++)
            {
                float dt = pattern[frame % pattern.Length];
                if (legacy)
                {
                    int count = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 60f)));
                    for (int i = 0; i < count; i++)
                        Call(chain, "Simulate", Top, Bottom, dt / count, false, Vector2.zero, Vector2.zero, false, Vector2.zero);
                }
                else Call(chain, "AdvanceSimulation", dt, Top, Bottom);
                Vector2 point = Get<Vector2[]>(chain, "jointPositions")[5];
                Check(!float.IsNaN(point.y), "Non-finite chain pose");
                if (frame >= 1500) { min = Mathf.Min(min, point.y); max = Mathf.Max(max, point.y); }
            }
            range = max - min;
            return Get<Vector2[]>(chain, "jointPositions")[5];
        }
        finally { UnityEngine.Object.DestroyImmediate(chain.gameObject); }
    }

    private static void VerifySwingLimit()
    {
        var chain = Create(false);
        try
        {
            Vector2[] positions = Get<Vector2[]>(chain, "jointPositions");
            Vector2[] previous = Get<Vector2[]>(chain, "previousJointPositions");
            Vector2 pivot = new Vector2(80f, 40f);
            foreach (float degrees in new[] { -75f, -28f, -20f, 20f, 28f, 75f })
            {
                float radians = degrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
                Vector2 tangent = new Vector2(-direction.y, direction.x) * Mathf.Sign(degrees);
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = pivot + direction * (i * 48.63689f);
                    previous[i] = positions[i] - tangent * i;
                }
                Vector2 oldEnd = positions[11];
                Call(chain, "LimitFreeSwing", pivot, 1f / 60f);
                float angle = Mathf.Abs(Vector2.SignedAngle(Vector2.down, positions[11] - pivot));
                Check(angle <= 30.001f, "Swing exceeded maximum angle");
                Check(positions[0] == pivot, "Top anchor moved");
                for (int i = 1; i < positions.Length; i++)
                {
                    Check(Mathf.Abs(Vector2.Distance(positions[i - 1], positions[i]) - 48.63689f) < .001f,
                        "Swing correction changed link length");
                    if (Mathf.Abs(degrees) > 30f)
                    {
                        Vector2 offset = positions[i] - pivot;
                        Vector2 outward = new Vector2(-offset.y, offset.x).normalized * Mathf.Sign(degrees);
                        Check(Vector2.Dot(positions[i] - previous[i], outward) < .001f,
                            "Hard limit retained outward velocity");
                    }
                }
                if (Mathf.Abs(degrees) < 25f) Check(positions[11] == oldEnd, "Small swing was changed");
                else Check(angle < Mathf.Abs(degrees), "Soft limit did not restore inward");
            }
            Call(chain, "ResetSimulation", Top, Bottom);
            for (int frame = 0; frame < 600; frame++)
            {
                float push = frame < 300 ? 64f : -64f;
                Call(chain, "Simulate", Top, Bottom, 1f / 60f, false, Vector2.zero, Vector2.zero,
                    true, new Vector2(push, 0f));
                Check(Mathf.Abs(Vector2.SignedAngle(Vector2.down, positions[11] - Top)) <= 30.001f,
                    "Repeated support motion escaped swing limit");
            }
            Debug.Log("CHAIN_SWING_LIMIT_PASS both directions, soft zone, lengths, outward velocity, repeated support motion");
        }
        finally { UnityEngine.Object.DestroyImmediate(chain.gameObject); }
    }

    private static void VerifyResetAndMotion()
    {
        var chain = Create(true);
        try
        {
            Call(chain, "AdvanceSimulation", 1f / 120f, Top, Bottom);
            Check(Get<float>(chain, "accumulatedSimulationTime") > 0, "Fractional frame was lost");
            Call(chain, "SyncCachedInputState");
            Check(Get<float>(chain, "accumulatedSimulationTime") == 0, "Input/lifecycle reset retained time");
            Call(chain, "AdvanceSimulation", 1f / 120f, Top, Bottom);
            Call(chain, "ResetSimulation", Top, Bottom);
            Check(Get<float>(chain, "accumulatedSimulationTime") == 0, "Pose reset retained time");
            Vector2 movedBottom = Bottom + new Vector2(12, 20);
            Call(chain, "AdvanceSimulation", 1f / 30f, Top, movedBottom);
            Check(Get<Vector2[]>(chain, "jointPositions")[11] == movedBottom, "Endpoint motion stopped responding");
            Call(chain, "AdvanceSimulation", 3f, Top, movedBottom);
            Check(Get<float>(chain, "accumulatedSimulationTime") < 1f / 60f, "Long-frame backlog retained");
        }
        finally { UnityEngine.Object.DestroyImmediate(chain.gameObject); }
    }
}
