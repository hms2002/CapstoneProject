using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneSetupValidatorWindow : EditorWindow
{
    private const string GlobalUiRootPrefabPath = "Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab";
    private const string ShopSlotPrefabPath = "Assets/LeeJunMo/Prefab/Dialogue/ShopSlot.prefab";
    private const string UpgradeTreePanelPrefabPath = "Assets/LeeJunMo/Prefab/UI/Upgrade/UpgradeTreePanel.prefab";

    private enum Severity
    {
        Info,
        Warning,
        Error
    }

    private sealed class ValidationResult
    {
        public string ScenePath;
        public Severity SeverityLevel;
        public string Message;
        public UnityEngine.Object Context;
        public string ObjectPath;
    }

    private readonly List<ValidationResult> results = new List<ValidationResult>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Validation/Scene Setup Validator")]
    public static void ShowWindow()
    {
        GetWindow<SceneSetupValidatorWindow>("Scene Validator");
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawSummary();
        DrawResults();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Validate Active Scene", EditorStyles.toolbarButton))
                ValidateActiveScene();

            if (GUILayout.Button("Auto Fix Active Scene", EditorStyles.toolbarButton))
                AutoFixActiveScene();

            if (GUILayout.Button("Auto Fix All Scenes", EditorStyles.toolbarButton))
                AutoFixAllScenes();

            if (GUILayout.Button("Auto Fix GlobalUIRoot Prefab", EditorStyles.toolbarButton))
                AutoFixRepresentativeGlobalUiRootPrefab();

            if (GUILayout.Button("Cleanup Active Scene", EditorStyles.toolbarButton))
                CleanupActiveScene();

            if (GUILayout.Button("Validate All Scenes", EditorStyles.toolbarButton))
                ValidateAllScenes();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                results.Clear();
        }
    }

    private void DrawSummary()
    {
        int errorCount = results.Count(result => result.SeverityLevel == Severity.Error);
        int warningCount = results.Count(result => result.SeverityLevel == Severity.Warning);
        int infoCount = results.Count(result => result.SeverityLevel == Severity.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Errors", errorCount.ToString());
        EditorGUILayout.LabelField("Warnings", warningCount.ToString());
        EditorGUILayout.LabelField("Infos", infoCount.ToString());
        EditorGUILayout.Space(6f);
    }

    private void DrawResults()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("No results yet. Run validation for the active scene or all scenes.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        foreach (ValidationResult result in results)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{result.SeverityLevel} - {result.ScenePath}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(result.ObjectPath))
                    EditorGUILayout.LabelField("Object", result.ObjectPath);

                if (result.Context != null)
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(64f)))
                        EditorGUIUtility.PingObject(result.Context);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ValidateActiveScene()
    {
        results.Clear();
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            AddResult(string.Empty, Severity.Error, "No active scene is loaded.", null, string.Empty);
            return;
        }

        ValidateScene(activeScene);
        ValidateRepresentativeGlobalUiRootPrefab();
    }

    private void AutoFixActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            results.Clear();
            AddResult(string.Empty, Severity.Error, "No active scene is loaded.", null, string.Empty);
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Auto Fix Scene Setup");

        AutoFixScene(activeScene);
        EditorSceneManager.MarkSceneDirty(activeScene);
        ValidateActiveScene();
    }

    private void CleanupActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            results.Clear();
            AddResult(string.Empty, Severity.Error, "No active scene is loaded.", null, string.Empty);
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Cleanup Scene Setup");

        CleanupScene(activeScene);
        EditorSceneManager.MarkSceneDirty(activeScene);
        ValidateActiveScene();
    }

    private void AutoFixAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        results.Clear();
        List<string> scenePaths = GetScenePaths().ToList();
        if (scenePaths.Count == 0)
        {
            AddResult(string.Empty, Severity.Error, "No enabled scenes are registered in Build Settings.", null, string.Empty);
            return;
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName($"Auto Fix Scene Setup - {scene.name}");

                AutoFixScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                ValidateScene(scene);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private void AutoFixRepresentativeGlobalUiRootPrefab()
    {
        results.Clear();

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(GlobalUiRootPrefabPath);
        if (prefabContents == null)
        {
            AddResult(GlobalUiRootPrefabPath, Severity.Error, "Representative GlobalUIRoot prefab could not be loaded for auto fix.", null, string.Empty);
            return;
        }

        try
        {
            GlobalUIRoot root = prefabContents.GetComponent<GlobalUIRoot>();
            if (root == null)
            {
                AddResult(GlobalUiRootPrefabPath, Severity.Error, "Representative GlobalUIRoot prefab has no GlobalUIRoot component.", prefabContents, prefabContents.name);
                return;
            }

            SerializedObject serializedRoot = new SerializedObject(root);
            AssignSerializedReference(serializedRoot, "loadingCanvas", FindChildCanvas(prefabContents.transform, "LoadingCanvas"));
            AssignSerializedReference(serializedRoot, "bossHudCanvas", FindChildCanvas(prefabContents.transform, "BossHUDCanvas"));
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();

            EnsureMouseCursorAuthoredPresentation(prefabContents.transform);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, GlobalUiRootPrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        ValidateRepresentativeGlobalUiRootPrefab();
    }

    private void ValidateAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        results.Clear();
        List<string> scenePaths = GetScenePaths().ToList();
        if (scenePaths.Count == 0)
        {
            AddResult(string.Empty, Severity.Error, "No enabled scenes are registered in Build Settings.", null, string.Empty);
            return;
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ValidateScene(scene);
            }

            ValidateRepresentativeGlobalUiRootPrefab();
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static IEnumerable<string> GetScenePaths()
    {
        List<string> scenePaths = new List<string>();
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;

        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene buildScene = buildScenes[i];
            if (buildScene == null || !buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;

            if (!scenePaths.Contains(buildScene.path, StringComparer.Ordinal))
                scenePaths.Add(buildScene.path);
        }

        return scenePaths;
    }

    private void ValidateScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        ValidateGlobalUIRoot(scene);
        ValidateLegacyUiRoots(scene);
        ValidateUniqueGlobalComponents(scene);
        ValidateCameraRig(scene);
        ValidateDialogueControllers(scene);
        ValidateCinematicDirectors(scene);
        ValidateDialogueViews(scene);
        ValidateUpgradeTrees(scene);
        ValidateMerchantShops(scene);
        ValidateRuntimePresentationFallbacks(scene);
    }

    private void AutoFixScene(Scene scene)
    {
        EnsureGlobalUIRootInstance(scene);
        AutoFixGlobalUIRoot(scene);
        AutoFixCameraRig(scene);
        DisableDuplicateGlobalComponentsOutsideRoot(scene);
        DisableLegacyUiRoots(scene);
        AutoFixDialogueControllers(scene);
        AutoFixCinematicDirectors(scene);
        AutoFixDialogueViews(scene);
        AutoFixUpgradeTrees(scene);
    }

    private void CleanupScene(Scene scene)
    {
        CleanupDuplicateGlobalComponentsOutsideRoot(scene);
        CleanupLegacyUiRoots(scene);
    }

    private void ValidateGlobalUIRoot(Scene scene)
    {
        GlobalUIRoot[] roots = FindSceneObjects<GlobalUIRoot>(scene, includeInactive: false);
        if (roots.Length == 0)
        {
            AddResult(scene.path, Severity.Error, "GlobalUIRoot is missing.", null, string.Empty);
            return;
        }

        if (roots.Length > 1)
        {
            foreach (GlobalUIRoot root in roots)
            {
                AddResult(scene.path, Severity.Error, "More than one GlobalUIRoot exists in this scene.", root, GetObjectPath(root.transform));
            }
        }

        GlobalUIRoot primaryRoot = roots[0];
        SerializedObject serializedRoot = new SerializedObject(primaryRoot);

        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "servicesRoot", "GlobalUIRoot.servicesRoot is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "gameplayHudCanvas", "GlobalUIRoot.gameplayHudCanvas is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "dialogueCanvas", "GlobalUIRoot.dialogueCanvas is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "popupCanvas", "GlobalUIRoot.popupCanvas is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "hoverCanvas", "GlobalUIRoot.hoverCanvas is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "rewardCanvas", "GlobalUIRoot.rewardCanvas is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "damagePopupCanvas", "GlobalUIRoot.damagePopupCanvas is not assigned.");
        ValidateSerializedReference(scene.path, primaryRoot, serializedRoot, "bossHudCanvas", "GlobalUIRoot.bossHudCanvas is not assigned.");
        ValidateOptionalPresentationReference(scene.path, primaryRoot, serializedRoot, "loadingCanvas", "GlobalUIRoot.loadingCanvas is not assigned. LoadingOverlayController can create a runtime fallback canvas.");
        ValidateOptionalPresentationReference(scene.path, primaryRoot, serializedRoot, "statusHudPresenterPrefab", "GlobalUIRoot.statusHudPresenterPrefab is not assigned. Status HUD can create a runtime fallback presenter.");
        ValidateOptionalPresentationReference(scene.path, primaryRoot, serializedRoot, "statusTooltipPrefab", "GlobalUIRoot.statusTooltipPrefab is not assigned. Status HUD tooltip can create a runtime fallback view.");

        SerializedProperty promptCanvasProperty = serializedRoot.FindProperty("promptCanvas");
        if (promptCanvasProperty == null || promptCanvasProperty.objectReferenceValue == null)
        {
            AddResult(scene.path, Severity.Warning, "GlobalUIRoot.promptCanvas is not assigned. This is fine only if prompt UI is still scene-local.", primaryRoot, GetObjectPath(primaryRoot.transform));
        }

        Transform servicesRoot = GetSerializedObjectReference<Transform>(serializedRoot, "servicesRoot");
        if (servicesRoot == null)
            return;

        ValidateChildComponentCount<UIManager>(scene.path, servicesRoot, 1, "Services should contain exactly one UIManager.");
        ValidateChildComponentCount<HoverUIController>(scene.path, servicesRoot, 1, "Services should contain exactly one HoverUIController.");
        ValidateChildComponentCount<EventSystem>(scene.path, servicesRoot, 1, "Services should contain exactly one EventSystem.");
        ValidateChildComponentCount<DamagePopupService>(scene.path, servicesRoot, 1, "Services should contain exactly one DamagePopupService.");
        ValidateUpgradeManagerPlacement(scene, servicesRoot);
    }

    private void AutoFixGlobalUIRoot(Scene scene)
    {
        GlobalUIRoot[] roots = FindSceneObjects<GlobalUIRoot>(scene, includeInactive: false);
        if (roots.Length != 1)
            return;

        GlobalUIRoot root = roots[0];
        SerializedObject serializedRoot = new SerializedObject(root);

        Transform servicesRoot = FindChildRecursive(root.transform, "Services");
        AssignSerializedReference(serializedRoot, "servicesRoot", servicesRoot);

        AssignSerializedReference(serializedRoot, "gameplayHudCanvas", FindChildCanvas(root.transform, "GameplayHUDCanvas"));
        AssignSerializedReference(serializedRoot, "dialogueCanvas", FindChildCanvas(root.transform, "DialogueCanvas"));
        AssignSerializedReference(serializedRoot, "popupCanvas", FindChildCanvas(root.transform, "PopupCanvas"));
        AssignSerializedReference(serializedRoot, "hoverCanvas", FindChildCanvas(root.transform, "HoverCanvas"));
        AssignSerializedReference(serializedRoot, "promptCanvas", FindChildCanvas(root.transform, "PromptCanvas"));
        AssignSerializedReference(serializedRoot, "rewardCanvas", FindChildCanvas(root.transform, "RewardCanvas"));
        AssignSerializedReference(serializedRoot, "damagePopupCanvas", FindChildCanvas(root.transform, "DamagePopupCanvas"));
        AssignSerializedReference(serializedRoot, "bossHudCanvas", FindChildCanvas(root.transform, "BossHUDCanvas"));
        AssignSerializedReference(serializedRoot, "loadingCanvas", FindChildCanvas(root.transform, "LoadingCanvas"));

        serializedRoot.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);

        if (servicesRoot == null)
            return;

        MoveUniqueComponentUnderRoot<UIManager>(scene, servicesRoot);
        MoveUniqueComponentUnderRoot<HoverUIController>(scene, servicesRoot);
        MoveUniqueComponentUnderRoot<EventSystem>(scene, servicesRoot);
        MoveUniqueComponentUnderRoot<DamagePopupService>(scene, servicesRoot);
        MoveUniqueComponentUnderRoot<UpgradeManager>(scene, servicesRoot);
    }

    private void ValidateLegacyUiRoots(Scene scene)
    {
        string[] legacyRootNames =
        {
            "TextCanvas",
            "NPCFeatureCanvas",
            "UIRoot"
        };

        foreach (Transform transform in FindSceneObjects<Transform>(scene, includeInactive: false))
        {
            if (transform == null)
                continue;

            if (legacyRootNames.Contains(transform.name, StringComparer.Ordinal))
            {
                AddResult(scene.path, Severity.Warning, "Legacy UI root still exists. Remove it after GlobalUIRoot migration is complete.", transform.gameObject, GetObjectPath(transform));
            }
        }
    }

    private void ValidateUniqueGlobalComponents(Scene scene)
    {
        ValidateUniqueComponent<UIManager>(scene, "UIManager should exist exactly once.");
        ValidateUniqueComponent<HoverUIController>(scene, "HoverUIController should exist exactly once.");
        ValidateUniqueComponent<DialogueView>(scene, "DialogueView should exist exactly once after GlobalUIRoot migration.");
        ValidateUniqueComponent<AffectionUI>(scene, "AffectionUI should exist exactly once after GlobalUIRoot migration.");
        ValidateUniqueComponent<ChestUIManager>(scene, "ChestUIManager should exist exactly once after GlobalUIRoot migration.");
        ValidateUniqueComponent<UpgradeTreeUI>(scene, "UpgradeTreeUI should exist exactly once after GlobalUIRoot migration.");
        ValidateUniqueComponent<RewardDisplayUI>(scene, "RewardDisplayUI should exist exactly once after GlobalUIRoot migration.");
        ValidateUniqueComponent<ItemDetailPanel>(scene, "ItemDetailPanel should exist exactly once after GlobalUIRoot migration.");
        ValidateUniqueComponent<DamagePopupService>(scene, "DamagePopupService should exist exactly once after GlobalUIRoot migration.");
    }

    private void ValidateUpgradeManagerPlacement(Scene scene, Transform servicesRoot)
    {
        UpgradeManager[] managers = FindSceneObjects<UpgradeManager>(scene, includeInactive: true);
        if (managers.Length == 0)
            return;

        if (managers.Length > 1)
        {
            foreach (UpgradeManager manager in managers)
            {
                AddResult(scene.path, Severity.Error, "UpgradeManager should exist exactly once when upgrade support is present in a scene.", manager, GetObjectPath(manager.transform));
            }

            return;
        }

        UpgradeManager managerInstance = managers[0];
        if (servicesRoot == null)
        {
            AddResult(scene.path, Severity.Error, "GlobalUIRoot.servicesRoot is missing, so UpgradeManager cannot be placed under Services.", managerInstance, GetObjectPath(managerInstance.transform));
            return;
        }

        if (!managerInstance.transform.IsChildOf(servicesRoot))
        {
            AddResult(scene.path, Severity.Warning, "UpgradeManager should be a child of GlobalUIRoot/Services in the scene hierarchy.", managerInstance, GetObjectPath(managerInstance.transform));
        }
    }

    private void ValidateDialogueControllers(Scene scene)
    {
        DialogueController[] controllers = FindSceneObjects<DialogueController>(scene, includeInactive: false);
        foreach (DialogueController controller in controllers)
        {
            SerializedObject serializedController = new SerializedObject(controller);

            ValidateSerializedReference(scene.path, controller, serializedController, "view", "DialogueController.view is missing.");
            ValidateSerializedReference(scene.path, controller, serializedController, "director", "DialogueController.director is missing.");
            ValidateSerializedReference(scene.path, controller, serializedController, "portraitController", "DialogueController.portraitController is missing.");
            ValidateSerializedReference(scene.path, controller, serializedController, "tagHandler", "DialogueController.tagHandler is missing.");
        }
    }

    private void ValidateCameraRig(Scene scene)
    {
        bool isBossScene =
            FindSceneObjects<BossEncounterDirector>(scene, includeInactive: true).Length > 0 ||
            FindSceneObjects<BossTalkManager>(scene, includeInactive: true).Length > 0;

        CinemachineCamera[] playerCams = FindSceneObjects<CinemachineCamera>(scene, includeInactive: true)
            .Where(camera => camera != null && string.Equals(camera.name, "PlayerCam", StringComparison.Ordinal))
            .ToArray();
        foreach (CinemachineCamera playerCam in playerCams)
        {
            AddResult(scene.path, Severity.Warning, "Scene-local PlayerCam still exists. CameraBootstrap should own the persistent PlayerCam.", playerCam, GetObjectPath(playerCam.transform));
        }

        CinemachineCamera[] bossCams = FindSceneObjects<CinemachineCamera>(scene, includeInactive: true)
            .Where(camera => camera != null && string.Equals(camera.name, "BossCam", StringComparison.Ordinal))
            .ToArray();

        if (isBossScene)
        {
            if (bossCams.Length != 1)
            {
                if (bossCams.Length == 0)
                {
                    AddResult(scene.path, Severity.Error, "Boss scene must contain exactly one BossCam for boss presentation integrity.", null, string.Empty);
                }
                else
                {
                    foreach (CinemachineCamera bossCam in bossCams)
                    {
                        AddResult(scene.path, Severity.Error, "Boss scene should contain exactly one BossCam.", bossCam, GetObjectPath(bossCam.transform));
                    }
                }
            }
        }
        else
        {
            foreach (CinemachineCamera bossCam in bossCams)
            {
                AddResult(scene.path, Severity.Warning, "Non-boss scene still contains BossCam. Remove it after camera bootstrap migration is complete.", bossCam, GetObjectPath(bossCam.transform));
            }
        }
    }

    private void AutoFixCameraRig(Scene scene)
    {
        bool isBossScene =
            FindSceneObjects<BossEncounterDirector>(scene, includeInactive: true).Length > 0 ||
            FindSceneObjects<BossTalkManager>(scene, includeInactive: true).Length > 0;

        CinemachineCamera[] bossCams = FindSceneObjects<CinemachineCamera>(scene, includeInactive: true)
            .Where(camera => camera != null && string.Equals(camera.name, "BossCam", StringComparison.Ordinal))
            .ToArray();

        if (!isBossScene)
        {
            foreach (CinemachineCamera bossCam in bossCams)
            {
                Undo.DestroyObjectImmediate(bossCam.gameObject);
            }

            return;
        }

        if (bossCams.Length <= 1)
            return;

        for (int i = 1; i < bossCams.Length; i++)
        {
            if (bossCams[i] == null)
                continue;

            Undo.DestroyObjectImmediate(bossCams[i].gameObject);
        }
    }

    private void ValidateCinematicDirectors(Scene scene)
    {
        CinematicDirector[] directors = FindSceneObjects<CinematicDirector>(scene, includeInactive: false);
        foreach (CinematicDirector director in directors)
        {
            SerializedObject serializedDirector = new SerializedObject(director);
            ValidateSerializedReference(scene.path, director, serializedDirector, "portraitController", "CinematicDirector.portraitController is missing.");
        }
    }

    private void AutoFixDialogueControllers(Scene scene)
    {
        DialogueController[] controllers = FindSceneObjects<DialogueController>(scene, includeInactive: false);
        DialogueView activeDialogueView = FindUniqueSceneObject<DialogueView>(scene, includeInactive: false);

        foreach (DialogueController controller in controllers)
        {
            SerializedObject serializedController = new SerializedObject(controller);

            SerializedProperty viewProperty = serializedController.FindProperty("view");
            if (viewProperty != null && activeDialogueView != null)
                viewProperty.objectReferenceValue = activeDialogueView;

            AssignSerializedReferenceIfMissing(serializedController, "director", FindUniqueSceneObject<CinematicDirector>(scene, includeInactive: false));
            AssignSerializedReferenceIfMissing(serializedController, "portraitController", FindUniqueSceneObject<PortraitController>(scene, includeInactive: false));
            AssignSerializedReferenceIfMissing(serializedController, "tagHandler", FindUniqueSceneObject<DialogueTagHandler>(scene, includeInactive: false));
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
    }

    private void AutoFixCinematicDirectors(Scene scene)
    {
        PortraitController portraitController = FindUniqueSceneObject<PortraitController>(scene, includeInactive: false);
        if (portraitController == null)
            return;

        CinematicDirector[] directors = FindSceneObjects<CinematicDirector>(scene, includeInactive: false);
        foreach (CinematicDirector director in directors)
        {
            SerializedObject serializedDirector = new SerializedObject(director);
            AssignSerializedReferenceIfMissing(serializedDirector, "portraitController", portraitController);
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }
    }

    private void ValidateDialogueViews(Scene scene)
    {
        DialogueView[] views = FindSceneObjects<DialogueView>(scene, includeInactive: false);
        foreach (DialogueView view in views)
        {
            SerializedObject serializedView = new SerializedObject(view);

            ValidateSerializedReference(scene.path, view, serializedView, "textBoxGroup", "DialogueView.textBoxGroup is missing.");
            ValidateSerializedReference(scene.path, view, serializedView, "nameText", "DialogueView.nameText is missing.");
            ValidateSerializedReference(scene.path, view, serializedView, "dialogueText", "DialogueView.dialogueText is missing.");
            ValidateSerializedReference(scene.path, view, serializedView, "choiceContainer", "DialogueView.choiceContainer is missing.");
            ValidateSerializedReference(scene.path, view, serializedView, "choiceButtonPrefab", "DialogueView.choiceButtonPrefab is missing.");

            SerializedProperty effectAnimatorProperty = serializedView.FindProperty("dialogueEffectAnimator");
            if (effectAnimatorProperty == null || effectAnimatorProperty.objectReferenceValue == null)
            {
                AddResult(scene.path, Severity.Warning, "DialogueView.dialogueEffectAnimator is missing. Boss dialogue effect sequence will not play.", view, GetObjectPath(view.transform));
            }
        }
    }

    private void AutoFixDialogueViews(Scene scene)
    {
        DialogueView[] views = FindSceneObjects<DialogueView>(scene, includeInactive: false);
        foreach (DialogueView view in views)
        {
            SerializedObject serializedView = new SerializedObject(view);

            AssignSerializedReferenceIfMissing(serializedView, "textBoxGroup",
                FindComponentByNamesInChildren<CanvasGroup>(view.transform, "TextBoxGroup", "DialoguePanel"));
            AssignSerializedReferenceIfMissing(serializedView, "affectionGroup",
                FindComponentByNamesInChildren<CanvasGroup>(view.transform, "AffectionUI"));
            AssignSerializedReferenceIfMissing(serializedView, "nameText",
                FindComponentByNamesInChildren<TextMeshProUGUI>(view.transform, "DisplayName"));
            AssignSerializedReferenceIfMissing(serializedView, "dialogueText",
                FindComponentByNamesInChildren<TextMeshProUGUI>(view.transform, "DialogueText"));
            AssignSerializedReferenceIfMissing(serializedView, "continueIcon",
                FindChildRecursive(view.transform, "ContinueIcon")?.gameObject);
            AssignSerializedReferenceIfMissing(serializedView, "choiceContainer",
                FindChildRecursive(view.transform, "DialogueChoices"));
            AssignSerializedReferenceIfMissing(serializedView, "dimPanelGraphic",
                FindComponentByNamesInChildren<Graphic>(view.transform, "DimPanel"));
            AssignSerializedReferenceIfMissing(serializedView, "dialogueEffectAnimator",
                FindComponentByNamesInChildren<Animator>(view.transform, "DialogueEffect"));

            SerializedProperty choiceButtonPrefabProperty = serializedView.FindProperty("choiceButtonPrefab");
            if (choiceButtonPrefabProperty != null && choiceButtonPrefabProperty.objectReferenceValue == null)
            {
                GameObject choiceButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LeeJunMo/Prefab/UI/Choice0.prefab");
                if (choiceButtonPrefab != null)
                    choiceButtonPrefabProperty.objectReferenceValue = choiceButtonPrefab;
            }

            serializedView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }
    }

    private void ValidateUpgradeTrees(Scene scene)
    {
        UpgradeTreeUI[] trees = FindSceneObjects<UpgradeTreeUI>(scene, includeInactive: true);
        foreach (UpgradeTreeUI tree in trees)
        {
            SerializedObject serializedTree = new SerializedObject(tree);

            ValidateSerializedReference(scene.path, tree, serializedTree, "contentRect", "UpgradeTreeUI.contentRect is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "slotParent", "UpgradeTreeUI.slotParent is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "lineParent", "UpgradeTreeUI.lineParent is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "slotPrefab", "UpgradeTreeUI.slotPrefab is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "linePrefab", "UpgradeTreeUI.linePrefab is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "leftOverflowArrow", $"UpgradeTreeUI.leftOverflowArrow is missing. Migrate the panel to {UpgradeTreePanelPrefabPath}.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "rightOverflowArrow", $"UpgradeTreeUI.rightOverflowArrow is missing. Migrate the panel to {UpgradeTreePanelPrefabPath}.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "upOverflowArrow", $"UpgradeTreeUI.upOverflowArrow is missing. Migrate the panel to {UpgradeTreePanelPrefabPath}.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "downOverflowArrow", $"UpgradeTreeUI.downOverflowArrow is missing. Migrate the panel to {UpgradeTreePanelPrefabPath}.");

            RectTransform contentRect = GetSerializedObjectReference<RectTransform>(serializedTree, "contentRect");
            Transform slotParent = GetSerializedObjectReference<Transform>(serializedTree, "slotParent");
            Transform lineParent = GetSerializedObjectReference<Transform>(serializedTree, "lineParent");

            ScrollRect scrollRect = GetSerializedObjectReference<ScrollRect>(serializedTree, "scrollRect")
                ?? tree.GetComponent<ScrollRect>()
                ?? tree.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect == null)
            {
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI is missing ScrollRect.", tree, GetObjectPath(tree.transform));
                continue;
            }

            if (scrollRect.horizontalScrollbar != null)
                AddResult(scene.path, Severity.Error, $"UpgradeTreeUI.ScrollRect.horizontalScrollbar should be unassigned. Use overflow arrow buttons from {UpgradeTreePanelPrefabPath} instead.", scrollRect.horizontalScrollbar, GetObjectPath(scrollRect.horizontalScrollbar.transform));

            if (scrollRect.viewport == null)
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI.ScrollRect.viewport is missing.", tree, GetObjectPath(tree.transform));

            if (contentRect != null && scrollRect.content != contentRect)
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI.ScrollRect.content does not match contentRect.", tree, GetObjectPath(tree.transform));

            if (contentRect != null && slotParent != null && slotParent.parent != contentRect)
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI.slotParent should be a direct child of contentRect.", slotParent.gameObject, GetObjectPath(slotParent));

            if (contentRect != null && lineParent != null && lineParent.parent != contentRect)
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI.lineParent should be a direct child of contentRect.", lineParent.gameObject, GetObjectPath(lineParent));

            if (slotParent != null && lineParent != null && slotParent == lineParent)
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI.slotParent and lineParent must be different objects.", tree, GetObjectPath(tree.transform));
        }
    }

    private void ValidateRuntimePresentationFallbacks(Scene scene)
    {
        foreach (LoadingOverlayController controller in FindSceneObjects<LoadingOverlayController>(scene, includeInactive: true))
        {
            SerializedObject serializedController = new SerializedObject(controller);
            ValidateOptionalPresentationReference(scene.path, controller, serializedController, "overlayView", "LoadingOverlayController.overlayView is not assigned. It can use GlobalUIRoot.loadingCanvas or create a runtime fallback canvas.");
        }

        foreach (MouseCursorService cursorService in FindSceneObjects<MouseCursorService>(scene, includeInactive: true))
        {
            SerializedObject serializedCursor = new SerializedObject(cursorService);
            Canvas authoredCanvas = GetSerializedObjectReference<Canvas>(serializedCursor, "authoredCursorCanvas");
            Image authoredImage = GetSerializedObjectReference<Image>(serializedCursor, "authoredCursorImage");
            RectTransform authoredRect = GetSerializedObjectReference<RectTransform>(serializedCursor, "authoredCursorRect");
            bool hasSerializedPresentation = authoredCanvas != null && authoredImage != null && authoredRect != null;
            bool hasLegacyChildPresentation = HasMouseCursorChildPresentation(cursorService.transform);

            if (!hasSerializedPresentation && !hasLegacyChildPresentation)
            {
                AddResult(scene.path, Severity.Warning, "MouseCursorService has no authored cursor canvas/image references. Sprite cursor mode can create runtime UI fallback.", cursorService, GetObjectPath(cursorService.transform));
            }
            else if (!hasSerializedPresentation)
            {
                AddResult(scene.path, Severity.Warning, "MouseCursorService uses legacy child-name cursor presentation. Assign authoredCursorCanvas/authoredCursorRect/authoredCursorImage to avoid fallback dependency.", cursorService, GetObjectPath(cursorService.transform));
            }
        }

        foreach (GamePresentationController controller in FindSceneObjects<GamePresentationController>(scene, includeInactive: true))
        {
            AddResult(scene.path, Severity.Info, "GamePresentationController intentionally creates the display letterbox overlay at runtime.", controller, GetObjectPath(controller.transform));
        }

        foreach (StatusHudPresenter presenter in FindSceneObjects<StatusHudPresenter>(scene, includeInactive: true))
        {
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            ValidateOptionalPresentationReference(scene.path, presenter, serializedPresenter, "container", "StatusHudPresenter.container is not assigned. Status HUD can configure its runtime fallback layout.");
            ValidateOptionalPresentationReference(scene.path, presenter, serializedPresenter, "entryViewPrefab", "StatusHudPresenter.entryViewPrefab is not assigned. Status HUD can create runtime entry views.");
        }

        foreach (StatusHudEntryView entryView in FindSceneObjects<StatusHudEntryView>(scene, includeInactive: true))
        {
            SerializedObject serializedEntry = new SerializedObject(entryView);
            ValidateOptionalPresentationReference(scene.path, entryView, serializedEntry, "backgroundImage", "StatusHudEntryView.backgroundImage is not assigned. Entry view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, entryView, serializedEntry, "iconImage", "StatusHudEntryView.iconImage is not assigned. Entry view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, entryView, serializedEntry, "durationFillImage", "StatusHudEntryView.durationFillImage is not assigned. Entry view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, entryView, serializedEntry, "stackText", "StatusHudEntryView.stackText is not assigned. Entry view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, entryView, serializedEntry, "durationText", "StatusHudEntryView.durationText is not assigned. Entry view can create runtime fallback visuals.");
        }

        foreach (StatusHudTooltipView tooltipView in FindSceneObjects<StatusHudTooltipView>(scene, includeInactive: true))
        {
            SerializedObject serializedTooltip = new SerializedObject(tooltipView);
            ValidateOptionalPresentationReference(scene.path, tooltipView, serializedTooltip, "backgroundImage", "StatusHudTooltipView.backgroundImage is not assigned. Tooltip view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, tooltipView, serializedTooltip, "iconImage", "StatusHudTooltipView.iconImage is not assigned. Tooltip view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, tooltipView, serializedTooltip, "nameText", "StatusHudTooltipView.nameText is not assigned. Tooltip view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, tooltipView, serializedTooltip, "storyText", "StatusHudTooltipView.storyText is not assigned. Tooltip view can create runtime fallback visuals.");
            ValidateOptionalPresentationReference(scene.path, tooltipView, serializedTooltip, "effectText", "StatusHudTooltipView.effectText is not assigned. Tooltip view can create runtime fallback visuals.");
        }

        foreach (BossHealthBarUI bossHud in FindSceneObjects<BossHealthBarUI>(scene, includeInactive: true))
        {
            SerializedObject serializedHud = new SerializedObject(bossHud);
            bool canCreateSplitFallback = GetSerializedBool(serializedHud, "createFallbackSplitHealthPresentation");

            if (canCreateSplitFallback && HasAnyMissingSerializedReference(serializedHud, "splitHealthRoot", "splitDividerImage"))
            {
                AddResult(scene.path, Severity.Warning, "BossHealthBarUI split-health references are incomplete while createFallbackSplitHealthPresentation is enabled. Split boss HUD can be created at runtime.", bossHud, GetObjectPath(bossHud.transform));
            }
        }
    }

    private void ValidateRepresentativeGlobalUiRootPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUiRootPrefabPath);
        if (prefab == null)
        {
            AddResult(GlobalUiRootPrefabPath, Severity.Error, "Representative GlobalUIRoot prefab could not be loaded.", null, string.Empty);
            return;
        }

        GlobalUIRoot root = prefab.GetComponent<GlobalUIRoot>();
        if (root == null)
        {
            AddResult(GlobalUiRootPrefabPath, Severity.Error, "Representative GlobalUIRoot prefab has no GlobalUIRoot component.", prefab, prefab.name);
            return;
        }

        SerializedObject serializedRoot = new SerializedObject(root);
        ValidateSerializedReference(GlobalUiRootPrefabPath, root, serializedRoot, "loadingCanvas", "Representative GlobalUIRoot.loadingCanvas is not assigned.");
        ValidateSerializedReference(GlobalUiRootPrefabPath, root, serializedRoot, "bossHudCanvas", "Representative GlobalUIRoot.bossHudCanvas is not assigned.");
        ValidateSerializedReference(GlobalUiRootPrefabPath, root, serializedRoot, "statusHudPresenterPrefab", "Representative GlobalUIRoot.statusHudPresenterPrefab is not assigned.");
        ValidateSerializedReference(GlobalUiRootPrefabPath, root, serializedRoot, "statusTooltipPrefab", "Representative GlobalUIRoot.statusTooltipPrefab is not assigned.");

        LoadingOverlayController loadingOverlay = prefab.GetComponentInChildren<LoadingOverlayController>(true);
        if (loadingOverlay == null)
        {
            AddResult(GlobalUiRootPrefabPath, Severity.Error, "Representative GlobalUIRoot prefab has no LoadingOverlayController.", prefab, prefab.name);
        }
        else
        {
            SerializedObject serializedLoading = new SerializedObject(loadingOverlay);
            ValidateSerializedReference(GlobalUiRootPrefabPath, loadingOverlay, serializedLoading, "overlayView", "Representative LoadingOverlayController.overlayView is not assigned.");
        }

        MouseCursorService cursorService = prefab.GetComponentInChildren<MouseCursorService>(true);
        if (cursorService == null)
        {
            AddResult(GlobalUiRootPrefabPath, Severity.Error, "Representative GlobalUIRoot prefab has no MouseCursorService.", prefab, prefab.name);
        }
        else
        {
            SerializedObject serializedCursor = new SerializedObject(cursorService);
            ValidateSerializedReference(GlobalUiRootPrefabPath, cursorService, serializedCursor, "authoredCursorCanvas", "Representative MouseCursorService.authoredCursorCanvas is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, cursorService, serializedCursor, "authoredCursorRect", "Representative MouseCursorService.authoredCursorRect is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, cursorService, serializedCursor, "authoredCursorImage", "Representative MouseCursorService.authoredCursorImage is not assigned.");
        }

        StatusHudPresenter statusPresenterPrefab = GetSerializedObjectReference<StatusHudPresenter>(serializedRoot, "statusHudPresenterPrefab");
        if (statusPresenterPrefab != null)
        {
            SerializedObject serializedPresenter = new SerializedObject(statusPresenterPrefab);
            ValidateSerializedReference(GlobalUiRootPrefabPath, statusPresenterPrefab, serializedPresenter, "container", "Representative StatusHudPresenter.container is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, statusPresenterPrefab, serializedPresenter, "entryViewPrefab", "Representative StatusHudPresenter.entryViewPrefab is not assigned.");
        }

        StatusHudTooltipView tooltipPrefab = GetSerializedObjectReference<StatusHudTooltipView>(serializedRoot, "statusTooltipPrefab");
        if (tooltipPrefab != null)
        {
            SerializedObject serializedTooltip = new SerializedObject(tooltipPrefab);
            ValidateSerializedReference(GlobalUiRootPrefabPath, tooltipPrefab, serializedTooltip, "backgroundImage", "Representative StatusHudTooltipView.backgroundImage is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, tooltipPrefab, serializedTooltip, "iconImage", "Representative StatusHudTooltipView.iconImage is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, tooltipPrefab, serializedTooltip, "nameText", "Representative StatusHudTooltipView.nameText is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, tooltipPrefab, serializedTooltip, "storyText", "Representative StatusHudTooltipView.storyText is not assigned.");
            ValidateSerializedReference(GlobalUiRootPrefabPath, tooltipPrefab, serializedTooltip, "effectText", "Representative StatusHudTooltipView.effectText is not assigned.");
        }
    }

    private void ValidateMerchantShops(Scene scene)
    {
        MerchantNPC[] merchants = FindSceneObjects<MerchantNPC>(scene, includeInactive: true);
        if (merchants == null || merchants.Length == 0)
            return;

        ShopSlot expectedPrefab = AssetDatabase.LoadAssetAtPath<ShopSlot>(ShopSlotPrefabPath);

        foreach (MerchantNPC merchant in merchants)
        {
            if (merchant == null)
                continue;

            SerializedObject serializedMerchant = new SerializedObject(merchant);
            SerializedProperty prefabProperty = serializedMerchant.FindProperty("slotPrefab");
            SerializedProperty anchorsProperty = serializedMerchant.FindProperty("slotAnchors");
            SerializedProperty legacySlotsProperty = serializedMerchant.FindProperty("shopSlots");

            bool hasSlotPrefab = prefabProperty != null && prefabProperty.objectReferenceValue != null;
            bool hasSlotAnchors = anchorsProperty != null && anchorsProperty.isArray && anchorsProperty.arraySize > 0;
            int legacySlotCount = CountAssignedObjectReferences(legacySlotsProperty);
            int childSlotCount = merchant.GetComponentsInChildren<ShopSlot>(true)
                .Count(slot => slot != null && slot.GetComponentInParent<MerchantNPC>(true) == merchant);

            if (!hasSlotPrefab || !hasSlotAnchors)
            {
                if (legacySlotCount > 0 || childSlotCount > 0)
                {
                    AddResult(
                        scene.path,
                        Severity.Warning,
                        "MerchantNPC still uses copied scene ShopSlot objects. Use Tools/Merchant/ShopSlot Prefab Migration to create empty anchors, assign the ShopSlot prefab, and clear copied slots.",
                        merchant,
                        GetObjectPath(merchant.transform));
                }

                continue;
            }

            if (expectedPrefab != null && prefabProperty.objectReferenceValue != expectedPrefab)
            {
                AddResult(
                    scene.path,
                    Severity.Warning,
                    $"MerchantNPC.slotPrefab does not reference the shared prefab at {ShopSlotPrefabPath}.",
                    merchant,
                    GetObjectPath(merchant.transform));
            }

            ValidateMerchantShopAnchors(scene.path, merchant, anchorsProperty);
            ValidateMerchantShopFilters(scene.path, merchant, anchorsProperty);

            if (legacySlotCount > 0 || childSlotCount > 0)
            {
                AddResult(
                    scene.path,
                    Severity.Warning,
                    "MerchantNPC has prefab-slot authoring but copied child/shopSlots still exist. Remove copied scene ShopSlot objects after confirming anchor order.",
                    merchant,
                    GetObjectPath(merchant.transform));
            }
        }
    }

    private void ValidateMerchantShopAnchors(
        string scenePath,
        MerchantNPC merchant,
        SerializedProperty anchorsProperty)
    {
        for (int i = 0; i < anchorsProperty.arraySize; i++)
        {
            SerializedProperty anchorEntry = anchorsProperty.GetArrayElementAtIndex(i);
            SerializedProperty anchorProperty = anchorEntry.FindPropertyRelative("anchor");
            if (anchorProperty != null && anchorProperty.objectReferenceValue != null)
                continue;

            AddResult(
                scenePath,
                Severity.Error,
                $"MerchantNPC.slotAnchors[{i}] has no anchor Transform.",
                merchant,
                GetObjectPath(merchant.transform));
        }
    }

    private void ValidateMerchantShopFilters(
        string scenePath,
        MerchantNPC merchant,
        SerializedProperty anchorsProperty)
    {
        bool hasWeaponSlot = false;
        bool hasRelicSlot = false;
        bool hasConsumableSlot = false;

        for (int i = 0; i < anchorsProperty.arraySize; i++)
        {
            SerializedProperty anchorEntry = anchorsProperty.GetArrayElementAtIndex(i);
            SerializedProperty filterProperty = anchorEntry.FindPropertyRelative("itemFilter");
            if (filterProperty == null)
                continue;

            ShopSlotItemFilter filter = (ShopSlotItemFilter)filterProperty.enumValueIndex;
            hasWeaponSlot |= filter == ShopSlotItemFilter.Weapon;
            hasRelicSlot |= filter == ShopSlotItemFilter.Relic;
            hasConsumableSlot |= filter == ShopSlotItemFilter.Consumable;
        }

        if (hasWeaponSlot && hasRelicSlot && hasConsumableSlot)
            return;

        AddResult(
            scenePath,
            Severity.Warning,
            "MerchantNPC prefab anchors should include separate Weapon, Relic, and Consumable filters for the requested shop display split.",
            merchant,
            GetObjectPath(merchant.transform));
    }

    private void AutoFixUpgradeTrees(Scene scene)
    {
        UpgradeTreeUI[] trees = FindSceneObjects<UpgradeTreeUI>(scene, includeInactive: true);
        foreach (UpgradeTreeUI tree in trees)
        {
            SerializedObject serializedTree = new SerializedObject(tree);

            RectTransform contentRect = FindChildRecursive(tree.transform, "Contents") as RectTransform
                ?? FindChildRecursive(tree.transform, "Content") as RectTransform;
            Transform slotParent = FindChildRecursive(tree.transform, "Slots")
                ?? FindChildRecursive(tree.transform, "SlotParent");
            Transform lineParent = FindChildRecursive(tree.transform, "Lines")
                ?? FindChildRecursive(tree.transform, "LineParent");

            AssignSerializedReference(serializedTree, "contentRect", contentRect);
            AssignSerializedReference(serializedTree, "slotParent", slotParent);
            AssignSerializedReference(serializedTree, "lineParent", lineParent);

            SerializedProperty slotPrefabProperty = serializedTree.FindProperty("slotPrefab");
            if (slotPrefabProperty != null && slotPrefabProperty.objectReferenceValue == null)
            {
                GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LeeJunMo/Prefab/UI/Upgrade/MS_Slot.prefab");
                if (slotPrefab != null)
                    slotPrefabProperty.objectReferenceValue = slotPrefab;
            }

            SerializedProperty linePrefabProperty = serializedTree.FindProperty("linePrefab");
            if (linePrefabProperty != null && linePrefabProperty.objectReferenceValue == null)
            {
                GameObject linePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LeeJunMo/Prefab/UI/Upgrade/line.prefab");
                if (linePrefab != null)
                    linePrefabProperty.objectReferenceValue = linePrefab;
            }

            serializedTree.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tree);

            ScrollRect scrollRect = tree.GetComponent<ScrollRect>();
            if (scrollRect == null)
                continue;

            if (contentRect != null)
            {
                Undo.RecordObject(scrollRect, "Fix UpgradeTreeUI ScrollRect");
                scrollRect.content = contentRect;
            }

            if (scrollRect.horizontalScrollbar != null)
            {
                Scrollbar horizontalScrollbar = scrollRect.horizontalScrollbar;
                Undo.RecordObject(scrollRect, "Remove UpgradeTreeUI Horizontal Scrollbar");
                scrollRect.horizontalScrollbar = null;
                scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

                Undo.RecordObject(horizontalScrollbar, "Disable UpgradeTreeUI Horizontal Scrollbar");
                horizontalScrollbar.interactable = false;

                if (horizontalScrollbar.gameObject.activeSelf)
                {
                    Undo.RecordObject(horizontalScrollbar.gameObject, "Disable UpgradeTreeUI Horizontal Scrollbar");
                    horizontalScrollbar.gameObject.SetActive(false);
                }

                EditorUtility.SetDirty(horizontalScrollbar);
            }

            if (scrollRect.viewport == null)
            {
                RectTransform viewport = FindChildRecursive(tree.transform, "Viewport") as RectTransform
                    ?? FindChildRecursive(tree.transform, "Panel") as RectTransform;
                if (viewport != null)
                {
                    Undo.RecordObject(scrollRect, "Fix UpgradeTreeUI Viewport");
                    scrollRect.viewport = viewport;
                }
            }

            if (contentRect != null)
            {
                if (slotParent != null && slotParent.parent != contentRect)
                    Undo.SetTransformParent(slotParent, contentRect, "Fix UpgradeTreeUI Slot Parent");

                if (lineParent != null && lineParent.parent != contentRect)
                    Undo.SetTransformParent(lineParent, contentRect, "Fix UpgradeTreeUI Line Parent");
            }

            if (contentRect != null)
            {
                Undo.RecordObject(contentRect, "Fix UpgradeTreeUI Content Rect");
                contentRect.pivot = new Vector2(0f, 0.5f);
                contentRect.anchorMin = new Vector2(0f, 0.5f);
                contentRect.anchorMax = new Vector2(0f, 0.5f);
                contentRect.anchoredPosition = Vector2.zero;
            }

            FixChildRectTransform(slotParent as RectTransform);
            FixChildRectTransform(lineParent as RectTransform);
            EditorUtility.SetDirty(scrollRect);
        }
    }

    private void ValidateUniqueComponent<T>(Scene scene, string message) where T : Component
    {
        T[] components = FindSceneObjects<T>(scene, includeInactive: false);
        if (components.Length == 1)
            return;

        Severity severity = components.Length == 0 ? Severity.Warning : Severity.Error;
        if (components.Length == 0)
        {
            AddResult(scene.path, severity, message, null, string.Empty);
            return;
        }

        foreach (T component in components)
        {
            AddResult(scene.path, severity, message, component, GetObjectPath(component.transform));
        }
    }

    private void ValidateChildComponentCount<T>(string scenePath, Transform root, int expectedCount, string message) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        if (components.Length == expectedCount)
            return;

        Severity severity = components.Length == 0 ? Severity.Error : Severity.Warning;
        if (components.Length == 0)
        {
            AddResult(scenePath, severity, message, root.gameObject, GetObjectPath(root));
            return;
        }

        foreach (T component in components)
        {
            AddResult(scenePath, severity, message, component, GetObjectPath(component.transform));
        }
    }

    private void ValidateSerializedReference(string scenePath, Component owner, SerializedObject serializedObject, string propertyName, string message)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null)
            return;

        AddResult(scenePath, Severity.Error, message, owner, GetObjectPath(owner.transform));
    }

    private void ValidateOptionalPresentationReference(string scenePath, Component owner, SerializedObject serializedObject, string propertyName, string message)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null)
            return;

        AddResult(scenePath, Severity.Warning, message, owner, GetObjectPath(owner.transform));
    }

    private static bool HasAnyMissingSerializedReference(SerializedObject serializedObject, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.objectReferenceValue == null)
                return true;
        }

        return false;
    }

    private static int CountAssignedObjectReferences(SerializedProperty arrayProperty)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
            return 0;

        int count = 0;
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue != null)
                count++;
        }

        return count;
    }

    private static bool HasMouseCursorChildPresentation(Transform cursorServiceRoot)
    {
        if (cursorServiceRoot == null)
            return false;

        Transform canvasTransform = cursorServiceRoot.Find("MouseCursorCanvas");
        if (canvasTransform == null)
            return false;

        return canvasTransform.GetComponent<Canvas>() != null &&
               canvasTransform.Find("CursorImage")?.GetComponent<Image>() != null;
    }

    private static bool GetSerializedBool(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
    }

    private static T GetSerializedObjectReference<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void AssignSerializedReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void AssignSerializedReferenceIfMissing(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    private static T[] FindSceneObjects<T>(Scene scene, bool includeInactive = true) where T : UnityEngine.Object
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(obj => obj != null && IsInScene(obj, scene) && (includeInactive || IsActiveInHierarchy(obj)))
            .ToArray();
    }

    private static T FindUniqueSceneObject<T>(Scene scene, bool includeInactive = true) where T : UnityEngine.Object
    {
        T[] objects = FindSceneObjects<T>(scene, includeInactive);
        return objects.Length == 1 ? objects[0] : null;
    }

    private static bool IsInScene(UnityEngine.Object obj, Scene scene)
    {
        return obj switch
        {
            Component component => component.gameObject.scene == scene,
            GameObject gameObject => gameObject.scene == scene,
            _ => false
        };
    }

    private static bool IsActiveInHierarchy(UnityEngine.Object obj)
    {
        return obj switch
        {
            Behaviour behaviour => behaviour.isActiveAndEnabled,
            Component component => component.gameObject.activeInHierarchy,
            GameObject gameObject => gameObject.activeInHierarchy,
            _ => true
        };
    }

    private void AddResult(string scenePath, Severity severity, string message, UnityEngine.Object context, string objectPath)
    {
        results.Add(new ValidationResult
        {
            ScenePath = scenePath,
            SeverityLevel = severity,
            Message = message,
            Context = context,
            ObjectPath = objectPath
        });
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && string.Equals(child.name, targetName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static Canvas FindChildCanvas(Transform root, string targetName)
    {
        Transform child = FindChildRecursive(root, targetName);
        if (child == null)
            return null;

        return child.GetComponent<Canvas>() ?? child.GetComponentInChildren<Canvas>(true);
    }

    private static T FindComponentByNamesInChildren<T>(Transform root, params string[] names) where T : Component
    {
        if (root == null || names == null)
            return null;

        foreach (string name in names)
        {
            Transform child = FindChildRecursive(root, name);
            if (child == null)
                continue;

            T component = child.GetComponent<T>() ?? child.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static void MoveUniqueComponentUnderRoot<T>(Scene scene, Transform newParent) where T : Component
    {
        T component = FindUniqueSceneObject<T>(scene, includeInactive: true);
        if (component == null || component.transform.parent == newParent)
            return;

        Undo.SetTransformParent(component.transform, newParent, $"Move {typeof(T).Name} Under Services");
    }

    private static void EnsureGlobalUIRootInstance(Scene scene)
    {
        GlobalUIRoot[] activeRoots = FindSceneObjects<GlobalUIRoot>(scene, includeInactive: false);
        if (activeRoots.Length > 0)
            return;

        GlobalUIRoot[] inactiveRoots = FindSceneObjects<GlobalUIRoot>(scene, includeInactive: true);
        GlobalUIRoot existingRoot = inactiveRoots.FirstOrDefault();
        if (existingRoot != null)
        {
            Undo.RecordObject(existingRoot.gameObject, "Reactivate GlobalUIRoot");
            existingRoot.gameObject.SetActive(true);
            EditorUtility.SetDirty(existingRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUiRootPrefabPath);
        if (prefab == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;

        SceneManager.MoveGameObjectToScene(instance, scene);
        instance.name = prefab.name;
        instance.SetActive(true);
        Undo.RegisterCreatedObjectUndo(instance, "Create GlobalUIRoot");
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void EnsureMouseCursorAuthoredPresentation(Transform root)
    {
        MouseCursorService cursorService = root.GetComponentInChildren<MouseCursorService>(true);
        if (cursorService == null)
        {
            Transform servicesRoot = FindChildRecursive(root, "Services") ?? root;
            GameObject serviceObject = new GameObject(nameof(MouseCursorService), typeof(MouseCursorService));
            serviceObject.transform.SetParent(servicesRoot, false);
            cursorService = serviceObject.GetComponent<MouseCursorService>();
        }

        Transform canvasTransform = cursorService.transform.Find("MouseCursorCanvas");
        if (canvasTransform == null)
        {
            GameObject canvasObject = new GameObject("MouseCursorCanvas", typeof(RectTransform), typeof(Canvas));
            canvasTransform = canvasObject.transform;
            canvasTransform.SetParent(cursorService.transform, false);
        }

        RectTransform canvasRect = canvasTransform as RectTransform;
        ConfigureFullScreenRect(canvasRect);

        Canvas canvas = GetOrAddComponent<Canvas>(canvasTransform.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        Transform imageTransform = canvasTransform.Find("CursorImage");
        if (imageTransform == null)
        {
            GameObject imageObject = new GameObject("CursorImage", typeof(RectTransform), typeof(Image));
            imageTransform = imageObject.transform;
            imageTransform.SetParent(canvasTransform, false);
        }

        RectTransform imageRect = imageTransform as RectTransform;
        if (imageRect != null)
        {
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.zero;
            imageRect.anchoredPosition = Vector2.zero;
        }

        Image cursorImage = GetOrAddComponent<Image>(imageTransform.gameObject);
        cursorImage.raycastTarget = false;
        cursorImage.enabled = false;

        SerializedObject serializedCursor = new SerializedObject(cursorService);
        AssignSerializedReference(serializedCursor, "authoredCursorCanvas", canvas);
        AssignSerializedReference(serializedCursor, "authoredCursorRect", imageRect);
        AssignSerializedReference(serializedCursor, "authoredCursorImage", cursorImage);
        serializedCursor.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cursorService);
    }

    private static void ConfigureFullScreenRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void DisableLegacyUiRoots(Scene scene)
    {
        GlobalUIRoot root = FindUniqueSceneObject<GlobalUIRoot>(scene, includeInactive: false);
        if (root == null)
            return;

        string[] legacyRootNames =
        {
            "TextCanvas",
            "NPCFeatureCanvas",
            "UIRoot"
        };

        foreach (Transform transform in FindSceneObjects<Transform>(scene, includeInactive: false))
        {
            if (transform == null || !legacyRootNames.Contains(transform.name, StringComparer.Ordinal))
                continue;

            if (transform == root.transform || transform.IsChildOf(root.transform))
                continue;

            Undo.RecordObject(transform.gameObject, "Disable Legacy UI Root");
            transform.gameObject.SetActive(false);
            EditorUtility.SetDirty(transform.gameObject);
        }
    }

    private static void DisableDuplicateGlobalComponentsOutsideRoot(Scene scene)
    {
        GlobalUIRoot root = FindUniqueSceneObject<GlobalUIRoot>(scene, includeInactive: false);
        if (root == null)
            return;

        DisableDuplicateComponentsOutsideRoot<UIManager>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<HoverUIController>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<EventSystem>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<DialogueView>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<AffectionUI>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<ChestUIManager>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<UpgradeTreeUI>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<RewardDisplayUI>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<ItemDetailPanel>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<DamagePopupService>(scene, root.transform);
        DisableDuplicateComponentsOutsideRoot<UpgradeManager>(scene, root.transform);
    }

    private static void CleanupDuplicateGlobalComponentsOutsideRoot(Scene scene)
    {
        GlobalUIRoot root = FindUniqueSceneObject<GlobalUIRoot>(scene, includeInactive: false);
        if (root == null)
            return;

        CleanupDuplicateComponentsOutsideRoot<UIManager>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<HoverUIController>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<EventSystem>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<DialogueView>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<AffectionUI>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<ChestUIManager>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<UpgradeTreeUI>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<RewardDisplayUI>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<ItemDetailPanel>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<DamagePopupService>(scene, root.transform);
        CleanupDuplicateComponentsOutsideRoot<UpgradeManager>(scene, root.transform);
    }

    private static void DisableDuplicateComponentsOutsideRoot<T>(Scene scene, Transform rootTransform) where T : Component
    {
        T[] activeComponents = FindSceneObjects<T>(scene, includeInactive: false);
        if (activeComponents.Length <= 1)
            return;

        T keptComponent = activeComponents.FirstOrDefault(component => component != null && component.transform.IsChildOf(rootTransform));
        if (keptComponent == null)
            return;

        foreach (T component in activeComponents)
        {
            if (component == null || component == keptComponent)
                continue;

            if (component.transform.IsChildOf(rootTransform))
                continue;

            if (component is Behaviour behaviour && HasOtherImportantBehaviours(component))
            {
                Undo.RecordObject(behaviour, $"Disable Duplicate {typeof(T).Name} Component");
                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
                continue;
            }

            Undo.RecordObject(component.gameObject, $"Disable Duplicate {typeof(T).Name}");
            component.gameObject.SetActive(false);
            EditorUtility.SetDirty(component.gameObject);
        }
    }

    private static void CleanupDuplicateComponentsOutsideRoot<T>(Scene scene, Transform rootTransform) where T : Component
    {
        T[] components = FindSceneObjects<T>(scene, includeInactive: true);
        if (components.Length <= 1)
            return;

        T keptComponent = components.FirstOrDefault(component =>
            component != null &&
            component.transform.IsChildOf(rootTransform) &&
            IsActiveInHierarchy(component));

        if (keptComponent == null)
            return;

        foreach (T component in components)
        {
            if (component == null || component == keptComponent)
                continue;

            if (component.transform.IsChildOf(rootTransform))
                continue;

            bool shouldCleanup = component switch
            {
                Behaviour behaviour => !behaviour.isActiveAndEnabled,
                _ => !IsActiveInHierarchy(component)
            };

            if (!shouldCleanup)
                continue;

            if (component is Behaviour && HasOtherImportantBehaviours(component))
            {
                Undo.DestroyObjectImmediate(component);
                continue;
            }

            Undo.DestroyObjectImmediate(component.gameObject);
        }
    }

    private static bool HasOtherImportantBehaviours(Component component)
    {
        Behaviour[] behaviours = component.GetComponents<Behaviour>();
        return behaviours.Any(other => other != null && other != component);
    }

    private static void CleanupLegacyUiRoots(Scene scene)
    {
        string[] legacyRootNames =
        {
            "TextCanvas",
            "NPCFeatureCanvas",
            "UIRoot"
        };

        foreach (Transform transform in FindSceneObjects<Transform>(scene, includeInactive: true))
        {
            if (transform == null || !legacyRootNames.Contains(transform.name, StringComparer.Ordinal))
                continue;

            if (transform.gameObject.activeSelf || transform.gameObject.activeInHierarchy)
                continue;

            Undo.DestroyObjectImmediate(transform.gameObject);
        }
    }

    private static void FixChildRectTransform(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        Undo.RecordObject(rectTransform, "Fix RectTransform");
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private static string GetObjectPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}

public sealed class MerchantShopSlotPrefabMigrationWindow : EditorWindow
{
    private const string ShopSlotPrefabPath = "Assets/LeeJunMo/Prefab/Dialogue/ShopSlot.prefab";
    private const string AnchorRootName = "ShopSlotAnchors";

    private readonly List<SlotCandidate> slotCandidates = new List<SlotCandidate>();
    private MerchantNPC selectedMerchant;
    private ShopSlot shopSlotPrefab;
    private Vector2 scrollPosition;

    private sealed class SlotCandidate
    {
        public ShopSlot Slot;
        public ShopSlotItemFilter Filter;
    }

    [MenuItem("Tools/Merchant/ShopSlot Prefab Migration")]
    public static void ShowWindow()
    {
        GetWindow<MerchantShopSlotPrefabMigrationWindow>("ShopSlot Migration");
    }

    private void OnEnable()
    {
        shopSlotPrefab = AssetDatabase.LoadAssetAtPath<ShopSlot>(ShopSlotPrefabPath);
        Selection.selectionChanged += HandleSelectionChanged;
        RefreshFromSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void OnGUI()
    {
        DrawSelectionFields();
        DrawCandidateList();
        DrawActions();
    }

    private void DrawSelectionFields()
    {
        EditorGUILayout.LabelField("Merchant ShopSlot Prefab Migration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this for scene review only. It creates empty layout anchors, wires MerchantNPC.slotPrefab/slotAnchors, and can delete copied scene ShopSlot objects through Undo.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        selectedMerchant = EditorGUILayout.ObjectField("Merchant", selectedMerchant, typeof(MerchantNPC), true) as MerchantNPC;
        shopSlotPrefab = EditorGUILayout.ObjectField("ShopSlot Prefab", shopSlotPrefab, typeof(ShopSlot), false) as ShopSlot;
        if (EditorGUI.EndChangeCheck())
            RefreshCandidates();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection"))
                RefreshFromSelection();

            if (GUILayout.Button("Refresh Slots"))
                RefreshCandidates();
        }

        if (shopSlotPrefab == null)
        {
            EditorGUILayout.HelpBox(
                $"ShopSlot prefab was not found at {ShopSlotPrefabPath}. Assign it before migration.",
                MessageType.Error);
        }
    }

    private void DrawCandidateList()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Copied ShopSlots", EditorStyles.boldLabel);

        if (selectedMerchant == null)
        {
            EditorGUILayout.HelpBox("Select or assign a MerchantNPC.", MessageType.Warning);
            return;
        }

        if (slotCandidates.Count == 0)
        {
            EditorGUILayout.HelpBox("No copied ShopSlot references were found on this merchant.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Weapon / Relic / Consumable Pattern"))
                ApplyDefaultFilterPattern();

            if (GUILayout.Button("Set All Any"))
                SetAllFilters(ShopSlotItemFilter.Any);
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(120f));
        for (int i = 0; i < slotCandidates.Count; i++)
        {
            SlotCandidate candidate = slotCandidates[i];
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField($"Slot {i}", candidate.Slot, typeof(ShopSlot), true);
                candidate.Filter = (ShopSlotItemFilter)EditorGUILayout.EnumPopup(candidate.Filter, GUILayout.Width(120f));

                if (candidate.Slot != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
                    EditorGUIUtility.PingObject(candidate.Slot);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(!CanMigrate()))
        {
            if (GUILayout.Button("Create Anchors And Replace Copied Slots"))
                MigrateSelectedMerchant(true);

            if (GUILayout.Button("Create Anchors Only"))
                MigrateSelectedMerchant(false);
        }

        EditorGUILayout.HelpBox(
            "Create Anchors Only is for inspection. If copied ShopSlots remain active in the scene, play mode can show duplicate slots after prefab instantiation.",
            MessageType.Warning);
    }

    private void HandleSelectionChanged()
    {
        MerchantNPC merchant = FindMerchantInSelection();
        if (merchant == selectedMerchant)
            return;

        selectedMerchant = merchant;
        RefreshCandidates();
        Repaint();
    }

    private void RefreshFromSelection()
    {
        selectedMerchant = FindMerchantInSelection();
        RefreshCandidates();
    }

    private static MerchantNPC FindMerchantInSelection()
    {
        GameObject activeGameObject = Selection.activeGameObject;
        return activeGameObject != null
            ? activeGameObject.GetComponentInParent<MerchantNPC>(true)
            : null;
    }

    private void RefreshCandidates()
    {
        slotCandidates.Clear();
        if (selectedMerchant == null)
            return;

        List<ShopSlot> slots = ReadAuthoredShopSlots(selectedMerchant);
        for (int i = 0; i < slots.Count; i++)
        {
            ShopSlot slot = slots[i];
            if (slot == null)
                continue;

            slotCandidates.Add(new SlotCandidate
            {
                Slot = slot,
                Filter = ReadSlotFilter(slot)
            });
        }
    }

    private static List<ShopSlot> ReadAuthoredShopSlots(MerchantNPC merchant)
    {
        List<ShopSlot> slots = new List<ShopSlot>();
        HashSet<ShopSlot> seen = new HashSet<ShopSlot>();

        SerializedObject serializedMerchant = new SerializedObject(merchant);
        SerializedProperty shopSlotsProperty = serializedMerchant.FindProperty("shopSlots");
        if (shopSlotsProperty != null && shopSlotsProperty.isArray)
        {
            for (int i = 0; i < shopSlotsProperty.arraySize; i++)
            {
                ShopSlot slot = shopSlotsProperty.GetArrayElementAtIndex(i).objectReferenceValue as ShopSlot;
                if (slot != null && seen.Add(slot))
                    slots.Add(slot);
            }
        }

        if (slots.Count > 0)
            return slots;

        ShopSlot[] childSlots = merchant.GetComponentsInChildren<ShopSlot>(true);
        for (int i = 0; i < childSlots.Length; i++)
        {
            ShopSlot slot = childSlots[i];
            if (slot != null && seen.Add(slot))
                slots.Add(slot);
        }

        return slots;
    }

    private static ShopSlotItemFilter ReadSlotFilter(ShopSlot slot)
    {
        SerializedObject serializedSlot = new SerializedObject(slot);
        SerializedProperty filterProperty = serializedSlot.FindProperty("itemFilter");
        return filterProperty != null
            ? (ShopSlotItemFilter)filterProperty.enumValueIndex
            : ShopSlotItemFilter.Any;
    }

    private bool CanMigrate()
    {
        return selectedMerchant != null && shopSlotPrefab != null && slotCandidates.Count > 0;
    }

    private void ApplyDefaultFilterPattern()
    {
        for (int i = 0; i < slotCandidates.Count; i++)
            slotCandidates[i].Filter = ResolveDefaultFilter(i);
    }

    private static ShopSlotItemFilter ResolveDefaultFilter(int index)
    {
        switch (index)
        {
            case 0:
                return ShopSlotItemFilter.Weapon;
            case 1:
                return ShopSlotItemFilter.Relic;
            case 2:
                return ShopSlotItemFilter.Consumable;
            default:
                return ShopSlotItemFilter.Any;
        }
    }

    private void SetAllFilters(ShopSlotItemFilter filter)
    {
        for (int i = 0; i < slotCandidates.Count; i++)
            slotCandidates[i].Filter = filter;
    }

    private void MigrateSelectedMerchant(bool replaceCopiedSlots)
    {
        if (!CanMigrate())
            return;

        if (replaceCopiedSlots && !EditorUtility.DisplayDialog(
                "Replace Copied ShopSlots",
                "This creates empty anchors, wires the merchant to the ShopSlot prefab, and deletes the copied scene ShopSlot objects. The operation is undoable. Continue?",
                "Replace",
                "Cancel"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Migrate Merchant ShopSlots To Prefab");

        Transform anchorRoot = FindOrCreateAnchorRoot(selectedMerchant, slotCandidates);
        List<Transform> anchors = CreateAnchors(anchorRoot, slotCandidates);
        List<ShopSlotItemFilter> filters = CaptureFilters(slotCandidates);

        if (replaceCopiedSlots)
            DestroyCopiedSlots(slotCandidates);

        ApplyMerchantAuthoring(selectedMerchant, shopSlotPrefab, anchors, filters);
        MarkSceneDirty(selectedMerchant);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeObject = selectedMerchant;
        RefreshCandidates();
        Repaint();
    }

    private static Transform FindOrCreateAnchorRoot(MerchantNPC merchant, List<SlotCandidate> candidates)
    {
        Transform parent = ResolveCommonSlotParent(merchant.transform, candidates);
        Transform existingRoot = parent.Find(AnchorRootName);
        if (existingRoot != null)
            return existingRoot;

        GameObject rootObject = new GameObject(AnchorRootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Create ShopSlot Anchor Root");
        Transform root = rootObject.transform;
        root.SetParent(parent, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        return root;
    }

    private static Transform ResolveCommonSlotParent(Transform fallbackParent, List<SlotCandidate> candidates)
    {
        Transform commonParent = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            ShopSlot slot = candidates[i].Slot;
            if (slot == null || slot.transform.parent == null)
                continue;

            if (commonParent == null)
            {
                commonParent = slot.transform.parent;
                continue;
            }

            if (commonParent != slot.transform.parent)
                return fallbackParent;
        }

        return commonParent != null ? commonParent : fallbackParent;
    }

    private static List<Transform> CreateAnchors(Transform anchorRoot, List<SlotCandidate> candidates)
    {
        List<Transform> anchors = new List<Transform>();
        for (int i = 0; i < candidates.Count; i++)
        {
            ShopSlot slot = candidates[i].Slot;
            if (slot == null)
                continue;

            GameObject anchorObject = new GameObject(BuildUniqueChildName(anchorRoot, $"{slot.name}_Anchor"));
            Undo.RegisterCreatedObjectUndo(anchorObject, "Create ShopSlot Anchor");

            Transform anchor = anchorObject.transform;
            anchor.SetParent(anchorRoot, false);
            anchor.SetPositionAndRotation(slot.transform.position, slot.transform.rotation);
            anchor.localScale = slot.transform.localScale;
            anchors.Add(anchor);
        }

        return anchors;
    }

    private static string BuildUniqueChildName(Transform parent, string baseName)
    {
        if (parent.Find(baseName) == null)
            return baseName;

        int index = 1;
        while (parent.Find($"{baseName}_{index}") != null)
            index++;

        return $"{baseName}_{index}";
    }

    private static List<ShopSlotItemFilter> CaptureFilters(List<SlotCandidate> candidates)
    {
        List<ShopSlotItemFilter> filters = new List<ShopSlotItemFilter>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Slot != null)
                filters.Add(candidates[i].Filter);
        }

        return filters;
    }

    private static void DestroyCopiedSlots(List<SlotCandidate> candidates)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            ShopSlot slot = candidates[i].Slot;
            if (slot != null)
                Undo.DestroyObjectImmediate(slot.gameObject);
        }
    }

    private static void ApplyMerchantAuthoring(
        MerchantNPC merchant,
        ShopSlot prefab,
        List<Transform> anchors,
        List<ShopSlotItemFilter> filters)
    {
        SerializedObject serializedMerchant = new SerializedObject(merchant);
        serializedMerchant.Update();

        SerializedProperty prefabProperty = serializedMerchant.FindProperty("slotPrefab");
        if (prefabProperty != null)
            prefabProperty.objectReferenceValue = prefab;

        SerializedProperty anchorsProperty = serializedMerchant.FindProperty("slotAnchors");
        if (anchorsProperty != null && anchorsProperty.isArray)
        {
            anchorsProperty.arraySize = anchors.Count;
            for (int i = 0; i < anchors.Count; i++)
            {
                SerializedProperty anchorElement = anchorsProperty.GetArrayElementAtIndex(i);
                SerializedProperty anchorProperty = anchorElement.FindPropertyRelative("anchor");
                SerializedProperty filterProperty = anchorElement.FindPropertyRelative("itemFilter");

                if (anchorProperty != null)
                    anchorProperty.objectReferenceValue = anchors[i];

                if (filterProperty != null)
                    filterProperty.enumValueIndex = (int)filters[i];
            }
        }

        SerializedProperty shopSlotsProperty = serializedMerchant.FindProperty("shopSlots");
        if (shopSlotsProperty != null && shopSlotsProperty.isArray)
            shopSlotsProperty.arraySize = 0;

        serializedMerchant.ApplyModifiedProperties();
        EditorUtility.SetDirty(merchant);
    }

    private static void MarkSceneDirty(MerchantNPC merchant)
    {
        Scene scene = merchant.gameObject.scene;
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}
