#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Capture the current persistent asset before LoadPrefabContents, bind once
// immediately after loading, then restore immediately before the scoped Save.
// Does not load, save, replace hierarchy references, or change localization state.
public static class UiPrefabPresentationPreservation
{
    public static Snapshot Capture(GameObject persistentPrefab, params string[] approvedSubtrees)
    {
        if (persistentPrefab == null || !EditorUtility.IsPersistent(persistentPrefab))
            throw new ArgumentException("Capture requires the current persistent prefab asset.");
        return new Snapshot(persistentPrefab, approvedSubtrees ?? Array.Empty<string>());
    }

    public sealed class Snapshot
    {
        readonly List<TextState> texts = new();
        readonly List<ScrollbarHandleState> scrollbarHandles = new();
        readonly List<InstanceState> instances = new();
        bool bound;

        internal Snapshot(GameObject prefab, string[] approved)
        {
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                texts.Add(new TextState(prefab.transform, text));
            var capturedHandles = new HashSet<RectTransform>();
            foreach (Scrollbar scrollbar in prefab.GetComponentsInChildren<Scrollbar>(true))
            {
                RectTransform handle = scrollbar.handleRect;
                if (handle == null || !capturedHandles.Add(handle)) continue;
                string path = RelativePath(prefab.transform, handle);
                if (!approved.Any(subtree => Related(path, subtree)))
                    scrollbarHandles.Add(new ScrollbarHandleState(prefab.transform, scrollbar));
            }
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)) continue;
                string path = RelativePath(prefab.transform, transform);
                // Skip approved roots and any instance containing an approved root.
                // Restoring those modification arrays would undo the authorized edit.
                if (approved.Any(subtree => Related(path, subtree))) continue;
                instances.Add(new InstanceState(prefab.transform, transform));
            }
        }

        public void BindLoadedRoot(GameObject loadedRoot)
        {
            if (bound || loadedRoot == null || EditorUtility.IsPersistent(loadedRoot))
                throw new InvalidOperationException("Bind once to the loaded contents before any wrapping/reparenting.");
            foreach (TextState state in texts) state.Bind(loadedRoot.transform);
            foreach (ScrollbarHandleState state in scrollbarHandles) state.Bind(loadedRoot.transform);
            foreach (InstanceState state in instances) state.Bind(loadedRoot.transform);
            bound = true;
        }

        public void RestoreBeforeSave()
        {
            if (!bound) throw new InvalidOperationException("Loaded object identities have not been bound.");
            // These component identities survive the approved renderer wrapping.
            foreach (TextState state in texts) state.Restore();
            // Restoring fonts can mark a nested component override. Restore the
            // captured unrelated modification arrays leaf before outer root.
            foreach (InstanceState state in instances.OrderByDescending(state => state.Route.Length)) state.Restore();
            // Applying native override arrays can invoke Scrollbar.OnValidate.
            // Restore unrelated handles after that callback, immediately before Save.
            foreach (ScrollbarHandleState state in scrollbarHandles) state.Restore();
        }

        public int TextCount => texts.Count;
        public int PreservedScrollbarHandleCount => scrollbarHandles.Count;
        public int PreservedNestedInstanceCount => instances.Count;
    }

    sealed class ScrollbarHandleState
    {
        static readonly FieldInfo TrackerField = typeof(Scrollbar).GetField("m_Tracker", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly int[] route;
        readonly string path;
        readonly int[] scrollbarRoute;
        readonly string scrollbarPath;
        readonly Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        readonly Vector3 anchoredPosition, localScale;
        readonly Quaternion localRotation;
        RectTransform loaded;
        Scrollbar loadedScrollbar;

        public ScrollbarHandleState(Transform root, Scrollbar owner)
        {
            if (TrackerField == null || TrackerField.FieldType != typeof(DrivenRectTransformTracker))
                throw new InvalidOperationException("Installed uGUI Scrollbar tracker field is unavailable; do not save with an unknown driven-rect owner.");
            RectTransform source = owner.handleRect;
            scrollbarRoute = Route(root, owner.transform); scrollbarPath = RelativePath(root, owner.transform);
            route = Route(root, source); path = RelativePath(root, source);
            anchorMin = source.anchorMin; anchorMax = source.anchorMax;
            pivot = source.pivot; sizeDelta = source.sizeDelta;
            anchoredPosition = source.anchoredPosition3D;
            localScale = source.localScale; localRotation = source.localRotation;
            var serialized = new SerializedObject(source);
            serialized.Update();
            Vector2 serializedAnchorMax = serialized.FindProperty("m_AnchorMax").vector2Value;
            RectTransform corresponding = PrefabUtility.GetCorrespondingObjectFromSource(source);
            Vector2 correspondingSerialized = corresponding == null ? default :
                new SerializedObject(corresponding).FindProperty("m_AnchorMax").vector2Value;
            string sourceValues = corresponding == null ? "none" :
                $"{corresponding.anchorMax}, serialized={correspondingSerialized}";
            Debug.Log($"UI_HANDLE_CAPTURE path={path}; native={anchorMax}; serialized={serializedAnchorMax}; source={sourceValues}; drivenBy={source.drivenByObject}");
        }

        public void Bind(Transform root)
        {
            Transform target = Follow(root, route);
            loaded = target as RectTransform;
            if (loaded == null || RelativePath(root, target) != path || EditorUtility.IsPersistent(loaded))
                throw new InvalidOperationException("Unrelated scrollbar Handle mapping changed before authoring: " + path);
            Transform owner = Follow(root, scrollbarRoute);
            loadedScrollbar = owner.GetComponent<Scrollbar>();
            if (loadedScrollbar == null || RelativePath(root, owner) != scrollbarPath ||
                loadedScrollbar.handleRect != loaded || EditorUtility.IsPersistent(loadedScrollbar))
                throw new InvalidOperationException("Unrelated scrollbar owner mapping changed before authoring: " + scrollbarPath);
        }

        public void Restore()
        {
            if (loaded == null) throw new InvalidOperationException("An unrelated scrollbar Handle was removed: " + path);
            Scrollbar owner = loadedScrollbar;
            RectTransform handle = loaded;
            if (owner == null || handle == null || owner.handleRect != handle)
                throw new InvalidOperationException("The captured scrollbar no longer owns its original Handle: " + scrollbarPath);
            Vector2 before = handle.anchorMax;
            Object driverBefore = handle.drivenByObject;
            // The tracker keeps undriven serialized values separately from current
            // native anchors. Clear its public API before restoring the authored
            // pose, so Save cannot export callback-cached anchor values instead.
            // Do not toggle enabled state or invoke unrelated component lifecycle.
            var tracker = (DrivenRectTransformTracker)TrackerField.GetValue(owner);
            tracker.Clear();
            TrackerField.SetValue(owner, tracker);
            Debug.Log($"UI_HANDLE_TRACKER_CLEAR path={path}; before={before}; afterClear={handle.anchorMax}; driverBefore={driverBefore}; driverAfter={handle.drivenByObject}");
            handle.localScale = localScale; handle.localRotation = localRotation;
            handle.anchorMin = anchorMin; handle.anchorMax = anchorMax;
            handle.pivot = pivot; handle.sizeDelta = sizeDelta;
            handle.anchoredPosition3D = anchoredPosition;
            if (handle.drivenByObject == owner)
                throw new InvalidOperationException("Scrollbar reattached its tracker during exact Handle restoration: " + path);
            EditorUtility.SetDirty(handle);
            Vector2 serializedAnchorMax = new SerializedObject(handle).FindProperty("m_AnchorMax").vector2Value;
            Debug.Log($"UI_HANDLE_RESTORE path={path}; captured={anchorMax}; after={handle.anchorMax}; serialized={serializedAnchorMax}; drivenBy={handle.drivenByObject}");
        }
    }

    sealed class TextState
    {
        static readonly string[] ObjectProperties = { "m_fontAsset", "m_sharedMaterial" };
        static readonly string[] FloatProperties = { "m_fontSize", "m_fontSizeBase", "m_fontSizeMin", "m_fontSizeMax" };
        readonly int[] route;
        readonly string path;
        readonly Object[] objectValues;
        readonly float[] floatValues;
        readonly Object[] sharedMaterials;
        readonly bool autoSize;
        TMP_Text loaded;

        public TextState(Transform root, TMP_Text source)
        {
            route = Route(root, source.transform); path = RelativePath(root, source.transform);
            var serialized = new SerializedObject(source);
            objectValues = ObjectProperties.Select(name => Required(serialized, name).objectReferenceValue).ToArray();
            floatValues = FloatProperties.Select(name => Required(serialized, name).floatValue).ToArray();
            autoSize = Required(serialized, "m_enableAutoSizing").boolValue;
            SerializedProperty materials = Required(serialized, "m_fontSharedMaterials");
            sharedMaterials = new Object[materials.arraySize];
            for (int i = 0; i < sharedMaterials.Length; i++) sharedMaterials[i] = materials.GetArrayElementAtIndex(i).objectReferenceValue;
        }

        public void Bind(Transform root)
        {
            Transform target = Follow(root, route);
            loaded = target.GetComponent<TMP_Text>();
            if (loaded == null || RelativePath(root, target) != path)
                throw new InvalidOperationException("TMP mapping changed before authoring: " + path);
        }

        public void Restore()
        {
            if (loaded == null) throw new InvalidOperationException("An original TMP object was removed: " + path);
            var serialized = new SerializedObject(loaded);
            for (int i = 0; i < ObjectProperties.Length; i++) Required(serialized, ObjectProperties[i]).objectReferenceValue = objectValues[i];
            for (int i = 0; i < FloatProperties.Length; i++) Required(serialized, FloatProperties[i]).floatValue = floatValues[i];
            Required(serialized, "m_enableAutoSizing").boolValue = autoSize;
            SerializedProperty materials = Required(serialized, "m_fontSharedMaterials");
            materials.arraySize = sharedMaterials.Length;
            for (int i = 0; i < sharedMaterials.Length; i++) materials.GetArrayElementAtIndex(i).objectReferenceValue = sharedMaterials[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // TMP OnValidate may select a default material when the restored font
            // differs. Restore the authored reference after that callback, then save.
            typeof(TMP_Text).GetField("m_sharedMaterial", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(loaded, objectValues[1]);
            EditorUtility.SetDirty(loaded);
        }
    }

    sealed class InstanceState
    {
        public readonly int[] Route;
        readonly string path;
        readonly SerializedObject originalHandle;
        readonly SerializedProperty originalModifications;
        readonly int originalCount;
        readonly InternalReference[] internalReferences;
        GameObject loaded;

        public InstanceState(Transform root, Transform source)
        {
            Route = UiPrefabPresentationPreservation.Route(root, source);
            path = RelativePath(root, source);
            Object handle = PrefabUtility.GetPrefabInstanceHandle(source.gameObject);
            if (handle == null) throw new InvalidOperationException("Persistent nested instance handle missing: " + path);
            originalHandle = new SerializedObject(handle);
            originalHandle.Update();
            originalModifications = RawModifications(originalHandle).Copy();
            originalCount = originalModifications.arraySize;
            internalReferences = new InternalReference[originalCount];
            for (int i = 0; i < originalCount; i++)
            {
                SerializedProperty reference = originalModifications.GetArrayElementAtIndex(i).FindPropertyRelative("objectReference");
                if (reference == null) throw new InvalidOperationException("Raw modification object reference missing: " + path);
                internalReferences[i] = InternalReference.Capture(root, reference.objectReferenceValue);
            }
            // Keep this original serialized buffer alive and never Update it after
            // LoadPrefabContents. GetPropertyModifications filters authored entries.
            Debug.Log($"Captured raw prefab overrides: {path}, {originalCount} entries.");
        }

        public void Bind(Transform root)
        {
            Transform target = Follow(root, Route);
            if (RelativePath(root, target) != path || !PrefabUtility.IsAnyPrefabInstanceRoot(target.gameObject))
                throw new InvalidOperationException("Nested instance mapping changed before authoring: " + path);
            loaded = target.gameObject;
            foreach (InternalReference reference in internalReferences) reference?.Bind(root);
        }

        public void Restore()
        {
            if (loaded == null) throw new InvalidOperationException("An unrelated nested instance was removed: " + path);
            if (originalModifications.arraySize != originalCount)
                throw new InvalidOperationException("Original serialized override snapshot changed: " + path);
            Object handle = PrefabUtility.GetPrefabInstanceHandle(loaded);
            if (handle == null) throw new InvalidOperationException("Nested instance handle missing: " + path);
            var serialized = new SerializedObject(handle);
            serialized.Update();
            int beforeCount = RawModifications(serialized).arraySize;
            // Native property copying preserves even unresolved PPtr identifiers,
            // array order, redundant authored values and future inheritance intent.
            // The filtered PropertyModification getter/setter are deliberately unused.
            serialized.CopyFromSerializedProperty(originalModifications);
            SerializedProperty restored = RawModifications(serialized);
            if (restored.arraySize != originalCount)
                throw new InvalidOperationException("Raw override array was not copied exactly: " + path);
            for (int i = 0; i < originalCount; i++)
                if (internalReferences[i] != null)
                    restored.GetArrayElementAtIndex(i).FindPropertyRelative("objectReference").objectReferenceValue = internalReferences[i].Loaded;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(handle);
            serialized.Update();
            if (RawModifications(serialized).arraySize != originalCount)
                throw new InvalidOperationException("Unity normalized the raw override array during Apply: " + path);
            Debug.Log($"UI_RAW_OVERRIDE_RESTORE path={path}; before={beforeCount}; restored={originalCount}");
        }

        static SerializedProperty RawModifications(SerializedObject handle)
        {
            const string propertyPath = "m_Modification.m_Modifications";
            SerializedProperty modifications = handle.FindProperty(propertyPath);
            if (modifications == null || !modifications.isArray)
                throw new InvalidOperationException("PrefabInstance serialized override array is unavailable: " + propertyPath);
            return modifications;
        }
    }

    sealed class InternalReference
    {
        readonly int[] route;
        readonly string path;
        readonly Type componentType;
        readonly int componentIndex;
        public Object Loaded { get; private set; }

        InternalReference(Transform root, Transform target, Component component)
        {
            route = Route(root, target); path = RelativePath(root, target);
            componentType = component != null ? component.GetType() : null;
            componentIndex = component == null ? -1 : Array.IndexOf(
                target.GetComponents(componentType).Where(item => item.GetType() == componentType).ToArray(), component);
            if (component != null && componentIndex < 0) throw new InvalidOperationException("Internal component reference is not on its captured object.");
        }

        public static InternalReference Capture(Transform root, Object reference)
        {
            Component component = reference as Component;
            Transform target = component != null ? component.transform : reference is GameObject go ? go.transform : null;
            return target != null && target.IsChildOf(root) ? new InternalReference(root, target, component) : null;
        }

        public void Bind(Transform root)
        {
            Transform target = Follow(root, route);
            if (RelativePath(root, target) != path) throw new InvalidOperationException("Internal override reference mapping changed: " + path);
            if (componentType == null) Loaded = target.gameObject;
            else
            {
                Component[] components = target.GetComponents(componentType).Where(item => item.GetType() == componentType).ToArray();
                if (componentIndex >= components.Length) throw new InvalidOperationException("Internal component type/index mapping changed: " + path);
                Loaded = components[componentIndex];
            }
            if (EditorUtility.IsPersistent(Loaded)) throw new InvalidOperationException("Mapped override reference escaped the loaded contents.");
        }
    }

    static SerializedProperty Required(SerializedObject serialized, string name) => serialized.FindProperty(name) ??
        throw new InvalidOperationException("TMP serialized presentation property missing: " + name);

    static bool Related(string path, string approved) => path.Length == 0 || approved.Length == 0 || path == approved ||
        path.StartsWith(approved + "/", StringComparison.Ordinal) || approved.StartsWith(path + "/", StringComparison.Ordinal);

    static int[] Route(Transform root, Transform target)
    {
        var indices = new List<int>();
        while (target != root)
        {
            if (target == null) throw new InvalidOperationException("Object is outside the captured prefab root.");
            indices.Insert(0, target.GetSiblingIndex()); target = target.parent;
        }
        return indices.ToArray();
    }

    static Transform Follow(Transform root, int[] route)
    {
        foreach (int index in route)
        {
            if (index >= root.childCount) throw new InvalidOperationException("Loaded hierarchy differs from the captured asset.");
            root = root.GetChild(index);
        }
        return root;
    }

    static string RelativePath(Transform root, Transform target)
    {
        var names = new List<string>();
        while (target != root)
        {
            if (target == null) throw new InvalidOperationException("Object is outside the captured prefab root.");
            names.Insert(0, target.name); target = target.parent;
        }
        return string.Join("/", names);
    }
}
#endif
