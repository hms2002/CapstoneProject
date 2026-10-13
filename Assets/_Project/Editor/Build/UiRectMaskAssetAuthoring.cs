#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Run explicitly by the task owner after source compilation; no automatic import/build hook.
public static class UiRectMaskAssetAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    public static readonly string[] ViewportPaths =
    {
        "PopupCanvas/SettingUI/Background/ContentsMask",
        "PopupCanvas/KeyMappingUI/Background/ContentsMask",
    };

    public static void Author()
    {
        var preservation = UiPrefabPresentationPreservation.Capture(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), ViewportPaths);
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            preservation.BindLoadedRoot(contents);
            // Validate both targets before changing either one.
            foreach (string path in ViewportPaths) ValidateViewport(contents.transform.Find(path), path);
            int changed = 0;
            foreach (string path in ViewportPaths)
            {
                Transform target = contents.transform.Find(path);
                if (target.GetComponent<HybridRectClipView>() != null) continue;
                Mask stencil = target.GetComponent<Mask>();
                Image image = target.GetComponent<Image>();
                var rectangle = target.gameObject.AddComponent<RectMask2D>();
                rectangle.enabled = false;
                rectangle.padding = Vector4.zero;
                rectangle.softness = Vector2Int.zero;
                var view = target.gameObject.AddComponent<HybridRectClipView>();
                Canvas[] canvases = target.GetComponentsInParent<Canvas>(true);
                Canvas rootCanvas = canvases[canvases.Length - 1];
                view.ConfigureForAuthoring(stencil, rectangle, image, (RectTransform)target, rootCanvas);
                EditorUtility.SetDirty(view);
                changed++;
            }
            if (changed > 0)
            {
                preservation.RestoreBeforeSave();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            Debug.Log($"Hybrid clipping authored on {changed} approved Settings/Keybind viewports; original stencil/image state and all hierarchy references retained.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void ValidateViewport(Transform target, string path)
    {
        if (target == null || !(target is RectTransform)) throw new InvalidOperationException("Authored viewport missing: " + path);
        Mask mask = target.GetComponent<Mask>();
        Image image = target.GetComponent<Image>();
        if (mask == null || image == null) throw new InvalidOperationException("Original Mask/Image missing: " + path);
        if (!mask.enabled || mask.showMaskGraphic || !image.enabled || image.sprite != null || image.overrideSprite != null ||
            image.type != Image.Type.Simple || image.preserveAspect || image.useSpriteMesh || image.material != image.defaultMaterial ||
            image.alphaHitTestMinimumThreshold != 0f || image.color.a != 1f)
            throw new InvalidOperationException("Viewport no longer has the approved hidden opaque rectangle/input contract: " + path);
        if (target.GetComponentsInParent<Canvas>(true).Length == 0)
            throw new InvalidOperationException("Viewport has no authored Canvas: " + path);
        RectMask2D rectangle = target.GetComponent<RectMask2D>();
        HybridRectClipView view = target.GetComponent<HybridRectClipView>();
        if ((rectangle != null) != (view != null))
            throw new InvalidOperationException("Partial hybrid authoring found; do not guess or replace existing configuration: " + path);
        if (rectangle != null && (rectangle.enabled || rectangle.padding != Vector4.zero || rectangle.softness != Vector2Int.zero))
            throw new InvalidOperationException("Existing hybrid is not stored in its original stencil state: " + path);
        if (view != null)
        {
            var serialized = new SerializedObject(view);
            if (serialized.FindProperty("originalMask").objectReferenceValue != mask ||
                serialized.FindProperty("rectMask").objectReferenceValue != rectangle ||
                serialized.FindProperty("maskImage").objectReferenceValue != image ||
                serialized.FindProperty("viewport").objectReferenceValue != target)
                throw new InvalidOperationException("Existing hybrid references do not match the approved viewport: " + path);
        }
    }
}
#endif
