using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WeaponRigAuthoringTools
{
    private const string GreenWeaponControllerGuid = "851fface87e33534281fcbb17a5fc4ae";
    private const string LightningSpearControllerGuid = "77860c96d6f80384baa71a65e096df34";
    private const string EquipPrefabFolder = "Assets/_Project/Prefabs/Items/Weapons/Equip";
    private const string LightningSpearPrefabFolder = "Assets/_Project/Prefabs/Items/Weapons/LightningSpear";

    [MenuItem("Tools/Weapons/Validate Weapon Rig")]
    private static void ValidateWeaponRig()
    {
        if (Selection.gameObjects.Length > 0)
        {
            foreach (GameObject selected in Selection.gameObjects)
                ValidateSelectedOrPrefabAsset(selected);
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EquipPrefabFolder, LightningSpearPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!fileName.StartsWith("WeaponPrefab"))
                continue;

            ValidatePrefabAtPath(path);
        }
    }

    [MenuItem("Tools/Weapons/Migrate Known Weapon Clips To MotionRoot")]
    private static void MigrateKnownWeaponClipsToMotionRoot()
    {
        var controllers = new List<AnimatorController>();
        AddControllerByGuid(controllers, GreenWeaponControllerGuid);
        AddControllerByGuid(controllers, LightningSpearControllerGuid);

        int migratedCurveCount = 0;
        foreach (AnimationClip clip in CollectClips(controllers))
            migratedCurveCount += MigrateRootTransformCurves(clip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"WeaponRigAuthoringTools migrated {migratedCurveCount} root transform curves to {WeaponVisualRig2D.MotionRootPath}.");
    }

    [MenuItem("Tools/Weapons/Scan Selected Animator Controller")]
    private static void ScanSelectedAnimatorController()
    {
        var controllers = new List<AnimatorController>();
        foreach (Object obj in Selection.objects)
        {
            if (obj is AnimatorController controller)
                controllers.Add(controller);
        }

        if (controllers.Count == 0)
        {
            Debug.LogWarning("Select one or more AnimatorController assets before scanning.");
            return;
        }

        ScanControllers(controllers);
    }

    private static void ValidateSelectedOrPrefabAsset(GameObject selected)
    {
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
        if (!string.IsNullOrEmpty(prefabPath) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == selected)
        {
            ValidatePrefabAtPath(prefabPath);
            return;
        }

        ValidateHierarchy(selected, selected.name);
    }

    private static void ValidatePrefabAtPath(string path)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ValidateHierarchy(prefabRoot, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ValidateHierarchy(GameObject root, string label)
    {
        var visualRig = root.GetComponentInChildren<WeaponVisualRig2D>(true);
        if (visualRig == null)
        {
            Debug.LogWarning($"{label}: missing WeaponVisualRig2D.");
        }
        else if (!visualRig.HasRequiredRig(out string missing))
        {
            Debug.LogWarning($"{label}: WeaponVisualRig2D missing {missing}.");
        }

        var presentationRig = root.GetComponentInChildren<WeaponPresentationRig2D>(true);
        if (presentationRig != null && !presentationRig.HasRequiredRig(out string presentationMissing))
            Debug.LogWarning($"{label}: WeaponPresentationRig2D missing {presentationMissing}.");

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController is AnimatorController controller)
                ScanController(controller, label);
        }
    }

    private static void ScanControllers(IEnumerable<AnimatorController> controllers)
    {
        foreach (AnimatorController controller in controllers)
            ScanController(controller, AssetDatabase.GetAssetPath(controller));
    }

    private static void ScanController(AnimatorController controller, string ownerLabel)
    {
        foreach (AnimationClip clip in CollectClips(controller))
        {
            int rootCurveCount = CountRootTransformCurves(clip);
            if (rootCurveCount > 0)
                Debug.LogWarning($"{ownerLabel}: {clip.name} still has {rootCurveCount} root transform curves.");
        }
    }

    private static int MigrateRootTransformCurves(AnimationClip clip)
    {
        if (clip == null)
            return 0;

        int migrated = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (!IsRootTransformCurve(binding))
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            var newBinding = binding;
            newBinding.path = WeaponVisualRig2D.MotionRootPath;

            Undo.RecordObject(clip, "Migrate weapon root animation curve");
            AnimationUtility.SetEditorCurve(clip, newBinding, curve);
            AnimationUtility.SetEditorCurve(clip, binding, null);
            migrated++;
        }

        if (migrated > 0)
        {
            EditorUtility.SetDirty(clip);
            int remaining = CountRootTransformCurves(clip);
            if (remaining > 0)
                Debug.LogWarning($"{AssetDatabase.GetAssetPath(clip)} still has {remaining} root transform curves after migration.");
        }

        return migrated;
    }

    private static int CountRootTransformCurves(AnimationClip clip)
    {
        if (clip == null)
            return 0;

        int count = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (IsRootTransformCurve(binding))
                count++;
        }

        return count;
    }

    private static bool IsRootTransformCurve(EditorCurveBinding binding)
    {
        return string.IsNullOrEmpty(binding.path)
            && binding.type == typeof(Transform)
            && IsTransformProperty(binding.propertyName);
    }

    private static bool IsTransformProperty(string propertyName)
    {
        return propertyName.StartsWith("m_LocalPosition.")
            || propertyName.StartsWith("m_LocalRotation.")
            || propertyName.StartsWith("m_LocalScale.")
            || propertyName.StartsWith("m_LocalEulerAngles.")
            || propertyName.StartsWith("localEulerAnglesRaw.")
            || propertyName.StartsWith("localEulerAnglesBaked.");
    }

    private static void AddControllerByGuid(List<AnimatorController> controllers, string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            Debug.LogWarning($"AnimatorController guid {guid} was not found.");
            return;
        }

        controllers.Add(controller);
    }

    private static IEnumerable<AnimationClip> CollectClips(IEnumerable<AnimatorController> controllers)
    {
        var clips = new HashSet<AnimationClip>();
        foreach (AnimatorController controller in controllers)
        {
            if (controller == null)
                continue;

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null && clips.Add(clip))
                    yield return clip;
            }
        }
    }

    private static IEnumerable<AnimationClip> CollectClips(AnimatorController controller)
    {
        if (controller == null)
            yield break;

        var clips = new HashSet<AnimationClip>();
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip != null && clips.Add(clip))
                yield return clip;
        }
    }
}


