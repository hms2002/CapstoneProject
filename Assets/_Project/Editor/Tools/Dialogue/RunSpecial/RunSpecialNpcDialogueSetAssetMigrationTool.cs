using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#pragma warning disable 0618

public static class RunSpecialNpcDialogueSetAssetMigrationTool
{
    private const string MenuPath = "Tools/RunSpecialNpc/Create Dialogue Set Asset From Selected Interactors";
    private const string OutputFolder = "Assets/_Project/Data/Dialogue/NPC/RunNpc";

    [MenuItem(MenuPath)]
    private static void CreateDialogueSetsFromSelectedInteractors()
    {
        List<RunSpecialNpcInteractor> interactors = CollectSelectedInteractors();
        if (interactors.Count == 0)
        {
            Debug.LogWarning("[RunSpecialNpcDialogueSetMigration] Select at least one RunSpecialNpcInteractor.");
            return;
        }

        EnsureOutputFolder();

        int created = 0;
        for (int i = 0; i < interactors.Count; i++)
        {
            if (CreateDialogueSetFor(interactors[i]))
                created++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[RunSpecialNpcDialogueSetMigration] Created {created}/{interactors.Count} dialogue set assets.");
    }

    [MenuItem(MenuPath, true)]
    private static bool CanCreateDialogueSetsFromSelectedInteractors()
    {
        return CollectSelectedInteractors().Count > 0;
    }

    private static bool CreateDialogueSetFor(RunSpecialNpcInteractor interactor)
    {
        if (interactor == null)
            return false;

        SerializedObject interactorObject = new(interactor);
        RunConstructionNpcFeature constructionFeature =
            FindFeatureReference<RunConstructionNpcFeature>(interactor, interactorObject);
        RunSameSceneTeleportNpcFeature teleportFeature =
            FindFeatureReference<RunSameSceneTeleportNpcFeature>(interactor, interactorObject);

        if (constructionFeature != null)
            return CreateConstructionDialogueSet(interactor, interactorObject, constructionFeature);

        if (teleportFeature != null)
            return CreateTeleportDialogueSet(interactor, interactorObject, teleportFeature);

        Debug.LogWarning(
            $"[RunSpecialNpcDialogueSetMigration] Could not infer feature type for {interactor.name}.",
            interactor);
        return false;
    }

    private static bool CreateConstructionDialogueSet(
        RunSpecialNpcInteractor interactor,
        SerializedObject interactorObject,
        RunConstructionNpcFeature feature)
    {
        RunSpecialNpcDialogueSetSO dialogueSet = ScriptableObject.CreateInstance<RunSpecialNpcDialogueSetSO>();
        SerializedObject setObject = new(dialogueSet);
        setObject.FindProperty("featureKind").enumValueIndex = (int)RunSpecialNpcFeatureKind.Construction;

        RunConstructionNpcDialogueProvider provider = interactor.GetComponent<RunConstructionNpcDialogueProvider>();
        if (provider != null)
            CopyConstructionProviderData(new SerializedObject(provider), setObject, feature);
        else
            CopyConstructionLegacyData(interactorObject, setObject, feature);

        setObject.ApplyModifiedPropertiesWithoutUndo();
        string assetPath = CreateAsset(dialogueSet, interactor);
        WireInteractor(interactor, interactorObject, dialogueSet, feature);
        Debug.Log($"[RunSpecialNpcDialogueSetMigration] Created {assetPath}.", dialogueSet);
        return true;
    }

    private static bool CreateTeleportDialogueSet(
        RunSpecialNpcInteractor interactor,
        SerializedObject interactorObject,
        RunSameSceneTeleportNpcFeature feature)
    {
        RunSpecialNpcDialogueSetSO dialogueSet = ScriptableObject.CreateInstance<RunSpecialNpcDialogueSetSO>();
        SerializedObject setObject = new(dialogueSet);
        setObject.FindProperty("featureKind").enumValueIndex = (int)RunSpecialNpcFeatureKind.SameSceneTeleport;

        RunSameSceneTeleportNpcDialogueProvider provider = interactor.GetComponent<RunSameSceneTeleportNpcDialogueProvider>();
        if (provider != null)
            CopyTeleportProviderData(new SerializedObject(provider), setObject, feature);
        else
            CopyTeleportLegacyData(interactorObject, setObject, feature);

        setObject.ApplyModifiedPropertiesWithoutUndo();
        string assetPath = CreateAsset(dialogueSet, interactor);
        WireInteractor(interactor, interactorObject, dialogueSet, feature);
        Debug.Log($"[RunSpecialNpcDialogueSetMigration] Created {assetPath}.", dialogueSet);
        return true;
    }

    private static void CopyConstructionProviderData(
        SerializedObject providerObject,
        SerializedObject setObject,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        CopyBranch(
            providerObject.FindProperty("notStartedLines"),
            providerObject.FindProperty("availableChoices"),
            setObject.FindProperty("constructionNotStarted"),
            primaryFeature);
        CopyBranch(
            providerObject.FindProperty("pendingLines"),
            null,
            setObject.FindProperty("constructionPending"),
            primaryFeature);
        CopyBranch(
            providerObject.FindProperty("completedLines"),
            null,
            setObject.FindProperty("constructionCompleted"),
            primaryFeature);
    }

    private static void CopyConstructionLegacyData(
        SerializedObject interactorObject,
        SerializedObject setObject,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        CopyBranch(
            interactorObject.FindProperty("openingLines"),
            interactorObject.FindProperty("choices"),
            setObject.FindProperty("constructionNotStarted"),
            primaryFeature);
        CopyBranch(
            interactorObject.FindProperty("noAvailableChoiceLines"),
            null,
            setObject.FindProperty("constructionPending"),
            primaryFeature);
    }

    private static void CopyTeleportProviderData(
        SerializedObject providerObject,
        SerializedObject setObject,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        CopyBranch(
            providerObject.FindProperty("availableLines"),
            providerObject.FindProperty("availableChoices"),
            setObject.FindProperty("teleportAvailable"),
            primaryFeature);
        CopyBranch(
            providerObject.FindProperty("lockedLines"),
            null,
            setObject.FindProperty("teleportLocked"),
            primaryFeature);
        CopyBranch(
            providerObject.FindProperty("unavailableLines"),
            null,
            setObject.FindProperty("teleportUnavailable"),
            primaryFeature);
    }

    private static void CopyTeleportLegacyData(
        SerializedObject interactorObject,
        SerializedObject setObject,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        CopyBranch(
            interactorObject.FindProperty("openingLines"),
            interactorObject.FindProperty("choices"),
            setObject.FindProperty("teleportAvailable"),
            primaryFeature);
        CopyBranch(
            interactorObject.FindProperty("noAvailableChoiceLines"),
            null,
            setObject.FindProperty("teleportLocked"),
            primaryFeature);
        CopyBranch(
            interactorObject.FindProperty("noAvailableChoiceLines"),
            null,
            setObject.FindProperty("teleportUnavailable"),
            primaryFeature);
    }

    private static void CopyBranch(
        SerializedProperty sourceLines,
        SerializedProperty sourceChoices,
        SerializedProperty targetBranch,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        if (targetBranch == null)
            return;

        CopyLineArray(sourceLines, targetBranch.FindPropertyRelative("lines"));
        CopyChoiceArray(sourceChoices, targetBranch.FindPropertyRelative("choices"), primaryFeature);
    }

    private static void CopyChoiceArray(
        SerializedProperty source,
        SerializedProperty target,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        if (target == null || !target.isArray)
            return;

        if (source == null || !source.isArray)
        {
            target.arraySize = 0;
            return;
        }

        target.arraySize = source.arraySize;
        for (int i = 0; i < source.arraySize; i++)
        {
            SerializedProperty sourceChoice = source.GetArrayElementAtIndex(i);
            SerializedProperty targetChoice = target.GetArrayElementAtIndex(i);
            SerializedProperty sourceFeature = sourceChoice.FindPropertyRelative("feature");
            bool executesFeature = sourceFeature?.objectReferenceValue != null;

            if (executesFeature &&
                primaryFeature != null &&
                sourceFeature.objectReferenceValue != primaryFeature)
            {
                Debug.LogWarning(
                    $"[RunSpecialNpcDialogueSetMigration] Choice feature '{sourceFeature.objectReferenceValue.name}' is not the selected primary feature '{primaryFeature.name}'. The migrated choice will execute the primary feature.",
                    primaryFeature);
            }

            targetChoice.FindPropertyRelative("label").stringValue =
                sourceChoice.FindPropertyRelative("label").stringValue;
            targetChoice.FindPropertyRelative("hideWhenActionUnavailable").boolValue =
                sourceChoice.FindPropertyRelative("hideWhenFeatureUnavailable").boolValue;
            targetChoice.FindPropertyRelative("action").enumValueIndex = executesFeature
                ? (int)RunSpecialNpcChoiceAction.ExecutePrimaryFeature
                : (int)RunSpecialNpcChoiceAction.None;
            CopyLineArray(
                sourceChoice.FindPropertyRelative("responseLines"),
                targetChoice.FindPropertyRelative("responseLines"));
            ClearLineArray(targetChoice.FindPropertyRelative("unavailableResponseLines"));
        }
    }

    private static void ClearLineArray(SerializedProperty target)
    {
        if (target != null && target.isArray)
            target.arraySize = 0;
    }

    private static void CopyLineArray(SerializedProperty source, SerializedProperty target)
    {
        if (target == null || !target.isArray)
            return;

        if (source == null || !source.isArray)
        {
            target.arraySize = 0;
            return;
        }

        target.arraySize = source.arraySize;
        for (int i = 0; i < source.arraySize; i++)
            CopyLine(source.GetArrayElementAtIndex(i), target.GetArrayElementAtIndex(i));
    }

    private static void CopyLine(SerializedProperty source, SerializedProperty target)
    {
        target.FindPropertyRelative("text").stringValue =
            source.FindPropertyRelative("text").stringValue;
        target.FindPropertyRelative("duration").floatValue =
            source.FindPropertyRelative("duration").floatValue;
        CopyTheme(
            source.FindPropertyRelative("theme"),
            target.FindPropertyRelative("theme"));
    }

    private static void CopyTheme(SerializedProperty source, SerializedProperty target)
    {
        if (source == null || target == null)
            return;

        target.FindPropertyRelative("useCustomColors").boolValue =
            source.FindPropertyRelative("useCustomColors").boolValue;
        target.FindPropertyRelative("borderColor").colorValue =
            source.FindPropertyRelative("borderColor").colorValue;
        target.FindPropertyRelative("fillColor").colorValue =
            source.FindPropertyRelative("fillColor").colorValue;
        target.FindPropertyRelative("fontColor").colorValue =
            source.FindPropertyRelative("fontColor").colorValue;
    }

    private static T FindFeatureReference<T>(
        RunSpecialNpcInteractor interactor,
        SerializedObject interactorObject)
        where T : RunSpecialNpcFeatureBase
    {
        T feature = interactor.GetComponent<T>();
        if (feature != null)
            return feature;

        feature = FindProviderFeatureReference<T>(interactor);
        if (feature != null)
            return feature;

        SerializedProperty choices = interactorObject.FindProperty("choices");
        if (choices == null || !choices.isArray)
            return null;

        for (int i = 0; i < choices.arraySize; i++)
        {
            SerializedProperty featureProperty =
                choices.GetArrayElementAtIndex(i).FindPropertyRelative("feature");
            if (featureProperty?.objectReferenceValue is T referencedFeature)
                return referencedFeature;
        }

        return null;
    }

    private static T FindProviderFeatureReference<T>(RunSpecialNpcInteractor interactor)
        where T : RunSpecialNpcFeatureBase
    {
        if (typeof(T) == typeof(RunConstructionNpcFeature))
        {
            RunConstructionNpcDialogueProvider provider =
                interactor.GetComponent<RunConstructionNpcDialogueProvider>();
            return ReadProviderFeature<T>(provider);
        }

        if (typeof(T) == typeof(RunSameSceneTeleportNpcFeature))
        {
            RunSameSceneTeleportNpcDialogueProvider provider =
                interactor.GetComponent<RunSameSceneTeleportNpcDialogueProvider>();
            return ReadProviderFeature<T>(provider);
        }

        return null;
    }

    private static T ReadProviderFeature<T>(UnityEngine.Object provider)
        where T : RunSpecialNpcFeatureBase
    {
        if (provider == null)
            return null;

        SerializedObject providerObject = new(provider);
        return providerObject.FindProperty("feature")?.objectReferenceValue as T;
    }

    private static string CreateAsset(RunSpecialNpcDialogueSetSO dialogueSet, RunSpecialNpcInteractor interactor)
    {
        string baseName = $"{interactor.gameObject.name}_RunSpecialDialogue.asset";
        string safeName = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars()));
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{safeName}");
        AssetDatabase.CreateAsset(dialogueSet, assetPath);
        return assetPath;
    }

    private static void WireInteractor(
        RunSpecialNpcInteractor interactor,
        SerializedObject interactorObject,
        RunSpecialNpcDialogueSetSO dialogueSet,
        RunSpecialNpcFeatureBase primaryFeature)
    {
        interactorObject.FindProperty("dialogueSet").objectReferenceValue = dialogueSet;
        interactorObject.FindProperty("primaryFeature").objectReferenceValue = primaryFeature;
        interactorObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(interactor);
        PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
        MarkOwnerDirty(interactor.gameObject);
    }

    private static void EnsureOutputFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder))
            return;

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();
    }

    private static List<RunSpecialNpcInteractor> CollectSelectedInteractors()
    {
        List<RunSpecialNpcInteractor> interactors = new();
        UnityEngine.Object[] selectedObjects = Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            UnityEngine.Object selectedObject = selectedObjects[i];
            if (selectedObject is RunSpecialNpcInteractor interactor)
            {
                AddUnique(interactors, interactor);
                continue;
            }

            if (selectedObject is GameObject gameObject)
            {
                RunSpecialNpcInteractor[] found =
                    gameObject.GetComponentsInChildren<RunSpecialNpcInteractor>(includeInactive: true);
                for (int j = 0; j < found.Length; j++)
                    AddUnique(interactors, found[j]);
            }
        }

        return interactors;
    }

    private static void AddUnique(
        List<RunSpecialNpcInteractor> interactors,
        RunSpecialNpcInteractor interactor)
    {
        if (interactor != null && !interactors.Contains(interactor))
            interactors.Add(interactor);
    }

    private static void MarkOwnerDirty(GameObject owner)
    {
        if (owner == null)
            return;

        if (EditorUtility.IsPersistent(owner))
        {
            EditorUtility.SetDirty(owner);
            return;
        }

        if (owner.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(owner.scene);

        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }
}

#pragma warning restore 0618
