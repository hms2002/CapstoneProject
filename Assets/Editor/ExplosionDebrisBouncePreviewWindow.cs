using CapstonePresentation;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

internal sealed class ExplosionDebrisBouncePreviewWindow : EditorWindow
{
    private const string HighArcPath =
        "Assets/Resources/DemonKing/Vfx/PF_ExplosionDebrisBounce_HighArc.prefab";
    private const string DiagonalScatterPath =
        "Assets/LeeJunMo/Prefab/Effect/Particle/ExplosionDebrisBounce/PF_ExplosionDebrisBounce_DiagonalScatter.prefab";
    private const string LowSkitterPath =
        "Assets/LeeJunMo/Prefab/Effect/Particle/ExplosionDebrisBounce/PF_ExplosionDebrisBounce_LowSkitter.prefab";
    private const int DefaultPreviewTextureSize = 512;

    private static readonly Vector3 PreviewOrigin = new(10000f, 10000f, 0f);

    private GameObject selectedPrefab;
    private GameObject previewInstance;
    private TopDownDebrisBounceEmitter2D previewEmitter;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private double lastUpdateTime;
    private bool isPlaying = true;
    private float previewSpeed = 1f;
    private float cameraSize = 3.6f;

    [MenuItem("Tools/VFX/Explosion Debris Bounce Preview")]
    private static void Open()
    {
        GetWindow<ExplosionDebrisBouncePreviewWindow>("Debris Bounce Preview");
    }

    private void OnEnable()
    {
        minSize = new Vector2(360f, 420f);
        EditorApplication.update += TickPreview;
        SelectDefaultPrefabIfNeeded();
        EnsurePreviewCamera();
        RestartPreview();
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
        DestroyPreviewInstance();
        DestroyPreviewCamera();
        ReleasePreviewTexture();
    }

    private void OnGUI()
    {
        DrawControls();

        Rect previewRect = GUILayoutUtility.GetRect(
            DefaultPreviewTextureSize,
            DefaultPreviewTextureSize,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        EnsurePreviewTexture(previewRect);
        RenderPreview();

        if (previewTexture != null)
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
        else
            EditorGUI.HelpBox(previewRect, "Preview texture is not available.", MessageType.Warning);

        EditorGUILayout.HelpBox(
            "2D top-down preview: debris moves on world XY ground points. Virtual height only offsets the visible particle upward on screen; contact puffs mark each bounce point on the ground plane.",
            MessageType.Info);
    }

    private void DrawControls()
    {
        EditorGUILayout.Space(4f);
        EditorGUI.BeginChangeCheck();
        selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab",
            selectedPrefab,
            typeof(GameObject),
            allowSceneObjects: false);
        if (EditorGUI.EndChangeCheck())
            RestartPreview();

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            DrawPresetButton("High", HighArcPath);
            DrawPresetButton("Diagonal", DiagonalScatterPath);
            DrawPresetButton("Low", LowSkitterPath);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                isPlaying = !isPlaying;

            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                RestartPreview();
        }

        previewSpeed = EditorGUILayout.Slider("Preview Speed", previewSpeed, 0.1f, 2.5f);
        cameraSize = EditorGUILayout.Slider("Camera Size", cameraSize, 1.2f, 6f);
    }

    private void DrawPresetButton(string label, string prefabPath)
    {
        if (!GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(72f)))
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Explosion debris bounce prefab is missing: {prefabPath}");
            return;
        }

        selectedPrefab = prefab;
        RestartPreview();
    }

    private void TickPreview()
    {
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = lastUpdateTime > 0d
            ? Mathf.Min((float)(now - lastUpdateTime), 0.05f)
            : 0f;
        lastUpdateTime = now;

        if (isPlaying && previewEmitter != null)
            previewEmitter.StepEditorPreview(deltaTime * previewSpeed);

        RenderPreview();
        Repaint();
    }

    private void RestartPreview()
    {
        DestroyPreviewInstance();
        SelectDefaultPrefabIfNeeded();
        lastUpdateTime = EditorApplication.timeSinceStartup;
        isPlaying = true;

        if (selectedPrefab == null)
            return;

        previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
        if (previewInstance == null)
            previewInstance = Object.Instantiate(selectedPrefab);

        previewInstance.name = $"{selectedPrefab.name}_Preview";
        previewInstance.transform.position = PreviewOrigin;
        SetHideFlagsRecursive(previewInstance.transform, HideFlags.HideAndDontSave);
        previewInstance.SetActive(true);

        previewEmitter = previewInstance.GetComponent<TopDownDebrisBounceEmitter2D>();
        if (previewEmitter == null)
        {
            Debug.LogWarning($"{selectedPrefab.name} has no TopDownDebrisBounceEmitter2D component.");
            return;
        }

        previewEmitter.RestartEditorPreview();
        RenderPreview();
    }

    private void SelectDefaultPrefabIfNeeded()
    {
        if (selectedPrefab != null)
            return;

        selectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HighArcPath);
    }

    private void EnsurePreviewCamera()
    {
        if (previewCamera != null)
            return;

        GameObject cameraObject = new("ExplosionDebrisBouncePreviewCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 40f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.08f, 0.08f, 0.085f, 1f);
        previewCamera.transform.position = PreviewOrigin + new Vector3(0f, 0f, -10f);
    }

    private void EnsurePreviewTexture(Rect previewRect)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(previewRect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(previewRect.height));
        if (previewTexture != null && previewTexture.width == width && previewTexture.height == height)
            return;

        ReleasePreviewTexture();
        previewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 1
        };
    }

    private void RenderPreview()
    {
        EnsurePreviewCamera();
        if (previewTexture == null)
            EnsurePreviewTexture(new Rect(0f, 0f, DefaultPreviewTextureSize, DefaultPreviewTextureSize));

        if (previewCamera == null || previewTexture == null)
            return;

        previewCamera.transform.position = PreviewOrigin + new Vector3(0f, 0f, -10f);
        previewCamera.orthographicSize = cameraSize;
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
    }

    private void DestroyPreviewInstance()
    {
        if (previewInstance == null)
            return;

        Object.DestroyImmediate(previewInstance);
        previewInstance = null;
        previewEmitter = null;
    }

    private void DestroyPreviewCamera()
    {
        if (previewCamera == null)
            return;

        GameObject cameraObject = previewCamera.gameObject;
        previewCamera.targetTexture = null;
        previewCamera = null;
        Object.DestroyImmediate(cameraObject);
    }

    private void ReleasePreviewTexture()
    {
        if (previewTexture == null)
            return;

        previewTexture.Release();
        Object.DestroyImmediate(previewTexture);
        previewTexture = null;
    }

    private static void SetHideFlagsRecursive(Transform root, HideFlags hideFlags)
    {
        root.gameObject.hideFlags = hideFlags;
        foreach (Transform child in root)
            SetHideFlagsRecursive(child, hideFlags);
    }
}
