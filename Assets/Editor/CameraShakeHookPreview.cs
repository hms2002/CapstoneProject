using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CameraShakeHookPreview
{
    private sealed class PreviewState
    {
        public SceneView SceneView;
        public Vector3 BasePivot;
        public Quaternion BaseRotation;
        public float BaseSize;
        public float StartTime;
        public float Duration;
        public float MaxOffset;
        public float Seed;
        public Vector3 DirectionBias;
    }

    private static PreviewState s_activePreview;

    static CameraShakeHookPreview()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    public static bool CanPreview()
    {
        return SceneView.lastActiveSceneView != null;
    }

    public static bool HasActivePreview()
    {
        return s_activePreview != null;
    }

    public static bool Preview(float amplitude, Vector3 direction)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return false;

        StopPreview();

        float clampedAmplitude = Mathf.Max(0f, amplitude);
        if (clampedAmplitude <= 0f)
            return false;

        Vector3 safeDirection = direction;
        safeDirection.z = 0f;
        if (safeDirection.sqrMagnitude > 0.0001f)
            safeDirection.Normalize();
        else
            safeDirection = Vector3.up;

        s_activePreview = new PreviewState
        {
            SceneView = sceneView,
            BasePivot = sceneView.pivot,
            BaseRotation = sceneView.rotation,
            BaseSize = sceneView.size,
            StartTime = (float)EditorApplication.timeSinceStartup,
            Duration = Mathf.Max(0.12f, 0.12f + (clampedAmplitude * 0.04f)),
            MaxOffset = Mathf.Max(0.03f, sceneView.size * clampedAmplitude * 0.05f),
            Seed = Random.value * 1000f,
            DirectionBias = safeDirection
        };

        EditorApplication.update += UpdatePreview;
        SceneView.RepaintAll();
        return true;
    }

    private static void UpdatePreview()
    {
        if (s_activePreview == null || s_activePreview.SceneView == null)
        {
            StopPreview();
            return;
        }

        float now = (float)EditorApplication.timeSinceStartup;
        float elapsed = now - s_activePreview.StartTime;
        if (elapsed >= s_activePreview.Duration)
        {
            StopPreview();
            return;
        }

        float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, s_activePreview.Duration));
        float falloff = 1f - normalized;

        Vector3 right = s_activePreview.BaseRotation * Vector3.right;
        Vector3 up = s_activePreview.BaseRotation * Vector3.up;

        float noiseX = (Mathf.PerlinNoise(s_activePreview.Seed, elapsed * 24f) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(s_activePreview.Seed + 17.31f, elapsed * 27f) - 0.5f) * 2f;

        Vector3 directionalBias = (right * s_activePreview.DirectionBias.x) + (up * s_activePreview.DirectionBias.y);
        Vector3 noiseOffset = (right * noiseX) + (up * noiseY);
        Vector3 offset = (directionalBias * 0.35f + noiseOffset).normalized * s_activePreview.MaxOffset * falloff;

        s_activePreview.SceneView.LookAtDirect(
            s_activePreview.BasePivot + offset,
            s_activePreview.BaseRotation,
            s_activePreview.BaseSize);
        s_activePreview.SceneView.Repaint();
        SceneView.RepaintAll();
    }

    public static void StopPreview()
    {
        EditorApplication.update -= UpdatePreview;

        if (s_activePreview != null && s_activePreview.SceneView != null)
        {
            s_activePreview.SceneView.LookAtDirect(
                s_activePreview.BasePivot,
                s_activePreview.BaseRotation,
                s_activePreview.BaseSize);
            s_activePreview.SceneView.Repaint();
        }

        s_activePreview = null;
        SceneView.RepaintAll();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode ||
            change == PlayModeStateChange.EnteredPlayMode)
        {
            StopPreview();
        }
    }
}
