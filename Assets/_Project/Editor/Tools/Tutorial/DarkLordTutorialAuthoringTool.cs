#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityGAS;
using Object = UnityEngine.Object;

public static class DarkLordTutorialAuthoringTool
{
    private const string TargetSceneName = "DarkLord_Tutorial";
    private const string RootName = "__TutorialBossPresentation";
    private const string DarkLordNpcDataPath = "Assets/_Project/Data/Dialogue/NPC/DarkLordNpcData.asset";
    private const string Script1JsonPath = "Assets/_Project/Data/Dialogue/Ink/TutorialDarkLordScript1.json";
    private const string Script2JsonPath = "Assets/_Project/Data/Dialogue/Ink/TutorialDarkLordScript2.json";
    private const string LaserVfxPrefabPath = "Assets/_Project/Resources/DemonKing/DemonKingEgoLaserVfx.prefab";
    private const string GlobalUiRootPrefabPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    private const string HeartTokenPrefabPath = "Assets/_Project/Prefabs/UI/HUD/HeartTokenUI.prefab";
    private const int PresentationHpSlots = 3;
    private const int PresentationHpSortingOrder = short.MaxValue;
    private static readonly string[] DefaultHudRootNames =
    {
        "GameplayHUDCanvas",
        "BossHUDCanvas"
    };

    private static readonly Type[] DefaultHudComponentTypes =
    {
        typeof(PlayerHealthHeartHUD),
        typeof(WeaponSkillHUD2D),
        typeof(PlayerConsumableHUD2D),
        typeof(StatusHudPresenter),
        typeof(BossHealthBarUI)
    };

    private static readonly Type[] DemonKingRuntimeComponentsToRemove =
    {
        typeof(BossDialogueRunner),
        typeof(BossEncounterDirector),
        typeof(DemonKingController),
        typeof(BossDeathPresentation),
        typeof(BossBattleEndHandler),
        typeof(BossEncounterEndDirector),
        typeof(AbilitySystem),
        typeof(GameplayEffectRunner),
        typeof(AttributeSet),
        typeof(TagSystem),
        typeof(AttributeStatSource),
        typeof(MovementMotor2D),
        typeof(AbilityMotionController2D),
        typeof(ExternalMovementController2D),
        typeof(KnockbackReceiver2D),
        typeof(CombatHurtbox2D),
        typeof(AttackTelegraphService),
        typeof(ElementGaugeSystem),
        typeof(StaggerGaugeSystem),
        typeof(DamagePopupSpawner2D),
        typeof(DamagePopupListener2D),
        typeof(MonsterHitFeedback2D),
        typeof(SpriteHitFlashController),
        typeof(SpeechBubbleComponent),
        typeof(BossSpeechController),
        typeof(Collider2D),
        typeof(Rigidbody2D)
    };

    [MenuItem("Tools/Tutorial/DarkLord Tutorial/Apply Default Authoring To Active Scene")]
    public static void ApplyDefaultAuthoringToActiveScene()
    {
        if (!CanEditActiveScene(out Scene scene))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply DarkLord tutorial authoring");

        Transform root = GetOrCreateRoot(scene);
        Transform demonKing = FindTransformByName(scene, "DemonKing");
        Transform playerSpawn = FindTransformByName(scene, "Spawn_Default") ??
                                FindTransformByName(scene, "PlayerSpawner") ??
                                FindTransformByName(scene, "CenterPoint");

        Vector3 bossFocusPosition = ResolveBossFocusPosition(demonKing);
        Vector3 playerFocusPosition = ResolvePlayerFocusPosition(playerSpawn);
        Vector3 initialAimTargetPosition = ResolveInitialAimTargetPosition(playerFocusPosition, bossFocusPosition);

        Transform bossFocusTarget = GetOrCreateChild(root, "BossFocusTarget", bossFocusPosition);
        Transform playerFocusTarget = GetOrCreateChild(root, "PlayerFocusTarget", playerFocusPosition);
        Transform initialAimTarget = GetOrCreateChild(root, "InitialAimTarget", initialAimTargetPosition);
        Transform leftLaserOrigin = GetOrCreateChild(root, "LaserOrigin_LeftDiagonal");
        Transform rightLaserOrigin = GetOrCreateChild(root, "LaserOrigin_RightDiagonal");
        Transform centerLaserOrigin = GetOrCreateChild(root, "LaserOrigin_Center");
        ConfigureLaserOrigins(playerFocusPosition, leftLaserOrigin, rightLaserOrigin, centerLaserOrigin);

        TutorialBossEncounterSequence sequence = GetOrAdd<TutorialBossEncounterSequence>(root.gameObject);
        TutorialBossLaserPresentation laserPresentation = GetOrAdd<TutorialBossLaserPresentation>(root.gameObject);
        TutorialPresentationHpView hpView = GetOrCreatePresentationHpView(root, scene);

        NPCData npcData = AssetDatabase.LoadAssetAtPath<NPCData>(DarkLordNpcDataPath);
        TextAsset firstDialogue = AssetDatabase.LoadAssetAtPath<TextAsset>(Script1JsonPath);
        TextAsset secondDialogue = AssetDatabase.LoadAssetAtPath<TextAsset>(Script2JsonPath);
        DemonKingEgoLaserVfx laserPrefab = LoadLaserVfxPrefab();

        ConfigureHpView(hpView);
        ConfigureLaserPresentation(
            laserPresentation,
            hpView,
            laserPrefab,
            leftLaserOrigin,
            rightLaserOrigin,
            centerLaserOrigin);
        ConfigureEncounterSequence(
            sequence,
            bossFocusTarget,
            playerFocusTarget,
            initialAimTarget,
            playerFocusPosition,
            demonKing,
            npcData,
            firstDialogue,
            secondDialogue,
            laserPresentation,
            hpView);
        DisableConflictingBossRuntime(scene);
        DeactivateDefaultHudRoots(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        if (firstDialogue == null || secondDialogue == null)
        {
            Debug.LogWarning(
                $"[DarkLordTutorialAuthoring] Ink JSON is missing. Let Unity import/compile the new .ink files, then run validation. Missing: " +
                $"{(firstDialogue == null ? Script1JsonPath : string.Empty)} " +
                $"{(secondDialogue == null ? Script2JsonPath : string.Empty)}");
        }

        Debug.Log("[DarkLordTutorialAuthoring] Applied default tutorial boss presentation authoring to the active scene.");
        ValidateActiveScene();
    }

    [MenuItem("Tools/Tutorial/DarkLord Tutorial/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        var report = new ValidationReport("DarkLord_Tutorial authoring validation");

        if (!scene.IsValid() || !scene.isLoaded)
        {
            report.Error("Active scene is not loaded.");
            report.Log();
            return;
        }

        if (!string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
            report.Error($"Active scene must be {TargetSceneName}; current scene is {scene.name}.");

        TutorialBossEncounterSequence sequence = FindSceneComponent<TutorialBossEncounterSequence>(scene);
        TutorialBossLaserPresentation laserPresentation = FindSceneComponent<TutorialBossLaserPresentation>(scene);
        TutorialPresentationHpView hpView = FindSceneComponent<TutorialPresentationHpView>(scene);

        if (sequence == null)
        {
            report.Error("TutorialBossEncounterSequence is missing.");
        }
        else
        {
            SerializedObject sequenceSo = new(sequence);
            RequireReference(report, sequenceSo, "bossFocusTarget", "Boss focus target");
            RequireReference(report, sequenceSo, "playerFocusTarget", "Player focus target");
            RequireReference(report, sequenceSo, "initialAimTarget", "Initial aim target");
            RequireReference(report, sequenceSo, "tutorialBossNpcData", "Tutorial boss NPCData");
            RequireReference(report, sequenceSo, "laserPresentation", "Laser presentation");
            RequireReference(report, sequenceSo, "presentationHpView", "Presentation HP view");
            RequireReference(report, sequenceSo, "firstDialogueInk", "Script 1 Ink JSON");
            RequireReference(report, sequenceSo, "secondDialogueInk", "Script 2 Ink JSON");
            RequireBool(report, sequenceSo, "applyInitialAimOnSequenceStart", true, "TutorialBossEncounterSequence.applyInitialAimOnSequenceStart");
            RequireBool(report, sequenceSo, "lockPlayerControls", true, "TutorialBossEncounterSequence.lockPlayerControls");
            RequireBool(report, sequenceSo, "blockPlayerTargetability", true, "TutorialBossEncounterSequence.blockPlayerTargetability");
            RequireBool(report, sequenceSo, "keepPlayerLockedAfterSequence", true, "TutorialBossEncounterSequence.keepPlayerLockedAfterSequence");
            RequireBool(report, sequenceSo, "hideDefaultHudDuringSequence", true, "TutorialBossEncounterSequence.hideDefaultHudDuringSequence");
            RequireBool(report, sequenceSo, "useCameraPresentationDirector", false, "TutorialBossEncounterSequence.useCameraPresentationDirector");
            RequireBool(report, sequenceSo, "focusPlayerBeforeFirstBossFocus", true, "TutorialBossEncounterSequence.focusPlayerBeforeFirstBossFocus");
            RequireBool(report, sequenceSo, "waitForSceneTransitionBeforeInitialPlayerFocus", true, "TutorialBossEncounterSequence.waitForSceneTransitionBeforeInitialPlayerFocus");
            RequireFloatAtLeast(report, sequenceSo, "initialPlayerFocusWaitSeconds", 0.75f, "TutorialBossEncounterSequence.initialPlayerFocusWaitSeconds");
            RequireBool(report, sequenceSo, "showFakeGameOver", true, "TutorialBossEncounterSequence.showFakeGameOver");
            RequireString(report, sequenceSo, "fakeGameOverCauseName", "마왕", "TutorialBossEncounterSequence.fakeGameOverCauseName");
            RequireString(report, sequenceSo, "fakeGameOverMessageText", "처치자 마왕", "TutorialBossEncounterSequence.fakeGameOverMessageText");
            RequireString(report, sequenceSo, "fakeGameOverLocationName", "마왕의 알현실", "TutorialBossEncounterSequence.fakeGameOverLocationName");
            RequireBool(report, sequenceSo, "hideFakeGameOverTimeText", true, "TutorialBossEncounterSequence.hideFakeGameOverTimeText");
            RequireString(report, sequenceSo, "fakeGameOverButtonLabel", "추락", "TutorialBossEncounterSequence.fakeGameOverButtonLabel");
        }

        if (laserPresentation == null)
        {
            report.Error("TutorialBossLaserPresentation is missing.");
        }
        else
        {
            SerializedObject laserSo = new(laserPresentation);
            RequireReference(report, laserSo, "presentationHpView", "Laser presentation HP view");
            SerializedProperty steps = laserSo.FindProperty("steps");
            if (steps == null || !steps.isArray || steps.arraySize < 3)
                report.Error("Laser presentation must have at least 3 steps.");
        }

        if (hpView == null)
        {
            report.Error("TutorialPresentationHpView is missing.");
        }
        else
        {
            SerializedObject hpSo = new(hpView);
            RequireInt(report, hpSo, "maxHp", PresentationHpSlots, "TutorialPresentationHpView.maxHp");
            RequireObjectArrayMinSize(
                report,
                hpSo,
                "heartSlots",
                PresentationHpSlots,
                "Tutorial presentation HP heart slots");
            RequireReference(report, hpSo, "filledHeartSprite", "Tutorial presentation HP filled heart sprite");
            RequireReference(report, hpSo, "emptyHeartSprite", "Tutorial presentation HP empty heart sprite");
            RequireReference(report, hpSo, "visibilityGroup", "Tutorial presentation HP visibility CanvasGroup");
            RequireBool(report, hpSo, "hideOnEnable", true, "TutorialPresentationHpView.hideOnEnable");
            RequireHpLayoutMatchesHud(report, hpView, scene);
            RequirePresentationHpCanvasUsable(report, hpView);
        }

        if (FindSceneComponent<GameOverPresentationController>(scene) == null)
            report.Error("GameOverPresentationController was not found in the active scene.");

        RequireNoSceneComponent<BossDialogueRunner>(report, scene, "BossDialogueRunner");
        RequireNoSceneComponent<BossEncounterDirector>(report, scene, "BossEncounterDirector");
        RequireDisabled(report, FindSceneComponent<BossBattleEndHandler>(scene), "BossBattleEndHandler");
        RequireDisabled(report, FindSceneComponent<BossEncounterEndDirector>(scene), "BossEncounterEndDirector");
        RequireNoUnneededDemonKingRuntimeComponents(report, scene);
        RequirePlayerPrefabPresentationComponents(report, scene);
        RequireDefaultHudInactive(report, scene);

        if (AssetDatabase.LoadAssetAtPath<TextAsset>(Script1JsonPath) == null)
            report.Warning($"Script 1 compiled Ink JSON is missing: {Script1JsonPath}");

        if (AssetDatabase.LoadAssetAtPath<TextAsset>(Script2JsonPath) == null)
            report.Warning($"Script 2 compiled Ink JSON is missing: {Script2JsonPath}");

        report.Log();
    }

    [MenuItem("Tools/Tutorial/DarkLord Tutorial/Apply Default Authoring To Active Scene", true)]
    private static bool CanApplyDefaultAuthoringToActiveScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/Tutorial/DarkLord Tutorial/Validate Active Scene", true)]
    private static bool CanValidateActiveScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool CanEditActiveScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[DarkLordTutorialAuthoring] Cannot edit tutorial authoring while Play Mode is active or changing.");
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[DarkLordTutorialAuthoring] Active scene is not loaded.");
            return false;
        }

        if (!string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
        {
            Debug.LogError($"[DarkLordTutorialAuthoring] Active scene must be {TargetSceneName}; current scene is {scene.name}.");
            return false;
        }

        return true;
    }

    private static Transform GetOrCreateRoot(Scene scene)
    {
        Transform root = FindTransformByName(scene, RootName);
        if (root != null)
            return root;

        GameObject rootObject = new(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Create tutorial boss presentation root");
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        rootObject.transform.position = Vector3.zero;
        return rootObject.transform;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        return GetOrCreateChild(parent, name, parent != null ? parent.position : Vector3.zero);
    }

    private static Transform GetOrCreateChild(Transform parent, string name, Vector3 worldPosition)
    {
        Transform child = FindDirectChild(parent, name);
        if (child == null)
        {
            GameObject childObject = new(name);
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {name}");
            child = childObject.transform;
            child.SetParent(parent, worldPositionStays: false);
        }

        Undo.RecordObject(child, $"Position {name}");
        child.position = worldPosition;
        return child;
    }

    private static TutorialPresentationHpView GetOrCreatePresentationHpView(Transform root, Scene scene)
    {
        Transform canvasTransform = FindDirectChild(root, "TutorialPresentationHpCanvas");
        if (canvasTransform == null)
        {
            GameObject canvasObject = new("TutorialPresentationHpCanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create tutorial HP canvas");
            canvasTransform = canvasObject.transform;
            canvasTransform.SetParent(root, worldPositionStays: false);
        }

        SetGameObjectActive(canvasTransform.gameObject, true, "Enable tutorial HP canvas");
        ConfigurePresentationCanvasRect(canvasTransform as RectTransform);

        Canvas canvas = GetOrAdd<Canvas>(canvasTransform.gameObject);
        ConfigurePresentationHpCanvas(canvas);

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasTransform.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasTransform.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            Object.DestroyImmediate(raycaster);

        Transform hpRoot = FindDirectChild(canvasTransform, "TutorialHpRoot");
        if (hpRoot == null)
        {
            GameObject hpRootObject = new("TutorialHpRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(hpRootObject, "Create tutorial HP root");
            hpRoot = hpRootObject.transform;
            hpRoot.SetParent(canvasTransform, worldPositionStays: false);
        }

        PlayerHealthHeartHUD hudTemplate = ResolvePlayerHealthHudTemplate(scene);

        RectTransform hpRootRect = (RectTransform)hpRoot;
        ConfigureRectTransformLikeHud(hpRootRect, hudTemplate != null ? hudTemplate.GetComponent<RectTransform>() : null);

        TMP_Text hpText = hpRoot.GetComponent<TMP_Text>();
        if (hpText != null)
        {
            Undo.RecordObject(hpText, "Disable old tutorial HP text");
            hpText.enabled = false;
        }

        Transform heartContainer = FindDirectChild(hpRoot, "HeartContainer");
        if (heartContainer == null)
        {
            GameObject containerObject = new("HeartContainer", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(containerObject, "Create tutorial HP heart container");
            heartContainer = containerObject.transform;
            heartContainer.SetParent(hpRoot, worldPositionStays: false);
        }

        RectTransform heartContainerRect = (RectTransform)heartContainer;
        heartContainerRect.anchorMin = Vector2.zero;
        heartContainerRect.anchorMax = Vector2.one;
        heartContainerRect.pivot = new Vector2(0.5f, 0.5f);
        heartContainerRect.anchoredPosition = Vector2.zero;
        heartContainerRect.sizeDelta = Vector2.zero;
        heartContainerRect.localScale = Vector3.one;

        GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(heartContainer.gameObject);
        ConfigureHeartGrid(grid, hudTemplate != null ? hudTemplate.GetComponent<GridLayoutGroup>() : null);

        HeartTokenUI heartTokenPrefab = ResolveHeartTokenPrefab(hudTemplate);
        Sprite filledHeartSprite = ResolveHudSprite(hudTemplate, "filledHeartSprite") ??
                                   ResolveHeartTokenSprite(heartTokenPrefab, "filledHeartSprite");
        Sprite emptyHeartSprite = ResolveHudSprite(hudTemplate, "emptyHeartSprite") ??
                                  ResolveHeartTokenSprite(heartTokenPrefab, "emptyHeartSprite");
        Color heartTint = ResolveHudColor(hudTemplate, "normalHeartColor", Color.white);
        HeartTokenUI[] heartSlots = EnsureHeartSlots(
            heartContainer,
            heartTokenPrefab,
            filledHeartSprite,
            emptyHeartSprite,
            heartTint,
            PresentationHpSlots);

        TutorialPresentationHpView hpView = GetOrAdd<TutorialPresentationHpView>(hpRoot.gameObject);
        CanvasGroup visibilityGroup = GetOrAdd<CanvasGroup>(hpRoot.gameObject);
        visibilityGroup.alpha = 0f;
        visibilityGroup.interactable = false;
        visibilityGroup.blocksRaycasts = false;

        SerializedObject hpSo = new(hpView);
        SetInt(hpSo, "maxHp", PresentationHpSlots);
        SetInt(hpSo, "currentHp", PresentationHpSlots);
        SetBool(hpSo, "resetToMaxOnEnable", true);
        SetObject(hpSo, "visibilityGroup", visibilityGroup);
        SetObject(hpSo, "visibilityRoot", null);
        SetBool(hpSo, "hideOnEnable", true);
        SetObject(hpSo, "hpText", null);
        SetString(hpSo, "hpFormat", "{0}/{1}");
        SetObjectArray(hpSo, "filledSlotRoots", Array.Empty<Object>());
        SetObjectArray(hpSo, "emptySlotRoots", Array.Empty<Object>());
        SetObjectArray(hpSo, "heartSlots", heartSlots);
        SetObject(hpSo, "filledHeartSprite", filledHeartSprite);
        SetObject(hpSo, "emptyHeartSprite", emptyHeartSprite);
        SetColor(hpSo, "heartTint", heartTint);
        hpSo.ApplyModifiedProperties();
        hpView.Refresh();
        hpView.SetVisible(false);

        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(scaler);
        EditorUtility.SetDirty(grid);
        EditorUtility.SetDirty(visibilityGroup);
        EditorUtility.SetDirty(hpView);
        return hpView;
    }

    private static void ConfigureEncounterSequence(
        TutorialBossEncounterSequence sequence,
        Transform bossFocusTarget,
        Transform playerFocusTarget,
        Transform initialAimTarget,
        Vector3 playerFocusPosition,
        Transform demonKing,
        NPCData npcData,
        TextAsset firstDialogue,
        TextAsset secondDialogue,
        TutorialBossLaserPresentation laserPresentation,
        TutorialPresentationHpView hpView)
    {
        SerializedObject so = new(sequence);
        SetBool(so, "playOnStart", true);
        SetBool(so, "playOnlyOnce", true);
        SetBool(so, "lockPlayerControls", true);
        SetBool(so, "blockPlayerTargetability", true);
        SetBool(so, "keepPlayerLockedAfterSequence", true);
        SetBool(so, "pauseRunTimer", true);
        SetBool(so, "applyInitialAimOnSequenceStart", true);
        SetObject(so, "initialAimTarget", initialAimTarget);
        SetVector2(so, "fallbackInitialAimDirection", ResolveFallbackInitialAimDirection(playerFocusPosition, initialAimTarget));
        SetBool(so, "hideDefaultHudDuringSequence", true);
        SetObject(so, "cameraDirector", null);
        SetBool(so, "useCameraPresentationDirector", false);
        SetObject(so, "bossFocusTarget", bossFocusTarget);
        SetFloat(so, "cameraFocusWaitSeconds", 0.65f);
        SetFloat(so, "cameraReturnWaitSeconds", 0.45f);
        SetBool(so, "zoomGameplayCameraDuringFocus", true);
        SetFloat(so, "focusOrthographicSize", 4f);
        SetFloat(so, "cameraZoomInSeconds", 0.35f);
        SetFloat(so, "cameraZoomOutSeconds", 0.25f);
        SetBool(so, "focusPlayerBeforeFirstBossFocus", true);
        SetBool(so, "waitForSceneTransitionBeforeInitialPlayerFocus", true);
        SetFloat(so, "initialPlayerFocusWaitSeconds", 0.75f);
        SetBool(so, "focusPlayerBeforeLaser", true);
        SetObject(so, "playerFocusTarget", playerFocusTarget);
        SetFloat(so, "playerFocusWaitSeconds", 0.45f);
        SetFloat(so, "playerFocusOrthographicSize", 3.25f);
        SetBool(so, "refocusBossAfterLaser", true);
        SetFloat(so, "bossRefocusWaitSeconds", 0.45f);
        SetBool(so, "useLetterbox", true);
        SetBool(so, "useCustomFadedLayers", true);
        SetGlobalCanvasLayers(so, "fadedLayers", GlobalCanvasLayer.GameplayHUD, GlobalCanvasLayer.Popup, GlobalCanvasLayer.Hover, GlobalCanvasLayer.Prompt, GlobalCanvasLayer.Reward, GlobalCanvasLayer.DamagePopup, GlobalCanvasLayer.BossHUD);
        SetObject(so, "bossVisualRoot", demonKing);
        SetBool(so, "scaleBossVisualOnFocus", true);
        SetFloat(so, "bossFocusScaleMultiplier", 1.15f);
        SetObject(so, "tutorialBossNpcData", npcData);
        SetObject(so, "firstDialogueInk", firstDialogue);
        SetString(so, "firstDialogueStartPath", "script_1");
        SetObject(so, "secondDialogueInk", secondDialogue);
        SetString(so, "secondDialogueStartPath", "script_2");
        SetObject(so, "laserPresentation", laserPresentation);
        SetObject(so, "presentationHpView", hpView);
        SetFloat(so, "delayAfterFirstDialogueSeconds", 0.2f);
        SetFloat(so, "delayAfterLaserSeconds", 0.2f);
        SetFloat(so, "collapseDelaySeconds", 0.75f);
        SetFloat(so, "gameOverDelaySeconds", 0.25f);
        SetBool(so, "showFakeGameOver", true);
        SetString(so, "fakeGameOverCauseName", "마왕");
        SetString(so, "fakeGameOverMessageText", "처치자 마왕");
        SetString(so, "fakeGameOverLocationName", "마왕의 알현실");
        SetBool(so, "hideFakeGameOverTimeText", true);
        SetString(so, "fakeGameOverButtonLabel", "추락");
        SetString(so, "returnSceneName", "ProtoTypeHub");
        SetBool(so, "useSceneTransitionService", true);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sequence);
    }

    private static void ConfigureLaserPresentation(
        TutorialBossLaserPresentation laserPresentation,
        TutorialPresentationHpView hpView,
        DemonKingEgoLaserVfx laserPrefab,
        Transform leftLaserOrigin,
        Transform rightLaserOrigin,
        Transform centerLaserOrigin)
    {
        SerializedObject so = new(laserPresentation);
        SetObject(so, "defaultLaserVfxPrefab", laserPrefab);
        SetObject(so, "presentationHpView", hpView);
        SetBool(so, "reduceHpOnEachStep", true);

        SerializedProperty steps = so.FindProperty("steps");
        if (steps != null && steps.isArray)
        {
            steps.arraySize = 3;
            ConfigureLaserStep(steps.GetArrayElementAtIndex(0), leftLaserOrigin, new Vector2(1f, 1f), 15f, 0.75f, laserPrefab);
            ConfigureLaserStep(steps.GetArrayElementAtIndex(1), rightLaserOrigin, new Vector2(-1f, 1f), 15f, 0.75f, laserPrefab);
            ConfigureLaserStep(steps.GetArrayElementAtIndex(2), centerLaserOrigin, Vector2.up, 15f, 1.35f, laserPrefab);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(laserPresentation);
    }

    private static void ConfigureLaserStep(
        SerializedProperty step,
        Transform origin,
        Vector2 direction,
        float length,
        float width,
        DemonKingEgoLaserVfx laserPrefab)
    {
        if (step == null)
            return;

        SetRelativeObject(step, "origin", origin);
        SetRelativeVector2(step, "direction", direction);
        SetRelativeFloat(step, "length", length);
        SetRelativeFloat(step, "width", width);
        SetRelativeFloat(step, "warningSeconds", 0.55f);
        SetRelativeFloat(step, "attackSeconds", 0.45f);
        SetRelativeFloat(step, "postDelaySeconds", 0.12f);
        SetRelativeBool(step, "spawnOppositeRay", false);
        SetRelativeBool(step, "showPrimitiveWarning", true);
        SetRelativeObject(step, "laserVfxPrefab", laserPrefab);
        SetRelativeColor(step, "warningColor", new Color(1f, 0.1f, 0.1f, 0.35f));
        SetRelativeColor(step, "fallbackAttackColor", new Color(1f, 0.05f, 0.05f, 0.65f));
    }

    private static void ConfigureHpView(TutorialPresentationHpView hpView)
    {
        if (hpView == null)
            return;

        SerializedObject so = new(hpView);
        SetInt(so, "maxHp", PresentationHpSlots);
        SetInt(so, "currentHp", PresentationHpSlots);
        SetBool(so, "resetToMaxOnEnable", true);
        SetBool(so, "hideOnEnable", true);
        so.ApplyModifiedProperties();
        ConfigurePresentationHpParentCanvases(hpView);
        hpView.Refresh();
        hpView.SetVisible(false);
        EditorUtility.SetDirty(hpView);
    }

    private static PlayerHealthHeartHUD ResolvePlayerHealthHudTemplate(Scene scene)
    {
        PlayerHealthHeartHUD sceneHud = FindSceneComponent<PlayerHealthHeartHUD>(scene);
        if (sceneHud != null)
            return sceneHud;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUiRootPrefabPath);
        return prefab != null ? prefab.GetComponentInChildren<PlayerHealthHeartHUD>(true) : null;
    }

    private static HeartTokenUI ResolveHeartTokenPrefab(PlayerHealthHeartHUD hudTemplate)
    {
        HeartTokenUI templatePrefab = GetSerializedReference<HeartTokenUI>(hudTemplate, "heartTokenPrefab");
        if (templatePrefab != null)
            return templatePrefab;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeartTokenPrefabPath);
        return prefab != null ? prefab.GetComponent<HeartTokenUI>() : null;
    }

    private static Sprite ResolveHudSprite(PlayerHealthHeartHUD hudTemplate, string propertyName)
    {
        return GetSerializedReference<Sprite>(hudTemplate, propertyName);
    }

    private static Sprite ResolveHeartTokenSprite(HeartTokenUI heartTokenPrefab, string propertyName)
    {
        return GetSerializedReference<Sprite>(heartTokenPrefab, propertyName);
    }

    private static Color ResolveHudColor(PlayerHealthHeartHUD hudTemplate, string propertyName, Color fallback)
    {
        if (hudTemplate == null)
            return fallback;

        SerializedObject so = new(hudTemplate);
        SerializedProperty property = so.FindProperty(propertyName);
        return property != null ? property.colorValue : fallback;
    }

    private static void ConfigurePresentationCanvasRect(RectTransform target)
    {
        if (target == null)
            return;

        Undo.RecordObject(target, "Configure tutorial HP canvas rect");
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = Vector2.zero;
        target.sizeDelta = Vector2.zero;
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
        EditorUtility.SetDirty(target);
    }

    private static void ConfigurePresentationHpParentCanvases(TutorialPresentationHpView hpView)
    {
        if (hpView == null)
            return;

        Canvas[] canvases = hpView.GetComponentsInParent<Canvas>(includeInactive: true);
        for (int i = 0; i < canvases.Length; i++)
            ConfigurePresentationHpCanvas(canvases[i]);
    }

    private static void ConfigurePresentationHpCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        SetGameObjectActive(canvas.gameObject, true, "Enable tutorial HP canvas");
        Undo.RecordObject(canvas, "Configure tutorial HP canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = PresentationHpSortingOrder;
        canvas.enabled = true;
        EditorUtility.SetDirty(canvas);
    }

    private static T GetSerializedReference<T>(Object target, string propertyName) where T : Object
    {
        if (target == null || string.IsNullOrWhiteSpace(propertyName))
            return null;

        SerializedObject so = new(target);
        SerializedProperty property = so.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void ConfigureRectTransformLikeHud(RectTransform target, RectTransform template)
    {
        if (target == null)
            return;

        Undo.RecordObject(target, "Configure tutorial HP HUD rect");
        if (template != null)
        {
            target.anchorMin = template.anchorMin;
            target.anchorMax = template.anchorMax;
            target.pivot = template.pivot;
            target.anchoredPosition = template.anchoredPosition;
            target.sizeDelta = template.sizeDelta;
            target.localScale = template.localScale;
            return;
        }

        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = Vector2.zero;
        target.sizeDelta = Vector2.zero;
        target.localScale = Vector3.one;
    }

    private static void ConfigureHeartGrid(GridLayoutGroup grid, GridLayoutGroup template)
    {
        if (grid == null)
            return;

        Undo.RecordObject(grid, "Configure tutorial HP heart grid");
        grid.padding = template != null
            ? new RectOffset(template.padding.left, template.padding.right, template.padding.top, template.padding.bottom)
            : new RectOffset(40, 0, 40, 0);
        grid.cellSize = template != null ? template.cellSize : new Vector2(50f, 50f);
        grid.spacing = template != null ? template.spacing : new Vector2(10f, 0f);
        grid.childAlignment = template != null ? template.childAlignment : TextAnchor.UpperLeft;
        grid.startCorner = template != null ? template.startCorner : GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = template != null ? template.startAxis : GridLayoutGroup.Axis.Horizontal;
        grid.constraint = template != null ? template.constraint : GridLayoutGroup.Constraint.Flexible;
        grid.constraintCount = template != null ? template.constraintCount : 2;
    }

    private static HeartTokenUI[] EnsureHeartSlots(
        Transform heartContainer,
        HeartTokenUI heartTokenPrefab,
        Sprite filledHeartSprite,
        Sprite emptyHeartSprite,
        Color heartTint,
        int slotCount)
    {
        HeartTokenUI[] heartSlots = new HeartTokenUI[Mathf.Max(0, slotCount)];
        for (int i = 0; i < heartSlots.Length; i++)
        {
            string slotName = $"TutorialHeartSlot_{i + 1:00}";
            Transform slotTransform = FindDirectChild(heartContainer, slotName);
            if (slotTransform == null)
                slotTransform = CreateHeartSlot(heartContainer, heartTokenPrefab, slotName);

            HeartTokenUI token = slotTransform.GetComponent<HeartTokenUI>();
            if (token == null)
                token = Undo.AddComponent<HeartTokenUI>(slotTransform.gameObject);

            Image image = slotTransform.GetComponent<Image>();
            if (image == null)
                image = Undo.AddComponent<Image>(slotTransform.gameObject);

            Undo.RecordObject(slotTransform.gameObject, "Configure tutorial HP heart slot");
            Undo.RecordObject(image, "Configure tutorial HP heart image");
            slotTransform.name = slotName;
            slotTransform.gameObject.SetActive(true);
            image.raycastTarget = false;
            image.preserveAspect = true;

            if (filledHeartSprite != null && emptyHeartSprite != null)
                token.SetSprites(filledHeartSprite, emptyHeartSprite);

            token.SetTint(heartTint);
            token.SetFilled(true);
            heartSlots[i] = token;

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(token);
        }

        DisableExtraHeartSlots(heartContainer, heartSlots.Length);
        return heartSlots;
    }

    private static Transform CreateHeartSlot(Transform heartContainer, HeartTokenUI heartTokenPrefab, string slotName)
    {
        GameObject slotObject;
        if (heartTokenPrefab != null)
        {
            slotObject = PrefabUtility.InstantiatePrefab(heartTokenPrefab.gameObject) as GameObject;
            if (slotObject == null)
                slotObject = new GameObject(slotName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }
        else
        {
            slotObject = new GameObject(slotName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }

        Undo.RegisterCreatedObjectUndo(slotObject, $"Create {slotName}");
        Transform slotTransform = slotObject.transform;
        slotTransform.SetParent(heartContainer, worldPositionStays: false);
        slotTransform.name = slotName;
        return slotTransform;
    }

    private static void DisableExtraHeartSlots(Transform heartContainer, int slotCount)
    {
        if (heartContainer == null)
            return;

        for (int i = 0; i < heartContainer.childCount; i++)
        {
            Transform child = heartContainer.GetChild(i);
            if (child == null || !child.name.StartsWith("TutorialHeartSlot_", StringComparison.Ordinal))
                continue;

            bool belongsToActiveSlots = false;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (string.Equals(child.name, $"TutorialHeartSlot_{slotIndex + 1:00}", StringComparison.Ordinal))
                {
                    belongsToActiveSlots = true;
                    break;
                }
            }

            if (belongsToActiveSlots || !child.gameObject.activeSelf)
                continue;

            Undo.RecordObject(child.gameObject, "Disable extra tutorial HP heart slot");
            child.gameObject.SetActive(false);
            EditorUtility.SetDirty(child.gameObject);
        }
    }

    private static void ConfigureLaserOrigins(
        Vector3 playerPosition,
        Transform leftLaserOrigin,
        Transform rightLaserOrigin,
        Transform centerLaserOrigin)
    {
        SetLaserOrigin(leftLaserOrigin, playerPosition, new Vector2(1f, 1f), 7.5f);
        SetLaserOrigin(rightLaserOrigin, playerPosition, new Vector2(-1f, 1f), 7.5f);
        SetLaserOrigin(centerLaserOrigin, playerPosition, Vector2.up, 7.5f);
    }

    private static void SetLaserOrigin(Transform origin, Vector3 targetPosition, Vector2 direction, float distance)
    {
        if (origin == null)
            return;

        Vector2 resolvedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Undo.RecordObject(origin, $"Position {origin.name}");
        origin.position = targetPosition - (Vector3)(resolvedDirection * distance);
        origin.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(resolvedDirection.y, resolvedDirection.x) * Mathf.Rad2Deg);
    }

    private static void DisableConflictingBossRuntime(Scene scene)
    {
        Transform demonKing = FindTransformByName(scene, "DemonKing");
        RemoveUnneededDemonKingRuntimeComponents(demonKing);

        BossEncounterDirector encounterDirector = FindSceneComponent<BossEncounterDirector>(scene);
        if (encounterDirector != null)
        {
            SerializedObject so = new(encounterDirector);
            SetBool(so, "autoPlayWhenPlayerSpawned", false);
            SetBool(so, "startBossCombatAfterDialogue", false);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(encounterDirector);
        }

        DisableSceneComponent(FindSceneComponent<BossBattleEndHandler>(scene));
        DisableSceneComponent(FindSceneComponent<BossEncounterEndDirector>(scene));
    }

    private static void DeactivateDefaultHudRoots(Scene scene)
    {
        for (int i = 0; i < DefaultHudRootNames.Length; i++)
        {
            Transform root = FindTransformByName(scene, DefaultHudRootNames[i]);
            SetGameObjectActive(root != null ? root.gameObject : null, false, "Disable default tutorial HUD root");
        }

        for (int typeIndex = 0; typeIndex < DefaultHudComponentTypes.Length; typeIndex++)
        {
            List<Component> components = FindSceneComponents(scene, DefaultHudComponentTypes[typeIndex]);
            for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component == null)
                    continue;

                SetGameObjectActive(component.gameObject, false, "Disable default tutorial HUD component root");
            }
        }
    }

    private static void RemoveUnneededDemonKingRuntimeComponents(Transform demonKing)
    {
        if (demonKing == null)
            return;

        for (int typeIndex = 0; typeIndex < DemonKingRuntimeComponentsToRemove.Length; typeIndex++)
        {
            Type componentType = DemonKingRuntimeComponentsToRemove[typeIndex];
            Component[] components = demonKing.GetComponents(componentType);
            for (int componentIndex = components.Length - 1; componentIndex >= 0; componentIndex--)
            {
                Component component = components[componentIndex];
                if (component == null)
                    continue;

                Undo.DestroyObjectImmediate(component);
            }
        }
    }

    private static void DisableSceneComponent(Behaviour component)
    {
        if (component == null || !component.enabled)
            return;

        Undo.RecordObject(component, $"Disable {component.GetType().Name}");
        component.enabled = false;
        EditorUtility.SetDirty(component);
    }

    private static void SetGameObjectActive(GameObject target, bool active, string undoName)
    {
        if (target == null || target.activeSelf == active)
            return;

        Undo.RecordObject(target, undoName);
        target.SetActive(active);
        EditorUtility.SetDirty(target);
    }

    private static Vector3 ResolveBossFocusPosition(Transform demonKing)
    {
        if (demonKing == null)
            return Vector3.zero;

        Bounds bounds = ResolveRendererBounds(demonKing);
        if (bounds.size.sqrMagnitude > 0.0001f)
            return bounds.center + new Vector3(0f, 0.45f, 0f);

        return demonKing.position + new Vector3(0f, 1.1f, 0f);
    }

    private static Vector3 ResolvePlayerFocusPosition(Transform playerSpawn)
    {
        return playerSpawn != null ? playerSpawn.position : Vector3.zero;
    }

    private static Vector3 ResolveInitialAimTargetPosition(Vector3 playerFocusPosition, Vector3 bossFocusPosition)
    {
        Vector3 direction = bossFocusPosition - playerFocusPosition;
        direction.z = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        return playerFocusPosition + direction.normalized * 2f;
    }

    private static Vector2 ResolveFallbackInitialAimDirection(Vector3 playerFocusPosition, Transform initialAimTarget)
    {
        Vector2 direction = initialAimTarget != null
            ? (Vector2)(initialAimTarget.position - playerFocusPosition)
            : Vector2.right;

        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.right;

        return direction.normalized;
    }

    private static Bounds ResolveRendererBounds(Transform root)
    {
        Bounds bounds = default;
        bool hasBounds = false;
        if (root == null)
            return bounds;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    private static DemonKingEgoLaserVfx LoadLaserVfxPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaserVfxPrefabPath);
        return prefab != null ? prefab.GetComponent<DemonKingEgoLaserVfx>() : null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null)
            return existing;

        T added = Undo.AddComponent<T>(target);
        EditorUtility.SetDirty(target);
        return added;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null || EditorUtility.IsPersistent(candidate))
                continue;

            if (candidate.gameObject.scene == scene)
                return candidate;
        }

        return null;
    }

    private static List<Component> FindSceneComponents(Scene scene, Type componentType)
    {
        List<Component> results = new();
        if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            return results;

        Object[] candidates = Resources.FindObjectsOfTypeAll(componentType);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is not Component candidate || EditorUtility.IsPersistent(candidate))
                continue;

            if (candidate.gameObject.scene == scene)
                results.Add(candidate);
        }

        return results;
    }

    private static Transform FindTransformByName(Scene scene, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindInChildren(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindInChildren(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private static void RequireNoSceneComponent<T>(ValidationReport report, Scene scene, string label) where T : Component
    {
        T component = FindSceneComponent<T>(scene);
        if (component != null)
            report.Error($"{label} should be removed from {TargetSceneName}.");
    }

    private static void RequireNoUnneededDemonKingRuntimeComponents(ValidationReport report, Scene scene)
    {
        Transform demonKing = FindTransformByName(scene, "DemonKing");
        if (demonKing == null)
        {
            report.Error("DemonKing object was not found.");
            return;
        }

        for (int typeIndex = 0; typeIndex < DemonKingRuntimeComponentsToRemove.Length; typeIndex++)
        {
            Type componentType = DemonKingRuntimeComponentsToRemove[typeIndex];
            Component[] components = demonKing.GetComponents(componentType);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component != null)
                    report.Error($"DemonKing should not keep tutorial-unneeded component: {component.GetType().Name}.");
            }
        }
    }

    private static void RequirePlayerPrefabPresentationComponents(ValidationReport report, Scene scene)
    {
        PlayerSpawner spawner = FindSceneComponent<PlayerSpawner>(scene);
        if (spawner == null)
        {
            report.Warning("PlayerSpawner was not found; cannot validate spawned player hit/death presentation components.");
            return;
        }

        SerializedObject spawnerSo = new(spawner);
        SerializedProperty playerPrefabProperty = spawnerSo.FindProperty("playerPrefab");
        GameObject playerPrefab = playerPrefabProperty != null
            ? playerPrefabProperty.objectReferenceValue as GameObject
            : null;
        if (playerPrefab == null)
        {
            report.Error("PlayerSpawner.playerPrefab is unassigned.");
            return;
        }

        if (playerPrefab.GetComponent<PlayerHitFeedback2D>() == null)
            report.Error("PlayerSpawner.playerPrefab must include PlayerHitFeedback2D for tutorial laser hits.");

        if (playerPrefab.GetComponent<PlayerDeathPresentation2D>() == null)
            report.Error("PlayerSpawner.playerPrefab must include PlayerDeathPresentation2D for the scripted tutorial death.");
    }

    private static void RequireHpLayoutMatchesHud(
        ValidationReport report,
        TutorialPresentationHpView hpView,
        Scene scene)
    {
        if (hpView == null)
            return;

        PlayerHealthHeartHUD hudTemplate = ResolvePlayerHealthHudTemplate(scene);
        if (hudTemplate == null)
        {
            report.Warning("PlayerHealthHeartHUD template was not found; cannot compare tutorial HP HUD layout.");
            return;
        }

        RectTransform hpRect = hpView.GetComponent<RectTransform>();
        RectTransform templateRect = hudTemplate.GetComponent<RectTransform>();
        if (!RectTransformLayoutMatches(hpRect, templateRect))
            report.Error("Tutorial presentation HP RectTransform must match PlayerHealthHeartHUD layout.");

        GridLayoutGroup hpGrid = hpView.GetComponentInChildren<GridLayoutGroup>(true);
        GridLayoutGroup templateGrid = hudTemplate.GetComponent<GridLayoutGroup>();
        if (!GridLayoutMatches(hpGrid, templateGrid))
            report.Error("Tutorial presentation HP GridLayoutGroup must match PlayerHealthHeartHUD layout.");
    }

    private static bool RectTransformLayoutMatches(RectTransform lhs, RectTransform rhs)
    {
        if (lhs == null || rhs == null)
            return false;

        return Approximately(lhs.anchorMin, rhs.anchorMin) &&
               Approximately(lhs.anchorMax, rhs.anchorMax) &&
               Approximately(lhs.pivot, rhs.pivot) &&
               Approximately(lhs.anchoredPosition, rhs.anchoredPosition) &&
               Approximately(lhs.sizeDelta, rhs.sizeDelta);
    }

    private static bool GridLayoutMatches(GridLayoutGroup lhs, GridLayoutGroup rhs)
    {
        if (lhs == null || rhs == null)
            return false;

        return lhs.padding.left == rhs.padding.left &&
               lhs.padding.right == rhs.padding.right &&
               lhs.padding.top == rhs.padding.top &&
               lhs.padding.bottom == rhs.padding.bottom &&
               Approximately(lhs.cellSize, rhs.cellSize) &&
               Approximately(lhs.spacing, rhs.spacing) &&
               lhs.childAlignment == rhs.childAlignment &&
               lhs.startCorner == rhs.startCorner &&
               lhs.startAxis == rhs.startAxis &&
               lhs.constraint == rhs.constraint &&
               lhs.constraintCount == rhs.constraintCount;
    }

    private static bool Approximately(Vector2 lhs, Vector2 rhs)
    {
        return Mathf.Approximately(lhs.x, rhs.x) && Mathf.Approximately(lhs.y, rhs.y);
    }

    private static void RequireDefaultHudInactive(ValidationReport report, Scene scene)
    {
        for (int i = 0; i < DefaultHudRootNames.Length; i++)
        {
            Transform root = FindTransformByName(scene, DefaultHudRootNames[i]);
            if (root == null)
            {
                report.Warning($"{DefaultHudRootNames[i]} was not found; cannot validate default HUD active state.");
                continue;
            }

            if (root.gameObject.activeSelf)
                report.Error($"{DefaultHudRootNames[i]} must be inactive in {TargetSceneName}.");
        }

        for (int typeIndex = 0; typeIndex < DefaultHudComponentTypes.Length; typeIndex++)
        {
            Type componentType = DefaultHudComponentTypes[typeIndex];
            List<Component> components = FindSceneComponents(scene, componentType);
            for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component == null)
                    continue;

                if (component.gameObject.activeSelf)
                    report.Error($"{componentType.Name} root '{component.gameObject.name}' must be inactive in {TargetSceneName}.");
            }
        }
    }

    private static void RequirePresentationHpCanvasUsable(ValidationReport report, TutorialPresentationHpView hpView)
    {
        if (hpView == null)
            return;

        Canvas canvas = hpView.GetComponentInParent<Canvas>(includeInactive: true);
        if (canvas == null)
        {
            report.Error("Tutorial presentation HP view must be under a Canvas.");
            return;
        }

        string canvasPath = BuildHierarchyPath(canvas.transform);

        if (!canvas.gameObject.activeSelf)
            report.Error($"Tutorial presentation HP Canvas '{canvasPath}' GameObject must stay active; hide it with CanvasGroup alpha instead.");

        if (!canvas.enabled)
            report.Error($"Tutorial presentation HP Canvas '{canvasPath}' must be enabled.");

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            report.Error($"Tutorial presentation HP Canvas '{canvasPath}' must use ScreenSpaceOverlay render mode.");

        if (IsNestedCanvas(canvas) && !canvas.overrideSorting)
            report.Error(
                $"Nested tutorial presentation HP Canvas '{canvasPath}' must override sorting so it can render over normal HUD layers. " +
                "Run Apply Default Authoring to rewrite stale HP canvas authoring.");

        if (canvas.sortingOrder < PresentationHpSortingOrder)
            report.Error(
                $"Tutorial presentation HP Canvas '{canvasPath}' sortingOrder must be at least {PresentationHpSortingOrder}; " +
                $"current value is {canvas.sortingOrder}.");

        if (canvas.transform is RectTransform rect)
        {
            Vector3 scale = rect.localScale;
            if (Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f || Mathf.Abs(scale.z) < 0.0001f)
                report.Error($"Tutorial presentation HP Canvas '{canvasPath}' RectTransform scale must not be zero.");
        }
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return "<missing>";

        StringBuilder builder = new(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, $"{current.name}/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private static bool IsNestedCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;

        Transform parent = canvas.transform.parent;
        return parent != null && parent.GetComponentInParent<Canvas>(includeInactive: true) != null;
    }

    private static void RequireReference(ValidationReport report, SerializedObject so, string propertyName, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            report.Error($"{label} is unassigned.");
    }

    private static void RequireBool(ValidationReport report, SerializedObject so, string propertyName, bool expected, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.boolValue != expected)
            report.Error($"{label} must be {expected}.");
    }

    private static void RequireInt(ValidationReport report, SerializedObject so, string propertyName, int expected, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.intValue != expected)
            report.Error($"{label} must be {expected}.");
    }

    private static void RequireFloatAtLeast(
        ValidationReport report,
        SerializedObject so,
        string propertyName,
        float minimum,
        string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.floatValue < minimum)
            report.Error($"{label} must be at least {minimum:0.###}.");
    }

    private static void RequireString(
        ValidationReport report,
        SerializedObject so,
        string propertyName,
        string expected,
        string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (!string.Equals(property.stringValue, expected, StringComparison.Ordinal))
            report.Error($"{label} must be '{expected}'.");
    }

    private static void RequireObjectArrayMinSize(
        ValidationReport report,
        SerializedObject so,
        string propertyName,
        int minimumSize,
        string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.arraySize < minimumSize)
        {
            report.Error($"{label} must have at least {minimumSize} entries.");
            return;
        }

        for (int i = 0; i < minimumSize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
                report.Error($"{label} entry {i + 1} is unassigned.");
        }
    }

    private static void RequireDisabled(ValidationReport report, Behaviour component, string label)
    {
        if (component == null)
        {
            report.Warning($"{label} was not found.");
            return;
        }

        if (component.enabled)
            report.Error($"{label} should be disabled for the tutorial boss presentation scene.");
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetVector2(SerializedObject so, string propertyName, Vector2 value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.vector2Value = value;
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetColor(SerializedObject so, string propertyName, Color value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    private static void SetObjectArray(SerializedObject so, string propertyName, IReadOnlyList<Object> values)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        int count = values != null ? values.Count : 0;
        property.arraySize = count;
        for (int i = 0; i < count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetGlobalCanvasLayers(SerializedObject so, string propertyName, params GlobalCanvasLayer[] values)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = values != null ? values.Length : 0;
        for (int i = 0; values != null && i < values.Length; i++)
            property.GetArrayElementAtIndex(i).enumValueIndex = (int)values[i];
    }

    private static void SetRelativeObject(SerializedProperty parent, string relativePath, Object value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetRelativeBool(SerializedProperty parent, string relativePath, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetRelativeFloat(SerializedProperty parent, string relativePath, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetRelativeVector2(SerializedProperty parent, string relativePath, Vector2 value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);
        if (property != null)
            property.vector2Value = value;
    }

    private static void SetRelativeColor(SerializedProperty parent, string relativePath, Color value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);
        if (property != null)
            property.colorValue = value;
    }

    private sealed class ValidationReport
    {
        private readonly string title;
        private readonly List<string> errors = new();
        private readonly List<string> warnings = new();

        public ValidationReport(string title)
        {
            this.title = title;
        }

        public void Error(string message)
        {
            errors.Add(message);
        }

        public void Warning(string message)
        {
            warnings.Add(message);
        }

        public void Log()
        {
            StringBuilder builder = new();
            builder.AppendLine($"[DarkLordTutorialAuthoring] {title}");
            builder.AppendLine($"Errors: {errors.Count}");
            for (int i = 0; i < errors.Count; i++)
                builder.AppendLine($"- ERROR: {errors[i]}");

            builder.AppendLine($"Warnings: {warnings.Count}");
            for (int i = 0; i < warnings.Count; i++)
                builder.AppendLine($"- WARN: {warnings[i]}");

            if (errors.Count > 0)
                Debug.LogError(builder.ToString());
            else if (warnings.Count > 0)
                Debug.LogWarning(builder.ToString());
            else
                Debug.Log(builder.ToString());
        }
    }
}
#endif





