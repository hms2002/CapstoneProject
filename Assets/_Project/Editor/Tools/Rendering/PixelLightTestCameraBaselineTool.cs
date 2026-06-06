using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class PixelLightTestCameraBaselineTool
{
    private const string ScenePath = "Assets/_Project/Scenes/PixelLightTest.unity";
    private const string ApplyMenuPath = "Tools/Rendering/Pixel Lighting/Apply PixelLightTest Camera Baseline";
    private const string ValidateMenuPath = "Tools/Rendering/Pixel Lighting/Validate PixelLightTest Camera Baseline";
    private const string ReplaceVisionMaskMenuPath = "Tools/Rendering/Pixel Lighting/Replace PixelLightTest Vision Mask With Global Light";

    private const int AssetsPixelsPerUnit = 16;
    private const int ReferenceResolutionX = 1280;
    private const int ReferenceResolutionY = 720;
    private const float PixelLightTestGlobalLightIntensity = 0.35f;

    private sealed class ApplyStats
    {
        public int ComponentsAdded;
        public int CameraChanges;
        public int UrpCameraChanges;
        public int PixelPerfectChanges;
        public int ScenesSaved;

        public bool HasChanges =>
            ComponentsAdded > 0 ||
            CameraChanges > 0 ||
            UrpCameraChanges > 0 ||
            PixelPerfectChanges > 0;
    }

    private sealed class LightingStats
    {
        public int VisionMaskRootsRemoved;
        public int GlobalLightsCreated;
        public int GlobalLightChanges;
        public int ScenesSaved;

        public bool HasChanges =>
            VisionMaskRootsRemoved > 0 ||
            GlobalLightsCreated > 0 ||
            GlobalLightChanges > 0;
    }

    [MenuItem(ApplyMenuPath)]
    public static void ApplyPixelLightTestCameraBaseline()
    {
        ApplyBaseline(askForConfirmation: true, restoreSceneSetup: true);
    }

    [MenuItem(ValidateMenuPath)]
    public static void ValidatePixelLightTestCameraBaseline()
    {
        ValidateBaseline(restoreSceneSetup: true);
    }

    public static void ApplyPixelLightTestCameraBaselineFromCommandLine()
    {
        ApplyBaseline(askForConfirmation: false, restoreSceneSetup: false);
    }

    [MenuItem(ReplaceVisionMaskMenuPath)]
    public static void ReplacePixelLightTestVisionMaskWithGlobalLight()
    {
        ReplaceVisionMaskWithGlobalLight(restoreSceneSetup: true);
    }

    private static void ApplyBaseline(bool askForConfirmation, bool restoreSceneSetup)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot apply PixelLightTest camera baseline while in Play Mode.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"PixelLightTest scene was not found at {ScenePath}.");
            return;
        }

        if (askForConfirmation)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Apply PixelLightTest Camera Baseline",
                "This will open and save Assets/_Project/Scenes/PixelLightTest.unity. The scene Main Camera will get the URP Pixel Perfect Camera baseline: Assets PPU 16, Reference Resolution 1280x720, Point filter mode, Orthographic, HDR, and Post Processing.",
                "Apply And Save",
                "Cancel");

            if (!confirmed)
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
        }

        SceneSetup[] originalSetup = restoreSceneSetup ? EditorSceneManager.GetSceneManagerSetup() : null;

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyStats stats = ApplyScene(scene);
            List<string> validationIssues = ValidateScene(scene);

            if (stats.HasChanges)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                stats.ScenesSaved++;
            }

            if (validationIssues.Count > 0)
            {
                Debug.LogWarning(BuildValidationMessage("PixelLightTest camera baseline applied with remaining issues", validationIssues));
            }
            else
            {
                Debug.Log(
                    $"PixelLightTest camera baseline applied. ScenesSaved={stats.ScenesSaved}, ComponentsAdded={stats.ComponentsAdded}, CameraChanges={stats.CameraChanges}, UrpCameraChanges={stats.UrpCameraChanges}, PixelPerfectChanges={stats.PixelPerfectChanges}.");
            }
        }
        finally
        {
            if (restoreSceneSetup && originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static void ValidateBaseline(bool restoreSceneSetup)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"PixelLightTest scene was not found at {ScenePath}.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSetup = restoreSceneSetup ? EditorSceneManager.GetSceneManagerSetup() : null;

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            List<string> validationIssues = ValidateScene(scene);
            if (validationIssues.Count > 0)
            {
                Debug.LogWarning(BuildValidationMessage("PixelLightTest camera baseline validation failed", validationIssues));
                return;
            }

            Debug.Log("PixelLightTest camera baseline validation passed.");
        }
        finally
        {
            if (restoreSceneSetup && originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static void ReplaceVisionMaskWithGlobalLight(bool restoreSceneSetup)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot replace PixelLightTest vision mask while in Play Mode.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"PixelLightTest scene was not found at {ScenePath}.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSetup = restoreSceneSetup ? EditorSceneManager.GetSceneManagerSetup() : null;

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LightingStats stats = ApplyLightingReplacement(scene);

            if (stats.HasChanges)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                stats.ScenesSaved++;
            }

            Debug.Log(
                $"PixelLightTest vision mask replaced with Global Light 2D. ScenesSaved={stats.ScenesSaved}, VisionMaskRootsRemoved={stats.VisionMaskRootsRemoved}, GlobalLightsCreated={stats.GlobalLightsCreated}, GlobalLightChanges={stats.GlobalLightChanges}.");
        }
        finally
        {
            if (restoreSceneSetup && originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static ApplyStats ApplyScene(Scene scene)
    {
        ApplyStats stats = new();
        Camera camera = ResolveMainCamera(scene);
        if (camera == null)
        {
            Debug.LogError($"No Camera was found in {ScenePath}.");
            return stats;
        }

        if (!camera.orthographic)
        {
            camera.orthographic = true;
            stats.CameraChanges++;
        }

        if (!camera.allowHDR)
        {
            camera.allowHDR = true;
            stats.CameraChanges++;
        }

        if (camera.allowMSAA)
        {
            camera.allowMSAA = false;
            stats.CameraChanges++;
        }

        if (stats.CameraChanges > 0)
            MarkSceneComponentDirty(camera);

        UniversalAdditionalCameraData urpCamera = camera.GetComponent<UniversalAdditionalCameraData>();
        if (urpCamera == null)
        {
            urpCamera = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            stats.ComponentsAdded++;
        }

        if (!urpCamera.renderPostProcessing)
        {
            urpCamera.renderPostProcessing = true;
            stats.UrpCameraChanges++;
        }

        if (!urpCamera.allowHDROutput)
        {
            urpCamera.allowHDROutput = true;
            stats.UrpCameraChanges++;
        }

        if (urpCamera.antialiasing != AntialiasingMode.None)
        {
            urpCamera.antialiasing = AntialiasingMode.None;
            stats.UrpCameraChanges++;
        }

        if (stats.UrpCameraChanges > 0)
            MarkSceneComponentDirty(urpCamera);

        PixelPerfectCamera pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
        if (pixelPerfect == null)
        {
            pixelPerfect = camera.gameObject.AddComponent<PixelPerfectCamera>();
            stats.ComponentsAdded++;
        }

        if (pixelPerfect.assetsPPU != AssetsPixelsPerUnit)
        {
            pixelPerfect.assetsPPU = AssetsPixelsPerUnit;
            stats.PixelPerfectChanges++;
        }

        if (pixelPerfect.refResolutionX != ReferenceResolutionX)
        {
            pixelPerfect.refResolutionX = ReferenceResolutionX;
            stats.PixelPerfectChanges++;
        }

        if (pixelPerfect.refResolutionY != ReferenceResolutionY)
        {
            pixelPerfect.refResolutionY = ReferenceResolutionY;
            stats.PixelPerfectChanges++;
        }

        if (pixelPerfect.cropFrame != PixelPerfectCamera.CropFrame.None)
        {
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
            stats.PixelPerfectChanges++;
        }

        if (pixelPerfect.gridSnapping != PixelPerfectCamera.GridSnapping.UpscaleRenderTexture)
        {
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            stats.PixelPerfectChanges++;
        }

        if (SetPixelPerfectFilterMode(pixelPerfect, PixelPerfectCamera.PixelPerfectFilterMode.Point))
            stats.PixelPerfectChanges++;

        if (stats.PixelPerfectChanges > 0)
            MarkSceneComponentDirty(pixelPerfect);

        return stats;
    }

    private static LightingStats ApplyLightingReplacement(Scene scene)
    {
        LightingStats stats = new();
        List<GameObject> visionMaskRoots = new();
        foreach (Transform transform in FindSceneComponents<Transform>(scene))
        {
            if (transform.parent == null && transform.gameObject.name == "GlobalVisionMaskRoot")
                visionMaskRoots.Add(transform.gameObject);
        }

        for (int i = 0; i < visionMaskRoots.Count; i++)
        {
            Object.DestroyImmediate(visionMaskRoots[i]);
            stats.VisionMaskRootsRemoved++;
        }

        Light2D globalLight = ResolvePixelLightTestGlobalLight(scene);
        if (globalLight == null)
        {
            GameObject lightObject = new("Global Light 2D");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            globalLight = lightObject.AddComponent<Light2D>();
            stats.GlobalLightsCreated++;
        }

        if (globalLight.gameObject.name != "Global Light 2D")
        {
            globalLight.gameObject.name = "Global Light 2D";
            stats.GlobalLightChanges++;
        }

        if (globalLight.lightType != Light2D.LightType.Global)
        {
            globalLight.lightType = Light2D.LightType.Global;
            stats.GlobalLightChanges++;
        }

        if (globalLight.blendStyleIndex != 0)
        {
            globalLight.blendStyleIndex = 0;
            stats.GlobalLightChanges++;
        }

        if (!Approximately(globalLight.intensity, PixelLightTestGlobalLightIntensity))
        {
            globalLight.intensity = PixelLightTestGlobalLightIntensity;
            stats.GlobalLightChanges++;
        }

        if (globalLight.color != Color.white)
        {
            globalLight.color = Color.white;
            stats.GlobalLightChanges++;
        }

        int[] sortingLayerIds = BuildAllSortingLayerIds();
        if (!SameLayerSet(globalLight.targetSortingLayers, sortingLayerIds))
        {
            globalLight.targetSortingLayers = sortingLayerIds;
            stats.GlobalLightChanges++;
        }

        if (stats.GlobalLightChanges > 0 || stats.GlobalLightsCreated > 0)
            MarkSceneComponentDirty(globalLight);

        return stats;
    }

    private static List<string> ValidateScene(Scene scene)
    {
        List<string> issues = new();
        Camera camera = ResolveMainCamera(scene);
        if (camera == null)
        {
            issues.Add("No Camera was found in the scene.");
            return issues;
        }

        if (!camera.orthographic)
            issues.Add("Main Camera must be Orthographic.");

        if (!camera.allowHDR)
            issues.Add("Main Camera HDR must be enabled.");

        if (camera.allowMSAA)
            issues.Add("Main Camera MSAA should be disabled for pixel-art point rendering.");

        UniversalAdditionalCameraData urpCamera = camera.GetComponent<UniversalAdditionalCameraData>();
        if (urpCamera == null)
        {
            issues.Add("Main Camera is missing UniversalAdditionalCameraData.");
        }
        else
        {
            if (!urpCamera.renderPostProcessing)
                issues.Add("URP Camera Post Processing must be enabled.");

            if (!urpCamera.allowHDROutput)
                issues.Add("URP Camera HDR Output must be enabled.");

            if (urpCamera.antialiasing != AntialiasingMode.None)
                issues.Add("URP Camera Anti-aliasing should be None for pixel-art point rendering.");
        }

        PixelPerfectCamera pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
        if (pixelPerfect == null)
        {
            issues.Add("Main Camera is missing PixelPerfectCamera.");
            return issues;
        }

        if (pixelPerfect.assetsPPU != AssetsPixelsPerUnit)
            issues.Add($"PixelPerfectCamera Assets PPU must be {AssetsPixelsPerUnit}.");

        if (pixelPerfect.refResolutionX != ReferenceResolutionX || pixelPerfect.refResolutionY != ReferenceResolutionY)
            issues.Add($"PixelPerfectCamera Reference Resolution must be {ReferenceResolutionX}x{ReferenceResolutionY}.");

        if (pixelPerfect.cropFrame != PixelPerfectCamera.CropFrame.None)
            issues.Add("PixelPerfectCamera Crop Frame must remain None for this test scene baseline.");

        if (pixelPerfect.gridSnapping != PixelPerfectCamera.GridSnapping.UpscaleRenderTexture)
            issues.Add("PixelPerfectCamera Grid Snapping must be Upscale Render Texture.");

        if (!TryGetPixelPerfectFilterMode(pixelPerfect, out PixelPerfectCamera.PixelPerfectFilterMode filterMode))
        {
            issues.Add("PixelPerfectCamera Filter Mode could not be read.");
        }
        else if (filterMode != PixelPerfectCamera.PixelPerfectFilterMode.Point)
        {
            issues.Add("PixelPerfectCamera Filter Mode must be Point.");
        }

        return issues;
    }

    private static Light2D ResolvePixelLightTestGlobalLight(Scene scene)
    {
        Light2D firstGlobalLight = null;
        foreach (Light2D light in FindSceneComponents<Light2D>(scene))
        {
            if (light.lightType != Light2D.LightType.Global)
                continue;

            if (firstGlobalLight == null)
                firstGlobalLight = light;

            if (light.gameObject.name == "Global Light 2D")
                return light;
        }

        return firstGlobalLight;
    }

    private static int[] BuildAllSortingLayerIds()
    {
        SortingLayer[] layers = SortingLayer.layers;
        int[] ids = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            ids[i] = layers[i].id;

        return ids;
    }

    private static bool SameLayerSet(int[] current, int[] expected)
    {
        if (current == null || expected == null || current.Length != expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            bool found = false;
            for (int j = 0; j < current.Length; j++)
            {
                if (current[j] != expected[i])
                    continue;

                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static Camera ResolveMainCamera(Scene scene)
    {
        Camera firstCamera = null;
        Camera namedCamera = null;

        foreach (Camera camera in FindSceneComponents<Camera>(scene))
        {
            if (firstCamera == null)
                firstCamera = camera;

            if (camera.gameObject.name == "Main Camera")
                namedCamera = camera;

            if (camera.CompareTag("MainCamera"))
                return camera;
        }

        return namedCamera != null ? namedCamera : firstCamera;
    }

    private static IEnumerable<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            yield break;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T[] components = roots[i].GetComponentsInChildren<T>(includeInactive: true);
            for (int j = 0; j < components.Length; j++)
                yield return components[j];
        }
    }

    private static bool SetPixelPerfectFilterMode(
        PixelPerfectCamera pixelPerfect,
        PixelPerfectCamera.PixelPerfectFilterMode filterMode)
    {
        SerializedObject serialized = new(pixelPerfect);
        SerializedProperty property = serialized.FindProperty("m_FilterMode");
        if (property == null)
            return false;

        int targetValue = (int)filterMode;
        if (property.enumValueIndex == targetValue)
            return false;

        property.enumValueIndex = targetValue;
        serialized.ApplyModifiedProperties();
        return true;
    }

    private static bool TryGetPixelPerfectFilterMode(
        PixelPerfectCamera pixelPerfect,
        out PixelPerfectCamera.PixelPerfectFilterMode filterMode)
    {
        SerializedObject serialized = new(pixelPerfect);
        SerializedProperty property = serialized.FindProperty("m_FilterMode");
        if (property == null)
        {
            filterMode = PixelPerfectCamera.PixelPerfectFilterMode.RetroAA;
            return false;
        }

        filterMode = (PixelPerfectCamera.PixelPerfectFilterMode)property.enumValueIndex;
        return true;
    }

    private static void MarkSceneComponentDirty(Component component)
    {
        EditorUtility.SetDirty(component);
        if (PrefabUtility.IsPartOfPrefabInstance(component))
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);

        EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private static string BuildValidationMessage(string title, List<string> issues)
    {
        StringBuilder builder = new();
        builder.AppendLine(title);
        for (int i = 0; i < issues.Count; i++)
            builder.AppendLine($"- {issues[i]}");

        return builder.ToString();
    }
}

