#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scoped API authoring for existing Keybinding/Settings objects. Root runs this with the Editor serialized.</summary>
public static class UiRenderLayerAuthoring
{
    public const string RowPrefabPath = "Assets/_Project/Prefabs/UI/KeyMapRow.prefab";
    const string GlobalRootPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    const string TitlePath = "Assets/_Project/Scenes/TitleScene.unity";
    static readonly string[] LayerNames = { "Render_Controls", "Render_Icons", "Render_Text" };

    public static void Author()
    {
        AuthorRowPrefab();
        var preservation = UiPrefabPresentationPreservation.Capture(
            AssetDatabase.LoadAssetAtPath<GameObject>(GlobalRootPath), "PopupCanvas/SettingUI", "PopupCanvas/KeyMappingUI");
        GameObject root = PrefabUtility.LoadPrefabContents(GlobalRootPath);
        try
        {
            preservation.BindLoadedRoot(root);
            bool changed = AuthorPanels(root);
            if (changed)
            {
                preservation.RestoreBeforeSave();
                PrefabUtility.SaveAsPrefabAsset(root, GlobalRootPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Scene scene = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
        bool sceneChanged = false;
        foreach (GameObject sceneRoot in scene.GetRootGameObjects()) sceneChanged |= AuthorPanels(sceneRoot);
        if (sceneChanged) EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("UI_RENDER_LAYERS_AUTHORED,Keybinding/Settings logical refs and source layout slots retained.");
    }

    public static void AuthorRowPrefab()
    {
        var preservation = UiPrefabPresentationPreservation.Capture(
            AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath), string.Empty);
        GameObject row = PrefabUtility.LoadPrefabContents(RowPrefabPath);
        try
        {
            preservation.BindLoadedRoot(row);
            if (row.GetComponent<UiRenderLayerLayout>() != null) return;
            var entries = new List<UiRenderLayerLayout.Binding>();
            var candidates = new List<RectTransform>();
            Collect(row.transform, candidates, true, true);
            foreach (RectTransform candidate in candidates) entries.Add(Wrap(candidate, Layer(candidate), true));
            row.AddComponent<UiRenderLayerLayout>().ConfigureAuthored(Array.Empty<RectTransform>(), entries.ToArray());
            preservation.RestoreBeforeSave();
            PrefabUtility.SaveAsPrefabAsset(row, RowPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(row); }
    }

    public static bool AuthorPanels(GameObject root)
    {
        bool changed = false;
        foreach (KeyBindingPanelUI panel in root.GetComponentsInChildren<KeyBindingPanelUI>(true))
        {
            if (panel.GetComponent<UiRenderLayerLayout>() != null) continue;
            var serialized = new SerializedObject(panel);
            var content = serialized.FindProperty("rowContainer").objectReferenceValue as Transform;
            if (content == null) throw new InvalidOperationException("The authored Keybinding row container is missing.");
            UiRenderLayerLayout layout = panel.gameObject.AddComponent<UiRenderLayerLayout>();
            layout.ConfigureAuthored(CreateLayers(content), Array.Empty<UiRenderLayerLayout.Binding>());
            serialized.Update(); serialized.FindProperty("renderLayout").objectReferenceValue = layout; serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel); changed = true;
        }
        foreach (SettingsPanelUI panel in root.GetComponentsInChildren<SettingsPanelUI>(true))
        {
            if (panel.GetComponent<UiRenderLayerLayout>() != null) continue;
            Transform content = panel.transform.Find("Background/ContentsMask/Contents");
            if (content == null) throw new InvalidOperationException("The authored Settings contents path is missing.");
            var candidates = new List<RectTransform>();
            // Row-root controls move with their Graphics to preserve child pointer routing.
            foreach (Transform row in content) Collect(row, candidates, row.GetComponent<Selectable>() == null, false);
            var entries = new List<UiRenderLayerLayout.Binding>();
            foreach (RectTransform candidate in candidates) entries.Add(Wrap(candidate, Layer(candidate), true));
            panel.gameObject.AddComponent<UiRenderLayerLayout>().ConfigureAuthored(CreateLayers(content), entries.ToArray());
            changed = true;
        }
        return changed;
    }

    static void Collect(Transform root, List<RectTransform> candidates, bool skipRoot, bool splitButtonGraphics)
    {
        if (!skipRoot && root is RectTransform rectangle)
        {
            Selectable selectable = root.GetComponent<Selectable>();
            Graphic graphic = root.GetComponent<Graphic>();
            if (selectable != null || graphic != null)
            {
                candidates.Add(rectangle);
                // A Slider/TMP dropdown owns parent-relative fill/handle/list geometry.
                // Move that control as one subtree; never extract its internal Graphics.
                if (selectable != null && (selectable is not Button || !splitButtonGraphics)) return;
            }
        }
        foreach (Transform child in root) Collect(child, candidates, false, splitButtonGraphics);
    }

    static int Layer(RectTransform target) => target.GetComponent<TMP_Text>() != null ? 2 : target.GetComponent<Selectable>() != null ? 0 : 1;

    static RectTransform[] CreateLayers(Transform content)
    {
        var result = new RectTransform[LayerNames.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var layer = new GameObject(LayerNames[i], typeof(RectTransform), typeof(LayoutElement));
            result[i] = layer.GetComponent<RectTransform>(); result[i].SetParent(content, false);
            Stretch(result[i]); layer.GetComponent<LayoutElement>().ignoreLayout = true;
        }
        return result;
    }

    static UiRenderLayerLayout.Binding Wrap(RectTransform target, int layer, bool suppressDetachedTextRaycast)
    {
        Transform parent = target.parent;
        int sibling = target.GetSiblingIndex();
        var slot = new GameObject(target.name + "_Layout", typeof(RectTransform)).GetComponent<RectTransform>();
        slot.SetParent(parent, false); slot.SetSiblingIndex(sibling);
        CopyRect(target, slot);
        // Authored explicit layout metadata stays at the original parent-space slot.
        if (target.TryGetComponent(out LayoutElement layout))
        {
            UnityEditorInternal.ComponentUtility.CopyComponent(layout);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(slot.gameObject);
        }
        var anchor = new GameObject(target.name + "_Render", typeof(RectTransform)).GetComponent<RectTransform>();
        anchor.SetParent(slot, false); Stretch(anchor); anchor.pivot = slot.pivot;
        target.SetParent(anchor, false); Stretch(target); target.pivot = slot.pivot;
        // The original Button target Graphic remains the hit surface. Detached decorative
        // text/icon targets must not intercept its clicks in the later Text/Icon layers.
        Graphic graphic = target.GetComponent<Graphic>();
        if (suppressDetachedTextRaycast && graphic != null && target.GetComponent<Selectable>() == null)
            graphic.raycastTarget = false;
        return new UiRenderLayerLayout.Binding { layoutSlot = slot, renderAnchor = anchor, target = target.gameObject, layer = layer };
    }

    static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta; target.anchoredPosition3D = source.anchoredPosition3D;
        target.localRotation = source.localRotation; target.localScale = source.localScale;
    }
    static void Stretch(RectTransform rectangle)
    {
        rectangle.anchorMin = Vector2.zero; rectangle.anchorMax = Vector2.one;
        rectangle.offsetMin = Vector2.zero; rectangle.offsetMax = Vector2.zero;
        rectangle.localRotation = Quaternion.identity; rectangle.localScale = Vector3.one;
        Vector3 position = rectangle.localPosition; position.z = 0; rectangle.localPosition = position;
    }
}
#endif
