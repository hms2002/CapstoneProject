using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class BossRewardRevealSceneValidatorWindow : EditorWindow
{
    private const string ChestDustName = "BossRewardChestRevealDust";
    private const string GateMaskName = "BossRewardGateRevealMask";
    private const string GateLoopDustName = "BossRewardGateRevealLoopDust";
    private const string GateBurstDustName = "BossRewardGateRevealBurstDust";
    private const string ChestDustPrefabPath = "Assets/LeeJunMo/Prefab/Effect/Particle/ChestReavealDust.prefab";
    private const string GateLoopDustPrefabPath = "Assets/LeeJunMo/Prefab/Effect/Particle/GateRevealLoopDust.prefab";
    private const string GateBurstDustPrefabPath = "Assets/LeeJunMo/Prefab/Effect/Particle/GateRevealBurstDust.prefab";
    private const string RouteCatalogPath = "Assets/LeeJunMo/Datas/Scene/RunRouteCatalog.asset";
    private const string ShadowRouteSetPath = "Assets/LeeJunMo/Datas/Scene/ShadowCorridorBossRouteSet.asset";
    private const string DragonRouteSetPath = "Assets/LeeJunMo/Datas/Scene/Dragon_CorridorBossRouteSet.asset";
    private const string SlimeRouteSetPath = "Assets/LeeJunMo/Datas/Scene/SlimeRouteSet.asset";
    private const string DemonKingRouteSetPath = "Assets/LeeJunMo/Datas/Scene/DemonkingRouteSet.asset";
    private const int RequiredNormalStageCount = 3;
    private static readonly Vector3 DefaultChestRewardDustLocalOffset = Vector3.zero;
    private static readonly Vector3 DefaultGateStartLocalOffset = new(0f, -2.95f, 0f);
    private static readonly Vector3 DefaultGateParticleSpawnLocalOffset = new(0f, -1.5f, 0f);
    private static readonly Vector3 DefaultGateRevealRootShakeAmplitude = new(0.5f, 0.038f, 0f);
    private const float DefaultGateRevealDurationSeconds = 1.5f;
    private const float DefaultGateCompleteShakeAmplitude = 2f;

    private static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/HeoMinSeok_Boss_Shadow.unity",
        "Assets/Scenes/HeoMinSeok_Boss_Dragon.unity",
        "Assets/Scenes/SangHyup_Boss_SlimeQueen.unity"
    };

    private static readonly string[] TargetNormalRouteSetPaths =
    {
        ShadowRouteSetPath,
        DragonRouteSetPath,
        SlimeRouteSetPath
    };

    private static readonly string[] TargetNormalBossSceneNames =
    {
        "HeoMinSeok_Boss_Shadow",
        "HeoMinSeok_Boss_Dragon",
        "SangHyup_Boss_SlimeQueen"
    };

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
        public Object Context;
        public string ObjectPath;
    }

    private sealed class ApplyStats
    {
        public int ScenesSaved;
        public int ComponentsAdded;
        public int ComponentsRemoved;
        public int ObjectsCreated;
        public int ReferencesAssigned;
        public int ComponentsDisabled;
        public int ObjectsDeactivated;
        public int RoutesNormalized;
        public int RouteCatalogUpdates;

        public bool HasChanges =>
            ComponentsAdded > 0 ||
            ComponentsRemoved > 0 ||
            ObjectsCreated > 0 ||
            ReferencesAssigned > 0 ||
            ComponentsDisabled > 0 ||
            ObjectsDeactivated > 0 ||
            RoutesNormalized > 0 ||
            RouteCatalogUpdates > 0;

        public void Add(ApplyStats other)
        {
            if (other == null)
                return;

            ScenesSaved += other.ScenesSaved;
            ComponentsAdded += other.ComponentsAdded;
            ComponentsRemoved += other.ComponentsRemoved;
            ObjectsCreated += other.ObjectsCreated;
            ReferencesAssigned += other.ReferencesAssigned;
            ComponentsDisabled += other.ComponentsDisabled;
            ObjectsDeactivated += other.ObjectsDeactivated;
            RoutesNormalized += other.RoutesNormalized;
            RouteCatalogUpdates += other.RouteCatalogUpdates;
        }
    }

    private readonly List<ValidationResult> results = new();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Validation/Boss Reward Reveal Scene Validator")]
    public static void ShowWindow()
    {
        GetWindow<BossRewardRevealSceneValidatorWindow>("Boss Reward Reveals");
    }

    [MenuItem("Tools/Validation/Apply Boss Reward Reveal Target Scene Setup")]
    public static void ApplyTargetSceneSetupMenu()
    {
        BossRewardRevealSceneValidatorWindow window =
            GetWindow<BossRewardRevealSceneValidatorWindow>("Boss Reward Reveals");
        window.ApplyTargetScenes();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawTargetScenes();
        DrawSummary();
        DrawResults();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Validate Target Scenes", EditorStyles.toolbarButton))
                ValidateTargetScenes();

            if (GUILayout.Button("Apply Target Scenes", EditorStyles.toolbarButton))
                ApplyTargetScenes();

            if (GUILayout.Button("Validate Loaded Scenes", EditorStyles.toolbarButton))
                ValidateLoadedScenes();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                results.Clear();
        }
    }

    private void DrawTargetScenes()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Target Scenes", EditorStyles.boldLabel);
        for (int i = 0; i < TargetScenePaths.Length; i++)
            EditorGUILayout.LabelField(TargetScenePaths[i]);
    }

    private void DrawSummary()
    {
        int errors = CountSeverity(Severity.Error);
        int warnings = CountSeverity(Severity.Warning);
        int infos = CountSeverity(Severity.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Errors", errors.ToString());
        EditorGUILayout.LabelField("Warnings", warnings.ToString());
        EditorGUILayout.LabelField("Infos", infos.ToString());
        EditorGUILayout.Space(6f);
    }

    private int CountSeverity(Severity severity)
    {
        int count = 0;
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].SeverityLevel == severity)
                count++;
        }

        return count;
    }

    private void DrawResults()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No results yet. Run validation to inspect boss reward chest/portal reveal authoring.",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        for (int i = 0; i < results.Count; i++)
        {
            ValidationResult result = results[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{result.SeverityLevel} - {result.ScenePath}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(result.ObjectPath))
                    EditorGUILayout.LabelField("Object", result.ObjectPath);

                if (result.Context != null && GUILayout.Button("Ping", GUILayout.Width(64f)))
                    EditorGUIUtility.PingObject(result.Context);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ValidateTargetScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        results.Clear();
        ValidateRouteCatalog();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int i = 0; i < TargetScenePaths.Length; i++)
            {
                string scenePath = TargetScenePaths[i];
                if (!File.Exists(scenePath))
                {
                    AddResult(scenePath, Severity.Error, "Target scene asset was not found.", null, string.Empty);
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ValidateScene(scenePath, scene);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        AddPassResultIfEmpty("Target scenes");
    }

    private void ValidateLoadedScenes()
    {
        results.Clear();
        ValidateRouteCatalog();

        int sceneCount = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            sceneCount++;
            ValidateScene(scene.path, scene);
        }

        if (sceneCount == 0)
            AddResult("Loaded Scenes", Severity.Warning, "No loaded scenes were available for validation.", null, string.Empty);

        AddPassResultIfEmpty("Loaded scenes");
    }

    private void ApplyTargetScenes()
    {
        if (Application.isPlaying)
        {
            results.Clear();
            AddResult("Apply", Severity.Error, "Cannot apply boss reward reveal scene setup while in Play Mode.", null, string.Empty);
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Apply Boss Reward Reveal Scene Setup",
                "This will update the run route catalog, then open and save the three target boss scenes. It assigns TreasureChest dust reveal settings, adds portal reveal components, assigns portal dust prefab references plus spawn anchors/offsets, removes stale chest reveal components, disables the duplicate BossBattleEndHandler owner, and normalizes boss reward portals to BossToCorridor. Continue?",
                "Apply And Save",
                "Cancel"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        results.Clear();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        ApplyStats totalStats = new();
        ApplyRouteCatalogDefaults(totalStats);

        try
        {
            for (int i = 0; i < TargetScenePaths.Length; i++)
            {
                string scenePath = TargetScenePaths[i];
                if (!File.Exists(scenePath))
                {
                    AddResult(scenePath, Severity.Error, "Target scene asset was not found.", null, string.Empty);
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ApplyStats sceneStats = ApplyScene(scenePath, scene);
                if (sceneStats.HasChanges)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sceneStats.ScenesSaved++;
                }

                totalStats.Add(sceneStats);
                ValidateScene(scenePath, scene);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        AddResult(
            "Apply",
            Severity.Info,
            $"Apply complete. ScenesSaved={totalStats.ScenesSaved}, ComponentsAdded={totalStats.ComponentsAdded}, ComponentsRemoved={totalStats.ComponentsRemoved}, ObjectsCreated={totalStats.ObjectsCreated}, ReferencesAssigned={totalStats.ReferencesAssigned}, ComponentsDisabled={totalStats.ComponentsDisabled}, ObjectsDeactivated={totalStats.ObjectsDeactivated}, RoutesNormalized={totalStats.RoutesNormalized}, RouteCatalogUpdates={totalStats.RouteCatalogUpdates}.",
            null,
            string.Empty);
    }

    private void ApplyRouteCatalogDefaults(ApplyStats stats)
    {
        RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(RouteCatalogPath);
        if (catalog == null)
        {
            AddResult(RouteCatalogPath, Severity.Error, "RunRouteCatalog asset was not found.", null, string.Empty);
            return;
        }

        List<Object> normalRouteSets = LoadRouteSetObjects(TargetNormalRouteSetPaths);
        CorridorBossRouteSetSO finalRouteSet = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(DemonKingRouteSetPath);
        if (normalRouteSets.Count != TargetNormalRouteSetPaths.Length || finalRouteSet == null)
        {
            AddResult(
                RouteCatalogPath,
                Severity.Error,
                "RunRouteCatalog apply skipped because one or more required RouteSet assets were missing.",
                catalog,
                RouteCatalogPath);
            return;
        }

        SerializedObject serializedCatalog = new(catalog);
        bool catalogChanged = false;
        catalogChanged |= AssignInt(serializedCatalog, "normalStageCount", RequiredNormalStageCount, stats);
        catalogChanged |= AssignObjectArray(serializedCatalog, "normalRouteSets", normalRouteSets, stats);
        catalogChanged |= AssignBool(serializedCatalog, "allowDuplicateNormalRoutes", false, stats);
        catalogChanged |= AssignObjectReference(serializedCatalog, "finalRouteSet", finalRouteSet, stats);

        if (catalogChanged)
        {
            serializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            stats.RouteCatalogUpdates++;
            AddResult(
                RouteCatalogPath,
                Severity.Info,
                "Updated RunRouteCatalog to Normal 3-stage route sets plus DemonKing final route set.",
                catalog,
                RouteCatalogPath);
        }

        bool routeSetChanged = ApplyRouteSetSceneNameDefaults(stats);
        if (catalogChanged || routeSetChanged)
            AssetDatabase.SaveAssets();
    }

    private bool ApplyRouteSetSceneNameDefaults(ApplyStats stats)
    {
        bool changedAny = false;
        for (int i = 0; i < TargetNormalRouteSetPaths.Length; i++)
        {
            CorridorBossRouteSetSO routeSet =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(TargetNormalRouteSetPaths[i]);
            if (routeSet == null)
                continue;

            SerializedObject serializedRouteSet = new(routeSet);
            bool changed = AssignString(serializedRouteSet, "bossSceneName", TargetNormalBossSceneNames[i], stats);
            if (!changed)
                continue;

            serializedRouteSet.ApplyModifiedProperties();
            EditorUtility.SetDirty(routeSet);
            stats.RouteCatalogUpdates++;
            changedAny = true;
            AddResult(
                TargetNormalRouteSetPaths[i],
                Severity.Info,
                $"Updated RouteSet bossSceneName to {TargetNormalBossSceneNames[i]}.",
                routeSet,
                TargetNormalRouteSetPaths[i]);
        }

        return changedAny;
    }

    private ApplyStats ApplyScene(string scenePath, Scene scene)
    {
        ApplyStats stats = new();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            AddResult(scenePath, Severity.Error, "Scene could not be loaded for apply.", null, string.Empty);
            return stats;
        }

        List<BossEncounterEndDirector> directors = FindSceneComponents<BossEncounterEndDirector>(scene);
        if (directors.Count == 0)
        {
            AddResult(scenePath, Severity.Error, "No BossEncounterEndDirector was found. Apply skipped.", null, string.Empty);
            return stats;
        }

        BossEncounterEndDirector director = directors[0];
        ApplyDirectorDefaults(director, stats);

        SerializedObject serializedDirector = new SerializedObject(director);
        TreasureChest chest = GetObjectReference<TreasureChest>(serializedDirector, "treasureChest");
        GameObject portal = GetObjectReference<GameObject>(serializedDirector, "exitPortal");

        ApplyChestSetup(scenePath, chest, stats);
        ApplyPortalSetup(scenePath, portal, stats);
        DisableDuplicateHandlers(scenePath, director, chest, portal, stats);

        return stats;
    }

    private static void ApplyDirectorDefaults(BossEncounterEndDirector director, ApplyStats stats)
    {
        if (director == null)
            return;

        SerializedObject serializedDirector = new(director);
        bool changed = false;
        changed |= AssignBool(serializedDirector, "suppressManagedBossAutomaticRewards", true);
        changed |= AssignBool(serializedDirector, "hideAuthoredObjectsOnStart", true);
        if (!changed)
            return;

        serializedDirector.ApplyModifiedProperties();
        EditorUtility.SetDirty(director);
        stats.ReferencesAssigned++;
    }

    private void DisableDuplicateHandlers(
        string scenePath,
        BossEncounterEndDirector director,
        TreasureChest directorChest,
        GameObject directorPortal,
        ApplyStats stats)
    {
        if (director == null)
            return;

        List<BossBattleEndHandler> handlers = FindSceneComponents<BossBattleEndHandler>(director.gameObject.scene);
        for (int i = 0; i < handlers.Count; i++)
        {
            BossBattleEndHandler handler = handlers[i];
            if (handler == null || !handler.enabled)
                continue;

            SerializedObject serializedHandler = new(handler);
            TreasureChest handlerChest = GetObjectReference<TreasureChest>(serializedHandler, "treasureChest");
            GameObject handlerPortal = GetObjectReference<GameObject>(serializedHandler, "exitPortal");
            if (handlerChest != directorChest || handlerPortal != directorPortal)
            {
                AddResult(
                    scenePath,
                    Severity.Warning,
                    "BossBattleEndHandler is enabled but does not reference the same chest and portal as BossEncounterEndDirector. Apply left it enabled for manual review.",
                    handler,
                    GetObjectPath(handler.transform));
                continue;
            }

            Undo.RecordObject(handler, "Disable duplicate boss reward owner");
            handler.enabled = false;
            EditorUtility.SetDirty(handler);
            stats.ComponentsDisabled++;
            AddResult(
                scenePath,
                Severity.Info,
                "Disabled BossBattleEndHandler so BossEncounterEndDirector is the single active reward owner.",
                handler,
                GetObjectPath(handler.transform));
        }
    }

    private void ApplyChestSetup(string scenePath, TreasureChest chest, ApplyStats stats)
    {
        if (chest == null)
        {
            AddResult(scenePath, Severity.Error, "Cannot apply chest reveal because the director treasureChest reference is missing.", null, string.Empty);
            return;
        }

        DeactivateAuthoredObject(chest.gameObject, stats);
        RemoveChestRevealComponents(chest, stats);

        ParticleSystem dustPrefab = LoadParticlePrefab(ChestDustPrefabPath);

        SerializedObject serializedChest = new(chest);
        AssignObjectReference(serializedChest, "rewardRevealDustParticle", dustPrefab, stats);
        AssignObjectReference(serializedChest, "rewardRevealDustAnchor", chest.transform, stats);
        AssignVector3(serializedChest, "rewardRevealDustLocalOffset", DefaultChestRewardDustLocalOffset, stats);
        AssignBool(serializedChest, "clearRewardRevealDustBeforePlay", true, stats);
        AssignFloat(serializedChest, "spawnedRewardRevealDustDestroyDelay", 2f, stats);
        serializedChest.ApplyModifiedProperties();
        EditorUtility.SetDirty(chest);
    }

    private static void RemoveChestRevealComponents(TreasureChest chest, ApplyStats stats)
    {
        if (chest == null)
            return;

        BossRewardObjectRevealPresentation[] reveals =
            chest.GetComponentsInChildren<BossRewardObjectRevealPresentation>(true);
        for (int i = 0; i < reveals.Length; i++)
        {
            BossRewardObjectRevealPresentation reveal = reveals[i];
            if (reveal == null)
                continue;

            Undo.DestroyObjectImmediate(reveal);
            stats.ComponentsRemoved++;
        }
    }

    private void ApplyPortalSetup(string scenePath, GameObject portalRoot, ApplyStats stats)
    {
        if (portalRoot == null)
        {
            AddResult(scenePath, Severity.Error, "Cannot apply gate reveal because the director exitPortal reference is missing.", null, string.Empty);
            return;
        }

        DeactivateAuthoredObject(portalRoot, stats);
        NormalizePortalSemantics(portalRoot, stats);

        BossRewardObjectRevealPresentation reveal = EnsureRevealComponent(portalRoot, stats);
        SpriteRenderer[] renderers = portalRoot.GetComponentsInChildren<SpriteRenderer>(true);
        Collider2D[] colliders = portalRoot.GetComponentsInChildren<Collider2D>(true);
        ParticleSystem loopDustPrefab = LoadParticlePrefab(GateLoopDustPrefabPath);
        ParticleSystem burstDustPrefab = LoadParticlePrefab(GateBurstDustPrefabPath);
        SpriteMask revealMask = EnsureGateMaskObject(
            scenePath,
            portalRoot.transform,
            renderers.Length > 0 ? renderers[0] : null,
            stats);

        SerializedObject serializedReveal = new(reveal);
        AssignObjectReference(serializedReveal, "revealRoot", portalRoot.transform, stats);
        AssignObjectReference(serializedReveal, "revealMask", revealMask, stats);
        AssignObjectArray(serializedReveal, "maskedRenderers", renderers, stats);
        AssignObjectArray(serializedReveal, "loopDustParticles", new Object[] { loopDustPrefab }, stats);
        AssignObjectArray(serializedReveal, "burstDustParticles", new Object[] { burstDustPrefab }, stats);
        AssignObjectReference(serializedReveal, "particleSpawnAnchor", portalRoot.transform, stats);
        AssignVector3(serializedReveal, "particleSpawnLocalOffset", DefaultGateParticleSpawnLocalOffset, stats);
        AssignObjectArray(serializedReveal, "collidersToDisableDuringReveal", colliders, stats);
        AssignVector3(serializedReveal, "startLocalOffset", DefaultGateStartLocalOffset, stats);
        AssignFloat(serializedReveal, "revealDurationSeconds", DefaultGateRevealDurationSeconds, stats);
        AssignBool(serializedReveal, "applyMaskDuringReveal", true);
        AssignBool(serializedReveal, "disableMaskAfterReveal", true);
        AssignBool(serializedReveal, "stopLoopDustOnComplete", true);
        AssignBool(serializedReveal, "clearParticlesBeforePlay", true);
        AssignBool(serializedReveal, "isolateGlobalVisionMasksDuringReveal", true);
        AssignBool(serializedReveal, "playLoopCameraShake", true, stats);
        AssignCameraShakeHook(
            serializedReveal,
            "loopCameraShake",
            CameraShakeHook.Create(0.035f, 1f, 0f, 0.18f),
            stats);
        AssignBool(serializedReveal, "playCompleteCameraShake", true, stats);
        AssignCameraShakeHook(
            serializedReveal,
            "completeCameraShake",
            CameraShakeHook.Create(DefaultGateCompleteShakeAmplitude, 1f, 0f, 0f),
            stats);
        AssignBool(serializedReveal, "shakeRevealRootDuringReveal", true, stats);
        AssignVector3(serializedReveal, "revealRootShakeAmplitude", DefaultGateRevealRootShakeAmplitude, stats);
        AssignFloat(serializedReveal, "revealRootShakeFrequency", 24f, stats);
        serializedReveal.ApplyModifiedProperties();
        EditorUtility.SetDirty(reveal);
    }

    private static BossRewardObjectRevealPresentation EnsureRevealComponent(GameObject owner, ApplyStats stats)
    {
        BossRewardObjectRevealPresentation reveal = ResolveReveal(owner);
        if (reveal != null)
            return reveal;

        reveal = Undo.AddComponent<BossRewardObjectRevealPresentation>(owner);
        EditorUtility.SetDirty(owner);
        stats.ComponentsAdded++;
        return reveal;
    }

    private static void DeactivateAuthoredObject(GameObject gameObject, ApplyStats stats)
    {
        if (gameObject == null || !gameObject.activeSelf)
            return;

        Undo.RecordObject(gameObject, "Deactivate authored boss reward object");
        gameObject.SetActive(false);
        EditorUtility.SetDirty(gameObject);
        stats.ObjectsDeactivated++;
    }

    private static void NormalizePortalSemantics(GameObject portalRoot, ApplyStats stats)
    {
        if (portalRoot == null)
            return;

        ScenePortal[] portals = portalRoot.GetComponentsInChildren<ScenePortal>(true);
        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal portal = portals[i];
            if (portal == null)
                continue;

            SerializedObject serializedPortal = new(portal);
            bool changed = false;
            SerializedProperty transitionType = serializedPortal.FindProperty("transitionType");
            if (transitionType != null && transitionType.enumValueIndex != (int)TransitionType.BossToCorridor)
            {
                transitionType.enumValueIndex = (int)TransitionType.BossToCorridor;
                changed = true;
                stats.RoutesNormalized++;
            }

            SerializedProperty startRunRouteCatalog = serializedPortal.FindProperty("startRunRouteCatalog");
            if (startRunRouteCatalog != null && startRunRouteCatalog.objectReferenceValue != null)
            {
                startRunRouteCatalog.objectReferenceValue = null;
                changed = true;
                stats.ReferencesAssigned++;
            }

            if (!changed)
                continue;

            serializedPortal.ApplyModifiedProperties();
            EditorUtility.SetDirty(portal);
        }
    }

    private static SpriteMask EnsureGateMaskObject(
        string scenePath,
        Transform portalRoot,
        SpriteRenderer sourceRenderer,
        ApplyStats stats)
    {
        Transform parent = portalRoot != null ? portalRoot.parent : null;
        GameObject maskObject = FindChildByName(parent, GateMaskName);
        if (maskObject == null && portalRoot != null)
            maskObject = FindChildByName(portalRoot, GateMaskName);
        if (maskObject == null)
        {
            maskObject = new GameObject(GateMaskName);
            Undo.RegisterCreatedObjectUndo(maskObject, "Create boss reward gate mask");
            if (parent != null)
                maskObject.transform.SetParent(parent, false);
            else if (portalRoot != null)
                SceneManager.MoveGameObjectToScene(maskObject, portalRoot.gameObject.scene);
            stats.ObjectsCreated++;
        }

        EnsureTransformParent(maskObject.transform, parent, portalRoot != null ? portalRoot.gameObject.scene : default);

        if (portalRoot != null)
        {
            Undo.RecordObject(maskObject.transform, "Position boss reward gate mask");
            if (parent != null)
            {
                maskObject.transform.localPosition = portalRoot.localPosition;
                maskObject.transform.localRotation = portalRoot.localRotation;
            }
            else
            {
                maskObject.transform.position = portalRoot.position;
                maskObject.transform.rotation = portalRoot.rotation;
            }

            maskObject.transform.localScale = portalRoot.localScale;
        }

        SpriteMask mask = maskObject.GetComponent<SpriteMask>();
        if (mask == null)
        {
            mask = Undo.AddComponent<SpriteMask>(maskObject);
            stats.ComponentsAdded++;
        }

        Undo.RecordObject(mask, "Configure boss reward gate mask");
        if (sourceRenderer != null)
        {
            mask.sprite = sourceRenderer.sprite;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = sourceRenderer.sortingLayerID;
            mask.backSortingLayerID = sourceRenderer.sortingLayerID;
            mask.frontSortingOrder = sourceRenderer.sortingOrder + 1;
            mask.backSortingOrder = sourceRenderer.sortingOrder - 1;
        }

        mask.enabled = false;
        EditorUtility.SetDirty(mask);
        EditorUtility.SetDirty(maskObject);
        return mask;
    }

    private static ParticleSystem EnsureParticleObject(
        string scenePath,
        Transform parent,
        string objectName,
        Vector3 localPosition,
        bool loop,
        ApplyStats stats)
    {
        GameObject particleObject = FindChildByName(parent, objectName);
        if (particleObject == null)
        {
            particleObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(particleObject, $"Create {objectName}");
            if (parent != null)
                particleObject.transform.SetParent(parent, false);
            else
                MoveToActiveScene(particleObject, scenePath);
            stats.ObjectsCreated++;
        }

        EnsureTransformParent(particleObject.transform, parent, SceneManager.GetActiveScene());

        Undo.RecordObject(particleObject.transform, $"Position {objectName}");
        if (parent != null)
        {
            particleObject.transform.localPosition = localPosition;
            particleObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            particleObject.transform.position = localPosition;
            particleObject.transform.rotation = Quaternion.identity;
        }

        particleObject.transform.localScale = Vector3.one;

        ParticleSystem particle = particleObject.GetComponent<ParticleSystem>();
        if (particle == null)
        {
            particle = Undo.AddComponent<ParticleSystem>(particleObject);
            stats.ComponentsAdded++;
        }

        ConfigureParticle(particle, loop);
        EditorUtility.SetDirty(particleObject);
        return particle;
    }

    private static void MoveToActiveScene(GameObject gameObject, string scenePath)
    {
        if (gameObject == null)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.MoveGameObjectToScene(gameObject, scene);
    }

    private static void ConfigureParticle(ParticleSystem particle, bool loop)
    {
        if (particle == null)
            return;

        Undo.RecordObject(particle, "Configure boss reward reveal particle");
        ParticleSystem.MainModule main = particle.main;
        main.loop = loop;
        main.playOnAwake = false;
        main.duration = loop ? 3f : 0.7f;
        main.startLifetime = loop ? 0.45f : 0.55f;
        main.startSpeed = loop ? 0.28f : 0.8f;
        main.startSize = loop ? 0.18f : 0.24f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.56f, 0.48f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.enabled = true;
        emission.rateOverTime = loop ? 14f : 0f;
        if (!loop)
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = loop ? 0.42f : 0.55f;

        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Configure boss reward reveal particle renderer");
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 20;
            EditorUtility.SetDirty(renderer);
        }

        EditorUtility.SetDirty(particle);
    }

    private static ParticleSystem LoadParticlePrefab(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[BossRewardRevealSceneValidator] Particle prefab was not found at {assetPath}.");
            return null;
        }

        ParticleSystem particle = prefab.GetComponent<ParticleSystem>();
        if (particle == null)
            particle = prefab.GetComponentInChildren<ParticleSystem>(true);

        if (particle == null)
            Debug.LogWarning($"[BossRewardRevealSceneValidator] Particle prefab has no ParticleSystem: {assetPath}.");

        return particle;
    }

    private static List<Object> LoadRouteSetObjects(string[] assetPaths)
    {
        List<Object> routeSets = new();
        if (assetPaths == null)
            return routeSets;

        for (int i = 0; i < assetPaths.Length; i++)
        {
            CorridorBossRouteSetSO routeSet =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(assetPaths[i]);
            if (routeSet != null)
                routeSets.Add(routeSet);
        }

        return routeSets;
    }

    private static void EnsureTransformParent(Transform target, Transform parent, Scene scene)
    {
        if (target == null || target.parent == parent)
            return;

        Undo.SetTransformParent(target, parent, "Reparent boss reward reveal helper");
        if (parent == null && scene.IsValid())
            SceneManager.MoveGameObjectToScene(target.gameObject, scene);
        EditorUtility.SetDirty(target);
    }

    private void ValidateRouteCatalog()
    {
        RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(RouteCatalogPath);
        if (catalog == null)
        {
            AddResult(RouteCatalogPath, Severity.Error, "RunRouteCatalog asset was not found.", null, string.Empty);
            return;
        }

        if (catalog.NormalStageCount != RequiredNormalStageCount)
        {
            AddResult(
                RouteCatalogPath,
                Severity.Error,
                $"RunRouteCatalog.normalStageCount should be {RequiredNormalStageCount} so normal boss routes run three times before the final boss.",
                catalog,
                RouteCatalogPath);
        }

        if (catalog.AllowDuplicateNormalRoutes)
        {
            AddResult(
                RouteCatalogPath,
                Severity.Error,
                "RunRouteCatalog.allowDuplicateNormalRoutes should be disabled for the three distinct normal boss routes.",
                catalog,
                RouteCatalogPath);
        }

        ValidateNormalRouteSets(catalog);
        ValidateFinalRouteSet(catalog);
    }

    private void ValidateNormalRouteSets(RunRouteCatalogSO catalog)
    {
        IReadOnlyList<CorridorBossRouteSetSO> actualRoutes = catalog.NormalRouteSets;
        if (actualRoutes == null || actualRoutes.Count != TargetNormalRouteSetPaths.Length)
        {
            AddResult(
                RouteCatalogPath,
                Severity.Error,
                "RunRouteCatalog.normalRouteSets should contain exactly Shadow, Dragon, and Slime Queen route sets.",
                catalog,
                RouteCatalogPath);
        }

        for (int i = 0; i < TargetNormalRouteSetPaths.Length; i++)
        {
            CorridorBossRouteSetSO expectedRoute =
                AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(TargetNormalRouteSetPaths[i]);
            if (expectedRoute == null)
            {
                AddResult(
                    RouteCatalogPath,
                    Severity.Error,
                    $"Required normal RouteSet asset was not found: {TargetNormalRouteSetPaths[i]}.",
                    catalog,
                    RouteCatalogPath);
                continue;
            }

            if (!string.Equals(expectedRoute.BossSceneName, TargetNormalBossSceneNames[i], StringComparison.Ordinal))
            {
                AddResult(
                    TargetNormalRouteSetPaths[i],
                    Severity.Error,
                    $"RouteSet bossSceneName should be {TargetNormalBossSceneNames[i]}.",
                    expectedRoute,
                    TargetNormalRouteSetPaths[i]);
            }

            if (!ContainsRouteSet(actualRoutes, expectedRoute))
            {
                AddResult(
                    RouteCatalogPath,
                    Severity.Error,
                    $"RunRouteCatalog.normalRouteSets is missing the normal boss scene route '{TargetNormalBossSceneNames[i]}'.",
                    catalog,
                    RouteCatalogPath);
            }

            if (actualRoutes == null ||
                i >= actualRoutes.Count ||
                !ReferenceEquals(actualRoutes[i], expectedRoute))
            {
                AddResult(
                    RouteCatalogPath,
                    Severity.Error,
                    "RunRouteCatalog.normalRouteSets order should be Shadow, Dragon, then Slime Queen for deterministic direct scene Play tests.",
                    catalog,
                    RouteCatalogPath);
                break;
            }
        }
    }

    private void ValidateFinalRouteSet(RunRouteCatalogSO catalog)
    {
        CorridorBossRouteSetSO expectedFinal =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(DemonKingRouteSetPath);
        if (expectedFinal == null)
        {
            AddResult(
                RouteCatalogPath,
                Severity.Error,
                $"Required final RouteSet asset was not found: {DemonKingRouteSetPath}.",
                catalog,
                RouteCatalogPath);
            return;
        }

        if (!string.Equals(expectedFinal.BossSceneName, "LeeJunmo_Boss_DemonKing", StringComparison.Ordinal))
        {
            AddResult(
                DemonKingRouteSetPath,
                Severity.Error,
                "DemonKingRouteSet bossSceneName should be LeeJunmo_Boss_DemonKing.",
                expectedFinal,
                DemonKingRouteSetPath);
        }

        if (!ReferenceEquals(catalog.FinalRouteSet, expectedFinal))
        {
            AddResult(
                RouteCatalogPath,
                Severity.Error,
                "RunRouteCatalog.finalRouteSet should reference DemonkingRouteSet.",
                catalog,
                RouteCatalogPath);
        }
    }

    private static bool ContainsRouteSet(
        IReadOnlyList<CorridorBossRouteSetSO> routeSets,
        CorridorBossRouteSetSO target)
    {
        if (routeSets == null || target == null)
            return false;

        for (int i = 0; i < routeSets.Count; i++)
        {
            if (ReferenceEquals(routeSets[i], target))
                return true;
        }

        return false;
    }

    private void ValidateScene(string scenePath, Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            AddResult(scenePath, Severity.Error, "Scene could not be loaded for validation.", null, string.Empty);
            return;
        }

        List<BossEncounterEndDirector> directors = FindSceneComponents<BossEncounterEndDirector>(scene);
        List<BossBattleEndHandler> handlers = FindSceneComponents<BossBattleEndHandler>(scene);

        if (directors.Count == 0 && handlers.Count == 0)
        {
            AddResult(scenePath, Severity.Error, "No boss reward end owner was found.", null, string.Empty);
            return;
        }

        if (directors.Count == 0)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "No BossEncounterEndDirector was found. These target boss scenes should use BossEncounterEndDirector as the single canonical reward owner.",
                null,
                string.Empty);
        }

        if (directors.Count > 1)
        {
            for (int i = 0; i < directors.Count; i++)
            {
                BossEncounterEndDirector director = directors[i];
                AddResult(
                    scenePath,
                    Severity.Error,
                    "Multiple BossEncounterEndDirector components were found. Keep exactly one reward owner for the scene.",
                    director,
                    GetObjectPath(director.transform));
            }
        }

        if (handlers.Count > 0 && directors.Count > 0)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                BossBattleEndHandler handler = handlers[i];
                if (handler == null || !handler.enabled)
                    continue;

                AddResult(
                    scenePath,
                    Severity.Warning,
                    "BossBattleEndHandler is present alongside BossEncounterEndDirector. Prefer one active reward owner; for these target scenes, keep BossEncounterEndDirector as canonical and remove or disable the legacy handler after scene review.",
                    handler,
                    GetObjectPath(handler.transform));
            }
        }

        BossEncounterEndDirector firstDirector = directors.Count > 0 ? directors[0] : null;
        ValidateSceneChestReferenceSet(scenePath, scene, firstDirector, handlers);

        if (firstDirector != null)
            ValidateDirector(scenePath, firstDirector);

        if (handlers.Count > 0)
        {
            for (int i = 0; i < handlers.Count; i++)
                ValidateHandler(scenePath, handlers[i], firstDirector);
        }
    }

    private void ValidateSceneChestReferenceSet(
        string scenePath,
        Scene scene,
        BossEncounterEndDirector director,
        List<BossBattleEndHandler> handlers)
    {
        List<TreasureChest> chests = FindSceneComponents<TreasureChest>(scene);
        if (chests.Count <= 1)
            return;

        HashSet<TreasureChest> referencedChests = new();
        List<string> referencedPaths = new();
        AddDirectorChestReference(director, referencedChests, referencedPaths);

        if (handlers != null)
        {
            for (int i = 0; i < handlers.Count; i++)
                AddHandlerChestReference(handlers[i], referencedChests, referencedPaths);
        }

        List<string> unreferencedPaths = new();
        for (int i = 0; i < chests.Count; i++)
        {
            TreasureChest chest = chests[i];
            if (chest != null && !referencedChests.Contains(chest))
                unreferencedPaths.Add(GetObjectPath(chest.transform));
        }

        if (unreferencedPaths.Count == 0)
            return;

        AddResult(
            scenePath,
            Severity.Warning,
            $"Multiple TreasureChest instances were found. Reward owners activate only referenced chests. Referenced={FormatPathList(referencedPaths)}. Unreferenced={FormatPathList(unreferencedPaths)}.",
            director,
            director != null ? GetObjectPath(director.transform) : string.Empty);
    }

    private static void AddDirectorChestReference(
        BossEncounterEndDirector director,
        HashSet<TreasureChest> referencedChests,
        List<string> referencedPaths)
    {
        if (director == null)
            return;

        SerializedObject serializedDirector = new(director);
        AddChestReference(
            GetObjectReference<TreasureChest>(serializedDirector, "treasureChest"),
            referencedChests,
            referencedPaths);
    }

    private static void AddHandlerChestReference(
        BossBattleEndHandler handler,
        HashSet<TreasureChest> referencedChests,
        List<string> referencedPaths)
    {
        if (handler == null || !handler.enabled)
            return;

        SerializedObject serializedHandler = new(handler);
        AddChestReference(
            GetObjectReference<TreasureChest>(serializedHandler, "treasureChest"),
            referencedChests,
            referencedPaths);
    }

    private static void AddChestReference(
        TreasureChest chest,
        HashSet<TreasureChest> referencedChests,
        List<string> referencedPaths)
    {
        if (chest == null || !referencedChests.Add(chest))
            return;

        referencedPaths.Add(GetObjectPath(chest.transform));
    }

    private static string FormatPathList(List<string> paths)
    {
        if (paths == null || paths.Count == 0)
            return "None";

        return string.Join(", ", paths);
    }

    private void ValidateDirector(string scenePath, BossEncounterEndDirector director)
    {
        if (director == null)
            return;

        SerializedObject serializedDirector = new SerializedObject(director);
        Object clearCondition = GetObjectReference<Object>(serializedDirector, "clearCondition");
        TreasureChest chest = GetObjectReference<TreasureChest>(serializedDirector, "treasureChest");
        GameObject portal = GetObjectReference<GameObject>(serializedDirector, "exitPortal");

        if (clearCondition == null)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "BossEncounterEndDirector.clearCondition is missing.",
                director,
                GetObjectPath(director.transform));
        }

        if (!GetBool(serializedDirector, "suppressManagedBossAutomaticRewards", defaultValue: false))
        {
            AddResult(
                scenePath,
                Severity.Error,
                "BossEncounterEndDirector.suppressManagedBossAutomaticRewards should be enabled so automatic BossControllerBase reward-ready does not duplicate this director path.",
                director,
                GetObjectPath(director.transform));
        }

        if (!GetBool(serializedDirector, "hideAuthoredObjectsOnStart", defaultValue: false))
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "BossEncounterEndDirector.hideAuthoredObjectsOnStart should stay enabled unless the chest and portal are already guaranteed inactive before runtime.",
                director,
                GetObjectPath(director.transform));
        }

        ValidateChest(scenePath, "BossEncounterEndDirector.treasureChest", chest);
        ValidatePortal(scenePath, "BossEncounterEndDirector.exitPortal", portal);
    }

    private void ValidateHandler(
        string scenePath,
        BossBattleEndHandler handler,
        BossEncounterEndDirector canonicalDirector)
    {
        if (handler == null)
            return;

        SerializedObject serializedHandler = new SerializedObject(handler);
        TreasureChest handlerChest = GetObjectReference<TreasureChest>(serializedHandler, "treasureChest");
        GameObject handlerPortal = GetObjectReference<GameObject>(serializedHandler, "exitPortal");

        if (GetObjectReference<Object>(serializedHandler, "boss") == null)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "BossBattleEndHandler.boss is missing.",
                handler,
                GetObjectPath(handler.transform));
        }

        if (!GetBool(serializedHandler, "hideAuthoredObjectsOnStart", defaultValue: false))
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "BossBattleEndHandler.hideAuthoredObjectsOnStart is disabled.",
                handler,
                GetObjectPath(handler.transform));
        }

        if (canonicalDirector != null)
        {
            SerializedObject serializedDirector = new SerializedObject(canonicalDirector);
            TreasureChest directorChest = GetObjectReference<TreasureChest>(serializedDirector, "treasureChest");
            GameObject directorPortal = GetObjectReference<GameObject>(serializedDirector, "exitPortal");

            if (handlerChest != directorChest)
            {
                AddResult(
                    scenePath,
                    Severity.Error,
                    "BossBattleEndHandler.treasureChest does not match BossEncounterEndDirector.treasureChest.",
                    handler,
                    GetObjectPath(handler.transform));
            }

            if (handlerPortal != directorPortal)
            {
                AddResult(
                    scenePath,
                    Severity.Error,
                    "BossBattleEndHandler.exitPortal does not match BossEncounterEndDirector.exitPortal.",
                    handler,
                    GetObjectPath(handler.transform));
            }

            return;
        }

        ValidateChest(scenePath, "BossBattleEndHandler.treasureChest", handlerChest);
        ValidatePortal(scenePath, "BossBattleEndHandler.exitPortal", handlerPortal);
    }

    private void ValidateChest(string scenePath, string ownerLabel, TreasureChest chest)
    {
        if (chest == null)
        {
            AddResult(scenePath, Severity.Error, $"{ownerLabel} is missing.", null, string.Empty);
            return;
        }

        if (chest.gameObject.activeSelf)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                $"{ownerLabel} is active in the scene. Keep the authored reward chest inactive and let the end owner activate it after boss clear.",
                chest,
                GetObjectPath(chest.transform));
        }

        BossRewardObjectRevealPresentation stalePortalReveal = ResolveReveal(chest.gameObject);
        if (stalePortalReveal != null)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                $"{ownerLabel} still has BossRewardObjectRevealPresentation. Chest reveal dust now belongs to TreasureChest; rerun Apply Target Scenes to remove the stale portal reveal component from the chest.",
                stalePortalReveal,
                GetObjectPath(stalePortalReveal.transform));
        }

        ValidateChestRewardReveal(scenePath, chest);
    }

    private void ValidatePortal(string scenePath, string ownerLabel, GameObject portalRoot)
    {
        if (portalRoot == null)
        {
            AddResult(scenePath, Severity.Error, $"{ownerLabel} is missing.", null, string.Empty);
            return;
        }

        if (portalRoot.activeSelf)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                $"{ownerLabel} is active in the scene. Keep the authored reward portal inactive and let the end owner activate it after boss clear.",
                portalRoot,
                GetObjectPath(portalRoot.transform));
        }

        ScenePortal[] portals = portalRoot.GetComponentsInChildren<ScenePortal>(true);
        if (portals.Length == 0)
        {
            AddResult(
                scenePath,
                Severity.Error,
                $"{ownerLabel} has no ScenePortal component in itself or children.",
                portalRoot,
                GetObjectPath(portalRoot.transform));
        }

        for (int i = 0; i < portals.Length; i++)
            ValidateScenePortal(scenePath, portals[i]);

        BossRewardObjectRevealPresentation reveal = ResolveReveal(portalRoot);
        if (reveal == null)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                $"{ownerLabel} has no BossRewardObjectRevealPresentation. The gate will activate immediately without mask/dust reveal.",
                portalRoot,
                GetObjectPath(portalRoot.transform));
            return;
        }

        ValidateGateReveal(scenePath, reveal);
    }

    private void ValidateScenePortal(string scenePath, ScenePortal portal)
    {
        if (portal == null)
            return;

        if (portal.PortalTransitionType == TransitionType.HubToRunStart)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "Boss reward portal must not use HubToRunStart.",
                portal,
                GetObjectPath(portal.transform));
        }

        if (portal.PortalTransitionType == TransitionType.None)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Boss reward portal uses TransitionType.None. Prefer explicit BossToCorridor or ReturnToHubAfterRun for these authored boss reward scenes.",
                portal,
                GetObjectPath(portal.transform));
        }

        if (portal.StartRunRouteCatalog != null)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "Boss reward portal should not carry StartRunRouteCatalog; hub start portals own that catalog.",
                portal,
                GetObjectPath(portal.transform));
        }
    }

    private void ValidateChestRewardReveal(string scenePath, TreasureChest chest)
    {
        if (chest == null)
            return;

        SerializedObject serializedChest = new SerializedObject(chest);
        Object dustParticle = GetObjectReference<Object>(serializedChest, "rewardRevealDustParticle");

        if (dustParticle == null)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "TreasureChest reward reveal dust particle is missing. The chest will still activate, but no dust reveal will play.",
                chest,
                GetObjectPath(chest.transform));
        }
    }

    private void ValidateGateReveal(string scenePath, BossRewardObjectRevealPresentation reveal)
    {
        SerializedObject serializedReveal = new SerializedObject(reveal);
        Transform revealRoot = GetObjectReference<Transform>(serializedReveal, "revealRoot");
        Object revealMask = GetObjectReference<Object>(serializedReveal, "revealMask");
        int rendererCount = CountObjectReferences(serializedReveal, "maskedRenderers");
        int loopDustCount = CountObjectReferences(serializedReveal, "loopDustParticles");
        int colliderCount = CountObjectReferences(serializedReveal, "collidersToDisableDuringReveal");
        float duration = GetFloat(serializedReveal, "revealDurationSeconds", defaultValue: 0f);
        Vector3 startOffset = GetVector3(serializedReveal, "startLocalOffset", Vector3.zero);
        bool isolatesGlobalVisionMasks =
            GetBool(serializedReveal, "isolateGlobalVisionMasksDuringReveal", defaultValue: false);
        bool hasLoopShake = GetBool(serializedReveal, "playLoopCameraShake", defaultValue: false);
        bool hasCompleteShake = GetBool(serializedReveal, "playCompleteCameraShake", defaultValue: false);
        bool hasRootShake = GetBool(serializedReveal, "shakeRevealRootDuringReveal", defaultValue: false);

        if (revealRoot == null)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "Gate reveal requires revealRoot. Assign the gate visual child that should rise from below.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (rendererCount == 0)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "Gate reveal requires maskedRenderers so SpriteMask interaction can be applied during reveal.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (loopDustCount == 0)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Gate reveal should have loopDustParticles assigned for ground dust during the rise.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (colliderCount == 0)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Gate reveal has no collidersToDisableDuringReveal. Portal interaction can be available before the gate reveal finishes.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        SpriteMask spriteMask = revealMask as SpriteMask;
        if (spriteMask != null &&
            revealRoot != null &&
            spriteMask.transform != null &&
            spriteMask.transform.IsChildOf(revealRoot))
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Gate revealMask is parented under revealRoot. Runtime will clone it outside the moving root for playback, but rerun Apply Target Scenes to move the authored mask outside the moving root.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (!isolatesGlobalVisionMasks && FindSceneComponents<GlobalVisionMaskController>(reveal.gameObject.scene).Count > 0)
        {
            AddResult(
                scenePath,
                Severity.Error,
                "Gate reveal is in a GlobalVisionMaskRoot scene. Enable isolateGlobalVisionMasksDuringReveal so the reward portal is not clipped by the player vision mask.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (duration <= 0f)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Gate reveal duration is zero. Use a positive revealDurationSeconds for a visible rise.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (startOffset.y >= 0f)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Gate reveal startLocalOffset.y should be negative so the gate starts below its final position.",
                reveal,
                GetObjectPath(reveal.transform));
        }

        if (!hasLoopShake || !hasCompleteShake || !hasRootShake)
        {
            AddResult(
                scenePath,
                Severity.Warning,
                "Gate reveal shake settings are incomplete. Run Apply Target Scenes to enable loop camera shake, completion camera shake, and portal root tremble.",
                reveal,
                GetObjectPath(reveal.transform));
        }
    }

    private static BossRewardObjectRevealPresentation ResolveReveal(GameObject root)
    {
        if (root == null)
            return null;

        BossRewardObjectRevealPresentation reveal = root.GetComponent<BossRewardObjectRevealPresentation>();
        if (reveal == null)
            reveal = root.GetComponentInChildren<BossRewardObjectRevealPresentation>(true);

        return reveal;
    }

    private static List<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> components = new();
        if (!scene.IsValid() || !scene.isLoaded)
            return components;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            T[] found = root.GetComponentsInChildren<T>(true);
            for (int j = 0; j < found.Length; j++)
            {
                T component = found[j];
                if (component != null && component.gameObject.scene == scene)
                    components.Add(component);
            }
        }

        return components;
    }

    private static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName)
        where T : Object
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static bool GetBool(SerializedObject serializedObject, string propertyName, bool defaultValue)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        return property != null ? property.boolValue : defaultValue;
    }

    private static float GetFloat(SerializedObject serializedObject, string propertyName, float defaultValue)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        return property != null ? property.floatValue : defaultValue;
    }

    private static Vector3 GetVector3(SerializedObject serializedObject, string propertyName, Vector3 defaultValue)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        return property != null ? property.vector3Value : defaultValue;
    }

    private static bool AssignBool(
        SerializedObject serializedObject,
        string propertyName,
        bool value,
        ApplyStats stats = null)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.boolValue == value)
            return false;

        property.boolValue = value;
        if (stats != null)
            stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignInt(
        SerializedObject serializedObject,
        string propertyName,
        int value,
        ApplyStats stats = null)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.intValue == value)
            return false;

        property.intValue = value;
        if (stats != null)
            stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignString(
        SerializedObject serializedObject,
        string propertyName,
        string value,
        ApplyStats stats = null)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || string.Equals(property.stringValue, value, StringComparison.Ordinal))
            return false;

        property.stringValue = value;
        if (stats != null)
            stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignFloatIfInvalid(
        SerializedObject serializedObject,
        string propertyName,
        float value,
        float minimumValue)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.floatValue >= minimumValue)
            return false;

        property.floatValue = value;
        return true;
    }

    private static bool AssignVector3IfInvalid(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.vector3Value.y < 0f)
            return false;

        property.vector3Value = value;
        return true;
    }

    private static bool AssignObjectReferenceIfMissing(
        SerializedObject serializedObject,
        string propertyName,
        Object value,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.objectReferenceValue != null || value == null)
            return false;

        property.objectReferenceValue = value;
        stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        Object value,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.objectReferenceValue == value || value == null)
            return false;

        property.objectReferenceValue = value;
        stats.ReferencesAssigned++;
        return true;
    }

    private static bool ClearObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.objectReferenceValue == null)
            return false;

        property.objectReferenceValue = null;
        stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignVector3(
        SerializedObject serializedObject,
        string propertyName,
        Vector3 value,
        ApplyStats stats = null)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || property.vector3Value == value)
            return false;

        property.vector3Value = value;
        if (stats != null)
            stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value,
        ApplyStats stats = null)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || Mathf.Approximately(property.floatValue, value))
            return false;

        property.floatValue = value;
        if (stats != null)
            stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignCameraShakeHook(
        SerializedObject serializedObject,
        string propertyName,
        CameraShakeHook value,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null)
            return false;

        bool changed = false;
        changed |= AssignRelativeFloat(property, "amplitude", value.amplitude);
        changed |= AssignRelativeFloat(property, "amplitudeMultiplier", value.amplitudeMultiplier);
        changed |= AssignRelativeFloat(property, "maxAmplitude", value.maxAmplitude);
        changed |= AssignRelativeFloat(property, "minIntervalSeconds", value.minIntervalSeconds);
        changed |= AssignRelativeEnum(property, "directionMode", (int)value.directionMode);
        changed |= AssignRelativeVector3(property, "customDirection", value.customDirection);
        changed |= AssignRelativeBool(property, "ignoreScreenShakeSetting", value.ignoreScreenShakeSetting);
        if (changed && stats != null)
            stats.ReferencesAssigned++;

        return changed;
    }

    private static bool AssignRelativeFloat(SerializedProperty parent, string propertyName, float value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;
        if (property == null || Mathf.Approximately(property.floatValue, value))
            return false;

        property.floatValue = value;
        return true;
    }

    private static bool AssignRelativeEnum(SerializedProperty parent, string propertyName, int value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;
        if (property == null || property.enumValueIndex == value)
            return false;

        property.enumValueIndex = value;
        return true;
    }

    private static bool AssignRelativeVector3(SerializedProperty parent, string propertyName, Vector3 value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;
        if (property == null || property.vector3Value == value)
            return false;

        property.vector3Value = value;
        return true;
    }

    private static bool AssignRelativeBool(SerializedProperty parent, string propertyName, bool value)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;
        if (property == null || property.boolValue == value)
            return false;

        property.boolValue = value;
        return true;
    }

    private static bool AssignObjectArray(
        SerializedObject serializedObject,
        string propertyName,
        IEnumerable<Object> values,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || !property.isArray)
            return false;

        List<Object> validValues = new();
        if (values != null)
        {
            foreach (Object value in values)
            {
                if (value != null)
                    validValues.Add(value);
            }
        }

        if (validValues.Count == 0)
            return false;

        if (ArrayMatches(property, validValues))
            return false;

        property.arraySize = validValues.Count;
        for (int i = 0; i < validValues.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = validValues[i];
        }

        stats.ReferencesAssigned++;
        return true;
    }

    private static bool ArrayMatches(SerializedProperty property, List<Object> values)
    {
        if (property == null || values == null || property.arraySize != values.Count)
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element == null || element.objectReferenceValue != values[i])
                return false;
        }

        return true;
    }

    private static bool AssignObjectArrayIfEmpty(
        SerializedObject serializedObject,
        string propertyName,
        IEnumerable<Object> values,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || !property.isArray || CountObjectReferences(serializedObject, propertyName) > 0)
            return false;

        List<Object> validValues = new();
        if (values != null)
        {
            foreach (Object value in values)
            {
                if (value != null)
                    validValues.Add(value);
            }
        }

        if (validValues.Count == 0)
            return false;

        property.arraySize = validValues.Count;
        for (int i = 0; i < validValues.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = validValues[i];
        }

        stats.ReferencesAssigned++;
        return true;
    }

    private static bool AssignObjectArrayIfEmptyOrContainsPersistent(
        SerializedObject serializedObject,
        string propertyName,
        IEnumerable<Object> values,
        ApplyStats stats)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || !property.isArray)
            return false;

        if (CountObjectReferences(serializedObject, propertyName) > 0 &&
            CountPersistentObjectReferences(serializedObject, propertyName) == 0)
        {
            return false;
        }

        List<Object> validValues = new();
        if (values != null)
        {
            foreach (Object value in values)
            {
                if (value != null)
                    validValues.Add(value);
            }
        }

        if (validValues.Count == 0)
            return false;

        property.arraySize = validValues.Count;
        for (int i = 0; i < validValues.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = validValues[i];
        }

        stats.ReferencesAssigned++;
        return true;
    }

    private static bool ClearObjectArrayIfNoObjectReference(
        SerializedObject serializedObject,
        string requiredReferenceName,
        string arrayPropertyName,
        ApplyStats stats)
    {
        SerializedProperty requiredReference = serializedObject != null
            ? serializedObject.FindProperty(requiredReferenceName)
            : null;
        SerializedProperty arrayProperty = serializedObject != null
            ? serializedObject.FindProperty(arrayPropertyName)
            : null;

        if (requiredReference == null ||
            requiredReference.objectReferenceValue != null ||
            arrayProperty == null ||
            !arrayProperty.isArray ||
            arrayProperty.arraySize == 0)
        {
            return false;
        }

        arrayProperty.arraySize = 0;
        stats.ReferencesAssigned++;
        return true;
    }

    private static GameObject FindChildByName(Transform parent, string objectName)
    {
        if (parent == null)
            return GameObject.Find(objectName);

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child.gameObject;
        }

        return null;
    }

    private static int CountObjectReferences(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || !property.isArray)
            return 0;

        int count = 0;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element != null && element.objectReferenceValue != null)
                count++;
        }

        return count;
    }

    private static int CountPersistentObjectReferences(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject != null ? serializedObject.FindProperty(propertyName) : null;
        if (property == null || !property.isArray)
            return 0;

        int count = 0;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            Object value = element != null ? element.objectReferenceValue : null;
            if (value != null && EditorUtility.IsPersistent(value))
                count++;
        }

        return count;
    }

    private void AddPassResultIfEmpty(string scope)
    {
        if (results.Count == 0)
            AddResult(scope, Severity.Info, "No boss reward reveal authoring issues found.", null, string.Empty);
    }

    private void AddResult(
        string scenePath,
        Severity severity,
        string message,
        Object context,
        string objectPath)
    {
        results.Add(new ValidationResult
        {
            ScenePath = string.IsNullOrEmpty(scenePath) ? "Scene" : scenePath,
            SeverityLevel = severity,
            Message = message,
            Context = context,
            ObjectPath = objectPath ?? string.Empty
        });
    }

    private static string GetObjectPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
