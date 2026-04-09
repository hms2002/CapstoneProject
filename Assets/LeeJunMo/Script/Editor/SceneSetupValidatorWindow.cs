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
        UpgradeTreeUI[] trees = FindSceneObjects<UpgradeTreeUI>(scene, includeInactive: false);
        foreach (UpgradeTreeUI tree in trees)
        {
            SerializedObject serializedTree = new SerializedObject(tree);

            ValidateSerializedReference(scene.path, tree, serializedTree, "contentRect", "UpgradeTreeUI.contentRect is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "slotParent", "UpgradeTreeUI.slotParent is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "lineParent", "UpgradeTreeUI.lineParent is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "slotPrefab", "UpgradeTreeUI.slotPrefab is missing.");
            ValidateSerializedReference(scene.path, tree, serializedTree, "linePrefab", "UpgradeTreeUI.linePrefab is missing.");

            RectTransform contentRect = GetSerializedObjectReference<RectTransform>(serializedTree, "contentRect");
            Transform slotParent = GetSerializedObjectReference<Transform>(serializedTree, "slotParent");
            Transform lineParent = GetSerializedObjectReference<Transform>(serializedTree, "lineParent");

            ScrollRect scrollRect = tree.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                AddResult(scene.path, Severity.Error, "UpgradeTreeUI is missing ScrollRect.", tree, GetObjectPath(tree.transform));
                continue;
            }

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

    private void AutoFixUpgradeTrees(Scene scene)
    {
        UpgradeTreeUI[] trees = FindSceneObjects<UpgradeTreeUI>(scene, includeInactive: false);
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
