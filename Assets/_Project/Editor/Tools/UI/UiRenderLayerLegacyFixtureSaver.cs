#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Root copies this complete Editor-only helper after its current Unity process exits.
public static class UiRenderLayerLegacyFixtureSaver
{
    public const string FixtureFolder = "Assets/_Project/Tests/Fixtures/UIBatchingLegacy";
    public static void SaveLegacyFixturesIfMissing()
    {
        GameObject global = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        GameObject row = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/KeyMapRow.prefab");
        if (global == null || row == null) throw new InvalidOperationException("The authored baseline UI assets are missing.");
        RetainIfMissing(row, "KeyMapRow");
        RetainIfMissing(global.transform.Find("PopupCanvas/SettingUI")?.gameObject, "SettingUI");
        RetainIfMissing(global.transform.Find("PopupCanvas/KeyMappingUI")?.gameObject, "KeyMappingUI");
        SaveTitleLegacyFixtureIfMissing();
        AssetDatabase.SaveAssets();
    }

    public static void SaveTitleLegacyFixtureIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FixtureFolder + "/TitleSettingUI.prefab") != null) return;
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/TitleScene.unity");
        try
        {
            GameObject settings = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || component.GetType().Name != "SettingsPanelUI") continue;
                    if (settings != null) throw new InvalidOperationException("Expected exactly one Title Settings panel.");
                    settings = component.gameObject;
                }
            RetainIfMissing(settings, "TitleSettingUI");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        AssetDatabase.SaveAssets();
    }

    static void RetainIfMissing(GameObject source, string name)
    {
        string path = FixtureFolder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return; // Never replace the retained original.
        if (source == null) throw new InvalidOperationException("Missing legacy UI source: " + name);
        foreach (MonoBehaviour script in source.GetComponentsInChildren<MonoBehaviour>(true))
            if (script != null && script.GetType().Name == "UiRenderLayerLayout")
                throw new InvalidOperationException("Cannot retain an original from already render-authored source: " + name);
        EnsureFolder(FixtureFolder);
        var staging = new GameObject("Inactive legacy fixture staging");
        staging.SetActive(false);
        try
        {
            GameObject copy = UnityEngine.Object.Instantiate(source, staging.transform, false);
            copy.name = name;
            AssertOwnedPresentationReferences(copy);
            // Persistent localization/input callbacks remain authored in the retained
            // fixture; tests clear controller callbacks on their inactive instance only.
            PrefabUtility.SaveAsPrefabAsset(copy, path, out bool success);
            if (!success) throw new InvalidOperationException("Could not retain legacy UI fixture: " + path);
            Debug.Log("UI_RENDER_LEGACY_FIXTURE," + path);
        }
        finally { UnityEngine.Object.DestroyImmediate(staging); }
    }

    static void AssertOwnedPresentationReferences(GameObject root)
    {
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            string name = component.GetType().Name;
            if (name != "LocalizeTmpFontEvent" && name != "LocalizeStringEvent" && name != "MenuButtonHighlightPresentation") continue;
            var serialized = new SerializedObject(component); var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var reference = property.objectReferenceValue;
                Transform target = reference is Component child ? child.transform : reference is GameObject go ? go.transform : null;
                if (target != null && !target.IsChildOf(root.transform))
                    throw new InvalidOperationException("An authored presentation reference escaped the inactive fixture: " + component.name + "/" + property.propertyPath);
            }
        }
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
#endif
