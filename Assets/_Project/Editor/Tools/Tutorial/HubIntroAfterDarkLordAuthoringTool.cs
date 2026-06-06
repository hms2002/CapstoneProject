#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HubIntroAfterDarkLordAuthoringTool
{
    private const string TargetSceneName = "ProtoTypeHub";
    private const string RootName = "__HubIntroAfterDarkLordSequence";
    private const string NpcFocusName = "HubIntro_NpcFocusTarget";
    private const string JunkFocusName = "HubIntro_JunkFocusTarget";
    private const string TrainingDummyFocusName = "HubIntro_TrainingDummyFocusTarget";
    private const string GateFocusName = "HubIntro_GateFocusTarget";
    private const int AfterDarkLordTutorialUntilSeenEnumIndex = 1;
    private const int DraftNpcId = 1002;
    private const string DraftNpcDataPath = "Assets/_Project/Data/Dialogue/NPC/MSUpgradeNpc.asset";
    private const string DraftInkFolder = "Assets/_Project/Data/Dialogue/Ink/HubIntroAfterDarkLord";
    private const float DefaultOpeningSpeechBubbleMinTextWidth = 160f;
    private const float DefaultOpeningSpeechBubbleMaxTextWidth = 360f;
    private const float DefaultOpeningSpeechBubbleMinTextHeight = 32f;
    private const string DefaultOpeningSpeechText = "거기, 바닥이 마음에 들더라도 슬슬 일어나. 아직 죽은 건 아니잖아?";

    private static readonly DraftDialogueAsset[] DraftDialogueAssets =
    {
        new(
            "Junk",
            "HubIntro_Junk",
            "HUB_INTRO_JUNK",
            "저쪽은 잡동사니 더미야. 겉보기엔 폐품뿐이지만 쓸 만한 부품이나 재료가 섞여 있을 때가 있어."),
        new(
            "Training Dummy",
            "HubIntro_TrainingDummy",
            "HUB_INTRO_TRAINING_DUMMY",
            "저건 훈련 허수아비야. 새 무기나 기술이 생겼다면 먼저 저기서 시험해. 실전에서 확인하다가 쓰러지는 것보단 싸게 먹히니까."),
        new(
            "Gate",
            "HubIntro_Gate",
            "HUB_INTRO_GATE",
            "저 문이 밖으로 나가는 게이트야. 준비가 끝났다면 저기로 나가면 돼. 나간 뒤에 버티는 건 장비가 아니라 네 판단이야."),
        new(
            "Final",
            "HubIntro_Final",
            "HUB_INTRO_FINAL",
            "뭐가 널 여기까지 떨어뜨렸는지는 묻지 않을게. 살아남고 싶으면 이곳에서 정비하고 필요한 건 강화해. 준비가 됐다면 게이트로 가."),
    };

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Apply Default Authoring To Active Scene")]
    public static void ApplyDefaultAuthoringToActiveScene()
    {
        if (!CanEditActiveScene(out Scene scene))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Hub intro after DarkLord authoring");

        SpeechBubbleComponent speechBubble = ResolveSingleSceneSpeechBubble(scene);
        Vector3 rootPosition = ResolveRootPosition(scene, speechBubble);

        Transform root = GetOrCreateRoot(scene, rootPosition);
        Transform npcFocus = GetOrCreateChild(root, NpcFocusName, ResolveNpcFocusPosition(scene, speechBubble, rootPosition));
        Transform junkFocus = GetOrCreateChild(root, JunkFocusName, ResolveNamedFocusPosition(scene, rootPosition + new Vector3(-4f, 0f, 0f), "junk", "misc", "stash", "crate", "box"));
        Transform trainingDummyFocus = GetOrCreateChild(root, TrainingDummyFocusName, ResolveNamedFocusPosition(scene, rootPosition + new Vector3(0f, -3f, 0f), "dummy", "training", "target"));
        Transform gateFocus = GetOrCreateChild(root, GateFocusName, ResolveNamedFocusPosition(scene, rootPosition + new Vector3(4f, 0f, 0f), "gate", "portal", "door"));

        HubIntroAfterDarkLordSequence sequence = GetOrAdd<HubIntroAfterDarkLordSequence>(root.gameObject);
        ConfigureHubIntroSequence(
            sequence,
            speechBubble,
            npcFocus,
            junkFocus,
            trainingDummyFocus,
            gateFocus);

        int configuredPlayerPresentations = ConfigurePlayerHubSpawnPresentations(scene);

        Selection.activeObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        if (configuredPlayerPresentations == 0)
        {
            Debug.LogWarning(
                "[HubIntroAuthoring] Hub intro scene authoring was applied, but no PlayerHubSpawnPresentation2D was found or added through PlayerSpawner.playerPrefab. Validate the active scene and wire the player prefab manually if needed.");
        }

        Debug.Log("[HubIntroAuthoring] Applied Hub intro after DarkLord authoring to the active scene. Review focus target positions and assign NPCData/Ink assets in the Inspector.");
        ValidateActiveScene();
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Create Or Wire Draft Dialogue Assets")]
    public static void CreateOrWireDraftDialogueAssets()
    {
        if (!CanEditActiveScene(out Scene scene))
            return;

        EnsureDraftDialogueAssets();
        AssetDatabase.Refresh();

        HubIntroAfterDarkLordSequence sequence = FindSceneComponent<HubIntroAfterDarkLordSequence>(scene);
        if (sequence == null)
        {
            Debug.LogError("[HubIntroAuthoring] HubIntroAfterDarkLordSequence is missing. Run Apply Default Authoring first.");
            return;
        }

        Undo.RecordObject(sequence, "Wire Hub intro draft dialogue assets");
        bool changed = WireDraftDialogueAssets(sequence);
        if (changed)
        {
            EditorUtility.SetDirty(sequence);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log("[HubIntroAuthoring] Created or wired Hub intro draft dialogue assets. Replace draft text before final content sign-off.");
        ValidateActiveScene();
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        ValidationReport report = new("Hub intro after DarkLord authoring validation");

        if (!scene.IsValid() || !scene.isLoaded)
        {
            report.Error("Active scene is not loaded.");
            report.Log();
            return;
        }

        if (!string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
            report.Error($"Active scene must be {TargetSceneName}; current scene is {scene.name}.");

        List<HubIntroAfterDarkLordSequence> sequences = FindSceneComponents<HubIntroAfterDarkLordSequence>(scene);
        if (sequences.Count == 0)
        {
            report.Error("HubIntroAfterDarkLordSequence is missing.");
        }
        else
        {
            if (sequences.Count > 1)
                report.Error($"Expected exactly one HubIntroAfterDarkLordSequence, found {sequences.Count}.");

            ValidateHubIntroSequence(report, sequences[0]);
        }

        ValidatePlayerSpawnPresentation(report, scene);
        report.Log();
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Select Authoring Root")]
    public static void SelectAuthoringRoot()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = FindTransformByName(scene, RootName);
        if (root == null)
        {
            Debug.LogWarning("[HubIntroAuthoring] Authoring root was not found in the active scene.");
            return;
        }

        Selection.activeObject = root.gameObject;
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Apply Default Authoring To Active Scene", true)]
    private static bool CanApplyDefaultAuthoringToActiveScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Validate Active Scene", true)]
    private static bool CanValidateActiveScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Create Or Wire Draft Dialogue Assets", true)]
    private static bool CanCreateOrWireDraftDialogueAssets()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/Tutorial/Hub Intro After DarkLord/Select Authoring Root", true)]
    private static bool CanSelectAuthoringRoot()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool CanEditActiveScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[HubIntroAuthoring] Cannot edit Hub intro authoring while Play Mode is active or changing.");
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[HubIntroAuthoring] Active scene is not loaded.");
            return false;
        }

        if (!string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
        {
            Debug.LogError($"[HubIntroAuthoring] Active scene must be {TargetSceneName}; current scene is {scene.name}.");
            return false;
        }

        return true;
    }

    private static Transform GetOrCreateRoot(Scene scene, Vector3 worldPosition)
    {
        Transform root = FindTransformByName(scene, RootName);
        if (root != null)
            return root;

        GameObject rootObject = new(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Hub intro authoring root");
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        rootObject.transform.position = worldPosition;
        return rootObject.transform;
    }

    private static Transform GetOrCreateChild(Transform parent, string name, Vector3 worldPosition)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
            return child;

        GameObject childObject = new(name);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {name}");
        child = childObject.transform;
        child.SetParent(parent, worldPositionStays: false);
        child.position = worldPosition;
        return child;
    }

    private static void ConfigureHubIntroSequence(
        HubIntroAfterDarkLordSequence sequence,
        SpeechBubbleComponent speechBubble,
        Transform npcFocus,
        Transform junkFocus,
        Transform trainingDummyFocus,
        Transform gateFocus)
    {
        Undo.RecordObject(sequence, "Configure Hub intro sequence");

        SerializedObject so = new(sequence);
        SetBool(so, "playOnStart", true);
        SetBool(so, "playOnlyOncePerScene", true);
        SetString(so, "hubSceneName", TargetSceneName);
        SetBool(so, "waitForHubSpawnPresentation", true);
        SetString(so, "darkLordTutorialCompletionId", HubIntroProgressGate.DefaultDarkLordTutorialCompletionId);
        SetString(so, "hubIntroSeenId", HubIntroProgressGate.DefaultHubIntroSeenId);
        SetBool(so, "allowEditorBypassTutorialCompletion", true);
        SetBool(so, "markSeenOnComplete", true);
        SetBool(so, "hideGameplayHud", true);
        SetBool(so, "blockExternalInput", true);
        SetBool(so, "lockPlayerControls", true);
        SetBool(so, "useLetterbox", true);
        SetObjectIfNull(so, "npcSpeechBubble", speechBubble);
        SetObject(so, "npcFocusTarget", npcFocus);
        SetStringIfEmpty(so, "openingSpeechText", DefaultOpeningSpeechText);
        SetFloatIfNotPositive(so, "openingSpeechBubbleMinTextWidth", DefaultOpeningSpeechBubbleMinTextWidth);
        SetFloatIfNotPositive(so, "openingSpeechBubbleMaxTextWidth", DefaultOpeningSpeechBubbleMaxTextWidth);
        SetFloatIfNotPositive(so, "openingSpeechBubbleMinTextHeight", DefaultOpeningSpeechBubbleMinTextHeight);
        SetObject(so, "finalNpcFocusTarget", npcFocus);
        SetBool(so, "finalDialogueUsesFastSilhouette", true);
        SetFloat(so, "finalSilhouetteFadeSeconds", 0.25f);
        SetString(so, "finalSilhouettePosition", "center");
        SetBool(so, "finalDialogueBoxOnly", true);
        SetFloat(so, "defaultFocusOrthographicSize", 4f);

        SerializedProperty focusSteps = so.FindProperty("focusSteps");
        if (focusSteps != null && focusSteps.isArray)
        {
            focusSteps.arraySize = 3;
            ConfigureFocusStep(focusSteps.GetArrayElementAtIndex(0), "Junk", junkFocus);
            ConfigureFocusStep(focusSteps.GetArrayElementAtIndex(1), "Training Dummy", trainingDummyFocus);
            ConfigureFocusStep(focusSteps.GetArrayElementAtIndex(2), "Gate", gateFocus);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sequence);
        WireDraftDialogueAssets(sequence);
    }

    private static void ConfigureFocusStep(SerializedProperty step, string label, Transform focusTarget)
    {
        if (step == null)
            return;

        SetRelativeString(step, "label", label);
        SetRelativeObject(step, "focusTarget", focusTarget);
        SetRelativeFloat(step, "focusWaitSeconds", 0.35f);
        SetRelativeBool(step, "overrideOrthographicSize", false);
        SetRelativeFloat(step, "orthographicSize", 4f);
    }

    private static int ConfigurePlayerHubSpawnPresentations(Scene scene)
    {
        List<PlayerHubSpawnPresentation2D> targets = ResolvePlayerHubSpawnPresentations(scene, createMissingOnSpawnerPrefab: true);
        for (int i = 0; i < targets.Count; i++)
            ConfigurePlayerHubSpawnPresentation(targets[i]);

        return targets.Count;
    }

    private static void ConfigurePlayerHubSpawnPresentation(PlayerHubSpawnPresentation2D presentation)
    {
        if (presentation == null)
            return;

        Undo.RecordObject(presentation, "Configure Hub spawn presentation gate");

        SerializedObject so = new(presentation);
        SetString(so, "hubSceneName", TargetSceneName);
        SetBool(so, "playOnHubSpawn", true);
        SetEnumIndex(so, "playCondition", AfterDarkLordTutorialUntilSeenEnumIndex);
        SetString(so, "darkLordTutorialCompletionId", HubIntroProgressGate.DefaultDarkLordTutorialCompletionId);
        SetString(so, "hubIntroSeenId", HubIntroProgressGate.DefaultHubIntroSeenId);
        SetBool(so, "allowEditorBypassTutorialCompletion", true);
        SetBool(so, "autoWakeWithoutInput", true);
        SetFloat(so, "autoWakeDelaySeconds", 2f);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(presentation);
        if (EditorUtility.IsPersistent(presentation))
            EditorUtility.SetDirty(presentation.gameObject);
        else
            PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
    }

    private static void EnsureDraftDialogueAssets()
    {
        EnsureAssetFolder("Assets/_Project/Data/Dialogue", "Ink");
        EnsureAssetFolder("Assets/_Project/Data/Dialogue/Ink", "HubIntroAfterDarkLord");

        for (int i = 0; i < DraftDialogueAssets.Length; i++)
            EnsureDraftInkAndJson(DraftDialogueAssets[i]);

        ValidateDraftNpcDataExists();
    }

    private static void EnsureDraftInkAndJson(DraftDialogueAsset asset)
    {
        if (asset == null)
            return;

        WriteAssetTextIfMissing(asset.InkPath, BuildDraftInk(asset));
        WriteAssetTextIfMissing(asset.JsonPath, BuildDraftInkJson(asset));
        AssetDatabase.ImportAsset(asset.InkPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(asset.JsonPath, ImportAssetOptions.ForceUpdate);
    }

    private static void ValidateDraftNpcDataExists()
    {
        NPCData npcData = AssetDatabase.LoadAssetAtPath<NPCData>(DraftNpcDataPath);
        if (npcData != null)
            return;

        Debug.LogError($"[HubIntroAuthoring] MSUpgradeNpc NPCData was not found at {DraftNpcDataPath}.");
    }

    private static bool WireDraftDialogueAssets(HubIntroAfterDarkLordSequence sequence)
    {
        if (sequence == null)
            return false;

        bool changed = false;
        SerializedObject so = new(sequence);
        NPCData npcData = AssetDatabase.LoadAssetAtPath<NPCData>(DraftNpcDataPath);
        changed |= SetObjectIfNullAndReport(so, "narratorNpcData", npcData);
        changed |= SetStringIfEmptyAndReport(so, "openingSpeechText", DefaultOpeningSpeechText);
        changed |= SetFloatIfNotPositiveAndReport(so, "openingSpeechBubbleMinTextWidth", DefaultOpeningSpeechBubbleMinTextWidth);
        changed |= SetFloatIfNotPositiveAndReport(so, "openingSpeechBubbleMaxTextWidth", DefaultOpeningSpeechBubbleMaxTextWidth);
        changed |= SetFloatIfNotPositiveAndReport(so, "openingSpeechBubbleMinTextHeight", DefaultOpeningSpeechBubbleMinTextHeight);

        SerializedProperty focusSteps = so.FindProperty("focusSteps");
        if (focusSteps != null && focusSteps.isArray)
        {
            if (focusSteps.arraySize < 3)
                focusSteps.arraySize = 3;

            changed |= WireDraftFocusStep(focusSteps.GetArrayElementAtIndex(0), "Junk");
            changed |= WireDraftFocusStep(focusSteps.GetArrayElementAtIndex(1), "Training Dummy");
            changed |= WireDraftFocusStep(focusSteps.GetArrayElementAtIndex(2), "Gate");
        }

        DraftDialogueAsset finalAsset = FindDraftDialogueAsset("Final");
        changed |= SetObjectIfNullAndReport(so, "finalDialogueInk", LoadDraftJson("Final"));
        changed |= SetStringIfEmptyAndReport(so, "finalDialogueStartPath", finalAsset?.Knot);
        if (changed)
            so.ApplyModifiedProperties();

        return changed;
    }

    private static bool WireDraftFocusStep(SerializedProperty step, string label)
    {
        DraftDialogueAsset asset = FindDraftDialogueAsset(label);
        bool changed = false;
        changed |= SetRelativeObjectIfNullAndReport(step, "dialogueInk", LoadDraftJson(label));
        changed |= SetRelativeStringIfEmptyAndReport(step, "dialogueStartPath", asset?.Knot);
        return changed;
    }

    private static TextAsset LoadDraftJson(string label)
    {
        DraftDialogueAsset asset = FindDraftDialogueAsset(label);
        return asset != null ? AssetDatabase.LoadAssetAtPath<TextAsset>(asset.JsonPath) : null;
    }

    private static DraftDialogueAsset FindDraftDialogueAsset(string label)
    {
        for (int i = 0; i < DraftDialogueAssets.Length; i++)
        {
            DraftDialogueAsset asset = DraftDialogueAssets[i];
            if (asset != null && string.Equals(asset.Label, label, StringComparison.Ordinal))
                return asset;
        }

        return null;
    }

    private static DraftDialogueAsset FindDraftDialogueAssetByJson(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        for (int i = 0; i < DraftDialogueAssets.Length; i++)
        {
            DraftDialogueAsset asset = DraftDialogueAssets[i];
            if (asset != null && string.Equals(asset.JsonPath, assetPath, StringComparison.OrdinalIgnoreCase))
                return asset;
        }

        return null;
    }

    private static string BuildDraftInk(DraftDialogueAsset asset)
    {
        return $"=== {asset.Knot} ===\n# speaker: {DraftNpcId}\n{asset.Line}\n-> END\n";
    }

    private static string BuildDraftInkJson(DraftDialogueAsset asset)
    {
        string knot = EscapeJson(asset.Knot);
        string line = EscapeJson(asset.Line);
        string lineContent = "[\"#\",\"^speaker: " +
                             DraftNpcId +
                             "\",\"/#\",\"^" +
                             line +
                             "\",\"\\n\",\"end\",{\"#f\":1}]";
        return "{\"inkVersion\":21,\"root\":[" +
               lineContent +
               ",\"done\",{\"" +
               knot +
               "\":" +
               lineContent +
               ",\"#f\":1}],\"listDefs\":{}}";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static void WriteAssetTextIfMissing(string assetPath, string content)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || File.Exists(ToFullPath(assetPath)))
            return;

        string fullPath = ToFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, content, Encoding.UTF8);
    }

    private static void EnsureAssetFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (AssetDatabase.IsValidFolder(path))
            return;

        AssetDatabase.CreateFolder(parent, child);
    }

    private static string ToFullPath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static List<PlayerHubSpawnPresentation2D> ResolvePlayerHubSpawnPresentations(
        Scene scene,
        bool createMissingOnSpawnerPrefab)
    {
        List<PlayerHubSpawnPresentation2D> targets = FindSceneComponents<PlayerHubSpawnPresentation2D>(scene);

        GameObject playerPrefab = ResolvePlayerPrefab(scene);
        if (playerPrefab == null)
            return targets;

        PlayerHubSpawnPresentation2D prefabPresentation = playerPrefab.GetComponent<PlayerHubSpawnPresentation2D>();
        if (prefabPresentation == null && createMissingOnSpawnerPrefab)
        {
            prefabPresentation = Undo.AddComponent<PlayerHubSpawnPresentation2D>(playerPrefab);
            EditorUtility.SetDirty(playerPrefab);
        }

        if (prefabPresentation != null && !targets.Contains(prefabPresentation))
            targets.Add(prefabPresentation);

        return targets;
    }

    private static GameObject ResolvePlayerPrefab(Scene scene)
    {
        PlayerSpawner spawner = FindSceneComponent<PlayerSpawner>(scene);
        if (spawner == null)
            return null;

        SerializedObject spawnerSo = new(spawner);
        SerializedProperty playerPrefab = spawnerSo.FindProperty("playerPrefab");
        return playerPrefab != null
            ? playerPrefab.objectReferenceValue as GameObject
            : null;
    }

    private static void ValidateHubIntroSequence(ValidationReport report, HubIntroAfterDarkLordSequence sequence)
    {
        SerializedObject so = new(sequence);
        RequireBool(report, so, "playOnStart", true, "HubIntroAfterDarkLordSequence.playOnStart");
        RequireBool(report, so, "playOnlyOncePerScene", true, "HubIntroAfterDarkLordSequence.playOnlyOncePerScene");
        RequireString(report, so, "hubSceneName", TargetSceneName, "HubIntroAfterDarkLordSequence.hubSceneName");
        RequireBool(report, so, "waitForHubSpawnPresentation", true, "HubIntroAfterDarkLordSequence.waitForHubSpawnPresentation");
        RequireString(report, so, "darkLordTutorialCompletionId", HubIntroProgressGate.DefaultDarkLordTutorialCompletionId, "DarkLord completion id");
        RequireString(report, so, "hubIntroSeenId", HubIntroProgressGate.DefaultHubIntroSeenId, "Hub intro seen id");
        RequireBool(report, so, "allowEditorBypassTutorialCompletion", true, "HubIntroAfterDarkLordSequence.allowEditorBypassTutorialCompletion");
        RequireBool(report, so, "markSeenOnComplete", true, "HubIntroAfterDarkLordSequence.markSeenOnComplete");
        RequireBool(report, so, "hideGameplayHud", true, "HubIntroAfterDarkLordSequence.hideGameplayHud");
        RequireBool(report, so, "blockExternalInput", true, "HubIntroAfterDarkLordSequence.blockExternalInput");
        RequireBool(report, so, "lockPlayerControls", true, "HubIntroAfterDarkLordSequence.lockPlayerControls");
        RequireBool(report, so, "useLetterbox", true, "HubIntroAfterDarkLordSequence.useLetterbox");
        RequireReference(report, so, "npcSpeechBubble", "NPC SpeechBubbleComponent");
        RequireReference(report, so, "npcFocusTarget", "NPC focus target");
        RequireReference(report, so, "narratorNpcData", "Narrator NPCData");
        RequireNonEmptyString(report, so, "openingSpeechText", "Opening speech text");
        ValidateOpeningSpeechBubbleLayout(report, so);
        RequireReference(report, so, "finalNpcFocusTarget", "Final NPC focus target");
        RequireReference(report, so, "finalDialogueInk", "Final dialogue Ink JSON");
        ValidateDraftDialogueStartPath(report, so, "finalDialogueInk", "finalDialogueStartPath", "Final dialogue");
        RequireBool(report, so, "finalDialogueUsesFastSilhouette", true, "HubIntroAfterDarkLordSequence.finalDialogueUsesFastSilhouette");
        RequireBool(report, so, "finalDialogueBoxOnly", true, "HubIntroAfterDarkLordSequence.finalDialogueBoxOnly");
        ValidateFocusSteps(report, so);
    }

    private static void ValidateOpeningSpeechBubbleLayout(ValidationReport report, SerializedObject so)
    {
        SerializedProperty preSizeProperty = so.FindProperty("preSizeOpeningSpeechBubbleBeforeTyping");
        if (preSizeProperty == null)
        {
            report.Error("HubIntroAfterDarkLordSequence.preSizeOpeningSpeechBubbleBeforeTyping property was not found.");
            return;
        }

        if (!preSizeProperty.boolValue)
            report.Warning("Opening SpeechBubble pre-size is disabled; long opening text may overflow during typing.");

        RequirePositiveFloat(report, so, "openingSpeechBubbleMinTextWidth", "Opening SpeechBubble min text width");
        RequirePositiveFloat(report, so, "openingSpeechBubbleMaxTextWidth", "Opening SpeechBubble max text width");
        RequirePositiveFloat(report, so, "openingSpeechBubbleMinTextHeight", "Opening SpeechBubble min text height");

        SerializedProperty minWidth = so.FindProperty("openingSpeechBubbleMinTextWidth");
        SerializedProperty maxWidth = so.FindProperty("openingSpeechBubbleMaxTextWidth");
        if (minWidth != null &&
            maxWidth != null &&
            maxWidth.floatValue < minWidth.floatValue)
        {
            report.Error("Opening SpeechBubble max text width must be greater than or equal to min text width.");
        }
    }

    private static void ValidateFocusSteps(ValidationReport report, SerializedObject so)
    {
        SerializedProperty focusSteps = so.FindProperty("focusSteps");
        if (focusSteps == null || !focusSteps.isArray)
        {
            report.Error("Focus steps property was not found.");
            return;
        }

        if (focusSteps.arraySize != 3)
            report.Error($"Focus steps must have exactly 3 entries; current size is {focusSteps.arraySize}.");

        int count = Mathf.Min(focusSteps.arraySize, 3);
        for (int i = 0; i < count; i++)
        {
            SerializedProperty step = focusSteps.GetArrayElementAtIndex(i);
            string label = ResolveRelativeString(step, "label");
            if (string.IsNullOrWhiteSpace(label))
                label = $"Focus step {i + 1}";

            RequireRelativeReference(report, step, "focusTarget", $"{label} focus target");
            RequireRelativeReference(report, step, "dialogueInk", $"{label} dialogue Ink JSON");
            ValidateDraftDialogueStartPath(report, step, "dialogueInk", "dialogueStartPath", $"{label} dialogue");
        }
    }

    private static void ValidateDraftDialogueStartPath(
        ValidationReport report,
        SerializedObject so,
        string inkPropertyName,
        string startPathPropertyName,
        string label)
    {
        SerializedProperty inkProperty = so.FindProperty(inkPropertyName);
        SerializedProperty startPathProperty = so.FindProperty(startPathPropertyName);
        ValidateDraftDialogueStartPath(report, inkProperty, startPathProperty, label);
    }

    private static void ValidateDraftDialogueStartPath(
        ValidationReport report,
        SerializedProperty parent,
        string inkRelativePath,
        string startPathRelativePath,
        string label)
    {
        SerializedProperty inkProperty = parent?.FindPropertyRelative(inkRelativePath);
        SerializedProperty startPathProperty = parent?.FindPropertyRelative(startPathRelativePath);
        ValidateDraftDialogueStartPath(report, inkProperty, startPathProperty, label);
    }

    private static void ValidateDraftDialogueStartPath(
        ValidationReport report,
        SerializedProperty inkProperty,
        SerializedProperty startPathProperty,
        string label)
    {
        if (inkProperty == null || startPathProperty == null)
            return;

        TextAsset ink = inkProperty.objectReferenceValue as TextAsset;
        DraftDialogueAsset draftAsset = FindDraftDialogueAssetByJson(AssetDatabase.GetAssetPath(ink));
        if (draftAsset == null)
            return;

        if (!string.Equals(startPathProperty.stringValue, draftAsset.Knot, StringComparison.Ordinal))
        {
            report.Error($"{label} start path must be '{draftAsset.Knot}' for the temporary Hub intro draft Ink JSON.");
        }
    }

    private static void ValidatePlayerSpawnPresentation(ValidationReport report, Scene scene)
    {
        PlayerSpawner spawner = FindSceneComponent<PlayerSpawner>(scene);
        if (spawner == null)
        {
            report.Error("PlayerSpawner is missing from the Hub scene.");
            return;
        }

        GameObject playerPrefab = ResolvePlayerPrefab(scene);
        if (playerPrefab == null)
        {
            report.Error("PlayerSpawner.playerPrefab is unassigned.");
            return;
        }

        PlayerHubSpawnPresentation2D presentation = playerPrefab.GetComponent<PlayerHubSpawnPresentation2D>();
        if (presentation == null)
        {
            report.Error("PlayerSpawner.playerPrefab must include PlayerHubSpawnPresentation2D.");
            return;
        }

        SerializedObject so = new(presentation);
        RequireString(report, so, "hubSceneName", TargetSceneName, "PlayerHubSpawnPresentation2D.hubSceneName");
        RequireBool(report, so, "playOnHubSpawn", true, "PlayerHubSpawnPresentation2D.playOnHubSpawn");
        RequireEnumIndex(report, so, "playCondition", AfterDarkLordTutorialUntilSeenEnumIndex, "PlayerHubSpawnPresentation2D.playCondition");
        RequireString(report, so, "darkLordTutorialCompletionId", HubIntroProgressGate.DefaultDarkLordTutorialCompletionId, "PlayerHubSpawnPresentation2D.darkLordTutorialCompletionId");
        RequireString(report, so, "hubIntroSeenId", HubIntroProgressGate.DefaultHubIntroSeenId, "PlayerHubSpawnPresentation2D.hubIntroSeenId");
        RequireBool(report, so, "allowEditorBypassTutorialCompletion", true, "PlayerHubSpawnPresentation2D.allowEditorBypassTutorialCompletion");
        RequireBool(report, so, "autoWakeWithoutInput", true, "PlayerHubSpawnPresentation2D.autoWakeWithoutInput");
    }

    private static SpeechBubbleComponent ResolveSingleSceneSpeechBubble(Scene scene)
    {
        List<SpeechBubbleComponent> speechBubbles = FindSceneComponents<SpeechBubbleComponent>(scene);
        return speechBubbles.Count == 1 ? speechBubbles[0] : null;
    }

    private static Vector3 ResolveRootPosition(Scene scene, SpeechBubbleComponent speechBubble)
    {
        if (speechBubble != null)
            return speechBubble.transform.position;

        Transform candidate = FindTransformByNameHints(scene, "npc", "guide", "mentor");
        return candidate != null ? candidate.position : Vector3.zero;
    }

    private static Vector3 ResolveNpcFocusPosition(Scene scene, SpeechBubbleComponent speechBubble, Vector3 fallback)
    {
        if (speechBubble != null)
            return speechBubble.transform.position;

        Transform candidate = FindTransformByNameHints(scene, "npc", "guide", "mentor");
        return candidate != null ? candidate.position : fallback;
    }

    private static Vector3 ResolveNamedFocusPosition(Scene scene, Vector3 fallback, params string[] nameHints)
    {
        Transform candidate = FindTransformByNameHints(scene, nameHints);
        return candidate != null ? candidate.position : fallback;
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
        List<T> components = FindSceneComponents<T>(scene);
        return components.Count > 0 ? components[0] : null;
    }

    private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> results = new();
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null || EditorUtility.IsPersistent(candidate))
                continue;

            if (candidate.gameObject.scene == scene)
                results.Add(candidate);
        }

        return results;
    }

    private static Transform FindTransformByName(Scene scene, string objectName)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(objectName))
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

    private static Transform FindTransformByNameHints(Scene scene, params string[] hints)
    {
        if (!scene.IsValid() || hints == null || hints.Length == 0)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform candidate = transforms[transformIndex];
                if (candidate == null || IsUnderGeneratedRoot(candidate))
                    continue;

                string candidateName = candidate.name.ToLowerInvariant();
                for (int hintIndex = 0; hintIndex < hints.Length; hintIndex++)
                {
                    string hint = hints[hintIndex];
                    if (!string.IsNullOrWhiteSpace(hint) && candidateName.Contains(hint.ToLowerInvariant()))
                        return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsUnderGeneratedRoot(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (string.Equals(current.name, RootName, StringComparison.Ordinal))
                return true;

            current = current.parent;
        }

        return false;
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

    private static void RequireReference(ValidationReport report, SerializedObject so, string propertyName, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            report.Error($"{label} is unassigned.");
    }

    private static void RequireRelativeReference(ValidationReport report, SerializedProperty parent, string relativePath, string label)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
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

    private static void RequireEnumIndex(ValidationReport report, SerializedObject so, string propertyName, int expected, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.enumValueIndex != expected)
            report.Error($"{label} must use enum index {expected}.");
    }

    private static void RequireString(ValidationReport report, SerializedObject so, string propertyName, string expected, string label)
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

    private static void RequireNonEmptyString(ValidationReport report, SerializedObject so, string propertyName, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (string.IsNullOrWhiteSpace(property.stringValue))
            report.Error($"{label} is empty.");
    }

    private static void RequirePositiveFloat(ValidationReport report, SerializedObject so, string propertyName, string label)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            report.Error($"{label} property was not found.");
            return;
        }

        if (property.floatValue <= 0f)
            report.Error($"{label} must be greater than 0.");
    }

    private static string ResolveRelativeString(SerializedProperty parent, string relativePath)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        return property != null ? property.stringValue : string.Empty;
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetObjectIfNull(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetEnumIndex(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetFloatIfNotPositive(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null && property.floatValue <= 0f)
            property.floatValue = value;
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetStringIfEmpty(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null && string.IsNullOrWhiteSpace(property.stringValue))
            property.stringValue = value;
    }

    private static bool SetObjectIfNullAndReport(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null || value == null)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetStringIfEmptyAndReport(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !string.IsNullOrWhiteSpace(property.stringValue) || string.IsNullOrWhiteSpace(value))
            return false;

        property.stringValue = value;
        return true;
    }

    private static bool SetFloatIfNotPositiveAndReport(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || property.floatValue > 0f)
            return false;

        property.floatValue = value;
        return true;
    }

    private static bool SetRelativeObjectIfNullAndReport(SerializedProperty parent, string relativePath, Object value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property == null || property.objectReferenceValue != null || value == null)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetRelativeStringIfEmptyAndReport(SerializedProperty parent, string relativePath, string value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property == null || !string.IsNullOrWhiteSpace(property.stringValue) || string.IsNullOrWhiteSpace(value))
            return false;

        property.stringValue = value;
        return true;
    }

    private static void SetRelativeObject(SerializedProperty parent, string relativePath, Object value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetRelativeBool(SerializedProperty parent, string relativePath, bool value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetRelativeFloat(SerializedProperty parent, string relativePath, float value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetRelativeString(SerializedProperty parent, string relativePath, string value)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property != null)
            property.stringValue = value;
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
            builder.AppendLine($"[HubIntroAuthoring] {title}");
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

    private sealed class DraftDialogueAsset
    {
        public readonly string Label;
        public readonly string AssetName;
        public readonly string Knot;
        public readonly string Line;

        public DraftDialogueAsset(string label, string assetName, string knot, string line)
        {
            Label = label;
            AssetName = assetName;
            Knot = knot;
            Line = line;
        }

        public string InkPath => $"{DraftInkFolder}/{AssetName}.ink";
        public string JsonPath => $"{DraftInkFolder}/{AssetName}.json";
    }
}
#endif


