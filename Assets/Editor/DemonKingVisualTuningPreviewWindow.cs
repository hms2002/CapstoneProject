using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CapstonePresentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityGAS;

using Object = UnityEngine.Object;

internal sealed class DemonKingVisualTuningPreviewWindow : EditorWindow
{
    private const string DarkLordAnimationFolder = "Assets/Sprites/Characters/Boss/DarkLord";
    private const string DemonKingVfxFolder = "Assets/Resources/DemonKing/Vfx";
    private const string DemonKingAbilityLogicFolder =
        "Assets/Script/Enemy/Boss/FSM/BossControllers/DemonKingBoss/ScriptableObjects/Abilities/Logics";
    private const int DefaultPreviewTextureSize = 512;
    private const float PreviewDepth = -10f;
    private const float MarkerRadius = 5f;
    private const float WindowPadding = 4f;
    private const float SplitterSize = 5f;
    private const float TopMinHeight = 260f;
    private const float PreviewMinWidth = 360f;
    private const float PatternControlsMinWidth = 320f;
    private const float TimelineMinWidth = 360f;
    private const float AdvancedToolsMinWidth = 300f;
    private const float RuntimePatternRunnerMinWidth = 320f;
    private const float BottomMinHeight = 160f;
    private const float PatternControlsDefaultWidth = 380f;
    private const float BottomDefaultHeight = 220f;
    private const float AdvancedToolsDefaultWidth = 340f;
    private const float RuntimePatternRunnerDefaultWidth = 360f;
    private const string PatternControlsWidthPrefKey = "DemonKingVisualTuningPreview.PatternControlsWidth";
    private const string BottomPaneHeightPrefKey = "DemonKingVisualTuningPreview.BottomPaneHeight";
    private const string AdvancedToolsWidthPrefKey = "DemonKingVisualTuningPreview.AdvancedToolsWidth";
    private const string RuntimePatternRunnerWidthPrefKey = "DemonKingVisualTuningPreview.RuntimePatternRunnerWidth";

    private static readonly Vector3 PreviewOrigin = new(14000f, 14000f, 0f);
    private static readonly Color PreviewBackground = new(0.075f, 0.075f, 0.085f, 1f);
    private static readonly Color BodyMarkerColor = new(0.9f, 0.9f, 0.9f, 0.78f);
    private static readonly Color HeldMarkerColor = new(0.25f, 0.85f, 1f, 1f);
    private static readonly Color ThrowMarkerColor = new(1f, 0.85f, 0.15f, 1f);
    private static readonly Color RecallMarkerColor = new(0.45f, 1f, 0.35f, 1f);
    private static readonly Color HitWindowColor = new(1f, 0.25f, 0.16f, 0.85f);

    private enum ToolTab
    {
        Preview,
        Composite,
        Animation,
        VfxHitTiming,
        Pattern,
        EgoSword,
        Sockets
    }

    private enum PreviewSubject
    {
        Composite,
        BodyClip,
        VfxPrefab,
        EgoSwordOffsets,
        SocketMap
    }

    private enum RuntimePatternPlaybackMode
    {
        OneShot,
        Loop
    }

    private readonly List<AnimationClip> darkLordClips = new();
    private readonly List<AnimationClip> vfxClips = new();
    private readonly List<GameObject> vfxPrefabs = new();
    private readonly List<ScriptableObject> abilityLogicAssets = new();
    private readonly List<SpriteFrameRow> clipRows = new();

    private ToolTab selectedTab = ToolTab.Preview;
    private PreviewSubject previewSubject = PreviewSubject.BodyClip;
    private AnimationClip selectedAnimationClip;
    private GameObject selectedVfxPrefab;
    private ScriptableObject selectedAbilityLogicAsset;
    private EgoSwordActor selectedEgoSword;
    private DemonKingVfxSocketMap selectedSocketMap;
    private Vector2 inspectorScroll;
    private Vector2 frameScroll;
    private Vector2 patternTimelineScroll;
    private Vector2 patternControlsScroll;
    private Vector2 runtimePatternRunnerScroll;
    private Vector2 serializedScroll;
    private ScriptableObject lastPatternWorkbenchAsset;
    private bool layoutPrefsLoaded;
    private float patternControlsPaneWidth;
    private float bottomPaneHeight;
    private float advancedToolsPaneWidth;
    private float runtimePatternRunnerPaneWidth;

    private Camera previewCamera;
    private RenderTexture previewTexture;
    private GameObject previewRoot;
    private GameObject previewInstance;
    private SpriteRenderer bodyPreviewRenderer;
    private SpriteRenderer egoSwordPreviewRenderer;
    private Animator[] previewAnimators = Array.Empty<Animator>();
    private ParticleSystem[] previewParticleSystems = Array.Empty<ParticleSystem>();
    private TopDownDebrisBounceEmitter2D[] previewDebrisEmitters = Array.Empty<TopDownDebrisBounceEmitter2D>();
    private Rect lastPreviewRect;
    private double lastEditorTime;
    private bool previewPlaying = true;
    private bool loopPreview = true;
    private bool previewFacingLeft = true;
    private float previewTime;
    private float previewSpeed = 1f;
    private float previewCameraSize = 3.5f;
    private bool compositeShowBody = true;
    private bool compositeShowVfx = true;
    private bool compositeShowSockets = true;
    private bool compositeShowEgoSword = true;
    private DemonKingVfxSocketId compositeVfxSocket = DemonKingVfxSocketId.HandCounterImpact;
    private Vector2 compositeFallbackLeftOffset = Vector2.zero;
    private Vector3 compositeVfxPreviewScale = Vector3.one;
    private float compositeVfxRotationDeg;
    private int patternSelectedPhaseIndex;
    private int patternAppliedPhaseIndex = -1;
    private float patternTimelineTime;
    private float patternLastAppliedTimelineTime;
    private float patternPhasePreviewTime;
    private int patternPreviewBodyFrameIndex = -1;
    private bool patternShowWarningShape = true;
    private bool showFullPatternSerialized;
    private DemonKingController runtimePatternDemonKing;
    private Transform runtimePatternTarget;
    private bool runtimePatternCancelCurrentBeforeRun = true;
    private bool runtimePatternSelectedOnly = true;
    private bool runtimePatternPlayerInvulnerable = true;
    private RuntimePatternPlaybackMode runtimePatternPlaybackMode = RuntimePatternPlaybackMode.OneShot;
    private bool runtimePatternLivePreviewEnabled;
    private bool runtimePatternLivePreviewFrameTarget = true;
    private bool runtimePatternLivePreviewUseGameCameraMask = true;
    private float runtimePatternLivePreviewCameraSize = 5.5f;
    private float runtimePatternLivePreviewPadding = 1.25f;
    private AbilitySystem runtimePatternAbilitySystem;
    private AbilityDefinition runtimePatternAbilityDefinition;
    private string runtimePatternStatus;
    private double runtimePatternStartedAt;
    private DemonKingController runtimePatternIsolatedDemon;
    private bool runtimePatternShouldRestoreBossCombat;
    private TagSystem runtimePatternInvulnerableTagSystem;
    private GameplayTag runtimePatternInvulnerableTag;
    private bool runtimePatternInvulnerableApplied;
    private DemonKingPatternPreviewShape activePatternPreviewShape;
    private DemonKingPatternPreviewPhase activePatternPreviewPhase;
    private readonly List<DemonKingPatternPreviewShape> activePatternPreviewShapes = new();
    private DemonKingPatternPreviewCue activeBodyPreviewCue;
    private DemonKingPatternPreviewCue activeEgoSwordPreviewCue;
    private Transform activeEgoSwordSpinPreviewTransform;
    private string activePatternPreviewSignature;
    private bool patternPreviewNeedsRebuild = true;

    private float stagedFrameRate = 12f;
    private bool stagedLoopTime;
    private float stagedClipLength = 1f;
    private bool clipRowsDirty;
    private AnimationClip loadedClipRowsFor;

    private int stagedEnableHitFrame = 1;
    private int stagedDisableHitFrame = 2;

    private sealed class SpriteFrameRow
    {
        public float Time;
        public Sprite Sprite;
    }

    [MenuItem("Tools/DemonKing/Visual Tuning Preview")]
    private static void Open()
    {
        GetWindow<DemonKingVisualTuningPreviewWindow>("DemonKing Visual Tuning");
    }

    private void OnEnable()
    {
        float topMinimumWidth = PreviewMinWidth + PatternControlsMinWidth + SplitterSize;
        float bottomMinimumWidth = TimelineMinWidth
            + AdvancedToolsMinWidth
            + RuntimePatternRunnerMinWidth
            + SplitterSize * 2f;
        float minimumWidth = Mathf.Max(topMinimumWidth, bottomMinimumWidth) + WindowPadding * 2f;
        float minimumHeight = TopMinHeight
            + BottomMinHeight
            + SplitterSize
            + EditorGUIUtility.singleLineHeight
            + WindowPadding * 3f
            + 4f;
        minSize = new Vector2(minimumWidth, minimumHeight);
        LoadLayoutPrefs();
        RefreshAssetLists();
        EnsureDefaultSelections();
        EnsurePreviewCamera();
        EditorApplication.update += TickPreview;
        RestartPreview();
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
        CleanupRuntimePatternAbility(cancelExecution: true);
        DestroyPreviewInstance();
        DestroyPreviewCamera();
        ReleasePreviewTexture();
    }

    private void OnSelectionChange()
    {
        Object active = Selection.activeObject;
        if (active is GameObject gameObject)
        {
            EgoSwordActor sword = gameObject.GetComponentInChildren<EgoSwordActor>(true);
            if (sword != null)
            {
                selectedEgoSword = sword;
                patternPreviewNeedsRebuild = true;
            }

            DemonKingVfxSocketMap socketMap = gameObject.GetComponentInChildren<DemonKingVfxSocketMap>(true);
            if (socketMap != null)
            {
                selectedSocketMap = socketMap;
                patternPreviewNeedsRebuild = true;
            }
        }
        else if (active is EgoSwordActor sword)
        {
            selectedEgoSword = sword;
            patternPreviewNeedsRebuild = true;
        }
        else if (active is DemonKingVfxSocketMap socketMap)
        {
            selectedSocketMap = socketMap;
            patternPreviewNeedsRebuild = true;
        }

        Repaint();
    }

    private void OnGUI()
    {
        EnsureLayoutPrefsLoaded();

        Rect contentRect = new(
            WindowPadding,
            WindowPadding,
            Mathf.Max(1f, position.width - WindowPadding * 2f),
            Mathf.Max(1f, position.height - WindowPadding * 2f));

        float headerHeight = EditorGUIUtility.singleLineHeight + 4f;
        Rect headerRect = new(contentRect.x, contentRect.y, contentRect.width, headerHeight);
        GUILayout.BeginArea(headerRect);
        DrawHeader();
        GUILayout.EndArea();

        Rect bodyRect = new(
            contentRect.x,
            headerRect.yMax + WindowPadding,
            contentRect.width,
            Mathf.Max(1f, contentRect.yMax - headerRect.yMax - WindowPadding));

        ClampLayoutPanes(bodyRect);

        Rect bottomRect = new(bodyRect.x, bodyRect.yMax - bottomPaneHeight, bodyRect.width, bottomPaneHeight);
        Rect heightSplitterRect = new(bodyRect.x, bottomRect.y - SplitterSize, bodyRect.width, SplitterSize);
        Rect topRect = new(bodyRect.x, bodyRect.y, bodyRect.width, Mathf.Max(1f, heightSplitterRect.y - bodyRect.y));

        DrawTopWorkbenchPanes(topRect);
        DrawSplitter(
            heightSplitterRect,
            MouseCursor.ResizeVertical,
            delta =>
            {
                bottomPaneHeight -= delta.y;
                ClampLayoutPanes(bodyRect);
            },
            () => EditorPrefs.SetFloat(BottomPaneHeightPrefKey, bottomPaneHeight));
        DrawBottomWorkbenchPanes(bottomRect);
    }

    private void DrawTopWorkbenchPanes(Rect topRect)
    {
        Rect controlsRect = new(
            topRect.xMax - patternControlsPaneWidth,
            topRect.y,
            patternControlsPaneWidth,
            topRect.height);
        Rect widthSplitterRect = new(controlsRect.x - SplitterSize, topRect.y, SplitterSize, topRect.height);
        Rect previewPaneRect = new(
            topRect.x,
            topRect.y,
            Mathf.Max(1f, widthSplitterRect.x - topRect.x),
            topRect.height);

        GUILayout.BeginArea(previewPaneRect);
        Rect previewRect = GUILayoutUtility.GetRect(
            DefaultPreviewTextureSize,
            DefaultPreviewTextureSize,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        lastPreviewRect = previewRect;
        EnsurePreviewTexture(previewRect);
        RenderPreview();
        DrawPreviewTexture(previewRect);
        GUILayout.EndArea();

        DrawSplitter(
            widthSplitterRect,
            MouseCursor.ResizeHorizontal,
            delta =>
            {
                patternControlsPaneWidth -= delta.x;
                ClampHorizontalPane(ref patternControlsPaneWidth, topRect.width, PreviewMinWidth, PatternControlsMinWidth);
            },
            () => EditorPrefs.SetFloat(PatternControlsWidthPrefKey, patternControlsPaneWidth));

        GUILayout.BeginArea(controlsRect, GUIContent.none, EditorStyles.helpBox);
        patternControlsScroll = EditorGUILayout.BeginScrollView(patternControlsScroll);
        DrawPatternControlPanel();
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawBottomWorkbenchPanes(Rect bottomRect)
    {
        Rect runtimeRect = new(
            bottomRect.xMax - runtimePatternRunnerPaneWidth,
            bottomRect.y,
            runtimePatternRunnerPaneWidth,
            bottomRect.height);
        Rect runtimeSplitterRect = new(runtimeRect.x - SplitterSize, bottomRect.y, SplitterSize, bottomRect.height);
        Rect advancedRect = new(
            runtimeSplitterRect.x - advancedToolsPaneWidth,
            bottomRect.y,
            advancedToolsPaneWidth,
            bottomRect.height);
        Rect widthSplitterRect = new(advancedRect.x - SplitterSize, bottomRect.y, SplitterSize, bottomRect.height);
        Rect timelineRect = new(
            bottomRect.x,
            bottomRect.y,
            Mathf.Max(1f, widthSplitterRect.x - bottomRect.x),
            bottomRect.height);

        GUILayout.BeginArea(timelineRect, GUIContent.none, EditorStyles.helpBox);
        DrawPatternTimelinePanel();
        GUILayout.EndArea();

        DrawSplitter(
            widthSplitterRect,
            MouseCursor.ResizeHorizontal,
            delta =>
            {
                advancedToolsPaneWidth -= delta.x;
                ClampBottomPaneWidths(bottomRect.width);
            },
            () => EditorPrefs.SetFloat(AdvancedToolsWidthPrefKey, advancedToolsPaneWidth));

        GUILayout.BeginArea(advancedRect, GUIContent.none, EditorStyles.helpBox);
        EditorGUILayout.LabelField("Advanced Asset Tools", EditorStyles.boldLabel);
        DrawAdvancedAssetTools();
        GUILayout.EndArea();

        DrawSplitter(
            runtimeSplitterRect,
            MouseCursor.ResizeHorizontal,
            delta =>
            {
                runtimePatternRunnerPaneWidth -= delta.x;
                ClampBottomPaneWidths(bottomRect.width);
            },
            () => EditorPrefs.SetFloat(RuntimePatternRunnerWidthPrefKey, runtimePatternRunnerPaneWidth));

        GUILayout.BeginArea(runtimeRect, GUIContent.none, EditorStyles.helpBox);
        runtimePatternRunnerScroll = EditorGUILayout.BeginScrollView(runtimePatternRunnerScroll);
        DrawRuntimePatternExecutionPanel();
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void LoadLayoutPrefs()
    {
        patternControlsPaneWidth = EditorPrefs.GetFloat(PatternControlsWidthPrefKey, PatternControlsDefaultWidth);
        bottomPaneHeight = EditorPrefs.GetFloat(BottomPaneHeightPrefKey, BottomDefaultHeight);
        advancedToolsPaneWidth = EditorPrefs.GetFloat(AdvancedToolsWidthPrefKey, AdvancedToolsDefaultWidth);
        runtimePatternRunnerPaneWidth = EditorPrefs.GetFloat(RuntimePatternRunnerWidthPrefKey, RuntimePatternRunnerDefaultWidth);
        layoutPrefsLoaded = true;
    }

    private void EnsureLayoutPrefsLoaded()
    {
        if (!layoutPrefsLoaded)
            LoadLayoutPrefs();
    }

    private void ClampLayoutPanes(Rect bodyRect)
    {
        float maxBottomHeight = Mathf.Max(BottomMinHeight, bodyRect.height - TopMinHeight - SplitterSize);
        if (bodyRect.height < TopMinHeight + BottomMinHeight + SplitterSize)
        {
            float available = Mathf.Max(1f, bodyRect.height - SplitterSize);
            float bottomRatio = BottomMinHeight / (TopMinHeight + BottomMinHeight);
            bottomPaneHeight = Mathf.Max(1f, available * bottomRatio);
        }
        else
        {
            bottomPaneHeight = Mathf.Clamp(bottomPaneHeight, BottomMinHeight, maxBottomHeight);
        }

        ClampHorizontalPane(ref patternControlsPaneWidth, bodyRect.width, PreviewMinWidth, PatternControlsMinWidth);
        ClampBottomPaneWidths(bodyRect.width);
    }

    private void ClampBottomPaneWidths(float totalWidth)
    {
        float availableWidth = Mathf.Max(1f, totalWidth - SplitterSize * 2f);
        float minimumTotalWidth = TimelineMinWidth + AdvancedToolsMinWidth + RuntimePatternRunnerMinWidth;
        if (availableWidth <= minimumTotalWidth)
        {
            advancedToolsPaneWidth = Mathf.Max(
                1f,
                availableWidth * (AdvancedToolsMinWidth / minimumTotalWidth));
            runtimePatternRunnerPaneWidth = Mathf.Max(
                1f,
                availableWidth * (RuntimePatternRunnerMinWidth / minimumTotalWidth));
            return;
        }

        runtimePatternRunnerPaneWidth = Mathf.Clamp(
            runtimePatternRunnerPaneWidth,
            RuntimePatternRunnerMinWidth,
            availableWidth - TimelineMinWidth - AdvancedToolsMinWidth);

        float remainingAfterRuntime = availableWidth - runtimePatternRunnerPaneWidth;
        advancedToolsPaneWidth = Mathf.Clamp(
            advancedToolsPaneWidth,
            AdvancedToolsMinWidth,
            remainingAfterRuntime - TimelineMinWidth);
    }

    private static void ClampHorizontalPane(
        ref float rightPaneWidth,
        float totalWidth,
        float leftMinWidth,
        float rightMinWidth)
    {
        float availableWidth = Mathf.Max(1f, totalWidth - SplitterSize);
        float minimumTotalWidth = leftMinWidth + rightMinWidth;
        if (availableWidth <= minimumTotalWidth)
        {
            rightPaneWidth = Mathf.Max(1f, availableWidth * (rightMinWidth / minimumTotalWidth));
            return;
        }

        rightPaneWidth = Mathf.Clamp(rightPaneWidth, rightMinWidth, availableWidth - leftMinWidth);
    }

    private static void DrawSplitter(
        Rect rect,
        MouseCursor cursor,
        Action<Vector2> onDrag,
        Action onDragEnd)
    {
        int controlId = GUIUtility.GetControlID(
            "DemonKingVisualTuningPreviewSplitter".GetHashCode(),
            FocusType.Passive,
            rect);
        EditorGUIUtility.AddCursorRect(rect, cursor, controlId);
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));

        Event current = Event.current;
        switch (current.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (current.button == 0 && rect.Contains(current.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    current.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    onDrag?.Invoke(current.delta);
                    GUI.changed = true;
                    current.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    onDragEnd?.Invoke();
                    current.Use();
                }
                break;
        }
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                RefreshAssetLists();
                EnsureDefaultSelections();
                RestartPreview();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Pattern Asset", GUILayout.Width(82f));
            EditorGUI.BeginChangeCheck();
            selectedAbilityLogicAsset = (ScriptableObject)EditorGUILayout.ObjectField(
                selectedAbilityLogicAsset,
                typeof(ScriptableObject),
                allowSceneObjects: false,
                GUILayout.Width(230f));
            if (EditorGUI.EndChangeCheck())
                OnPatternAssetChanged();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(previewPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                previewPlaying = !previewPlaying;

            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(66f)))
            {
                if (TryCreateCurrentPatternDefinition(out DemonKingPatternPreviewDefinition definition))
                    RestartPatternTimeline(definition);
                else
                    RestartPreview();
            }

            loopPreview = GUILayout.Toggle(loopPreview, "Loop", EditorStyles.toolbarButton, GUILayout.Width(48f));
            GUILayout.Label("Speed", GUILayout.Width(42f));
            previewSpeed = GUILayout.HorizontalSlider(previewSpeed, 0.05f, 3f, GUILayout.Width(90f));
            GUILayout.Label(previewSpeed.ToString("0.00", CultureInfo.InvariantCulture), GUILayout.Width(34f));
            GUILayout.Label("Time", GUILayout.Width(32f));
            if (TryCreateCurrentPatternDefinition(out DemonKingPatternPreviewDefinition headerDefinition))
            {
                EditorGUI.BeginChangeCheck();
                patternTimelineTime = GUILayout.HorizontalSlider(
                    patternTimelineTime,
                    0f,
                    Mathf.Max(0.01f, headerDefinition.TotalDuration),
                    GUILayout.Width(130f));
                if (EditorGUI.EndChangeCheck())
                {
                    patternAppliedPhaseIndex = -1;
                    ApplyPatternTimelineTime(headerDefinition, restartOnPhaseChange: true);
                }
            }
            else
            {
                previewTime = GUILayout.HorizontalSlider(previewTime, 0f, Mathf.Max(0.01f, ResolvePreviewLength()), GUILayout.Width(130f));
            }
        }
    }

    private void DrawAdvancedAssetTools()
    {
        selectedTab = (ToolTab)GUILayout.Toolbar((int)selectedTab, Enum.GetNames(typeof(ToolTab)));
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.ExpandHeight(true));
        switch (selectedTab)
        {
            case ToolTab.Preview:
                DrawPreviewTab();
                break;
            case ToolTab.Composite:
                DrawCompositeTab();
                break;
            case ToolTab.Animation:
                DrawAnimationTab();
                break;
            case ToolTab.VfxHitTiming:
                DrawVfxTab();
                break;
            case ToolTab.Pattern:
                DrawPatternTab();
                break;
            case ToolTab.EgoSword:
                DrawEgoSwordTab();
                break;
            case ToolTab.Sockets:
                DrawSocketTab();
                break;
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawPreviewTexture(Rect previewRect)
    {
        if (previewTexture != null)
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
        else
            EditorGUI.HelpBox(previewRect, "Preview texture is not available.", MessageType.Warning);

        if (IsLiveRuntimePreviewVisible())
        {
            DrawLiveRuntimePreviewOverlay(previewRect);
            return;
        }

        Handles.BeginGUI();
        DrawPreviewOverlays(previewRect);
        Handles.EndGUI();
    }

    private void DrawPreviewTab()
    {
        EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        previewSpeed = EditorGUILayout.Slider("Speed", previewSpeed, 0.05f, 3f);
        previewCameraSize = EditorGUILayout.Slider("Camera Size", previewCameraSize, 0.8f, 8f);
        loopPreview = EditorGUILayout.Toggle("Loop", loopPreview);
        previewFacingLeft = EditorGUILayout.Toggle("Left Facing Baseline", previewFacingLeft);
        previewTime = EditorGUILayout.Slider("Time", previewTime, 0f, Mathf.Max(0.01f, ResolvePreviewLength()));
        if (EditorGUI.EndChangeCheck())
            ApplyBodyFramePreview();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Preview uses hidden temporary instances and does not mutate scene or prefab contents. Use each tab's Apply button to save changes.",
            MessageType.Info);
    }

    private void DrawCompositeTab()
    {
        EditorGUILayout.LabelField("Composite Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Composite mode overlays the selected body clip, one selected VFX prefab, socket markers, and EgoSword markers in the same preview space. It is still an authoring preview, not a full pattern coroutine simulation.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        compositeShowBody = EditorGUILayout.Toggle("Show Body Clip", compositeShowBody);
        using (new EditorGUI.DisabledScope(!compositeShowBody))
        {
            selectedAnimationClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Body Clip",
                selectedAnimationClip,
                typeof(AnimationClip),
                allowSceneObjects: false);
        }

        compositeShowVfx = EditorGUILayout.Toggle("Show VFX", compositeShowVfx);
        using (new EditorGUI.DisabledScope(!compositeShowVfx))
        {
            selectedVfxPrefab = (GameObject)EditorGUILayout.ObjectField(
                "VFX Prefab",
                selectedVfxPrefab,
                typeof(GameObject),
                allowSceneObjects: false);
            compositeVfxSocket = (DemonKingVfxSocketId)EditorGUILayout.EnumPopup("VFX Socket", compositeVfxSocket);
            compositeFallbackLeftOffset = EditorGUILayout.Vector2Field("Fallback Left Offset", compositeFallbackLeftOffset);
            compositeVfxPreviewScale = EditorGUILayout.Vector3Field("Preview VFX Scale", compositeVfxPreviewScale);
            compositeVfxRotationDeg = EditorGUILayout.FloatField("Preview VFX Rotation", compositeVfxRotationDeg);
        }

        compositeShowSockets = EditorGUILayout.Toggle("Show Socket Markers", compositeShowSockets);
        using (new EditorGUI.DisabledScope(!compositeShowSockets))
        {
            selectedSocketMap = (DemonKingVfxSocketMap)EditorGUILayout.ObjectField(
                "Socket Map",
                selectedSocketMap,
                typeof(DemonKingVfxSocketMap),
                allowSceneObjects: true);
        }

        compositeShowEgoSword = EditorGUILayout.Toggle("Show EgoSword Markers", compositeShowEgoSword);
        using (new EditorGUI.DisabledScope(!compositeShowEgoSword))
        {
            selectedEgoSword = (EgoSwordActor)EditorGUILayout.ObjectField(
                "EgoSword",
                selectedEgoSword,
                typeof(EgoSwordActor),
                allowSceneObjects: true);
        }

        if (EditorGUI.EndChangeCheck())
        {
            previewSubject = PreviewSubject.Composite;
            LoadClipRows(selectedAnimationClip);
            LoadTimedHitFramesFromPrefab();
            RestartPreview();
        }

        if (GUILayout.Button("Show Composite Preview"))
        {
            previewSubject = PreviewSubject.Composite;
            RestartPreview();
        }
    }

    private void DrawAnimationTab()
    {
        EditorGUILayout.LabelField("Body / VFX Animation Clip", EditorStyles.boldLabel);
        DrawAnimationClipSelector();
        if (selectedAnimationClip == null)
        {
            EditorGUILayout.HelpBox("Select a DarkLord or DemonKing VFX AnimationClip.", MessageType.Info);
            return;
        }

        EnsureClipRowsLoaded();

        EditorGUI.BeginChangeCheck();
        stagedFrameRate = EditorGUILayout.FloatField("Frame Rate", Mathf.Max(0.01f, stagedFrameRate));
        stagedLoopTime = EditorGUILayout.Toggle("Loop Time", stagedLoopTime);
        stagedClipLength = EditorGUILayout.FloatField("Clip Length", Mathf.Max(0.01f, stagedClipLength));
        if (EditorGUI.EndChangeCheck())
            clipRowsDirty = true;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Frame", GUILayout.Width(92f)))
            {
                float nextTime = clipRows.Count > 0
                    ? clipRows[clipRows.Count - 1].Time + 1f / Mathf.Max(0.01f, stagedFrameRate)
                    : 0f;
                clipRows.Add(new SpriteFrameRow
                {
                    Time = nextTime,
                    Sprite = clipRows.Count > 0 ? clipRows[clipRows.Count - 1].Sprite : null
                });
                stagedClipLength = Mathf.Max(stagedClipLength, nextTime + 1f / Mathf.Max(0.01f, stagedFrameRate));
                clipRowsDirty = true;
            }

            using (new EditorGUI.DisabledScope(!clipRowsDirty))
            {
                if (GUILayout.Button("Revert", GUILayout.Width(80f)))
                    LoadClipRows(selectedAnimationClip);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!clipRowsDirty))
            {
                if (GUILayout.Button("Apply Clip", GUILayout.Width(96f)))
                    ApplyClipRows();
            }
        }

        DrawFrameRows();
    }

    private void DrawVfxTab()
    {
        EditorGUILayout.LabelField("VFX Prefab / Hit Timing", EditorStyles.boldLabel);
        DrawVfxPrefabSelector();
        if (selectedVfxPrefab == null)
        {
            EditorGUILayout.HelpBox("Select a Resources/DemonKing/Vfx prefab.", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        Vector3 nextRootScale = EditorGUILayout.Vector3Field("Root Scale", selectedVfxPrefab.transform.localScale);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedVfxPrefab.transform, "Tune DemonKing VFX Root Scale");
            selectedVfxPrefab.transform.localScale = nextRootScale;
            EditorUtility.SetDirty(selectedVfxPrefab);
            SavePrefabAssetIfPersistent(selectedVfxPrefab);
            RestartPreview();
        }

        DrawTimedHitEffectEditor(selectedVfxPrefab);
        DrawVfxColliderEditor(selectedVfxPrefab);
    }

    private void DrawPatternTab()
    {
        EditorGUILayout.LabelField("Pattern Workbench", EditorStyles.boldLabel);
        DrawAbilityLogicSelector();
        if (selectedAbilityLogicAsset == null)
        {
            EditorGUILayout.HelpBox("Select an AL_DemonKing_* asset.", MessageType.Info);
            return;
        }

        SerializedObject serializedAsset = new(selectedAbilityLogicAsset);
        serializedAsset.Update();
        if (lastPatternWorkbenchAsset != selectedAbilityLogicAsset)
        {
            lastPatternWorkbenchAsset = selectedAbilityLogicAsset;
            patternSelectedPhaseIndex = 0;
            patternPhasePreviewTime = 0f;
            serializedScroll = Vector2.zero;
        }

        DemonKingPatternPreviewDefinition definition = CreatePatternPreviewDefinition(serializedAsset);
        if (definition == null)
        {
            EditorGUILayout.HelpBox(
                "This AL asset has no DemonKing pattern preview definition yet. Full serialized editing remains available below.",
                MessageType.Info);
        }
        else
        {
            DrawPatternWorkbench(serializedAsset, definition);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Full Serialized Asset", EditorStyles.boldLabel);
        DrawSerializedObjectEditor(serializedAsset, "Apply Pattern Asset", false);
    }

    private void DrawPatternControlPanel()
    {
        EditorGUILayout.LabelField("Pattern Workbench", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        DrawAbilityLogicSelector();
        if (EditorGUI.EndChangeCheck())
            OnPatternAssetChanged();

        if (selectedAbilityLogicAsset == null)
        {
            EditorGUILayout.HelpBox("Select an AL_DemonKing_* asset. The selected pattern drives the preview target automatically.", MessageType.Info);
            return;
        }

        SerializedObject serializedAsset = new(selectedAbilityLogicAsset);
        serializedAsset.Update();
        ResetPatternWorkbenchIfAssetChanged();

        DemonKingPatternPreviewDefinition definition = CreatePatternPreviewDefinition(serializedAsset);
        if (definition == null)
        {
            EditorGUILayout.HelpBox("No pattern preview definition exists for this AL yet.", MessageType.Info);
            DrawSerializedObjectEditor(serializedAsset, "Apply Pattern Asset", false);
            return;
        }

        EditorGUILayout.HelpBox(definition.Description, MessageType.Info);
        DrawPatternEgoSwordSelector(definition);
        DrawPatternMappingControls(serializedAsset, definition);
        DrawPatternQuickControls(serializedAsset, definition);

        definition = CreatePatternPreviewDefinition(serializedAsset);
        if (definition != null && definition.Phases.Count > 0)
        {
            patternSelectedPhaseIndex = Mathf.Clamp(patternSelectedPhaseIndex, 0, definition.Phases.Count - 1);
            DemonKingPatternPreviewPhase selectedPhase = definition.Phases[patternSelectedPhaseIndex];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Phase", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(selectedPhase.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Body", string.IsNullOrEmpty(selectedPhase.BodyClipName) ? "(none)" : selectedPhase.BodyClipName);
                EditorGUILayout.LabelField("Body Field", string.IsNullOrEmpty(selectedPhase.BodyPropertyPath) ? "(none)" : selectedPhase.BodyPropertyPath);
                EditorGUILayout.LabelField("VFX", string.IsNullOrEmpty(selectedPhase.VfxPrefabName) ? "(none)" : selectedPhase.VfxPrefabName);
                EditorGUILayout.LabelField("Socket", selectedPhase.HasSocket ? selectedPhase.SocketId.ToString() : "(none)");
                EditorGUILayout.LabelField("Policy", selectedPhase.Policy);
                if (!string.IsNullOrEmpty(selectedPhase.Notes))
                    EditorGUILayout.HelpBox(selectedPhase.Notes, MessageType.None);
            }
        }

        showFullPatternSerialized = EditorGUILayout.Foldout(showFullPatternSerialized, "Full Serialized Asset", true);
        if (showFullPatternSerialized)
            DrawSerializedObjectEditor(serializedAsset, "Apply Pattern Asset", false);
    }

    private void DrawPatternEgoSwordSelector(DemonKingPatternPreviewDefinition definition)
    {
        bool wantsEgoSword = definition != null && definition.PrefersEgoSwordPreview;
        if (!wantsEgoSword && selectedEgoSword == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("EgoSword Preview", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            selectedEgoSword = (EgoSwordActor)EditorGUILayout.ObjectField(
                "EgoSword Actor",
                selectedEgoSword,
                typeof(EgoSwordActor),
                allowSceneObjects: true);
            if (EditorGUI.EndChangeCheck())
            {
                patternPreviewNeedsRebuild = true;
                RestartPreview();
            }

            if (selectedEgoSword == null)
            {
                EditorGUILayout.HelpBox(
                    "Select the scene EgoSwordActor to preview sword sprite, spin, vertical strike, and laser positioning. Marker-only preview is used until one is assigned.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Source", selectedEgoSword.name);
            }
        }
    }

    private DemonKingPatternPreviewDefinition CreatePatternPreviewDefinition(SerializedObject serializedAsset)
    {
        DemonKingPatternPreviewDefinition definition =
            DemonKingPatternPreviewDefinition.Create(selectedAbilityLogicAsset, serializedAsset, selectedEgoSword);
        return definition;
    }

    private void DrawPatternTimelinePanel()
    {
        if (selectedAbilityLogicAsset == null)
        {
            EditorGUILayout.HelpBox("Pattern timeline appears after selecting an AL_DemonKing_* asset.", MessageType.Info);
            return;
        }

        SerializedObject serializedAsset = new(selectedAbilityLogicAsset);
        serializedAsset.Update();
        ResetPatternWorkbenchIfAssetChanged();
        DemonKingPatternPreviewDefinition definition = CreatePatternPreviewDefinition(serializedAsset);
        if (definition == null || definition.Phases.Count == 0)
        {
            EditorGUILayout.HelpBox("This pattern has no timeline descriptor.", MessageType.Info);
            return;
        }

        float totalDuration = Mathf.Max(0.01f, definition.TotalDuration);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"{definition.DisplayName} Timeline", EditorStyles.boldLabel, GUILayout.Width(180f));
            if (GUILayout.Button(previewPlaying ? "Pause" : "Play", GUILayout.Width(62f)))
                previewPlaying = !previewPlaying;
            if (GUILayout.Button("Restart", GUILayout.Width(70f)))
                RestartPatternTimeline(definition);
            loopPreview = GUILayout.Toggle(loopPreview, "Loop", GUILayout.Width(52f));
            patternShowWarningShape = GUILayout.Toggle(patternShowWarningShape, "Warning/Hit", GUILayout.Width(92f));
            GUILayout.Label("Speed", GUILayout.Width(42f));
            previewSpeed = EditorGUILayout.Slider(previewSpeed, 0.05f, 3f, GUILayout.Width(150f));
            GUILayout.Label($"{patternTimelineTime:0.00}s / {totalDuration:0.00}s", GUILayout.Width(116f));
            GUILayout.FlexibleSpace();
        }

        EditorGUI.BeginChangeCheck();
        patternTimelineTime = EditorGUILayout.Slider(patternTimelineTime, 0f, totalDuration);
        if (EditorGUI.EndChangeCheck())
        {
            patternAppliedPhaseIndex = -1;
            ApplyPatternTimelineTime(definition, restartOnPhaseChange: true);
        }

        patternTimelineScroll = EditorGUILayout.BeginScrollView(patternTimelineScroll);
        for (int i = 0; i < definition.Phases.Count; i++)
        {
            DemonKingPatternPreviewPhase phase = definition.Phases[i];
            bool selected = i == patternSelectedPhaseIndex;
            string label = $"{phase.StartSeconds:0.00}-{phase.EndSeconds:0.00}s  [{phase.Category}] {phase.Name}";
            GUIStyle style = selected ? EditorStyles.miniButtonMid : EditorStyles.toolbarButton;
            if (GUILayout.Toggle(selected, label, style) != selected)
            {
                patternSelectedPhaseIndex = i;
                patternTimelineTime = phase.StartSeconds + Mathf.Clamp(phase.DefaultPreviewTime, 0f, Mathf.Max(0.01f, phase.DurationSeconds));
                ApplyPatternTimelineTime(definition, restartOnPhaseChange: true);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawPatternWorkbench(SerializedObject serializedAsset, DemonKingPatternPreviewDefinition definition)
    {
        if (definition == null)
            return;

        EditorGUILayout.HelpBox(definition.Description, MessageType.Info);
        DrawPatternMappingControls(serializedAsset, definition);
        DrawPatternQuickControls(serializedAsset, definition);

        definition = CreatePatternPreviewDefinition(serializedAsset);
        if (definition == null || definition.Phases.Count == 0)
            return;

        patternSelectedPhaseIndex = Mathf.Clamp(patternSelectedPhaseIndex, 0, definition.Phases.Count - 1);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Phase Timeline", EditorStyles.boldLabel);
        patternShowWarningShape = EditorGUILayout.Toggle("Show Warning / Hit Shape", patternShowWarningShape);

        patternTimelineScroll = EditorGUILayout.BeginScrollView(patternTimelineScroll, GUILayout.MinHeight(118f), GUILayout.MaxHeight(190f));
        for (int i = 0; i < definition.Phases.Count; i++)
        {
            DemonKingPatternPreviewPhase phase = definition.Phases[i];
            bool selected = i == patternSelectedPhaseIndex;
            string label = $"{phase.StartSeconds:0.00}s - {phase.EndSeconds:0.00}s  [{phase.Category}]  {phase.Name}";
            if (GUILayout.Toggle(selected, label, "Button") != selected)
            {
                patternSelectedPhaseIndex = i;
                patternPhasePreviewTime = Mathf.Clamp(phase.DefaultPreviewTime, 0f, Mathf.Max(0.01f, phase.DurationSeconds));
                ApplyPatternPreviewPhase(phase, restartPreview: true);
            }
        }

        EditorGUILayout.EndScrollView();

        DemonKingPatternPreviewPhase selectedPhase = definition.Phases[patternSelectedPhaseIndex];
        activePatternPreviewShape = patternShowWarningShape ? selectedPhase.Shape : null;
        EditorGUI.BeginChangeCheck();
        patternPhasePreviewTime = EditorGUILayout.Slider(
            "Phase Preview Time",
            Mathf.Clamp(patternPhasePreviewTime, 0f, Mathf.Max(0.01f, selectedPhase.DurationSeconds)),
            0f,
            Mathf.Max(0.01f, selectedPhase.DurationSeconds));
        if (EditorGUI.EndChangeCheck())
            ApplyPatternPreviewPhase(selectedPhase, restartPreview: true);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(selectedPhase.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Body", string.IsNullOrEmpty(selectedPhase.BodyClipName) ? "(none)" : selectedPhase.BodyClipName);
            EditorGUILayout.LabelField("Body Field", string.IsNullOrEmpty(selectedPhase.BodyPropertyPath) ? "(none)" : selectedPhase.BodyPropertyPath);
            EditorGUILayout.LabelField("VFX", string.IsNullOrEmpty(selectedPhase.VfxPrefabName) ? "(none)" : selectedPhase.VfxPrefabName);
            EditorGUILayout.LabelField("Socket", selectedPhase.HasSocket ? selectedPhase.SocketId.ToString() : "(none)");
            EditorGUILayout.LabelField("Policy", selectedPhase.Policy);
            if (!string.IsNullOrEmpty(selectedPhase.Notes))
                EditorGUILayout.HelpBox(selectedPhase.Notes, MessageType.None);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Selected Phase", GUILayout.Width(160f)))
                ApplyPatternPreviewPhase(selectedPhase, restartPreview: true);

            if (GUILayout.Button("Open Composite Tab", GUILayout.Width(140f)))
            {
                ApplyPatternPreviewPhase(selectedPhase, restartPreview: true);
                selectedTab = ToolTab.Composite;
            }
        }
    }

    private void DrawPatternMappingControls(SerializedObject serializedAsset, DemonKingPatternPreviewDefinition definition)
    {
        if (serializedAsset == null || definition == null || definition.MappingRows.Count == 0)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Animation / VFX Mapping", EditorStyles.boldLabel);
        DrawBodyCueCoverageDiagnostics(definition);
        for (int i = 0; i < definition.MappingRows.Count; i++)
        {
            DemonKingPatternPreviewMappingRow row = definition.MappingRows[i];
            SerializedObject targetObject = ResolveMappingSerializedObject(row);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(row.Label, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        row.Source == DemonKingPatternPreviewMappingSource.EgoSwordActor ? "EgoSwordActor" : "AL Asset",
                        EditorStyles.miniLabel,
                        GUILayout.Width(92f));
                }

                if (targetObject == null)
                {
                    EditorGUILayout.HelpBox("Select an EgoSwordActor to edit this mapping.", MessageType.Warning);
                    continue;
                }

                targetObject.Update();
                EditorGUI.BeginChangeCheck();
                DrawBodyAnimationMapping(targetObject, row.BodyPropertyPath);
                DrawVfxCueMapping(targetObject, row.VfxPropertyPath);
                DrawObjectVfxMapping(targetObject, row.ObjectVfxPropertyPath, "VFX Prefab Override");
                DrawStringMapping(targetObject, row.ResourcePathPropertyPath, "Resources Path");
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetObject.targetObject, "Tune DemonKing Pattern Mapping");
                    targetObject.ApplyModifiedProperties();
                    MarkEditedObjectDirty(targetObject.targetObject);
                    if (serializedAsset.targetObject == targetObject.targetObject)
                        serializedAsset.Update();
                    patternPreviewNeedsRebuild = true;
                    RestartPreview();
                }
            }
        }
    }

    private static void DrawBodyCueCoverageDiagnostics(DemonKingPatternPreviewDefinition definition)
    {
        if (definition == null || definition.Phases.Count == 0)
            return;

        List<string> missing = new();
        for (int i = 0; i < definition.Phases.Count; i++)
        {
            DemonKingPatternPreviewPhase phase = definition.Phases[i];
            if (phase == null
                || string.IsNullOrWhiteSpace(phase.BodyClipName)
                || phase.HasEditableBodyCue)
            {
                continue;
            }

            string key = $"[{phase.Category}] {phase.Name}: {phase.BodyClipName}";
            if (!missing.Contains(key))
                missing.Add(key);
        }

        if (missing.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Body Cue Coverage: every previewed body pose in this pattern is connected to an editable BodyAnimationRef field.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "Body Cue Coverage missing editable fields:\n" + string.Join("\n", missing),
            MessageType.Warning);
    }

    private SerializedObject ResolveMappingSerializedObject(DemonKingPatternPreviewMappingRow row)
    {
        if (row == null)
            return null;

        if (row.Source == DemonKingPatternPreviewMappingSource.EgoSwordActor)
            return selectedEgoSword != null ? new SerializedObject(selectedEgoSword) : null;

        return selectedAbilityLogicAsset != null ? new SerializedObject(selectedAbilityLogicAsset) : null;
    }

    private static void DrawBodyAnimationMapping(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(propertyPath))
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property == null)
        {
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(propertyPath), "Missing serialized body animation field");
            return;
        }

        EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(propertyPath), EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        DrawRelativeProperty(property, "clip", "Clip");
        DrawRelativeProperty(property, "sampleMode", "Sample Mode");
        DrawRelativeProperty(property, "frameIndex", "Frame Index");
        DrawRelativeProperty(property, "fallbackStateName", "Fallback State");
        EditorGUI.indentLevel--;
    }

    private static void DrawVfxCueMapping(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(propertyPath))
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property == null)
        {
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(propertyPath), "Missing serialized VFX cue field");
            return;
        }

        EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(propertyPath), EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        DrawRelativeProperty(property, "prefabOverride", "Prefab Override");
        DrawRelativeProperty(property, "fallbackKind", "Fallback Kind");
        DrawRelativeProperty(property, "socketId", "Socket");
        DrawRelativeProperty(property, "fallbackLeftOffset", "Fallback Left Offset");
        DrawRelativeProperty(property, "targetSize", "Target Size");
        DrawRelativeProperty(property, "scale", "Scale");
        DrawRelativeProperty(property, "rotationOffsetDeg", "Rotation Offset");
        DrawRelativeProperty(property, "flipX", "Flip X");
        DrawRelativeProperty(property, "leaveFragment", "Leave Fragment");
        EditorGUI.indentLevel--;
    }

    private static void DrawRelativeProperty(SerializedProperty parent, string relativePath, string label)
    {
        SerializedProperty property = parent?.FindPropertyRelative(relativePath);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private static void DrawObjectVfxMapping(SerializedObject serializedObject, string propertyPath, string label)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(propertyPath))
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private static void DrawStringMapping(SerializedObject serializedObject, string propertyPath, string label)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(propertyPath))
            return;

        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void DrawPatternQuickControls(SerializedObject serializedAsset, DemonKingPatternPreviewDefinition definition)
    {
        if (serializedAsset == null || definition == null)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Pattern Quick Controls", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        foreach (DemonKingPatternPreviewFieldGroup group in definition.FieldGroups)
        {
            List<string> visibleProperties = group.PropertyNames
                .Where(propertyName => !definition.MappingPropertyPaths.Contains(propertyName)
                    && serializedAsset.FindProperty(propertyName) != null)
                .ToList();
            if (visibleProperties.Count == 0)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel);
                foreach (string propertyName in visibleProperties)
                {
                    SerializedProperty property = serializedAsset.FindProperty(propertyName);
                    if (property == null)
                        continue;

                    bool legacyNoRuntimeEffect = definition.LegacyNoRuntimeEffectProperties.Contains(propertyName);
                    GUIContent label = new(
                        legacyNoRuntimeEffect
                            ? $"{ObjectNames.NicifyVariableName(propertyName)} (legacy/no runtime effect)"
                            : ObjectNames.NicifyVariableName(propertyName));
                    using (new EditorGUI.DisabledScope(legacyNoRuntimeEffect))
                        EditorGUILayout.PropertyField(property, label, includeChildren: true);
                }
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(serializedAsset.targetObject, "Tune DemonKing Pattern Quick Controls");
            serializedAsset.ApplyModifiedProperties();
            MarkEditedObjectDirty(serializedAsset.targetObject);
            RestartPreview();
        }
    }

    private void DrawRuntimePatternExecutionPanel()
    {
        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Actual Pattern Runner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play Mode only. Runs the selected AL through the live DemonKing AbilitySystem so sockets, VFX, warnings, movement, and cleanup use the same runtime code as the fight.",
                EditorApplication.isPlaying ? MessageType.Info : MessageType.Warning);

            runtimePatternDemonKing = (DemonKingController)EditorGUILayout.ObjectField(
                "Runtime DemonKing",
                runtimePatternDemonKing,
                typeof(DemonKingController),
                true);
            runtimePatternTarget = (Transform)EditorGUILayout.ObjectField(
                "Runtime Target",
                runtimePatternTarget,
                typeof(Transform),
                true);
            runtimePatternCancelCurrentBeforeRun = EditorGUILayout.ToggleLeft(
                "Cancel current DemonKing ability before run",
                runtimePatternCancelCurrentBeforeRun);
            bool previousSelectedOnly = runtimePatternSelectedOnly;
            bool previousPlayerInvulnerable = runtimePatternPlayerInvulnerable;
            runtimePatternSelectedOnly = EditorGUILayout.ToggleLeft(
                "Use selected pattern only during test",
                runtimePatternSelectedOnly);
            runtimePatternPlayerInvulnerable = EditorGUILayout.ToggleLeft(
                "Make player invulnerable during test",
                runtimePatternPlayerInvulnerable);
            if (previousSelectedOnly && !runtimePatternSelectedOnly)
                RestoreRuntimePatternSelectedOnly();
            if (previousPlayerInvulnerable && !runtimePatternPlayerInvulnerable)
                ReleaseRuntimePatternPlayerInvulnerability();
            runtimePatternPlaybackMode = (RuntimePatternPlaybackMode)EditorGUILayout.EnumPopup(
                "Playback Mode",
                runtimePatternPlaybackMode);

            EditorGUILayout.Space(4f);
            runtimePatternLivePreviewEnabled = EditorGUILayout.ToggleLeft(
                "Show live scene in preview window",
                runtimePatternLivePreviewEnabled);
            using (new EditorGUI.DisabledScope(!runtimePatternLivePreviewEnabled))
            {
                runtimePatternLivePreviewFrameTarget = EditorGUILayout.ToggleLeft(
                    "Auto-frame target with DemonKing",
                    runtimePatternLivePreviewFrameTarget);
                runtimePatternLivePreviewUseGameCameraMask = EditorGUILayout.ToggleLeft(
                    "Use Game Camera culling mask",
                    runtimePatternLivePreviewUseGameCameraMask);
                runtimePatternLivePreviewCameraSize = EditorGUILayout.Slider(
                    "Live Camera Size",
                    runtimePatternLivePreviewCameraSize,
                    1f,
                    14f);
                runtimePatternLivePreviewPadding = EditorGUILayout.Slider(
                    "Frame Padding",
                    runtimePatternLivePreviewPadding,
                    0f,
                    5f);
                if (!EditorApplication.isPlaying)
                    EditorGUILayout.HelpBox("Live preview appears here after entering Play Mode.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find Live DemonKing"))
                    runtimePatternDemonKing = Object.FindAnyObjectByType<DemonKingController>(FindObjectsInactive.Exclude);

                if (GUILayout.Button("Use Selection"))
                    AssignRuntimePatternSelection();
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Runtime State"))
                    RefreshRuntimePatternState();

                if (GUILayout.Button("Move Player To Center"))
                    MoveRuntimePatternPlayerToCenter();
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || selectedAbilityLogicAsset == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Actual Pattern"))
                    RunSelectedPatternActual();

                if (GUILayout.Button("Cancel Actual Pattern"))
                {
                    CleanupRuntimePatternAbility(cancelExecution: true);
                    runtimePatternStatus = "Actual pattern runner cancelled and cleaned up.";
                }
            }

            if (!string.IsNullOrWhiteSpace(runtimePatternStatus))
                EditorGUILayout.HelpBox(runtimePatternStatus, MessageType.None);
        }
    }

    private void AssignRuntimePatternSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return;

        DemonKingController demon = selected.GetComponentInParent<DemonKingController>();
        if (demon == null)
            demon = selected.GetComponentInChildren<DemonKingController>(true);

        if (demon != null)
        {
            runtimePatternDemonKing = demon;
            return;
        }

        runtimePatternTarget = selected.transform;
    }

    private void RunSelectedPatternActual()
    {
        StartRuntimePatternActivation(loopRestart: false);
    }

    private void RefreshRuntimePatternState()
    {
        if (!EditorApplication.isPlaying)
        {
            runtimePatternStatus = "Enter Play Mode before refreshing runtime state.";
            return;
        }

        DemonKingController demon = ResolveRuntimePatternDemonKing();
        if (demon == null)
        {
            runtimePatternStatus = "No live DemonKingController found to refresh.";
            return;
        }

        CleanupRuntimePatternAbility(cancelExecution: true);
        demon.RefreshWorkbenchRuntimeState();
        runtimePatternStatus = $"Refreshed runtime state for '{demon.name}'.";
    }

    private void MoveRuntimePatternPlayerToCenter()
    {
        if (!EditorApplication.isPlaying)
        {
            runtimePatternStatus = "Enter Play Mode before moving the player.";
            return;
        }

        DemonKingController demon = ResolveRuntimePatternDemonKing();
        GameObject player = ResolveRuntimePatternPlayer(runtimePatternTarget != null ? runtimePatternTarget.gameObject : null);
        if (player == null)
        {
            runtimePatternStatus = "No live Player object found to move.";
            return;
        }

        Vector3 center = demon != null ? demon.ArenaCenterPosition : Vector3.zero;
        player.GetComponent<MovementMotor2D>()?.StopAllMotion();
        if (player.TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        player.transform.position = new Vector3(center.x, center.y, player.transform.position.z);
        runtimePatternTarget = player.transform;
        runtimePatternStatus = demon != null
            ? $"Moved player to '{demon.name}' arena center."
            : "Moved player to world origin because no live DemonKing was found.";
    }

    private bool StartRuntimePatternActivation(bool loopRestart)
    {
        if (!EditorApplication.isPlaying)
        {
            runtimePatternStatus = "Enter Play Mode before running an actual pattern.";
            return false;
        }

        if (selectedAbilityLogicAsset is not AbilityLogic logic)
        {
            runtimePatternStatus = "Selected asset is not an AbilityLogic.";
            return false;
        }

        DemonKingController demon = ResolveRuntimePatternDemonKing();
        if (demon == null)
        {
            runtimePatternStatus = "No live DemonKingController found. Assign one or click Find Live DemonKing in Play Mode.";
            return false;
        }

        AbilitySystem abilitySystem = demon.GetComponent<AbilitySystem>();
        if (abilitySystem == null)
        {
            runtimePatternStatus = $"'{demon.name}' has no AbilitySystem.";
            return false;
        }

        GameObject target = ResolveRuntimePatternTarget(demon);

        if (!loopRestart)
            CleanupRuntimePatternAbility(cancelExecution: true);

        if (!loopRestart && runtimePatternCancelCurrentBeforeRun)
        {
            abilitySystem.ResetTransientRuntimeState();
        }
        else if (abilitySystem.IsBusy)
        {
            runtimePatternStatus = "DemonKing AbilitySystem is busy. Enable cancel-before-run or wait for the current ability to finish.";
            return false;
        }

        BeginRuntimePatternTestSession(demon, target);

        AbilityDefinition runtimeAbility = ScriptableObject.CreateInstance<AbilityDefinition>();
        runtimeAbility.hideFlags = HideFlags.HideAndDontSave;
        runtimeAbility.abilityName = $"Workbench Actual - {selectedAbilityLogicAsset.name}";
        runtimeAbility.logic = logic;
        runtimeAbility.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;
        runtimeAbility.cooldown = 0f;
        runtimeAbility.castTime = 0f;
        runtimeAbility.recoveryTime = 0f;
        runtimeAbility.canCastWhileMoving = true;
        runtimeAbility.interruptible = true;
        runtimeAbility.requireTargetObject = false;

        AbilitySpec spec = abilitySystem.GiveAbility(runtimeAbility);
        if (!abilitySystem.TryActivateAbility(spec, target))
        {
            abilitySystem.TakeAbility(runtimeAbility);
            DestroyImmediate(runtimeAbility);
            if (!loopRestart)
                CleanupRuntimePatternTestSession();
            runtimePatternStatus = "Actual pattern activation was rejected by the live AbilitySystem.";
            return false;
        }

        runtimePatternAbilitySystem = abilitySystem;
        runtimePatternAbilityDefinition = runtimeAbility;
        runtimePatternStartedAt = EditorApplication.timeSinceStartup;
        string modeLabel = runtimePatternPlaybackMode == RuntimePatternPlaybackMode.Loop ? "Looping" : "Running";
        runtimePatternStatus = $"{modeLabel} actual pattern on '{demon.name}' from '{selectedAbilityLogicAsset.name}'.";
        return true;
    }

    private DemonKingController ResolveRuntimePatternDemonKing()
    {
        if (runtimePatternDemonKing != null && runtimePatternDemonKing.isActiveAndEnabled)
            return runtimePatternDemonKing;

        runtimePatternDemonKing = Object.FindAnyObjectByType<DemonKingController>(FindObjectsInactive.Exclude);
        return runtimePatternDemonKing;
    }

    private GameObject ResolveRuntimePatternTarget(DemonKingController demon)
    {
        if (runtimePatternTarget != null && runtimePatternTarget.gameObject.activeInHierarchy)
            return runtimePatternTarget.gameObject;

        if (demon != null && demon.CurrentTarget != null)
        {
            runtimePatternTarget = demon.CurrentTarget;
            return demon.CurrentTarget.gameObject;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            runtimePatternTarget = player.transform;
            return player;
        }

        return null;
    }

    private void BeginRuntimePatternTestSession(DemonKingController demon, GameObject target)
    {
        if (runtimePatternSelectedOnly)
            BeginRuntimePatternSelectedOnly(demon);

        if (runtimePatternPlayerInvulnerable)
            ApplyRuntimePatternPlayerInvulnerability(target);
    }

    private void BeginRuntimePatternSelectedOnly(DemonKingController demon)
    {
        if (demon == null)
            return;

        if (runtimePatternIsolatedDemon == demon)
            return;

        RestoreRuntimePatternSelectedOnly();
        runtimePatternIsolatedDemon = demon;
        runtimePatternShouldRestoreBossCombat = demon.IsCombatActive;
        if (demon.IsCombatActive)
            demon.SetCombatActive(false);
    }

    private void RestoreRuntimePatternSelectedOnly()
    {
        DemonKingController demon = runtimePatternIsolatedDemon;
        bool shouldRestore = runtimePatternShouldRestoreBossCombat;
        runtimePatternIsolatedDemon = null;
        runtimePatternShouldRestoreBossCombat = false;

        if (!EditorApplication.isPlaying || demon == null || !shouldRestore || demon.IsDead)
            return;

        demon.SetCombatActive(true);
    }

    private void ApplyRuntimePatternPlayerInvulnerability(GameObject preferredTarget)
    {
        if (runtimePatternInvulnerableApplied)
            return;

        GameObject player = ResolveRuntimePatternPlayer(preferredTarget);
        if (player == null)
            return;

        TagSystem tagSystem = player.GetComponent<TagSystem>();
        if (tagSystem == null)
            return;

        runtimePatternInvulnerableTag ??= Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        if (runtimePatternInvulnerableTag == null)
            return;

        tagSystem.AddTag(runtimePatternInvulnerableTag, 1);
        runtimePatternInvulnerableTagSystem = tagSystem;
        runtimePatternInvulnerableApplied = true;
    }

    private static GameObject ResolveRuntimePatternPlayer(GameObject preferredTarget)
    {
        if (preferredTarget != null && preferredTarget.CompareTag("Player"))
            return preferredTarget;

        try
        {
            return GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private void ReleaseRuntimePatternPlayerInvulnerability()
    {
        if (!runtimePatternInvulnerableApplied)
            return;

        if (runtimePatternInvulnerableTagSystem != null && runtimePatternInvulnerableTag != null)
            runtimePatternInvulnerableTagSystem.RemoveTag(runtimePatternInvulnerableTag, 1);

        runtimePatternInvulnerableTagSystem = null;
        runtimePatternInvulnerableApplied = false;
    }

    private void CleanupRuntimePatternTestSession()
    {
        ReleaseRuntimePatternPlayerInvulnerability();
        RestoreRuntimePatternSelectedOnly();
    }

    private void TickRuntimePatternExecution()
    {
        if (runtimePatternAbilityDefinition == null)
            return;

        if (!EditorApplication.isPlaying)
        {
            CleanupRuntimePatternAbility(cancelExecution: false);
            runtimePatternStatus = "Play Mode ended; actual pattern runner cleaned up.";
            return;
        }

        if (runtimePatternAbilitySystem == null)
        {
            CleanupRuntimePatternAbility(cancelExecution: false);
            runtimePatternStatus = "Actual pattern runner lost its AbilitySystem and cleaned up.";
            return;
        }

        AbilitySpec spec = runtimePatternAbilitySystem.FindSpec(runtimePatternAbilityDefinition);
        bool isCurrent = spec != null &&
            (runtimePatternAbilitySystem.CurrentExecSpec == spec || runtimePatternAbilitySystem.CurrentCastSpec == spec);
        if (spec == null || isCurrent)
            return;

        double elapsed = EditorApplication.timeSinceStartup - runtimePatternStartedAt;
        runtimePatternAbilitySystem.TakeAbility(runtimePatternAbilityDefinition);
        DestroyImmediate(runtimePatternAbilityDefinition);
        runtimePatternAbilityDefinition = null;
        runtimePatternAbilitySystem = null;

        if (runtimePatternPlaybackMode == RuntimePatternPlaybackMode.Loop && EditorApplication.isPlaying)
        {
            runtimePatternStatus = $"Actual pattern completed in {elapsed:0.00}s; restarting loop.";
            if (!StartRuntimePatternActivation(loopRestart: true))
                CleanupRuntimePatternTestSession();
            return;
        }

        CleanupRuntimePatternTestSession();
        runtimePatternStatus = $"Actual pattern completed in {elapsed:0.00}s.";
    }

    private void CleanupRuntimePatternAbility(bool cancelExecution)
    {
        AbilityDefinition ability = runtimePatternAbilityDefinition;
        AbilitySystem abilitySystem = runtimePatternAbilitySystem;

        runtimePatternAbilityDefinition = null;
        runtimePatternAbilitySystem = null;

        if (abilitySystem != null && ability != null)
        {
            AbilitySpec spec = abilitySystem.FindSpec(ability);
            if (cancelExecution && spec != null &&
                (abilitySystem.CurrentExecSpec == spec || abilitySystem.CurrentCastSpec == spec))
            {
                abilitySystem.ResetTransientRuntimeState();
            }

            abilitySystem.TakeAbility(ability);
        }

        if (ability != null)
            DestroyImmediate(ability);

        CleanupRuntimePatternTestSession();
    }

    private void OnPatternAssetChanged()
    {
        lastPatternWorkbenchAsset = null;
        patternSelectedPhaseIndex = 0;
        patternAppliedPhaseIndex = -1;
        patternTimelineTime = 0f;
        patternLastAppliedTimelineTime = 0f;
        patternPhasePreviewTime = 0f;
        patternPreviewNeedsRebuild = true;
        serializedScroll = Vector2.zero;
        previewSubject = PreviewSubject.Composite;
        RestartPreview();
    }

    private void ResetPatternWorkbenchIfAssetChanged()
    {
        if (lastPatternWorkbenchAsset == selectedAbilityLogicAsset)
            return;

        lastPatternWorkbenchAsset = selectedAbilityLogicAsset;
        patternSelectedPhaseIndex = 0;
        patternAppliedPhaseIndex = -1;
        patternTimelineTime = 0f;
        patternLastAppliedTimelineTime = 0f;
        patternPhasePreviewTime = 0f;
        patternPreviewNeedsRebuild = true;
        serializedScroll = Vector2.zero;
    }

    private void RestartPatternTimeline(DemonKingPatternPreviewDefinition definition)
    {
        patternTimelineTime = 0f;
        patternLastAppliedTimelineTime = 0f;
        patternSelectedPhaseIndex = 0;
        patternAppliedPhaseIndex = -1;
        patternPreviewNeedsRebuild = true;
        ApplyPatternTimelineTime(definition, restartOnPhaseChange: true);
    }

    private void ApplyPatternTimelineTime(DemonKingPatternPreviewDefinition definition, bool restartOnPhaseChange)
    {
        if (definition == null || definition.Phases.Count == 0)
            return;

        float totalDuration = Mathf.Max(0.01f, definition.TotalDuration);
        patternTimelineTime = Mathf.Clamp(patternTimelineTime, 0f, totalDuration);
        float previousTimelineTime = patternLastAppliedTimelineTime;
        int nextPhaseIndex = definition.ResolvePhaseIndex(patternTimelineTime);
        if (nextPhaseIndex < 0 || nextPhaseIndex >= definition.Phases.Count)
            return;

        DemonKingPatternPreviewPhase phase = definition.Phases[nextPhaseIndex];
        patternSelectedPhaseIndex = nextPhaseIndex;
        patternPhasePreviewTime = Mathf.Clamp(
            patternTimelineTime - phase.StartSeconds,
            0f,
            Mathf.Max(0.01f, phase.DurationSeconds));

        bool phaseChanged = patternAppliedPhaseIndex != nextPhaseIndex;
        patternAppliedPhaseIndex = nextPhaseIndex;
        bool timeMovedBackward = patternTimelineTime < previousTimelineTime;
        bool forceRebuild = patternPreviewNeedsRebuild || timeMovedBackward || (restartOnPhaseChange && phaseChanged);
        ApplyPatternVisualTimeline(definition, previousTimelineTime, forceRebuild);

        patternLastAppliedTimelineTime = patternTimelineTime;
    }

    private void ApplyPatternVisualTimeline(
        DemonKingPatternPreviewDefinition definition,
        float previousTimelineTime,
        bool forceRebuild)
    {
        if (definition == null)
            return;

        previewSubject = PreviewSubject.Composite;
        compositeShowSockets = true;
        compositeShowEgoSword = definition.PrefersEgoSwordPreview || selectedEgoSword != null;
        activePatternPreviewShapes.Clear();
        activeBodyPreviewCue = null;
        activeEgoSwordPreviewCue = null;

        List<DemonKingPatternPreviewCue> activeCues =
            DemonKingPatternPreviewPlaybackRunner.ResolveActiveCues(definition, patternTimelineTime);
        string signature = DemonKingPatternPreviewPlaybackRunner.BuildSignature(activeCues);
        bool signatureChanged = !string.Equals(signature, activePatternPreviewSignature, StringComparison.Ordinal);
        bool shouldRebuild = forceRebuild || signatureChanged || previewRoot == null;

        if (shouldRebuild)
        {
            RebuildPatternPreviewAtTime(definition, activeCues, patternTimelineTime);
            activePatternPreviewSignature = signature;
            patternPreviewNeedsRebuild = false;
        }
        else
        {
            StepPreviewInstance(Mathf.Max(0f, patternTimelineTime - previousTimelineTime));
            SelectActiveBodyCue(activeCues);
            SelectActiveEgoSwordCue(activeCues);
            UpdateEgoSwordCuePreviewPose();
        }

        if (patternShowWarningShape)
        {
            for (int i = 0; i < activeCues.Count; i++)
            {
                DemonKingPatternPreviewCue cue = activeCues[i];
                if (cue.Kind == DemonKingPatternPreviewCueKind.Shape && cue.Shape != null)
                    activePatternPreviewShapes.Add(cue.Shape);
            }
        }

        activePatternPreviewShape = activePatternPreviewShapes.Count > 0 ? activePatternPreviewShapes[0] : null;
        ApplyBodyFramePreview();
        UpdateEgoSwordCuePreviewPose();
        RenderPreview();
    }

    private void RebuildPatternPreviewAtTime(
        DemonKingPatternPreviewDefinition definition,
        IReadOnlyList<DemonKingPatternPreviewCue> activeCues,
        float timelineSeconds)
    {
        DestroyPreviewInstance();
        EnsurePreviewCamera();
        EnsurePreviewRoot();

        GameObject firstPreviewObject = null;
        SelectActiveBodyCue(activeCues);
        SelectActiveEgoSwordCue(activeCues);

        for (int i = 0; i < activeCues.Count; i++)
        {
            DemonKingPatternPreviewCue cue = activeCues[i];
            float localTime = cue.ResolveLocalTime(timelineSeconds);
            switch (cue.Kind)
            {
                case DemonKingPatternPreviewCueKind.Body:
                    if (bodyPreviewRenderer == null)
                    {
                        GameObject body = CreateBodyPreviewObject("DemonKing_PatternBodyPreview");
                        firstPreviewObject ??= body;
                    }
                    break;
                case DemonKingPatternPreviewCueKind.Vfx:
                    GameObject vfxPrefab = ResolveVfxPrefab(cue);
                    if (vfxPrefab != null)
                    {
                        GameObject vfx = CreateVfxPreviewObject(
                            vfxPrefab,
                            ResolvePreviewCueLocalPosition(cue),
                            cue.VfxScale,
                            cue.VfxRotationDeg,
                            startPlaybackComponents: false);
                        if (vfx != null)
                        {
                            ConfigureSpecialPreviewVfx(vfx, cue);
                            SimulatePreviewObject(vfx, localTime);
                            firstPreviewObject ??= vfx;
                        }
                    }
                    break;
                case DemonKingPatternPreviewCueKind.EgoSword:
                    GameObject sword = CreateEgoSwordCuePreviewObject(cue, localTime);
                    firstPreviewObject ??= sword;
                    break;
            }
        }

        previewInstance = firstPreviewObject;
        CachePreviewPlaybackComponents();
    }

    private void SelectActiveBodyCue(IReadOnlyList<DemonKingPatternPreviewCue> activeCues)
    {
        activeBodyPreviewCue = activeCues?.FirstOrDefault(cue => cue.Kind == DemonKingPatternPreviewCueKind.Body);
        if (activeBodyPreviewCue != null)
        {
            AnimationClip clip = ResolveDarkLordClip(activeBodyPreviewCue.BodyClipName);
            if (clip != null && selectedAnimationClip != clip)
            {
                selectedAnimationClip = clip;
                LoadClipRows(selectedAnimationClip);
            }
        }
    }

    private void SelectActiveEgoSwordCue(IReadOnlyList<DemonKingPatternPreviewCue> activeCues)
    {
        activeEgoSwordPreviewCue = activeCues?.FirstOrDefault(cue => cue.Kind == DemonKingPatternPreviewCueKind.EgoSword);
    }

    private Vector3 ResolvePreviewCueLocalPosition(DemonKingPatternPreviewCue cue)
    {
        if (cue == null)
            return Vector3.zero;

        if (cue.HasSocket && selectedSocketMap != null)
            return selectedSocketMap.ResolveLocalOffset(cue.SocketId, cue.FallbackLeftOffset, previewFacingLeft);

        Vector3 offset = cue.FallbackLeftOffset;
        if (!previewFacingLeft)
            offset.x = -offset.x;
        return offset;
    }

    private GameObject ResolveVfxPrefab(DemonKingPatternPreviewCue cue)
    {
        if (cue == null)
            return null;

        if (cue.VfxPrefabReference != null)
            return cue.VfxPrefabReference;

        GameObject prefab = ResolveVfxPrefab(cue.VfxPrefabName);
        if (prefab != null)
            return prefab;

        if (!string.IsNullOrWhiteSpace(cue.VfxResourcePath))
            prefab = Resources.Load<GameObject>(cue.VfxResourcePath);

        if (prefab == null && !string.IsNullOrWhiteSpace(cue.VfxPrefabName))
            prefab = Resources.Load<GameObject>($"DemonKing/Vfx/{cue.VfxPrefabName}");

        if (prefab == null && !string.IsNullOrWhiteSpace(cue.VfxPrefabName))
            prefab = Resources.Load<GameObject>($"DemonKing/{cue.VfxPrefabName}");

        return prefab;
    }

    private void ConfigureSpecialPreviewVfx(GameObject vfx, DemonKingPatternPreviewCue cue)
    {
        if (vfx == null || cue == null)
            return;

        DemonKingEgoLaserVfx laser = vfx.GetComponentInChildren<DemonKingEgoLaserVfx>(true);
        if (laser == null)
            return;

        Vector3 rotatedDirection = Quaternion.Euler(0f, 0f, cue.VfxRotationDeg)
            * (previewFacingLeft ? Vector3.left : Vector3.right);
        Vector2 direction = rotatedDirection;
        float length = cue.Shape != null && cue.Shape.Size.x > 0.01f ? cue.Shape.Size.x : 6f;
        float width = cue.Shape != null && cue.Shape.Size.y > 0.01f ? cue.Shape.Size.y : 0.75f;
        laser.Play(vfx.transform.position, direction, length, width, cue.DurationSeconds);
    }

    private void SimulatePreviewObject(GameObject root, float localTime)
    {
        if (root == null)
            return;

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string stateName = ResolveDefaultStateName(animator.runtimeAnimatorController);
            if (!string.IsNullOrEmpty(stateName))
                animator.Play(Animator.StringToHash(stateName), 0, 0f);
            animator.Update(0f);
            if (localTime > 0f)
                animator.Update(localTime);
        }

        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem != null)
                particleSystem.Simulate(Mathf.Max(0f, localTime), withChildren: true, restart: true);
        }

        TopDownDebrisBounceEmitter2D[] debrisEmitters = root.GetComponentsInChildren<TopDownDebrisBounceEmitter2D>(true);
        for (int i = 0; i < debrisEmitters.Length; i++)
        {
            TopDownDebrisBounceEmitter2D emitter = debrisEmitters[i];
            if (emitter == null)
                continue;

            emitter.RestartEditorPreview();
            if (localTime > 0f)
                emitter.StepEditorPreview(localTime);
        }
    }

    private bool TryCreateCurrentPatternDefinition(out DemonKingPatternPreviewDefinition definition)
    {
        definition = null;
        if (selectedAbilityLogicAsset == null)
            return false;

        SerializedObject serializedAsset = new(selectedAbilityLogicAsset);
        serializedAsset.Update();
        definition = CreatePatternPreviewDefinition(serializedAsset);
        return definition != null && definition.Phases.Count > 0;
    }

    private void ApplyPatternPreviewPhase(DemonKingPatternPreviewPhase phase, bool restartPreview)
    {
        if (phase == null)
            return;

        if (TryCreateCurrentPatternDefinition(out DemonKingPatternPreviewDefinition definition))
        {
            int phaseIndex = definition.Phases.IndexOf(phase);
            if (phaseIndex < 0)
                phaseIndex = definition.ResolvePhaseIndex(phase.StartSeconds);

            patternSelectedPhaseIndex = Mathf.Clamp(phaseIndex, 0, definition.Phases.Count - 1);
            patternPhasePreviewTime = Mathf.Clamp(patternPhasePreviewTime, 0f, Mathf.Max(0.01f, phase.DurationSeconds));
            patternTimelineTime = phase.StartSeconds + patternPhasePreviewTime;
            if (restartPreview)
                patternPreviewNeedsRebuild = true;
            ApplyPatternTimelineTime(definition, restartOnPhaseChange: restartPreview);
            return;
        }

        previewSubject = PreviewSubject.Composite;
        compositeShowBody = !string.IsNullOrEmpty(phase.BodyClipName);
        compositeShowVfx = !string.IsNullOrEmpty(phase.VfxPrefabName);
        compositeShowSockets = true;
        compositeShowEgoSword = true;
        activePatternPreviewShape = patternShowWarningShape ? phase.Shape : null;
        activePatternPreviewPhase = phase;
        patternPreviewBodyFrameIndex = phase.BodyFrameIndex;

        AnimationClip bodyClip = ResolveDarkLordClip(phase.BodyClipName);
        if (bodyClip != null)
        {
            selectedAnimationClip = bodyClip;
            LoadClipRows(selectedAnimationClip);
        }
        else if (!string.IsNullOrEmpty(phase.BodyClipName))
        {
            compositeShowBody = false;
        }

        GameObject vfxPrefab = ResolveVfxPrefab(phase.VfxPrefabName);
        if (vfxPrefab != null)
        {
            selectedVfxPrefab = vfxPrefab;
            LoadTimedHitFramesFromPrefab();
        }
        else if (!string.IsNullOrEmpty(phase.VfxPrefabName))
        {
            compositeShowVfx = false;
        }

        if (phase.HasSocket)
        {
            compositeVfxSocket = phase.SocketId;
            compositeFallbackLeftOffset = phase.FallbackLeftOffset;
        }

        compositeVfxPreviewScale = phase.VfxScale;
        compositeVfxRotationDeg = phase.VfxRotationDeg;

        if (restartPreview)
            RestartPreview();

        previewTime = ResolvePatternPhaseClipTime(phase);
        if (restartPreview && previewTime > 0f)
            StepPreviewInstance(previewTime);
        ApplyBodyFramePreview();
        RenderPreview();
    }

    private float ResolvePatternPhaseClipTime(DemonKingPatternPreviewPhase phase)
    {
        if (phase == null)
            return 0f;

        if (patternPreviewBodyFrameIndex >= 0 && selectedAnimationClip != null)
            return ResolvePatternBodySampleTime(selectedAnimationClip);

        return Mathf.Clamp(patternPhasePreviewTime, 0f, Mathf.Max(0.01f, phase.DurationSeconds));
    }

    private float ResolvePatternBodySampleTime(AnimationClip clip)
    {
        if (clip == null)
            return 0f;

        if (patternPreviewBodyFrameIndex == DemonKingPatternPreviewPhase.LastBodyFrameIndex)
            return Mathf.Max(0f, clip.length);

        return patternPreviewBodyFrameIndex / Mathf.Max(0.01f, clip.frameRate);
    }

    private AnimationClip ResolveDarkLordClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
            return null;

        return darkLordClips.FirstOrDefault(clip => clip != null && clip.name == clipName)
            ?? vfxClips.FirstOrDefault(clip => clip != null && clip.name == clipName);
    }

    private GameObject ResolveVfxPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return null;

        return vfxPrefabs.FirstOrDefault(prefab => prefab != null && prefab.name == prefabName);
    }

    private void DrawEgoSwordTab()
    {
        EditorGUILayout.LabelField("EgoSword Actor", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        selectedEgoSword = (EgoSwordActor)EditorGUILayout.ObjectField(
            "Target",
            selectedEgoSword,
            typeof(EgoSwordActor),
            allowSceneObjects: true);
        if (EditorGUI.EndChangeCheck())
            previewSubject = PreviewSubject.EgoSwordOffsets;

        if (selectedEgoSword == null)
        {
            EditorGUILayout.HelpBox("Select an EgoSwordActor from the scene, prefab stage, or prefab asset.", MessageType.Info);
            return;
        }

        SerializedObject serializedSword = new(selectedEgoSword);
        DrawSerializedObjectEditor(serializedSword, "Apply EgoSword", true);
    }

    private void DrawSocketTab()
    {
        EditorGUILayout.LabelField("DemonKing VFX Socket Map", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        selectedSocketMap = (DemonKingVfxSocketMap)EditorGUILayout.ObjectField(
            "Target",
            selectedSocketMap,
            typeof(DemonKingVfxSocketMap),
            allowSceneObjects: true);
        if (EditorGUI.EndChangeCheck())
            previewSubject = PreviewSubject.SocketMap;

        if (selectedSocketMap == null)
        {
            EditorGUILayout.HelpBox("Select a DemonKingVfxSocketMap. Offsets are authored against the left-facing DarkLord baseline.", MessageType.Info);
            return;
        }

        SerializedObject serializedSocketMap = new(selectedSocketMap);
        DrawSerializedObjectEditor(serializedSocketMap, "Apply Socket Map", true);
    }

    private void DrawAnimationClipSelector()
    {
        EditorGUI.BeginChangeCheck();
        selectedAnimationClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Clip",
            selectedAnimationClip,
            typeof(AnimationClip),
            allowSceneObjects: false);
        if (EditorGUI.EndChangeCheck())
        {
            LoadClipRows(selectedAnimationClip);
            previewSubject = PreviewSubject.BodyClip;
            RestartPreview();
        }

        string[] names = darkLordClips.Concat(vfxClips).Select(clip => clip != null ? clip.name : "(null)").ToArray();
        int currentIndex = Mathf.Max(0, darkLordClips.Concat(vfxClips).ToList().IndexOf(selectedAnimationClip));
        if (names.Length > 0)
        {
            int nextIndex = EditorGUILayout.Popup("Known Clips", currentIndex, names);
            AnimationClip nextClip = darkLordClips.Concat(vfxClips).ElementAtOrDefault(nextIndex);
            if (nextClip != null && nextClip != selectedAnimationClip)
            {
                selectedAnimationClip = nextClip;
                LoadClipRows(selectedAnimationClip);
                previewSubject = PreviewSubject.BodyClip;
                RestartPreview();
            }
        }
    }

    private void DrawVfxPrefabSelector()
    {
        EditorGUI.BeginChangeCheck();
        selectedVfxPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab",
            selectedVfxPrefab,
            typeof(GameObject),
            allowSceneObjects: false);
        if (EditorGUI.EndChangeCheck())
        {
            previewSubject = PreviewSubject.VfxPrefab;
            LoadTimedHitFramesFromPrefab();
            RestartPreview();
        }

        string[] names = vfxPrefabs.Select(prefab => prefab != null ? prefab.name : "(null)").ToArray();
        int currentIndex = Mathf.Max(0, vfxPrefabs.IndexOf(selectedVfxPrefab));
        if (names.Length > 0)
        {
            int nextIndex = EditorGUILayout.Popup("Known Prefabs", currentIndex, names);
            GameObject nextPrefab = vfxPrefabs.ElementAtOrDefault(nextIndex);
            if (nextPrefab != null && nextPrefab != selectedVfxPrefab)
            {
                selectedVfxPrefab = nextPrefab;
                previewSubject = PreviewSubject.VfxPrefab;
                LoadTimedHitFramesFromPrefab();
                RestartPreview();
            }
        }
    }

    private void DrawAbilityLogicSelector()
    {
        bool changed = false;
        EditorGUI.BeginChangeCheck();
        selectedAbilityLogicAsset = (ScriptableObject)EditorGUILayout.ObjectField(
            "Asset",
            selectedAbilityLogicAsset,
            typeof(ScriptableObject),
            allowSceneObjects: false);
        if (EditorGUI.EndChangeCheck())
            changed = true;

        string[] names = abilityLogicAssets.Select(asset => asset != null ? asset.name : "(null)").ToArray();
        int currentIndex = Mathf.Max(0, abilityLogicAssets.IndexOf(selectedAbilityLogicAsset));
        if (names.Length > 0)
        {
            int nextIndex = EditorGUILayout.Popup("Known Assets", currentIndex, names);
            ScriptableObject nextAsset = abilityLogicAssets.ElementAtOrDefault(nextIndex);
            if (nextAsset != null && nextAsset != selectedAbilityLogicAsset)
            {
                selectedAbilityLogicAsset = nextAsset;
                changed = true;
            }
        }

        if (changed)
            OnPatternAssetChanged();
    }

    private void DrawFrameRows()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Sprite Frames", EditorStyles.boldLabel);
        frameScroll = EditorGUILayout.BeginScrollView(frameScroll, GUILayout.MinHeight(160f));

        for (int i = 0; i < clipRows.Count; i++)
        {
            SpriteFrameRow row = clipRows[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(i.ToString(CultureInfo.InvariantCulture), GUILayout.Width(24f));

                EditorGUI.BeginChangeCheck();
                row.Time = EditorGUILayout.FloatField(row.Time, GUILayout.Width(72f));
                row.Sprite = (Sprite)EditorGUILayout.ObjectField(row.Sprite, typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                    clipRowsDirty = true;

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    clipRows.RemoveAt(i);
                    clipRowsDirty = true;
                    i--;
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTimedHitEffectEditor(GameObject prefab)
    {
        TimedAnimatedHitEffect2D timedHit = prefab.GetComponentInChildren<TimedAnimatedHitEffect2D>(true);
        if (timedHit == null)
        {
            EditorGUILayout.HelpBox("No TimedAnimatedHitEffect2D found. This VFX will preview visuals only.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Timed Hit Effect", EditorStyles.boldLabel);
        SerializedObject timedHitObject = new(timedHit);
        SerializedProperty referenceClipProperty = timedHitObject.FindProperty("referenceClip");
        SerializedProperty targetLayersProperty = timedHitObject.FindProperty("targetLayers");
        SerializedProperty applyOnceProperty = timedHitObject.FindProperty("applyOnlyOncePerEffect");
        SerializedProperty destroyProperty = timedHitObject.FindProperty("destroyOnFinished");

        timedHitObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(referenceClipProperty);
        EditorGUILayout.PropertyField(targetLayersProperty);
        EditorGUILayout.PropertyField(applyOnceProperty);
        EditorGUILayout.PropertyField(destroyProperty);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(timedHit, "Tune DemonKing Timed Hit Effect");
            timedHitObject.ApplyModifiedProperties();
            MarkEditedObjectDirty(timedHit);
            SavePrefabAssetIfPersistent(prefab);
            RestartPreview();
        }

        AnimationClip clip = referenceClipProperty.objectReferenceValue as AnimationClip;
        if (clip == null)
            clip = ResolveFirstAnimatorClip(prefab);
        if (clip == null)
        {
            EditorGUILayout.HelpBox("Assign a reference clip or AnimatorController clip before editing hit event frames.", MessageType.Warning);
            return;
        }

        EditorGUILayout.ObjectField("Event Clip", clip, typeof(AnimationClip), false);
        EditorGUI.BeginChangeCheck();
        stagedEnableHitFrame = EditorGUILayout.IntField("Enable Hit Frame", Mathf.Max(0, stagedEnableHitFrame));
        stagedDisableHitFrame = EditorGUILayout.IntField("Disable Hit Frame", Mathf.Max(stagedEnableHitFrame + 1, stagedDisableHitFrame));
        if (EditorGUI.EndChangeCheck())
            stagedDisableHitFrame = Mathf.Max(stagedEnableHitFrame + 1, stagedDisableHitFrame);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Read Events", GUILayout.Width(100f)))
                LoadTimedHitFramesFromClip(clip);

            if (GUILayout.Button("Apply Events", GUILayout.Width(108f)))
                ApplyTimedHitEvents(clip);
        }
    }

    private void DrawVfxColliderEditor(GameObject prefab)
    {
        Collider2D[] colliders = prefab.GetComponentsInChildren<Collider2D>(true);
        if (colliders == null || colliders.Length == 0)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Hit Colliders", EditorStyles.boldLabel);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField("Collider", collider, typeof(Collider2D), true);
                SerializedObject colliderObject = new(collider);
                colliderObject.Update();
                DrawColliderProperties(colliderObject, collider);
                if (colliderObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(collider);
                    EditorUtility.SetDirty(prefab);
                    SavePrefabAssetIfPersistent(prefab);
                    RestartPreview();
                }
            }
        }
    }

    private static void DrawColliderProperties(SerializedObject colliderObject, Collider2D collider)
    {
        SerializedProperty isTrigger = colliderObject.FindProperty("m_IsTrigger");
        SerializedProperty offset = colliderObject.FindProperty("m_Offset");
        if (isTrigger != null)
            EditorGUILayout.PropertyField(isTrigger, new GUIContent("Is Trigger"));
        if (offset != null)
            EditorGUILayout.PropertyField(offset);

        switch (collider)
        {
            case CircleCollider2D:
                EditorGUILayout.PropertyField(colliderObject.FindProperty("m_Radius"));
                break;
            case BoxCollider2D:
                EditorGUILayout.PropertyField(colliderObject.FindProperty("m_Size"));
                break;
            case CapsuleCollider2D:
                EditorGUILayout.PropertyField(colliderObject.FindProperty("m_Size"));
                EditorGUILayout.PropertyField(colliderObject.FindProperty("m_Direction"));
                break;
            case PolygonCollider2D:
                EditorGUILayout.HelpBox("Polygon points remain authored in the normal Inspector/Sprite Editor for v1.", MessageType.Info);
                break;
        }
    }

    private void DrawSerializedObjectEditor(SerializedObject serializedObject, string applyLabel, bool sceneObjectsAllowed)
    {
        if (serializedObject == null || serializedObject.targetObject == null)
            return;

        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        serializedScroll = EditorGUILayout.BeginScrollView(serializedScroll, GUILayout.MinHeight(220f));
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator, true);
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }
        EditorGUILayout.EndScrollView();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(serializedObject.targetObject, applyLabel);
            serializedObject.ApplyModifiedProperties();
            MarkEditedObjectDirty(serializedObject.targetObject);
            RestartPreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Assets", GUILayout.Width(96f)))
                SavePrefabAssetIfNeeded(serializedObject.targetObject);

            GUILayout.FlexibleSpace();
            if (!sceneObjectsAllowed)
                EditorGUILayout.LabelField("Asset-only", GUILayout.Width(72f));
            else
                EditorGUILayout.LabelField("Undo-enabled live edit", GUILayout.Width(140f));
        }
    }

    private void TickPreview()
    {
        TickRuntimePatternExecution();

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = lastEditorTime > 0d ? Mathf.Min((float)(now - lastEditorTime), 0.05f) : 0f;
        lastEditorTime = now;

        if (previewPlaying)
        {
            if (TryCreateCurrentPatternDefinition(out DemonKingPatternPreviewDefinition definition))
            {
                patternTimelineTime += deltaTime * Mathf.Max(0.01f, previewSpeed);
                float length = Mathf.Max(0.01f, definition.TotalDuration);
                if (patternTimelineTime > length)
                {
                    if (loopPreview)
                    {
                        patternTimelineTime %= length;
                        patternAppliedPhaseIndex = -1;
                    }
                    else
                    {
                        patternTimelineTime = length;
                    }
                }

                ApplyPatternTimelineTime(definition, restartOnPhaseChange: true);
            }
            else
            {
                previewTime += deltaTime * Mathf.Max(0.01f, previewSpeed);
                float length = ResolvePreviewLength();
                if (length > 0.01f && previewTime > length)
                {
                    if (loopPreview)
                        previewTime %= length;
                    else
                        previewTime = length;
                }

                StepPreviewInstance(deltaTime * Mathf.Max(0.01f, previewSpeed));
                ApplyBodyFramePreview();
            }
        }

        RenderPreview();
        Repaint();
    }

    private void RestartPreview()
    {
        if (previewSubject == PreviewSubject.Composite
            && TryCreateCurrentPatternDefinition(out DemonKingPatternPreviewDefinition patternDefinition))
        {
            patternPreviewNeedsRebuild = true;
            ApplyPatternTimelineTime(patternDefinition, restartOnPhaseChange: true);
            return;
        }

        DestroyPreviewInstance();
        EnsurePreviewCamera();
        lastEditorTime = EditorApplication.timeSinceStartup;
        previewTime = 0f;

        switch (previewSubject)
        {
            case PreviewSubject.Composite:
                CreateCompositePreviewInstance();
                break;
            case PreviewSubject.VfxPrefab:
                CreateVfxPreviewInstance();
                break;
            case PreviewSubject.BodyClip:
            case PreviewSubject.EgoSwordOffsets:
            case PreviewSubject.SocketMap:
                CreateBodyPreviewInstance();
                break;
        }

        ApplyBodyFramePreview();
        RenderPreview();
    }

    private void CreateBodyPreviewInstance()
    {
        EnsurePreviewRoot();
        previewInstance = CreateBodyPreviewObject("DemonKing_BodyClipPreview");
    }

    private GameObject CreateBodyPreviewObject(string objectName)
    {
        GameObject body = new(objectName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        body.transform.SetParent(previewRoot.transform, false);
        body.transform.localPosition = Vector3.zero;
        bodyPreviewRenderer = body.AddComponent<SpriteRenderer>();
        bodyPreviewRenderer.sortingOrder = 1;
        return body;
    }

    private void CreateVfxPreviewInstance()
    {
        EnsurePreviewRoot();
        if (selectedVfxPrefab == null)
            return;

        previewInstance = CreateVfxPreviewObject(Vector3.zero, Vector3.one, 0f);
    }

    private void CreateCompositePreviewInstance()
    {
        EnsurePreviewRoot();

        GameObject body = null;
        if (compositeShowBody)
            body = CreateBodyPreviewObject("DemonKing_CompositeBodyPreview");

        GameObject vfx = null;
        if (compositeShowVfx && selectedVfxPrefab != null)
        {
            Vector3 localOffset = ResolveCompositeVfxLocalOffset();
            vfx = CreateVfxPreviewObject(localOffset, compositeVfxPreviewScale, compositeVfxRotationDeg);
        }

        GameObject sword = null;
        if (compositeShowEgoSword && selectedEgoSword != null)
            sword = CreateEgoSwordSpritePreviewObject();

        previewInstance = body != null ? body : vfx != null ? vfx : sword;
    }

    private GameObject CreateEgoSwordSpritePreviewObject()
    {
        SpriteRenderer sourceRenderer = selectedEgoSword != null
            ? selectedEgoSword.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        if (sourceRenderer == null || sourceRenderer.sprite == null)
            return null;

        GameObject sword = new("EgoSword_SpritePreview")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        sword.transform.SetParent(previewRoot.transform, false);
        sword.transform.localPosition = ResolveEgoSwordPreviewLocalOffset();
        sword.transform.localRotation = Quaternion.identity;
        sword.transform.localScale = sourceRenderer.transform.localScale;
        egoSwordPreviewRenderer = sword.AddComponent<SpriteRenderer>();
        egoSwordPreviewRenderer.sprite = sourceRenderer.sprite;
        egoSwordPreviewRenderer.flipX = sourceRenderer.flipX;
        egoSwordPreviewRenderer.flipY = sourceRenderer.flipY;
        egoSwordPreviewRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        egoSwordPreviewRenderer.sortingOrder = Mathf.Max(2, sourceRenderer.sortingOrder);
        egoSwordPreviewRenderer.color = sourceRenderer.color;
        return sword;
    }

    private GameObject CreateEgoSwordCuePreviewObject(DemonKingPatternPreviewCue cue, float localTime)
    {
        if (cue == null)
            return null;

        EgoSwordPreviewPose pose = ResolveEgoSwordPreviewPose(cue, localTime);
        GameObject sword = CreateEgoSwordSpritePreviewObject();
        if (sword != null)
        {
            sword.transform.localPosition = pose.LocalPosition;
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, pose.RotationDeg);
        }

        if (pose.ShowSpin)
        {
            GameObject spinPrefab = ResolveVfxPrefab("SwordSpin4FrameVfx");
            if (spinPrefab != null)
            {
                GameObject spin = CreateVfxPreviewObject(
                    spinPrefab,
                    pose.LocalPosition,
                    Vector3.one,
                    0f,
                    startPlaybackComponents: false);
                SimulatePreviewObject(spin, localTime);
                activeEgoSwordSpinPreviewTransform = spin != null ? spin.transform : null;
            }
        }

        return sword;
    }

    private void UpdateEgoSwordCuePreviewPose()
    {
        if (activeEgoSwordPreviewCue == null)
            return;

        float localTime = activeEgoSwordPreviewCue.ResolveLocalTime(patternTimelineTime);
        EgoSwordPreviewPose pose = ResolveEgoSwordPreviewPose(activeEgoSwordPreviewCue, localTime);
        if (egoSwordPreviewRenderer != null)
        {
            Transform swordTransform = egoSwordPreviewRenderer.transform;
            swordTransform.localPosition = pose.LocalPosition;
            swordTransform.localRotation = Quaternion.Euler(0f, 0f, pose.RotationDeg);
        }

        if (activeEgoSwordSpinPreviewTransform != null)
        {
            activeEgoSwordSpinPreviewTransform.localPosition = pose.LocalPosition;
            activeEgoSwordSpinPreviewTransform.localRotation = Quaternion.identity;
        }
    }

    private EgoSwordPreviewPose ResolveEgoSwordPreviewPose(DemonKingPatternPreviewCue cue, float localTime)
    {
        if (cue == null)
            return EgoSwordPreviewPose.Hidden;

        float duration = Mathf.Max(0.01f, cue.DurationSeconds);
        float t = Mathf.Clamp01(localTime / duration);
        Vector3 held = ReadEgoSwordVector3("heldOffset", new Vector3(0.85f, 0.1f, 0f));
        Vector3 throwOrigin = ReadEgoSwordVector3("throwOriginLocalOffset", held);
        Vector3 recallTarget = ReadEgoSwordVector3("recallTargetLocalOffset", held);
        float flyingRotation = ReadEgoSwordFloat("flyingRotationDegreesPerSecond", 720f);
        float throwRotation = ReadEgoSwordFloat("throwInitialRotation", 0f);
        float recallRotation = ReadEgoSwordFloat("recallInitialRotation", 0f);
        float recallLiftHeight = ReadEgoSwordFloat("recallLiftHeight", 2.2f);
        float recallLiftSeconds = ReadEgoSwordFloat("recallLiftSeconds", 0.16f);
        float recallLiftHoldSeconds = ReadEgoSwordFloat("recallLiftHoldSeconds", 0.18f);
        float recallReturnMinimumSeconds = ReadEgoSwordFloat("recallReturnMinimumSeconds", 0.35f);
        float hoverHeight = ReadEgoSwordFloat("verticalHoverHeight", 2.2f);
        float liftSeconds = ReadEgoSwordFloat("verticalStrikeLiftSeconds", 0.1f);
        float liftHeight = ReadEgoSwordFloat("verticalStrikeLiftHeight", 0.45f);
        float dropSeconds = ReadEgoSwordFloat("verticalStrikeDropSeconds", 0.16f);

        Vector3 droppedPoint = new(-1.8f, -0.1f, -0.04f);
        if (!previewFacingLeft)
        {
            held.x = -held.x;
            throwOrigin.x = -throwOrigin.x;
            recallTarget.x = -recallTarget.x;
            droppedPoint.x = -droppedPoint.x;
        }

        return cue.EgoSwordMode switch
        {
            DemonKingEgoSwordPreviewMode.ThrowSpin => new EgoSwordPreviewPose(
                Vector3.Lerp(throwOrigin, droppedPoint, t),
                throwRotation + flyingRotation * localTime,
                showSpin: true),
            DemonKingEgoSwordPreviewMode.RecallSpin => ResolveRecallSpinPose(
                localTime,
                duration,
                droppedPoint,
                recallTarget,
                recallLiftHeight,
                recallLiftSeconds,
                recallLiftHoldSeconds,
                recallReturnMinimumSeconds,
                recallRotation,
                flyingRotation),
            DemonKingEgoSwordPreviewMode.VerticalTrack => new EgoSwordPreviewPose(
                Vector3.Lerp(droppedPoint, new Vector3(0f, hoverHeight, -0.04f), Mathf.SmoothStep(0f, 1f, t)),
                flyingRotation * localTime,
                showSpin: false),
            DemonKingEgoSwordPreviewMode.VerticalCommit => ResolveVerticalStrikeCommitPose(
                localTime,
                liftSeconds,
                liftHeight,
                dropSeconds,
                hoverHeight,
                flyingRotation),
            DemonKingEgoSwordPreviewMode.CrossLaserWarning => new EgoSwordPreviewPose(Vector3.zero, 0f, showSpin: false),
            DemonKingEgoSwordPreviewMode.CrossLaserFire => new EgoSwordPreviewPose(Vector3.zero, 0f, showSpin: false),
            DemonKingEgoSwordPreviewMode.Fixed => new EgoSwordPreviewPose(droppedPoint, 0f, showSpin: false),
            _ => new EgoSwordPreviewPose(held, 0f, showSpin: false)
        };
    }

    private static EgoSwordPreviewPose ResolveVerticalStrikeCommitPose(
        float localTime,
        float liftSeconds,
        float liftHeight,
        float dropSeconds,
        float hoverHeight,
        float flyingRotationDegreesPerSecond)
    {
        Vector3 hover = new(0f, hoverHeight, -0.04f);
        Vector3 apex = hover + Vector3.up * Mathf.Max(0f, liftHeight);
        Vector3 ground = new(0f, 0f, -0.04f);
        if (liftHeight > 0f && liftSeconds > 0f && localTime < liftSeconds)
        {
            float liftT = Mathf.Clamp01(localTime / Mathf.Max(0.01f, liftSeconds));
            return new EgoSwordPreviewPose(
                Vector3.Lerp(hover, apex, Mathf.SmoothStep(0f, 1f, liftT)),
                flyingRotationDegreesPerSecond * localTime,
                showSpin: false);
        }

        float dropT = Mathf.Clamp01((localTime - Mathf.Max(0f, liftSeconds)) / Mathf.Max(0.01f, dropSeconds));
        return new EgoSwordPreviewPose(
            Vector3.Lerp(apex, ground, dropT * dropT),
            flyingRotationDegreesPerSecond * localTime,
            showSpin: false);
    }

    private static EgoSwordPreviewPose ResolveRecallSpinPose(
        float localTime,
        float duration,
        Vector3 droppedPoint,
        Vector3 recallTarget,
        float liftHeight,
        float liftSeconds,
        float liftHoldSeconds,
        float returnMinimumSeconds,
        float recallInitialRotation,
        float flyingRotationDegreesPerSecond)
    {
        Vector3 liftedPoint = droppedPoint + Vector3.up * Mathf.Max(0f, liftHeight);
        float safeLiftSeconds = Mathf.Max(0f, liftSeconds);
        if (safeLiftSeconds > 0.001f && localTime < safeLiftSeconds)
        {
            float liftT = Mathf.Clamp01(localTime / safeLiftSeconds);
            float easedLiftT = 1f - Mathf.Pow(1f - liftT, 3f);
            return new EgoSwordPreviewPose(
                Vector3.Lerp(droppedPoint, liftedPoint, easedLiftT),
                recallInitialRotation,
                showSpin: false);
        }

        float safeLiftHoldSeconds = Mathf.Max(0f, liftHoldSeconds);
        if (localTime < safeLiftSeconds + safeLiftHoldSeconds)
        {
            return new EgoSwordPreviewPose(
                liftedPoint,
                recallInitialRotation,
                showSpin: false);
        }

        float returnStartTime = safeLiftSeconds + safeLiftHoldSeconds;
        float returnSeconds = Mathf.Max(0.01f, Mathf.Max(returnMinimumSeconds, duration - returnStartTime));
        float returnT = Mathf.Clamp01((localTime - returnStartTime) / returnSeconds);
        return new EgoSwordPreviewPose(
            Vector3.Lerp(liftedPoint, recallTarget, returnT),
            recallInitialRotation + flyingRotationDegreesPerSecond * Mathf.Max(0f, localTime - returnStartTime),
            showSpin: true);
    }

    private Vector3 ReadEgoSwordVector3(string propertyName, Vector3 fallback)
    {
        if (selectedEgoSword == null)
            return fallback;

        SerializedObject serializedSword = new(selectedEgoSword);
        SerializedProperty property = serializedSword.FindProperty(propertyName);
        if (property == null)
            return fallback;

        if (property.propertyType == SerializedPropertyType.Vector3)
            return property.vector3Value;
        if (property.propertyType == SerializedPropertyType.Vector2)
            return property.vector2Value;
        return fallback;
    }

    private float ReadEgoSwordFloat(string propertyName, float fallback)
    {
        if (selectedEgoSword == null)
            return fallback;

        SerializedObject serializedSword = new(selectedEgoSword);
        SerializedProperty property = serializedSword.FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Float
            ? property.floatValue
            : fallback;
    }

    private GameObject CreateVfxPreviewObject(Vector3 localPosition, Vector3 localScaleMultiplier, float localRotationDeg)
    {
        return CreateVfxPreviewObject(
            selectedVfxPrefab,
            localPosition,
            localScaleMultiplier,
            localRotationDeg,
            startPlaybackComponents: true);
    }

    private GameObject CreateVfxPreviewObject(
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localScaleMultiplier,
        float localRotationDeg,
        bool startPlaybackComponents)
    {
        if (prefab == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(prefab);
        if (instance == null)
            return null;

        instance.name = $"{prefab.name}_Preview";
        instance.transform.SetParent(previewRoot.transform, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(0f, 0f, localRotationDeg);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, localScaleMultiplier);
        SetHideFlagsRecursive(instance.transform, HideFlags.HideAndDontSave);
        instance.SetActive(true);

        if (startPlaybackComponents)
        {
            CachePreviewPlaybackComponents();
            StartPreviewPlaybackComponents();
        }

        return instance;
    }

    private void CachePreviewPlaybackComponents()
    {
        if (previewRoot == null)
            return;

        previewAnimators = previewRoot.GetComponentsInChildren<Animator>(true);
        previewParticleSystems = previewRoot.GetComponentsInChildren<ParticleSystem>(true);
        previewDebrisEmitters = previewRoot.GetComponentsInChildren<TopDownDebrisBounceEmitter2D>(true);
    }

    private void StartPreviewPlaybackComponents()
    {
        for (int i = 0; i < previewAnimators.Length; i++)
        {
            Animator animator = previewAnimators[i];
            if (animator == null)
                continue;

            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string stateName = ResolveDefaultStateName(animator.runtimeAnimatorController);
            if (!string.IsNullOrEmpty(stateName))
            {
                animator.Play(Animator.StringToHash(stateName), 0, 0f);
                animator.Update(0f);
            }
        }

        for (int i = 0; i < previewDebrisEmitters.Length; i++)
            previewDebrisEmitters[i]?.RestartEditorPreview();
    }

    private Vector3 ResolveCompositeVfxLocalOffset()
    {
        if (selectedSocketMap != null)
            return selectedSocketMap.ResolveLocalOffset(compositeVfxSocket, compositeFallbackLeftOffset, previewFacingLeft);

        Vector3 offset = compositeFallbackLeftOffset;
        if (!previewFacingLeft)
            offset.x = -offset.x;

        return offset;
    }

    private Vector3 ResolveEgoSwordPreviewLocalOffset()
    {
        if (selectedEgoSword == null)
            return Vector3.zero;

        string propertyName = "heldOffset";
        if (activePatternPreviewPhase != null)
        {
            string name = activePatternPreviewPhase.Name;
            string category = activePatternPreviewPhase.Category;
            if (name.IndexOf("throw", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("throw", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("release", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                propertyName = "throwOriginLocalOffset";
            }
            else if (name.IndexOf("recall", StringComparison.OrdinalIgnoreCase) >= 0
                     || category.IndexOf("recall", StringComparison.OrdinalIgnoreCase) >= 0
                     || name.IndexOf("recover", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                propertyName = "recallTargetLocalOffset";
            }
        }

        SerializedObject sword = new(selectedEgoSword);
        SerializedProperty property = sword.FindProperty(propertyName);
        Vector3 offset = Vector3.zero;
        if (property != null)
        {
            if (property.propertyType == SerializedPropertyType.Vector3)
                offset = property.vector3Value;
            else if (property.propertyType == SerializedPropertyType.Vector2)
                offset = property.vector2Value;
        }
        if (!previewFacingLeft)
            offset.x = -offset.x;
        offset.z = -0.04f;
        return offset;
    }

    private void StepPreviewInstance(float deltaTime)
    {
        if (previewInstance == null)
            return;

        for (int i = 0; i < previewAnimators.Length; i++)
        {
            Animator animator = previewAnimators[i];
            if (animator != null && animator.enabled)
                animator.Update(deltaTime);
        }

        for (int i = 0; i < previewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = previewParticleSystems[i];
            if (particleSystem != null)
                particleSystem.Simulate(deltaTime, withChildren: true, restart: false);
        }

        for (int i = 0; i < previewDebrisEmitters.Length; i++)
            previewDebrisEmitters[i]?.StepEditorPreview(deltaTime);
    }

    private void ApplyBodyFramePreview()
    {
        if (bodyPreviewRenderer == null)
            return;

        AnimationClip clip = selectedAnimationClip;
        float bodySampleTime = previewTime;
        if (previewSubject == PreviewSubject.Composite && activeBodyPreviewCue != null)
        {
            clip = ResolveDarkLordClip(activeBodyPreviewCue.BodyClipName);
            bodySampleTime = activeBodyPreviewCue.ResolveBodySampleTime(
                clip,
                patternTimelineTime,
                fallbackPhaseTime: patternPhasePreviewTime);
        }
        else if (previewSubject == PreviewSubject.Composite && patternPreviewBodyFrameIndex >= 0 && clip != null)
        {
            bodySampleTime = ResolvePatternBodySampleTime(clip);
        }

        Sprite sprite = ResolveSpriteAtTime(clip, bodySampleTime);
        if (sprite == null && clipRows.Count > 0)
            sprite = clipRows[0].Sprite;

        bodyPreviewRenderer.sprite = sprite;
        bodyPreviewRenderer.flipX = !previewFacingLeft;
    }

    private void DrawPreviewOverlays(Rect previewRect)
    {
        if (previewCamera == null || previewRect.width <= 0f || previewRect.height <= 0f)
            return;

        DrawCenterMarker(previewRect);
        if (previewSubject == PreviewSubject.VfxPrefab
            || (previewSubject == PreviewSubject.Composite && compositeShowVfx))
            DrawHitWindowOverlay(previewRect);
        if (previewSubject == PreviewSubject.EgoSwordOffsets
            || (previewSubject == PreviewSubject.Composite && compositeShowEgoSword))
            DrawEgoSwordOverlay(previewRect);
        if (previewSubject == PreviewSubject.SocketMap
            || (previewSubject == PreviewSubject.Composite && compositeShowSockets))
            DrawSocketOverlay(previewRect);
        if (previewSubject == PreviewSubject.Composite && compositeShowVfx)
            DrawCompositeVfxSocketOverlay(previewRect);
        if (previewSubject == PreviewSubject.Composite && patternShowWarningShape)
        {
            if (activePatternPreviewShapes.Count > 0)
            {
                for (int i = 0; i < activePatternPreviewShapes.Count; i++)
                    DrawPatternPreviewShapeOverlay(previewRect, activePatternPreviewShapes[i]);
            }
            else if (activePatternPreviewShape != null)
            {
                DrawPatternPreviewShapeOverlay(previewRect, activePatternPreviewShape);
            }
        }
    }

    private void DrawCenterMarker(Rect previewRect)
    {
        DrawMarker(previewRect, PreviewOrigin, BodyMarkerColor, "Body");
    }

    private void DrawHitWindowOverlay(Rect previewRect)
    {
        AnimationClip clip = ResolveSelectedVfxReferenceClip();
        if (clip == null || clip.frameRate <= 0f)
            return;

        float enableTime = stagedEnableHitFrame / clip.frameRate;
        float disableTime = stagedDisableHitFrame / clip.frameRate;
        if (previewTime < enableTime || previewTime > disableTime)
            return;

        Vector3 centerWorld = PreviewOrigin;
        if (previewSubject == PreviewSubject.Composite)
            centerWorld += ResolveCompositeVfxLocalOffset();

        Vector2 center = WorldToPreviewGui(centerWorld, previewRect);
        Handles.color = HitWindowColor;
        Handles.DrawWireDisc(center, Vector3.forward, MarkerRadius * 2.1f);
        Handles.Label(center + new Vector2(8f, -22f), "Hit Window");
    }

    private void DrawEgoSwordOverlay(Rect previewRect)
    {
        if (selectedEgoSword == null)
            return;

        SerializedObject sword = new(selectedEgoSword);
        DrawVectorMarker(sword, "heldOffset", previewRect, HeldMarkerColor, "Held");
        DrawVectorMarker(sword, "throwOriginLocalOffset", previewRect, ThrowMarkerColor, "Throw");
        DrawVectorMarker(sword, "recallTargetLocalOffset", previewRect, RecallMarkerColor, "Recall");
    }

    private void DrawSocketOverlay(Rect previewRect)
    {
        if (selectedSocketMap == null)
            return;

        SerializedObject socketMap = new(selectedSocketMap);
        SerializedProperty sockets = socketMap.FindProperty("sockets");
        if (sockets == null || !sockets.isArray)
            return;

        for (int i = 0; i < sockets.arraySize; i++)
        {
            SerializedProperty entry = sockets.GetArrayElementAtIndex(i);
            if (entry == null)
                continue;

            SerializedProperty enabledProperty = entry.FindPropertyRelative("enabled");
            if (enabledProperty != null && !enabledProperty.boolValue)
                continue;

            SerializedProperty idProperty = entry.FindPropertyRelative("id");
            SerializedProperty offsetProperty = entry.FindPropertyRelative("leftFacingLocalOffset");
            SerializedProperty colorProperty = entry.FindPropertyRelative("gizmoColor");
            if (offsetProperty == null)
                continue;

            Vector2 offset = offsetProperty.vector2Value;
            if (!previewFacingLeft)
                offset.x = -offset.x;

            Color color = colorProperty != null ? colorProperty.colorValue : Color.white;
            string label = idProperty != null ? idProperty.enumDisplayNames[idProperty.enumValueIndex] : $"Socket {i}";
            DrawMarker(previewRect, PreviewOrigin + (Vector3)offset, color, label);
        }
    }

    private void DrawCompositeVfxSocketOverlay(Rect previewRect)
    {
        Vector3 offset = ResolveCompositeVfxLocalOffset();
        DrawMarker(previewRect, PreviewOrigin + offset, HitWindowColor, "Composite VFX");
    }

    private void DrawPatternPreviewShapeOverlay(Rect previewRect, DemonKingPatternPreviewShape shape)
    {
        if (shape == null || shape.Kind == DemonKingPatternPreviewShapeKind.None)
            return;

        Handles.color = shape.Color;
        switch (shape.Kind)
        {
            case DemonKingPatternPreviewShapeKind.Circle:
                DrawPatternCircle(previewRect, shape);
                break;
            case DemonKingPatternPreviewShapeKind.Rectangle:
                DrawPatternRectangle(previewRect, shape);
                break;
            case DemonKingPatternPreviewShapeKind.Sector:
                DrawPatternSector(previewRect, shape);
                break;
        }

        if (shape.HasPlayerAnchor)
            DrawMarker(previewRect, PreviewOrigin + (Vector3)shape.PlayerAnchorOffset, new Color(0.25f, 0.8f, 1f, 0.95f), "Player Anchor");
    }

    private void DrawPatternCircle(Rect previewRect, DemonKingPatternPreviewShape shape)
    {
        Vector3 center = PreviewOrigin + (Vector3)shape.CenterOffset;
        Vector2 radii = shape.Size.sqrMagnitude > 0.0001f
            ? shape.Size * 0.5f
            : Vector2.one * shape.Radius;
        List<Vector3> points = new();
        for (int i = 0; i <= 48; i++)
        {
            float angle = i / 48f * Mathf.PI * 2f;
            Vector3 world = center + new Vector3(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y, 0f);
            points.Add(WorldToPreviewGui(world, previewRect));
        }

        Handles.DrawAAPolyLine(2f, points.ToArray());
        Handles.Label(WorldToPreviewGui(center, previewRect) + new Vector2(8f, -36f), shape.Label);
    }

    private void DrawPatternRectangle(Rect previewRect, DemonKingPatternPreviewShape shape)
    {
        Vector2 half = shape.Size * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, shape.RotationDeg);
        Vector3 center = PreviewOrigin + (Vector3)shape.CenterOffset;
        Vector3[] corners =
        {
            center + rotation * new Vector3(-half.x, -half.y, 0f),
            center + rotation * new Vector3(half.x, -half.y, 0f),
            center + rotation * new Vector3(half.x, half.y, 0f),
            center + rotation * new Vector3(-half.x, half.y, 0f)
        };

        Vector3[] guiCorners = corners
            .Select(corner => (Vector3)WorldToPreviewGui(corner, previewRect))
            .ToArray();
        Handles.DrawAAPolyLine(2f, guiCorners.Concat(new[] { guiCorners[0] }).ToArray());
        Handles.Label((Vector2)guiCorners[0] + new Vector2(8f, -18f), shape.Label);
    }

    private void DrawPatternSector(Rect previewRect, DemonKingPatternPreviewShape shape)
    {
        Vector3 origin = PreviewOrigin + (Vector3)shape.CenterOffset;
        List<Vector3> points = new()
        {
            WorldToPreviewGui(origin, previewRect)
        };
        float halfAngle = shape.AngleDeg * 0.5f;
        for (int i = 0; i <= 24; i++)
        {
            float t = i / 24f;
            float angle = shape.RotationDeg - halfAngle + shape.AngleDeg * t;
            Vector2 direction = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            points.Add(WorldToPreviewGui(origin + (Vector3)(direction * shape.Radius), previewRect));
        }

        points.Add(WorldToPreviewGui(origin, previewRect));
        Handles.DrawAAPolyLine(2f, points.ToArray());
        Handles.Label((Vector2)points[0] + new Vector2(8f, -36f), shape.Label);
    }

    private float WorldDistanceToPreviewPixels(float worldDistance, Rect previewRect)
    {
        float cameraSize = previewCamera != null ? Mathf.Max(0.01f, previewCamera.orthographicSize) : previewCameraSize;
        return worldDistance * previewRect.height / (cameraSize * 2f);
    }

    private void DrawVectorMarker(SerializedObject serializedObject, string propertyName, Rect previewRect, Color color, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        Vector3 localOffset = property.vector3Value;
        if (!previewFacingLeft)
            localOffset.x = -localOffset.x;

        DrawMarker(previewRect, PreviewOrigin + localOffset, color, label);
    }

    private void DrawMarker(Rect previewRect, Vector3 world, Color color, string label)
    {
        Vector2 guiPosition = WorldToPreviewGui(world, previewRect);
        Handles.color = color;
        Handles.DrawSolidDisc(guiPosition, Vector3.forward, MarkerRadius);
        Handles.Label(guiPosition + new Vector2(7f, -18f), label);
    }

    private Vector2 WorldToPreviewGui(Vector3 world, Rect previewRect)
    {
        Vector3 viewport = previewCamera.WorldToViewportPoint(world);
        return new Vector2(
            previewRect.x + viewport.x * previewRect.width,
            previewRect.yMax - viewport.y * previewRect.height);
    }

    private void EnsureClipRowsLoaded()
    {
        if (loadedClipRowsFor != selectedAnimationClip)
            LoadClipRows(selectedAnimationClip);
    }

    private void LoadClipRows(AnimationClip clip)
    {
        loadedClipRowsFor = clip;
        clipRows.Clear();
        clipRowsDirty = false;
        if (clip == null)
            return;

        stagedFrameRate = Mathf.Max(0.01f, clip.frameRate);
        stagedLoopTime = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
        stagedClipLength = Mathf.Max(0.01f, clip.length);

        EditorCurveBinding binding = ResolveSpriteBinding(clip);
        if (string.IsNullOrEmpty(binding.propertyName))
            return;

        ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (keyframes == null)
            return;

        foreach (ObjectReferenceKeyframe keyframe in keyframes.OrderBy(keyframe => keyframe.time))
        {
            clipRows.Add(new SpriteFrameRow
            {
                Time = Mathf.Max(0f, keyframe.time),
                Sprite = keyframe.value as Sprite
            });
        }
    }

    private void ApplyClipRows()
    {
        if (selectedAnimationClip == null)
            return;

        ObjectReferenceKeyframe[] keyframes = clipRows
            .Where(row => row != null && row.Sprite != null)
            .OrderBy(row => Mathf.Max(0f, row.Time))
            .Select(row => new ObjectReferenceKeyframe
            {
                time = Mathf.Max(0f, row.Time),
                value = row.Sprite
            })
            .ToArray();

        if (keyframes.Length == 0)
        {
            EditorUtility.DisplayDialog("DemonKing Visual Tuning", "Cannot apply an animation clip with no sprite frames.", "OK");
            return;
        }

        Undo.RecordObject(selectedAnimationClip, "Tune DemonKing Animation Clip");
        selectedAnimationClip.frameRate = Mathf.Max(0.01f, stagedFrameRate);
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(selectedAnimationClip))
        {
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                AnimationUtility.SetObjectReferenceCurve(selectedAnimationClip, binding, null);
        }

        AnimationUtility.SetObjectReferenceCurve(selectedAnimationClip, CreateSpriteBinding(), keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(selectedAnimationClip);
        settings.loopTime = stagedLoopTime;
        SetClipStopTimeIfAvailable(
            settings,
            Mathf.Max(stagedClipLength, keyframes[keyframes.Length - 1].time + 1f / selectedAnimationClip.frameRate));
        AnimationUtility.SetAnimationClipSettings(selectedAnimationClip, settings);

        EditorUtility.SetDirty(selectedAnimationClip);
        AssetDatabase.SaveAssets();
        LoadClipRows(selectedAnimationClip);
        RestartPreview();
    }

    private void LoadTimedHitFramesFromPrefab()
    {
        AnimationClip clip = ResolveSelectedVfxReferenceClip();
        if (clip != null)
            LoadTimedHitFramesFromClip(clip);
    }

    private void LoadTimedHitFramesFromClip(AnimationClip clip)
    {
        if (clip == null)
            return;

        float frameRate = Mathf.Max(0.01f, clip.frameRate);
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        AnimationEvent enableEvent = events.FirstOrDefault(evt => evt.functionName == nameof(TimedAnimatedHitEffect2D.EnableHitCollision));
        AnimationEvent disableEvent = events.FirstOrDefault(evt => evt.functionName == nameof(TimedAnimatedHitEffect2D.DisableHitCollision));

        stagedEnableHitFrame = Mathf.Max(0, Mathf.RoundToInt(enableEvent.time * frameRate));
        stagedDisableHitFrame = Mathf.Max(stagedEnableHitFrame + 1, Mathf.RoundToInt(disableEvent.time * frameRate));
    }

    private void ApplyTimedHitEvents(AnimationClip clip)
    {
        if (clip == null)
            return;

        Undo.RecordObject(clip, "Tune DemonKing Timed Hit Events");
        float frameRate = Mathf.Max(0.01f, clip.frameRate);
        float enableTime = Mathf.Max(0, stagedEnableHitFrame) / frameRate;
        float disableTime = Mathf.Max(stagedEnableHitFrame + 1, stagedDisableHitFrame) / frameRate;

        List<AnimationEvent> events = AnimationUtility.GetAnimationEvents(clip)
            .Where(evt => evt.functionName != nameof(TimedAnimatedHitEffect2D.EnableHitCollision)
                && evt.functionName != nameof(TimedAnimatedHitEffect2D.DisableHitCollision))
            .ToList();

        events.Add(new AnimationEvent
        {
            time = enableTime,
            functionName = nameof(TimedAnimatedHitEffect2D.EnableHitCollision)
        });
        events.Add(new AnimationEvent
        {
            time = disableTime,
            functionName = nameof(TimedAnimatedHitEffect2D.DisableHitCollision)
        });

        AnimationUtility.SetAnimationEvents(clip, events.OrderBy(evt => evt.time).ToArray());
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        RestartPreview();
    }

    private Sprite ResolveSpriteAtTime(AnimationClip clip, float time)
    {
        if (clip == null)
            return null;

        List<SpriteFrameRow> rows = clip == selectedAnimationClip && clipRows.Count > 0
            ? clipRows
            : ResolveClipRows(clip);
        if (rows.Count == 0)
            return null;

        SpriteFrameRow selected = rows[0];
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Time <= time)
                selected = rows[i];
            else
                break;
        }

        return selected.Sprite;
    }

    private static List<SpriteFrameRow> ResolveClipRows(AnimationClip clip)
    {
        List<SpriteFrameRow> rows = new();
        if (clip == null)
            return rows;

        EditorCurveBinding binding = ResolveSpriteBinding(clip);
        if (string.IsNullOrEmpty(binding.propertyName))
            return rows;

        ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (keyframes == null)
            return rows;

        rows.AddRange(keyframes
            .OrderBy(keyframe => keyframe.time)
            .Select(keyframe => new SpriteFrameRow
            {
                Time = Mathf.Max(0f, keyframe.time),
                Sprite = keyframe.value as Sprite
            }));
        return rows;
    }

    private float ResolvePreviewLength()
    {
        switch (previewSubject)
        {
            case PreviewSubject.Composite:
                float compositeLength = 0.1f;
                if (compositeShowBody && selectedAnimationClip != null)
                    compositeLength = Mathf.Max(compositeLength, selectedAnimationClip.length);
                if (compositeShowVfx)
                {
                    AnimationClip compositeVfxClip = ResolveSelectedVfxReferenceClip();
                    if (compositeVfxClip != null)
                        compositeLength = Mathf.Max(compositeLength, compositeVfxClip.length);
                }

                return compositeLength;
            case PreviewSubject.VfxPrefab:
                AnimationClip vfxClip = ResolveSelectedVfxReferenceClip();
                return Mathf.Max(0.1f, vfxClip != null ? vfxClip.length : 1f);
            case PreviewSubject.BodyClip:
                return Mathf.Max(0.1f, selectedAnimationClip != null ? selectedAnimationClip.length : stagedClipLength);
            default:
                return Mathf.Max(0.1f, selectedAnimationClip != null ? selectedAnimationClip.length : 1f);
        }
    }

    private AnimationClip ResolveSelectedVfxReferenceClip()
    {
        if (selectedVfxPrefab == null)
            return null;

        TimedAnimatedHitEffect2D timedHit = selectedVfxPrefab.GetComponentInChildren<TimedAnimatedHitEffect2D>(true);
        if (timedHit != null)
        {
            SerializedObject timedHitObject = new(timedHit);
            AnimationClip referenceClip = timedHitObject.FindProperty("referenceClip")?.objectReferenceValue as AnimationClip;
            if (referenceClip != null)
                return referenceClip;
        }

        return ResolveFirstAnimatorClip(selectedVfxPrefab);
    }

    private static AnimationClip ResolveFirstAnimatorClip(GameObject root)
    {
        if (root == null)
            return null;

        Animator animator = root.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        AnimationClip[] clips = controller != null ? controller.animationClips : null;
        return clips != null && clips.Length > 0 ? clips[0] : null;
    }

    private static EditorCurveBinding ResolveSpriteBinding(AnimationClip clip)
    {
        if (clip == null)
            return default;

        return AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .FirstOrDefault(binding =>
                binding.type == typeof(SpriteRenderer)
                && binding.propertyName == "m_Sprite");
    }

    private static EditorCurveBinding CreateSpriteBinding()
    {
        return new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };
    }

    private static string ResolveDefaultStateName(RuntimeAnimatorController controller)
    {
        if (controller is AnimatorController animatorController
            && animatorController.layers != null
            && animatorController.layers.Length > 0)
        {
            AnimatorState defaultState = animatorController.layers[0].stateMachine.defaultState;
            if (defaultState != null)
                return defaultState.name;

            ChildAnimatorState[] states = animatorController.layers[0].stateMachine.states;
            if (states != null && states.Length > 0 && states[0].state != null)
                return states[0].state.name;
        }

        return "Play";
    }

    private void RefreshAssetLists()
    {
        darkLordClips.Clear();
        vfxClips.Clear();
        vfxPrefabs.Clear();
        abilityLogicAssets.Clear();

        darkLordClips.AddRange(FindAssetsAt<AnimationClip>(DarkLordAnimationFolder)
            .Where(clip => clip != null)
            .OrderBy(clip => clip.name, StringComparer.Ordinal));
        vfxClips.AddRange(FindAssetsAt<AnimationClip>(DemonKingVfxFolder)
            .Where(clip => clip != null)
            .OrderBy(clip => clip.name, StringComparer.Ordinal));
        vfxPrefabs.AddRange(FindPrefabsAt(DemonKingVfxFolder)
            .Where(prefab => prefab != null)
            .OrderBy(prefab => prefab.name, StringComparer.Ordinal));
        abilityLogicAssets.AddRange(FindScriptableAssetsAt(DemonKingAbilityLogicFolder, "AL_DemonKing_")
            .Where(asset => asset != null && asset.name.StartsWith("AL_DemonKing_", StringComparison.Ordinal))
            .OrderBy(asset => asset.name, StringComparer.Ordinal));
    }

    private void EnsureDefaultSelections()
    {
        if (selectedAnimationClip == null)
        {
            selectedAnimationClip = darkLordClips.FirstOrDefault(clip => clip.name == "DarkLord_Sword_Idle")
                ?? darkLordClips.FirstOrDefault();
            LoadClipRows(selectedAnimationClip);
        }

        if (selectedVfxPrefab == null)
        {
            selectedVfxPrefab = vfxPrefabs.FirstOrDefault(prefab => prefab.name == "DemonKingImpactVfx")
                ?? vfxPrefabs.FirstOrDefault();
            LoadTimedHitFramesFromPrefab();
        }

        if (selectedAbilityLogicAsset == null)
            selectedAbilityLogicAsset = abilityLogicAssets.FirstOrDefault();
    }

    private static IEnumerable<T> FindAssetsAt<T>(string folder) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return Array.Empty<T>();

        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
            .Where(asset => asset != null);
    }

    private static IEnumerable<GameObject> FindPrefabsAt(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return Array.Empty<GameObject>();

        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .Where(prefab => prefab != null);
    }

    private static IEnumerable<ScriptableObject> FindScriptableAssetsAt(string folder, string nameFilter)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return Array.Empty<ScriptableObject>();

        return AssetDatabase.FindAssets(nameFilter, new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Select(path => AssetDatabase.LoadAssetAtPath<ScriptableObject>(path))
            .Where(asset => asset != null);
    }

    private static void SetClipStopTimeIfAvailable(AnimationClipSettings settings, float stopTime)
    {
        if (settings == null)
            return;

        Type settingsType = typeof(AnimationClipSettings);
        PropertyInfo property = settingsType.GetProperty(
            "stopTime",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(settings, stopTime);
            return;
        }

        FieldInfo field = settingsType.GetField(
            "stopTime",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? settingsType.GetField(
                "m_StopTime",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        field?.SetValue(settings, stopTime);
    }

    private void EnsurePreviewCamera()
    {
        if (previewCamera != null)
            return;

        GameObject cameraObject = new("DemonKingVisualTuningPreviewCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 40f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = PreviewBackground;
        previewCamera.transform.position = PreviewOrigin + new Vector3(0f, 0f, PreviewDepth);
    }

    private void EnsurePreviewRoot()
    {
        if (previewRoot != null)
            return;

        previewRoot = new GameObject("DemonKingVisualTuningPreviewRoot")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        previewRoot.transform.position = PreviewOrigin;
    }

    private void EnsurePreviewTexture(Rect previewRect)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(previewRect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(previewRect.height));
        if (previewTexture != null && previewTexture.width == width && previewTexture.height == height)
            return;

        ReleasePreviewTexture();
        previewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 1
        };
    }

    private void RenderPreview()
    {
        EnsurePreviewCamera();
        if (previewTexture == null)
            EnsurePreviewTexture(new Rect(0f, 0f, DefaultPreviewTextureSize, DefaultPreviewTextureSize));
        if (previewCamera == null || previewTexture == null)
            return;

        if (TryRenderLiveRuntimePreview())
            return;

        previewCamera.transform.position = PreviewOrigin + new Vector3(0f, 0f, PreviewDepth);
        previewCamera.transform.rotation = Quaternion.identity;
        previewCamera.orthographicSize = previewCameraSize;
        previewCamera.cullingMask = ~0;
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
    }

    private bool TryRenderLiveRuntimePreview()
    {
        if (!TryResolveLiveRuntimePreviewFrame(
                out _,
                out _,
                out Vector3 center,
                out float cameraSize))
        {
            return false;
        }

        Camera gameCamera = Camera.main;
        previewCamera.transform.position = new Vector3(center.x, center.y, PreviewDepth);
        previewCamera.transform.rotation = Quaternion.identity;
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = cameraSize;
        previewCamera.nearClipPlane = gameCamera != null ? gameCamera.nearClipPlane : 0.01f;
        previewCamera.farClipPlane = gameCamera != null ? Mathf.Max(gameCamera.farClipPlane, 40f) : 40f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = PreviewBackground;
        previewCamera.cullingMask = runtimePatternLivePreviewUseGameCameraMask && gameCamera != null
            ? gameCamera.cullingMask
            : ~0;
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();

        return true;
    }

    private bool TryResolveLiveRuntimePreviewFrame(
        out DemonKingController demon,
        out Transform target,
        out Vector3 center,
        out float cameraSize)
    {
        demon = null;
        target = null;
        center = PreviewOrigin;
        cameraSize = runtimePatternLivePreviewCameraSize;

        if (!runtimePatternLivePreviewEnabled || !EditorApplication.isPlaying)
            return false;

        demon = ResolveRuntimePatternDemonKing();
        if (demon == null)
            return false;

        target = runtimePatternTarget != null && runtimePatternTarget.gameObject.activeInHierarchy
            ? runtimePatternTarget
            : null;
        if (target == null)
        {
            GameObject targetObject = ResolveRuntimePatternTarget(demon);
            target = targetObject != null ? targetObject.transform : null;
        }

        Vector3 demonPosition = demon.transform.position;
        center = demonPosition;
        cameraSize = Mathf.Max(0.1f, runtimePatternLivePreviewCameraSize);

        if (runtimePatternLivePreviewFrameTarget && target != null && target != demon.transform)
        {
            Vector3 targetPosition = target.position;
            center = (demonPosition + targetPosition) * 0.5f;
            Vector3 delta = targetPosition - demonPosition;
            float aspect = previewTexture != null && previewTexture.height > 0
                ? Mathf.Max(0.1f, previewTexture.width / (float)previewTexture.height)
                : 1f;
            float verticalSize = Mathf.Abs(delta.y) * 0.5f + runtimePatternLivePreviewPadding;
            float horizontalSize = Mathf.Abs(delta.x) * 0.5f / aspect + runtimePatternLivePreviewPadding;
            cameraSize = Mathf.Max(cameraSize, verticalSize, horizontalSize);
        }

        return true;
    }

    private bool IsLiveRuntimePreviewVisible()
    {
        return runtimePatternLivePreviewEnabled
            && EditorApplication.isPlaying
            && runtimePatternDemonKing != null
            && runtimePatternDemonKing.isActiveAndEnabled;
    }

    private void DrawLiveRuntimePreviewOverlay(Rect previewRect)
    {
        string demonName = runtimePatternDemonKing != null ? runtimePatternDemonKing.name : "(none)";
        string targetName = runtimePatternTarget != null ? runtimePatternTarget.name : "(none)";
        Rect overlayRect = new(previewRect.x + 8f, previewRect.y + 8f, 260f, 64f);
        GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);
        GUI.Label(
            new Rect(overlayRect.x + 8f, overlayRect.y + 5f, overlayRect.width - 16f, 18f),
            "Live Runtime Preview",
            EditorStyles.boldLabel);
        GUI.Label(
            new Rect(overlayRect.x + 8f, overlayRect.y + 25f, overlayRect.width - 16f, 18f),
            $"DemonKing: {demonName}");
        GUI.Label(
            new Rect(overlayRect.x + 8f, overlayRect.y + 39f, overlayRect.width - 16f, 18f),
            $"Target: {targetName}",
            EditorStyles.miniLabel);
    }

    private void DestroyPreviewInstance()
    {
        if (previewInstance != null)
        {
            Object.DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        if (previewRoot != null)
        {
            Object.DestroyImmediate(previewRoot);
            previewRoot = null;
        }

        bodyPreviewRenderer = null;
        egoSwordPreviewRenderer = null;
        activeBodyPreviewCue = null;
        activeEgoSwordPreviewCue = null;
        activeEgoSwordSpinPreviewTransform = null;
        activePatternPreviewSignature = null;
        activePatternPreviewShapes.Clear();
        previewAnimators = Array.Empty<Animator>();
        previewParticleSystems = Array.Empty<ParticleSystem>();
        previewDebrisEmitters = Array.Empty<TopDownDebrisBounceEmitter2D>();
    }

    private void DestroyPreviewCamera()
    {
        if (previewCamera == null)
            return;

        GameObject cameraObject = previewCamera.gameObject;
        previewCamera.targetTexture = null;
        previewCamera = null;
        Object.DestroyImmediate(cameraObject);
    }

    private void ReleasePreviewTexture()
    {
        if (previewTexture == null)
            return;

        previewTexture.Release();
        Object.DestroyImmediate(previewTexture);
        previewTexture = null;
    }

    private static void SetHideFlagsRecursive(Transform root, HideFlags hideFlags)
    {
        if (root == null)
            return;

        root.gameObject.hideFlags = hideFlags;
        foreach (Transform child in root)
            SetHideFlagsRecursive(child, hideFlags);
    }

    private static void SavePrefabAssetIfNeeded(Object target)
    {
        if (target == null)
            return;

        GameObject root = target switch
        {
            Component component => component.gameObject,
            GameObject gameObject => gameObject,
            _ => null
        };

        if (root != null)
            SavePrefabAssetIfPersistent(root);
        else
            AssetDatabase.SaveAssets();
    }

    private static void MarkEditedObjectDirty(Object target)
    {
        if (target == null)
            return;

        EditorUtility.SetDirty(target);
        if (target is Component component)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            if (!EditorUtility.IsPersistent(component) && component.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
        else if (target is GameObject gameObject)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            if (!EditorUtility.IsPersistent(gameObject) && gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    private static void SavePrefabAssetIfPersistent(GameObject gameObject)
    {
        if (gameObject == null)
            return;

        if (EditorUtility.IsPersistent(gameObject))
        {
            string assetPath = AssetDatabase.GetAssetPath(gameObject);
            GameObject prefabRoot = !string.IsNullOrEmpty(assetPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)
                : gameObject;
            if (prefabRoot != null)
                PrefabUtility.SavePrefabAsset(prefabRoot);
        }

        AssetDatabase.SaveAssets();
    }
}

internal enum DemonKingPatternPreviewCueKind
{
    Body,
    Vfx,
    EgoSword,
    Shape
}

internal enum DemonKingEgoSwordPreviewMode
{
    Held,
    ThrowSpin,
    RecallSpin,
    Fixed,
    VerticalTrack,
    VerticalCommit,
    CrossLaserWarning,
    CrossLaserFire
}

internal readonly struct EgoSwordPreviewPose
{
    public static EgoSwordPreviewPose Hidden => new(Vector3.zero, 0f, showSpin: false);

    public Vector3 LocalPosition { get; }
    public float RotationDeg { get; }
    public bool ShowSpin { get; }

    public EgoSwordPreviewPose(Vector3 localPosition, float rotationDeg, bool showSpin)
    {
        LocalPosition = localPosition;
        RotationDeg = rotationDeg;
        ShowSpin = showSpin;
    }
}

internal sealed class DemonKingPatternPreviewCue
{
    public DemonKingPatternPreviewCueKind Kind { get; private set; }
    public float StartSeconds { get; private set; }
    public float DurationSeconds { get; private set; }
    public float EndSeconds => StartSeconds + DurationSeconds;
    public string BodyClipName { get; private set; }
    public int BodyFrameIndex { get; private set; } = -1;
    public string VfxPrefabName { get; private set; }
    public GameObject VfxPrefabReference { get; private set; }
    public string VfxResourcePath { get; private set; }
    public bool HasSocket { get; private set; }
    public DemonKingVfxSocketId SocketId { get; private set; }
    public Vector2 FallbackLeftOffset { get; private set; }
    public Vector3 VfxScale { get; private set; } = Vector3.one;
    public float VfxRotationDeg { get; private set; }
    public DemonKingEgoSwordPreviewMode EgoSwordMode { get; private set; }
    public DemonKingPatternPreviewShape Shape { get; private set; }

    public string Signature =>
        $"{Kind}:{StartSeconds:0.###}:{DurationSeconds:0.###}:{BodyClipName}:{BodyFrameIndex}:{VfxPrefabName}:{VfxPrefabReference?.GetInstanceID()}:{VfxResourcePath}:{EgoSwordMode}:{Shape?.Label}";

    public bool IsActive(float timelineSeconds)
    {
        return timelineSeconds >= StartSeconds && timelineSeconds < EndSeconds;
    }

    public float ResolveLocalTime(float timelineSeconds)
    {
        return Mathf.Clamp(timelineSeconds - StartSeconds, 0f, Mathf.Max(0.01f, DurationSeconds));
    }

    public float ResolveBodySampleTime(AnimationClip clip, float timelineSeconds, float fallbackPhaseTime)
    {
        if (clip == null)
            return Mathf.Max(0f, fallbackPhaseTime);

        if (BodyFrameIndex == DemonKingPatternPreviewPhase.LastBodyFrameIndex)
            return Mathf.Max(0f, clip.length);

        if (BodyFrameIndex >= 0)
            return BodyFrameIndex / Mathf.Max(0.01f, clip.frameRate);

        float localTime = ResolveLocalTime(timelineSeconds);
        return Mathf.Clamp(localTime, 0f, Mathf.Max(0f, clip.length));
    }

    public static DemonKingPatternPreviewCue Body(DemonKingPatternPreviewPhase phase)
    {
        return new DemonKingPatternPreviewCue
        {
            Kind = DemonKingPatternPreviewCueKind.Body,
            StartSeconds = phase.StartSeconds,
            DurationSeconds = phase.DurationSeconds,
            BodyClipName = phase.BodyClipName,
            BodyFrameIndex = phase.BodyFrameIndex
        };
    }

    public static DemonKingPatternPreviewCue Vfx(DemonKingPatternPreviewPhase phase)
    {
        return new DemonKingPatternPreviewCue
        {
            Kind = DemonKingPatternPreviewCueKind.Vfx,
            StartSeconds = phase.StartSeconds,
            DurationSeconds = phase.DurationSeconds,
            VfxPrefabName = phase.VfxPrefabName,
            VfxPrefabReference = phase.VfxPrefabReference,
            HasSocket = phase.HasSocket,
            SocketId = phase.SocketId,
            FallbackLeftOffset = phase.FallbackLeftOffset,
            VfxScale = phase.VfxScale,
            VfxRotationDeg = phase.VfxRotationDeg
        };
    }

    public static DemonKingPatternPreviewCue Vfx(
        float startSeconds,
        float durationSeconds,
        string prefabName,
        string resourcePath,
        Vector2 fallbackLeftOffset,
        Vector3 scale,
        float rotationDeg,
        DemonKingPatternPreviewShape shape = null,
        GameObject prefabReference = null)
    {
        return new DemonKingPatternPreviewCue
        {
            Kind = DemonKingPatternPreviewCueKind.Vfx,
            StartSeconds = startSeconds,
            DurationSeconds = Mathf.Max(0.01f, durationSeconds),
            VfxPrefabName = prefabName,
            VfxPrefabReference = prefabReference,
            VfxResourcePath = resourcePath,
            FallbackLeftOffset = fallbackLeftOffset,
            VfxScale = scale == Vector3.zero ? Vector3.one : scale,
            VfxRotationDeg = rotationDeg,
            Shape = shape
        };
    }

    public static DemonKingPatternPreviewCue EgoSword(
        float startSeconds,
        float durationSeconds,
        DemonKingEgoSwordPreviewMode mode)
    {
        return new DemonKingPatternPreviewCue
        {
            Kind = DemonKingPatternPreviewCueKind.EgoSword,
            StartSeconds = startSeconds,
            DurationSeconds = Mathf.Max(0.01f, durationSeconds),
            EgoSwordMode = mode
        };
    }

    public static DemonKingPatternPreviewCue ShapeCue(DemonKingPatternPreviewPhase phase)
    {
        return new DemonKingPatternPreviewCue
        {
            Kind = DemonKingPatternPreviewCueKind.Shape,
            StartSeconds = phase.StartSeconds,
            DurationSeconds = phase.DurationSeconds,
            Shape = phase.Shape
        };
    }
}

internal static class DemonKingPatternPreviewPlaybackRunner
{
    public static List<DemonKingPatternPreviewCue> ResolveActiveCues(
        DemonKingPatternPreviewDefinition definition,
        float timelineSeconds)
    {
        return definition != null
            ? definition.ResolveActiveCues(timelineSeconds)
            : new List<DemonKingPatternPreviewCue>();
    }

    public static string BuildSignature(IReadOnlyList<DemonKingPatternPreviewCue> activeCues)
    {
        if (activeCues == null || activeCues.Count == 0)
            return "(empty)";

        return string.Join("|", activeCues.Select(cue => cue.Signature));
    }
}

internal enum DemonKingPatternPreviewShapeKind
{
    None,
    Circle,
    Rectangle,
    Sector
}

internal sealed class DemonKingPatternPreviewShape
{
    public DemonKingPatternPreviewShapeKind Kind { get; private set; }
    public string Label { get; private set; }
    public Vector2 CenterOffset { get; private set; }
    public Vector2 Size { get; private set; }
    public float Radius { get; private set; }
    public float AngleDeg { get; private set; }
    public float RotationDeg { get; private set; }
    public bool HasPlayerAnchor { get; private set; }
    public Vector2 PlayerAnchorOffset { get; private set; }
    public Color Color { get; private set; } = new(1f, 0.75f, 0.15f, 0.8f);

    public static DemonKingPatternPreviewShape Circle(string label, Vector2 centerOffset, float diameter)
    {
        float safeDiameter = Mathf.Max(0.1f, diameter);
        return new DemonKingPatternPreviewShape
        {
            Kind = DemonKingPatternPreviewShapeKind.Circle,
            Label = label,
            CenterOffset = centerOffset,
            Size = new Vector2(safeDiameter, safeDiameter * DemonKingCombatUtil.TopDownCircleWarningYScale),
            Radius = safeDiameter * 0.5f
        };
    }

    public static DemonKingPatternPreviewShape Rectangle(string label, Vector2 centerOffset, Vector2 size, float rotationDeg)
    {
        return new DemonKingPatternPreviewShape
        {
            Kind = DemonKingPatternPreviewShapeKind.Rectangle,
            Label = label,
            CenterOffset = centerOffset,
            Size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y)),
            RotationDeg = rotationDeg
        };
    }

    public static DemonKingPatternPreviewShape Sector(
        string label,
        Vector2 originOffset,
        float radius,
        float angleDeg,
        float rotationDeg)
    {
        return new DemonKingPatternPreviewShape
        {
            Kind = DemonKingPatternPreviewShapeKind.Sector,
            Label = label,
            CenterOffset = originOffset,
            Radius = Mathf.Max(0.05f, radius),
            AngleDeg = Mathf.Clamp(angleDeg, 1f, 360f),
            RotationDeg = rotationDeg
        };
    }

    public DemonKingPatternPreviewShape WithPlayerAnchor(Vector2 playerAnchorOffset)
    {
        HasPlayerAnchor = true;
        PlayerAnchorOffset = playerAnchorOffset;
        return this;
    }
}

internal sealed class DemonKingPatternPreviewFieldGroup
{
    public string Title { get; }
    public IReadOnlyList<string> PropertyNames { get; }

    public DemonKingPatternPreviewFieldGroup(string title, params string[] propertyNames)
    {
        Title = title;
        PropertyNames = propertyNames ?? Array.Empty<string>();
    }
}

internal enum DemonKingPatternPreviewMappingSource
{
    PatternAsset,
    EgoSwordActor
}

internal sealed class DemonKingPatternPreviewMappingRow
{
    public string Label { get; }
    public DemonKingPatternPreviewMappingSource Source { get; }
    public string BodyPropertyPath { get; }
    public string VfxPropertyPath { get; }
    public string ObjectVfxPropertyPath { get; }
    public string ResourcePathPropertyPath { get; }

    public DemonKingPatternPreviewMappingRow(
        string label,
        DemonKingPatternPreviewMappingSource source,
        string bodyPropertyPath = null,
        string vfxPropertyPath = null,
        string objectVfxPropertyPath = null,
        string resourcePathPropertyPath = null)
    {
        Label = label;
        Source = source;
        BodyPropertyPath = bodyPropertyPath;
        VfxPropertyPath = vfxPropertyPath;
        ObjectVfxPropertyPath = objectVfxPropertyPath;
        ResourcePathPropertyPath = resourcePathPropertyPath;
    }
}

internal sealed class DemonKingPatternPreviewPhase
{
    public const int LastBodyFrameIndex = int.MaxValue;

    public string Category { get; }
    public string Name { get; }
    public float StartSeconds { get; set; }
    public float DurationSeconds { get; }
    public float EndSeconds => StartSeconds + DurationSeconds;
    public float DefaultPreviewTime { get; private set; }
    public string BodyClipName { get; private set; }
    public string BodyPropertyPath { get; private set; }
    public bool HasEditableBodyCue => !string.IsNullOrWhiteSpace(BodyPropertyPath);
    public int BodyFrameIndex { get; private set; } = -1;
    public string VfxPrefabName { get; private set; }
    public GameObject VfxPrefabReference { get; private set; }
    public bool HasSocket { get; private set; }
    public DemonKingVfxSocketId SocketId { get; private set; }
    public Vector2 FallbackLeftOffset { get; private set; }
    public Vector3 VfxScale { get; private set; } = Vector3.one;
    public float VfxRotationDeg { get; private set; }
    public DemonKingPatternPreviewShape Shape { get; private set; }
    public string Policy { get; private set; } = "No additional preview policy.";
    public string Notes { get; private set; }

    public DemonKingPatternPreviewPhase(string category, string name, float durationSeconds)
    {
        Category = category;
        Name = name;
        DurationSeconds = Mathf.Max(0.01f, durationSeconds);
    }

    public DemonKingPatternPreviewPhase WithBody(string clipName, int frameIndex = -1, string bodyPropertyPath = null)
    {
        BodyClipName = clipName;
        BodyFrameIndex = frameIndex;
        BodyPropertyPath = bodyPropertyPath;
        return this;
    }

    public DemonKingPatternPreviewPhase WithVfx(
        string prefabName,
        DemonKingVfxSocketId socketId,
        Vector2 fallbackLeftOffset = default,
        float rotationDeg = 0f,
        Vector3 scale = default,
        GameObject prefabReference = null)
    {
        VfxPrefabName = prefabName;
        VfxPrefabReference = prefabReference;
        HasSocket = true;
        SocketId = socketId;
        FallbackLeftOffset = fallbackLeftOffset;
        VfxRotationDeg = rotationDeg;
        VfxScale = scale == Vector3.zero ? Vector3.one : scale;
        return this;
    }

    public DemonKingPatternPreviewPhase WithVfx(string prefabName)
    {
        VfxPrefabName = prefabName;
        return this;
    }

    public DemonKingPatternPreviewPhase WithVfx(GameObject prefabReference, string fallbackName)
    {
        VfxPrefabReference = prefabReference;
        VfxPrefabName = prefabReference != null ? prefabReference.name : fallbackName;
        return this;
    }

    public DemonKingPatternPreviewPhase WithShape(DemonKingPatternPreviewShape shape)
    {
        Shape = shape;
        return this;
    }

    public DemonKingPatternPreviewPhase WithPolicy(string policy)
    {
        Policy = policy;
        return this;
    }

    public DemonKingPatternPreviewPhase WithNotes(string notes)
    {
        Notes = notes;
        return this;
    }

    public DemonKingPatternPreviewPhase WithDefaultPreviewTime(float seconds)
    {
        DefaultPreviewTime = Mathf.Clamp(seconds, 0f, DurationSeconds);
        return this;
    }
}

internal sealed class DemonKingPatternPreviewDefinition
{
    private const float LeftRotationDeg = 180f;
    private static readonly Vector2 ForwardLineCenter = new(-2f, 0f);

    public string DisplayName { get; }
    public string Description { get; }
    public List<DemonKingPatternPreviewPhase> Phases { get; } = new();
    public List<DemonKingPatternPreviewCue> Cues { get; } = new();
    public List<DemonKingPatternPreviewFieldGroup> FieldGroups { get; } = new();
    public List<DemonKingPatternPreviewMappingRow> MappingRows { get; } = new();
    public HashSet<string> MappingPropertyPaths { get; } = new(StringComparer.Ordinal);
    public HashSet<string> LegacyNoRuntimeEffectProperties { get; } = new(StringComparer.Ordinal);
    public bool PrefersEgoSwordPreview { get; private set; }
    public float TotalDuration => Phases.Count == 0 ? 0f : Phases[Phases.Count - 1].EndSeconds;

    private DemonKingPatternPreviewDefinition(string displayName, string description)
    {
        DisplayName = displayName;
        Description = description;
    }

    public static DemonKingPatternPreviewDefinition Create(
        ScriptableObject asset,
        SerializedObject serializedObject,
        EgoSwordActor selectedEgoSword = null)
    {
        if (asset == null || serializedObject == null)
            return null;

        if (Matches(asset, nameof(AbilityLogic_DemonKingPierceCombo)))
            return BuildPierceCombo(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingHeavySlash)))
            return BuildHeavySlash(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingThrowEgoSword)))
            return BuildThrowEgoSword(serializedObject, selectedEgoSword);
        if (Matches(asset, nameof(AbilityLogic_DemonKingHomingMagic)))
            return BuildHomingMagic(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingBombardment)))
            return BuildBombardment(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingExplosionJump)))
            return BuildExplosionJump(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingRecallEgoSword)))
            return BuildRecallEgoSword(serializedObject, selectedEgoSword);
        if (Matches(asset, nameof(AbilityLogic_DemonKingWallBounceRush)))
            return BuildWallBounceRush(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingGroggyRecoverCounter)))
            return BuildGroggyRecoverCounter(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingFinalDesperation)))
            return BuildFinalDesperation(serializedObject);
        if (Matches(asset, nameof(AbilityLogic_DemonKingEgoSwordVerticalStrike)))
            return BuildEgoSwordVerticalStrike(selectedEgoSword);
        if (Matches(asset, nameof(AbilityLogic_DemonKingEgoSwordCrossLaser)))
            return BuildEgoSwordCrossLaser(selectedEgoSword);

        return null;
    }

    public void AddCue(DemonKingPatternPreviewCue cue)
    {
        if (cue != null)
            Cues.Add(cue);
    }

    public void PreferEgoSword()
    {
        PrefersEgoSwordPreview = true;
    }

    public List<DemonKingPatternPreviewCue> ResolveActiveCues(float timelineSeconds)
    {
        List<DemonKingPatternPreviewCue> active = new();
        for (int i = 0; i < Cues.Count; i++)
        {
            DemonKingPatternPreviewCue cue = Cues[i];
            if (cue != null && cue.IsActive(timelineSeconds))
                active.Add(cue);
        }

        return active;
    }

    private static DemonKingPatternPreviewDefinition BuildPierceCombo(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "PierceCombo",
            "Three-step sword dash pattern. Preview rows expand each tracking warning, lock-on, dash, return, and final hold beat.");
        definition.AddGroup("Timing", "pierceCount", "firstWarningSeconds", "warningStepDecrease", "lockOnSeconds", "lungeSeconds", "returnSeconds", "intervalSeconds", "dashEndPoseHoldSeconds");
        definition.AddGroup("Animation / VFX", "readyAnimation", "dashAnimation", "stabVfx");
        definition.AddGroup("Warning / Hit", "hitWidth", "damage", "knockback");
        definition.AddGroup("SFX / Shake", "dashCommitSound");
        definition.AddMapping("Ready Pose", bodyPropertyPath: "readyAnimation");
        definition.AddMapping("Dash Pose / Stab VFX", bodyPropertyPath: "dashAnimation", vfxPropertyPath: "stabVfx");

        int count = Mathf.Clamp(Int(serializedObject, "pierceCount", 3), 1, 8);
        float firstWarning = Float(serializedObject, "firstWarningSeconds", 1f);
        float warningStep = Float(serializedObject, "warningStepDecrease", 0.2f);
        float lockOn = Float(serializedObject, "lockOnSeconds", 0.25f);
        float lunge = Float(serializedObject, "lungeSeconds", 0.16f);
        float returnSeconds = Float(serializedObject, "returnSeconds", 0.12f);
        float interval = Float(serializedObject, "intervalSeconds", 0.12f);
        float endHold = Float(serializedObject, "dashEndPoseHoldSeconds", 0.1f);
        float hitWidth = Float(serializedObject, "hitWidth", 1.05f);
        float time = 0f;
        for (int i = 0; i < count; i++)
        {
            float warning = Mathf.Max(0.01f, firstWarning - warningStep * i);
            AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Warning", $"Pierce {i + 1} ready", warning)
                .WithBody(BodyName(serializedObject, "readyAnimation", "DarkLord_Sword_DashStabReady"), BodyFrame(serializedObject, "readyAnimation", 0), "readyAnimation")
                .WithShape(DemonKingPatternPreviewShape.Rectangle("Dash warning", ForwardLineCenter, new Vector2(4f, hitWidth), LeftRotationDeg))
                .WithPolicy("DashStabReady first frame is held while the warning and boss facing follow the current target."));
            if (lockOn > 0f)
            {
                AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("LockOn", $"Pierce {i + 1} locked warning", lockOn)
                    .WithBody(BodyName(serializedObject, "readyAnimation", "DarkLord_Sword_DashStabReady"), BodyFrame(serializedObject, "readyAnimation", 0), "readyAnimation")
                    .WithShape(DemonKingPatternPreviewShape.Rectangle("Locked dash warning", ForwardLineCenter, new Vector2(4f, hitWidth), LeftRotationDeg))
                    .WithPolicy("The final target line freezes and uses blink telegraph style before the dash."));
            }
            AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Commit", $"Pierce {i + 1} dash", lunge)
                .WithBody(BodyName(serializedObject, "dashAnimation", "DarkLord_Sword_DashStab"), BodyFrame(serializedObject, "dashAnimation", -1), "dashAnimation")
                .WithVfx(
                    VfxName(serializedObject, "stabVfx", "DemonKingStabVfx"),
                    VfxSocket(serializedObject, "stabVfx", DemonKingVfxSocketId.SwordStabOrigin),
                    VfxFallbackOffset(serializedObject, "stabVfx", Vector2.zero),
                    VfxRotation(serializedObject, "stabVfx"),
                    VfxScale(serializedObject, "stabVfx"),
                    VfxPrefab(serializedObject, "stabVfx"))
                .WithShape(DemonKingPatternPreviewShape.Rectangle("Dash hit", ForwardLineCenter, new Vector2(4f, hitWidth), LeftRotationDeg))
                .WithPolicy("DashStab plays for the actual contact-damage lunge only."));
            if (i < count - 1)
            {
                AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Recover", $"Pierce {i + 1} return", returnSeconds)
                    .WithBody(BodyName(serializedObject, "readyAnimation", "DarkLord_Sword_DashStabReady"), BodyFrame(serializedObject, "readyAnimation", 0), "readyAnimation")
                    .WithPolicy("Recover returns to the ready pose before the next dash."));
                AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Interval", $"Pierce {i + 1} interval", interval)
                    .WithBody(BodyName(serializedObject, "readyAnimation", "DarkLord_Sword_DashStabReady"), BodyFrame(serializedObject, "readyAnimation", 0), "readyAnimation"));
            }
            else
            {
                AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Recover", "Final dash end hold", endHold)
                    .WithBody(BodyName(serializedObject, "readyAnimation", "DarkLord_Sword_DashStabReady"), BodyFrame(serializedObject, "readyAnimation", 0), "readyAnimation"));
            }
        }

        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildHeavySlash(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "HeavySlash",
            "Approach stops before the player, then Slash_1 tracks a player-attached origin before Slash_2 commit and delayed DarkLordExplosion2 line explosions.");
        definition.AddGroup("Movement", "moveSpeedMultiplier", "fallbackMoveSeconds", "stopBeforeTargetDistance");
        definition.AddGroup("Warning / Hit", "trackingWarningSeconds", "lockOnSeconds", "slashCommitDashSeconds", "slashCommitDashEaseOutPower", "slashRadius", "playerAnchorInWarningRadius", "slashAngle", "damage", "knockback");
        definition.AddGroup("Legacy", "warningSeconds");
        definition.AddGroup("Line Explosions", "fallbackLineLength", "lineWidth", "explosionSpacing", "explosionDiameter", "explosionWarningSeconds", "explosionStepInterval", "explosionDamage");
        definition.AddGroup("Animation / VFX", "approachAnimation", "slashWarningAnimation", "slashCommitAnimation", "slashVfx", "lineExplosionVfx", "slashEndPoseHoldSeconds");
        definition.AddGroup("SFX / Shake", "approachSound", "slashCommitSound", "lineExplosionSound", "slashImpactCameraShake", "lineExplosionCameraShake");
        definition.AddMapping("Approach Body", bodyPropertyPath: "approachAnimation");
        definition.AddMapping("Slash Warning Hold", bodyPropertyPath: "slashWarningAnimation");
        definition.AddMapping("Slash Commit / Slash VFX", bodyPropertyPath: "slashCommitAnimation", vfxPropertyPath: "slashVfx");
        definition.AddMapping("Follow-up Line Explosion VFX", vfxPropertyPath: "lineExplosionVfx");

        float time = 0f;
        float move = Float(serializedObject, "fallbackMoveSeconds", 0.35f);
        float trackingWarning = Float(serializedObject, "trackingWarningSeconds", 2f);
        float lockOn = Float(serializedObject, "lockOnSeconds", 0.4f);
        float commitDash = Float(serializedObject, "slashCommitDashSeconds", 0.16f);
        float radius = Float(serializedObject, "slashRadius", 3.9f);
        float playerAnchor = Mathf.Clamp01(Float(serializedObject, "playerAnchorInWarningRadius", 0.5f));
        float angle = Float(serializedObject, "slashAngle", 110f);
        float lineLength = Float(serializedObject, "fallbackLineLength", 40f);
        float lineWidth = Float(serializedObject, "lineWidth", 0.7f);
        float explosionDiameter = Float(serializedObject, "explosionDiameter", 1.35f);
        float explosionStep = Float(serializedObject, "explosionStepInterval", 0.04f);
        float hold = Float(serializedObject, "slashEndPoseHoldSeconds", 0.12f);
        Vector2 slashPreviewOrigin = new(radius * playerAnchor, 0f);

        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Move", "Approach and face target", move)
            .WithBody(BodyName(serializedObject, "approachAnimation", string.Empty), BodyFrame(serializedObject, "approachAnimation", -1), "approachAnimation")
            .WithPolicy("Uses stopBeforeTargetDistance only for the first approach move; slash warning and hit origin are not offset by this value."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Warning", "Slash_1 tracking warning", trackingWarning)
            .WithBody(BodyName(serializedObject, "slashWarningAnimation", "DarkLord_Sword_Slash"), BodyFrame(serializedObject, "slashWarningAnimation", 1), "slashWarningAnimation")
            .WithShape(DemonKingPatternPreviewShape.Sector("Slash sector", slashPreviewOrigin, radius, angle, LeftRotationDeg).WithPlayerAnchor(Vector2.zero))
            .WithPolicy("Slash_1 tracks player position and angle; playerAnchorInWarningRadius controls where the player sits inside the sector before LockOn."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("LockOn", "Blink locked slash warning", Mathf.Max(0.01f, lockOn))
            .WithBody(BodyName(serializedObject, "slashWarningAnimation", "DarkLord_Sword_Slash"), BodyFrame(serializedObject, "slashWarningAnimation", 1), "slashWarningAnimation")
            .WithShape(DemonKingPatternPreviewShape.Sector("Locked slash sector", slashPreviewOrigin, radius, angle, LeftRotationDeg).WithPlayerAnchor(Vector2.zero))
            .WithPolicy("Position and angle freeze for blink warning before the commit dash."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Commit", "Dash to locked SwordSlashOrigin and Slash_2 impact", Mathf.Max(0.01f, commitDash))
            .WithBody(BodyName(serializedObject, "slashCommitAnimation", "DarkLord_Sword_Slash"), BodyFrame(serializedObject, "slashCommitAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "slashCommitAnimation")
            .WithVfx(
                VfxName(serializedObject, "slashVfx", "DarkLordSlashVfx"),
                VfxSocket(serializedObject, "slashVfx", DemonKingVfxSocketId.SwordSlashOrigin),
                VfxFallbackOffset(serializedObject, "slashVfx", Vector2.zero),
                VfxRotation(serializedObject, "slashVfx"),
                VfxScale(serializedObject, "slashVfx"),
                VfxPrefab(serializedObject, "slashVfx"))
            .WithShape(DemonKingPatternPreviewShape.Sector("Slash damage", slashPreviewOrigin, radius, angle, LeftRotationDeg).WithPlayerAnchor(Vector2.zero))
            .WithPolicy("Boss dashes with afterimage so SwordSlashOrigin lands on the locked sector origin while the player stays at the locked anchor point."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Explosion", "Follow-up line explosions", Mathf.Max(0.01f, explosionStep * 6f))
            .WithVfx(VfxPrefab(serializedObject, "lineExplosionVfx"), VfxName(serializedObject, "lineExplosionVfx", "DarkLordExplosion2Vfx"))
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Line explosion lanes", new Vector2(-lineLength * 0.1f, 0f), new Vector2(Mathf.Min(lineLength, 8f), lineWidth), LeftRotationDeg))
            .WithPolicy($"DarkLordExplosion2 repeats at spacing, diameter {explosionDiameter:0.00}."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Recover", "Slash end pose hold", hold)
            .WithBody(BodyName(serializedObject, "slashCommitAnimation", "DarkLord_Sword_Slash"), BodyFrame(serializedObject, "slashCommitAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "slashCommitAnimation"));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildThrowEgoSword(SerializedObject serializedObject, EgoSwordActor selectedEgoSword)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "ThrowEgoSword",
            "Sword throw warning, DarkLord_Sword_Throwing frame timing, then EgoSword appears from the throw origin.");
        definition.PreferEgoSword();
        definition.AddGroup("Timing", "warningSeconds", "throwReleaseDelaySeconds", "throwSpeedMultiplier", "wallBounceCount", "throwEndPoseHoldSeconds");
        definition.AddGroup("Animation", "aimAnimation", "throwAnimation", "throwEndPoseAnimation");
        definition.AddGroup("SFX / Shake", "throwReleaseSound");
        definition.AddMapping("Aim Body", bodyPropertyPath: "aimAnimation");
        definition.AddMapping("Throw Body Clip", bodyPropertyPath: "throwAnimation");
        definition.AddMapping("Throw End Pose Body", bodyPropertyPath: "throwEndPoseAnimation");
        definition.AddMapping(
            "EgoSword Throw Spin VFX",
            vfxPropertyPath: "swordSpinVfx",
            source: DemonKingPatternPreviewMappingSource.EgoSwordActor);
        float time = 0f;
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Warning", "Aim held sword", Float(serializedObject, "warningSeconds", 1.4f))
            .WithBody(BodyName(serializedObject, "aimAnimation", "DarkLord_Sword_Idle"), BodyFrame(serializedObject, "aimAnimation", 0), "aimAnimation")
            .WithPolicy("No EgoSword actor is visible while held; the body owns the sword pose."));
        float throwReleaseDelay = Float(serializedObject, "throwReleaseDelaySeconds", 1.5f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Animation", "Throw wind-up frame", throwReleaseDelay)
            .WithBody(BodyName(serializedObject, "throwAnimation", "DarkLord_Sword_Throwing"), 0, "throwAnimation")
            .WithPolicy("Frame 0 communicates the long throw preparation; release starts when Throw_1 appears."));
        float releaseStart = time;
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Commit", "Throw release frame", 0.5f)
            .WithBody(BodyName(serializedObject, "throwAnimation", "DarkLord_Sword_Throwing"), DemonKingPatternPreviewPhase.LastBodyFrameIndex, "throwAnimation")
            .WithVfx(
                EgoSwordVfxName(selectedEgoSword, "swordSpinVfx", "SwordSpin4FrameVfx"),
                DemonKingVfxSocketId.SwordThrowEffectOrigin,
                prefabReference: EgoSwordVfxPrefab(selectedEgoSword, "swordSpinVfx"))
            .WithPolicy("Throw_1/release frame starts EgoSword throw and spin VFX; EgoSword offsets are reviewed with the marker overlay."));
        definition.AddCue(DemonKingPatternPreviewCue.EgoSword(
            releaseStart,
            Mathf.Max(0.01f, time - releaseStart),
            DemonKingEgoSwordPreviewMode.ThrowSpin));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Recover", "Throw end pose hold", Float(serializedObject, "throwEndPoseHoldSeconds", 0.12f))
            .WithBody(BodyName(serializedObject, "throwEndPoseAnimation", "DarkLord_Sword_Throwing"), BodyFrame(serializedObject, "throwEndPoseAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "throwEndPoseAnimation")
            .WithPolicy("End pose is independent from the throw release clip so it can be tuned separately."));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildHomingMagic(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "HomingMagic",
            "Loads stock Balt VFX above the boss, then consumes the target-side orb for each fired projectile.");
        definition.AddGroup("Projectile", "projectileCount", "moveSeconds", "shotIntervalSeconds", "projectileSpeedMultiplier", "projectileRadius", "projectileDamage", "lifetimeSeconds");
        definition.AddGroup("Stock VFX", "stockOrbVisualPrefab", "firedProjectileVisualPrefab", "stockOrbBaseLocalOffset", "stockOrbSpacing", "stockOrbArcHeight");
        definition.AddGroup("Animation", "castAnimation", "fireAnimation");
        definition.AddGroup("Movement", "orbSpawnRadius", "wallProbeRadius");
        definition.AddGroup("SFX / Shake", "castSound", "fireSound");
        definition.AddMapping("Cast / Stock Body", bodyPropertyPath: "castAnimation", objectVfxPropertyPath: "stockOrbVisualPrefab");
        definition.AddMapping("Fire / Projectile Body", bodyPropertyPath: "fireAnimation", objectVfxPropertyPath: "firedProjectileVisualPrefab");

        float time = 0f;
        int count = Mathf.Clamp(Int(serializedObject, "projectileCount", 5), 1, 10);
        float interval = Float(serializedObject, "shotIntervalSeconds", 0.4f);
        float move = Float(serializedObject, "moveSeconds", 0.18f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Load", "Overhead stock arc", 0.12f)
            .WithBody(BodyName(serializedObject, "castAnimation", "DarkLord_Hand_Balt"), BodyFrame(serializedObject, "castAnimation", 0), "castAnimation")
            .WithVfx(
                ObjectPrefabName(serializedObject, "stockOrbVisualPrefab", "HomingMagicBaltStockVfx"),
                DemonKingVfxSocketId.HomingStockCenter,
                new Vector2(0f, 1.6f),
                prefabReference: ObjectPrefab(serializedObject, "stockOrbVisualPrefab"))
            .WithPolicy("Stock VFX count and arch spacing are pattern-owned."));
        for (int i = 0; i < count; i++)
        {
            AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Fire", $"Consume stock {i + 1}", Mathf.Max(0.01f, interval))
                .WithBody(BodyName(serializedObject, "fireAnimation", "DarkLord_Hand_Balt"), BodyFrame(serializedObject, "fireAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "fireAnimation")
                .WithVfx(
                    ObjectPrefabName(serializedObject, "firedProjectileVisualPrefab", "HomingMagicBaltProjectileVfx"),
                    DemonKingVfxSocketId.HomingStockCenter,
                    new Vector2(0f, 1.6f),
                    prefabReference: ObjectPrefab(serializedObject, "firedProjectileVisualPrefab"))
                .WithPolicy($"Fire projectile, then dash/reposition for about {move:0.00}s."));
        }

        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildBombardment(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "Bombardment",
            "Hand charge, GroggyCounter release impact, one-second release hold, then DarkLordExplosion2 lane strikes.");
        definition.AddGroup("Timing", "strikeCount", "moveSeconds", "warningSeconds", "warningIntervalSeconds", "releaseImpactPoseHoldSeconds");
        definition.AddGroup("Animation / VFX", "chargeAnimation", "releaseAnimation", "postReleaseAnimation", "releaseImpactVfx", "laneExplosionVfx");
        definition.AddGroup("Lane", "sideOffset", "laneWidth", "fallbackMapHeight", "explosionDiameter", "explosionSpacing", "damage");
        definition.AddGroup("SFX / Shake", "chargeSound", "releaseSound", "explosionSound", "bombardmentReleaseImpactCameraShake", "explosionCameraShake");
        definition.AddMapping("Charge Body", bodyPropertyPath: "chargeAnimation");
        definition.AddMapping("Release Body / Impact VFX", bodyPropertyPath: "releaseAnimation", vfxPropertyPath: "releaseImpactVfx");
        definition.AddMapping("Post Release Body", bodyPropertyPath: "postReleaseAnimation");
        definition.AddMapping("Lane Explosion VFX", vfxPropertyPath: "laneExplosionVfx");

        float time = 0f;
        float move = Float(serializedObject, "moveSeconds", 0.5f);
        float warning = Float(serializedObject, "warningSeconds", 0.6f);
        float interval = Float(serializedObject, "warningIntervalSeconds", 0.3f);
        float explosionDiameter = Float(serializedObject, "explosionDiameter", 1.35f);
        int count = Mathf.Clamp(Int(serializedObject, "strikeCount", 6), 1, 12);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Charge", "Hand charge while moving", move)
            .WithBody(BodyName(serializedObject, "chargeAnimation", "DarkLord_Hand_Charge"), BodyFrame(serializedObject, "chargeAnimation", -1), "chargeAnimation")
            .WithPolicy("Charge pose and movement happen before lane release."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Release", "GroggyCounter impact frame", Float(serializedObject, "releaseImpactPoseHoldSeconds", 1f))
            .WithBody(BodyName(serializedObject, "releaseAnimation", "DarkLord_Hand_GroggyCounter"), BodyFrame(serializedObject, "releaseAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "releaseAnimation")
            .WithVfx(
                VfxName(serializedObject, "releaseImpactVfx", "DemonKingImpactVfx"),
                VfxSocket(serializedObject, "releaseImpactVfx", DemonKingVfxSocketId.HandCounterImpact),
                VfxFallbackOffset(serializedObject, "releaseImpactVfx", Vector2.zero),
                VfxRotation(serializedObject, "releaseImpactVfx"),
                VfxScale(serializedObject, "releaseImpactVfx"),
                VfxPrefab(serializedObject, "releaseImpactVfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("Release impact", Vector2.zero, explosionDiameter))
            .WithPolicy("Impact VFX, release sound, shake, and release-frame hold are owned by Bombardment."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Recover", "Post release body", 0.12f)
            .WithBody(BodyName(serializedObject, "postReleaseAnimation", "DarkLord_Hand_Idle"), BodyFrame(serializedObject, "postReleaseAnimation", 0), "postReleaseAnimation")
            .WithPolicy("Body pose after release hold, before lane explosions continue."));
        for (int i = 0; i < count; i++)
        {
            AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Explosion", $"Lane strike {i + 1}", Mathf.Max(0.01f, interval + warning))
                .WithVfx(VfxPrefab(serializedObject, "laneExplosionVfx"), VfxName(serializedObject, "laneExplosionVfx", "DarkLordExplosion2Vfx"))
                .WithShape(DemonKingPatternPreviewShape.Rectangle("Lane explosions", ForwardLineCenter, new Vector2(5f, Float(serializedObject, "laneWidth", 1.6f)), 90f))
                .WithPolicy("Lane strike warning resolves into DarkLordExplosion2 cluster."));
        }

        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildExplosionJump(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "ExplosionJump",
            "JumpAttack first frame is held through travel; last frame, impact, damage, sound, and shake occur on landing.");
        definition.AddGroup("Jump", "travelSeconds", "jumpArcHeight", "jumpMotionProfile", "landingPoseHoldSeconds", "landingFrameSwitchRatio");
        definition.AddGroup("Landing Hit", "impactDiameter", "damage", "knockback");
        definition.AddGroup("Radial Explosion", "radialWarningSeconds", "radialLineWidth", "radialFallbackLength", "radialExplosionDiameter", "radialExplosionSpacing", "radialExplosionStepInterval", "radialDamage");
        definition.AddGroup("Animation / VFX", "jumpTravelAnimation", "jumpLandingAnimation", "landingImpactVfx", "radialExplosionVfx");
        definition.AddGroup("SFX / Shake", "jumpStartSound", "landingImpactSound", "radialExplosionSound", "landingImpactCameraShake", "radialExplosionCameraShake");
        definition.LegacyNoRuntimeEffectProperties.Add("landingFrameSwitchRatio");
        definition.AddMapping("Jump Travel Body", bodyPropertyPath: "jumpTravelAnimation");
        definition.AddMapping("Landing Body / Impact VFX", bodyPropertyPath: "jumpLandingAnimation", vfxPropertyPath: "landingImpactVfx");
        definition.AddMapping("Radial Explosion VFX", vfxPropertyPath: "radialExplosionVfx");

        float time = 0f;
        float travel = Float(serializedObject, "travelSeconds", 0.7f);
        float impactDiameter = Float(serializedObject, "impactDiameter", 3.2f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Warning", "Jump travel / landing warning", travel)
            .WithBody(BodyName(serializedObject, "jumpTravelAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "jumpTravelAnimation", 0), "jumpTravelAnimation")
            .WithShape(DemonKingPatternPreviewShape.Circle("Landing warning", Vector2.zero, impactDiameter))
            .WithPolicy("JumpAttack_0 is held while the root follows the serialized jumpMotionProfile."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Commit", "Landing impact frame", Float(serializedObject, "landingPoseHoldSeconds", 0.14f))
            .WithBody(BodyName(serializedObject, "jumpLandingAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "jumpLandingAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "jumpLandingAnimation")
            .WithVfx(
                VfxName(serializedObject, "landingImpactVfx", "DemonKingImpactVfx"),
                VfxSocket(serializedObject, "landingImpactVfx", DemonKingVfxSocketId.FootLandingImpact),
                VfxFallbackOffset(serializedObject, "landingImpactVfx", Vector2.zero),
                VfxRotation(serializedObject, "landingImpactVfx"),
                VfxScale(serializedObject, "landingImpactVfx"),
                VfxPrefab(serializedObject, "landingImpactVfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("Landing impact", Vector2.zero, impactDiameter))
            .WithPolicy("JumpAttack_1/last frame switches exactly when impact VFX and hit callback commit."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Explosion", "Radial explosion wave", Float(serializedObject, "radialWarningSeconds", 0.6f))
            .WithVfx(VfxPrefab(serializedObject, "radialExplosionVfx"), VfxName(serializedObject, "radialExplosionVfx", "DemonKingExplosionVfx"))
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Radial lines", ForwardLineCenter, new Vector2(5f, Float(serializedObject, "radialLineWidth", 1.3f)), LeftRotationDeg)));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildRecallEgoSword(SerializedObject serializedObject, EgoSwordActor selectedEgoSword)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "RecallEgoSword",
            "SwordRecover first frame is held while EgoSword lifts like VerticalStrike, then returns to the DemonKing socket with spin VFX.");
        definition.PreferEgoSword();
        definition.AddGroup("Timing", "recallSpeedMultiplier", "timeoutSeconds", "recoverEndPoseHoldSeconds");
        definition.AddGroup("Animation", "recoverAnimation", "recoverCompleteAnimation");
        definition.AddGroup("SFX / Shake", "recallStartSound", "recallCompleteSound");
        definition.AddMapping("Recover Body Clip", bodyPropertyPath: "recoverAnimation");
        definition.AddMapping("Recover Complete Body", bodyPropertyPath: "recoverCompleteAnimation");
        definition.AddMapping(
            "EgoSword Recall Spin VFX",
            vfxPropertyPath: "swordSpinVfx",
            source: DemonKingPatternPreviewMappingSource.EgoSwordActor);
        float time = 0f;
        float recallDuration = Float(serializedObject, "timeoutSeconds", 2.5f);
        float recallLiftSeconds = EgoSwordFloat(selectedEgoSword, "recallLiftSeconds", 0.16f);
        float recallLiftHoldSeconds = EgoSwordFloat(selectedEgoSword, "recallLiftHoldSeconds", 0.18f);
        float recallReturnMinimumSeconds = EgoSwordFloat(selectedEgoSword, "recallReturnMinimumSeconds", 0.35f);
        float returnDuration = Mathf.Max(
            recallReturnMinimumSeconds,
            recallDuration - recallLiftSeconds - recallLiftHoldSeconds);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Lift", "Lift sword before return", recallLiftSeconds)
            .WithBody(BodyName(serializedObject, "recoverAnimation", "DarkLord_Hand_SwordRecover"), 0, "recoverAnimation")
            .WithPolicy("First frame remains held while EgoSword rises before flying back; spin starts on return."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Hold", "Hover before return", recallLiftHoldSeconds)
            .WithBody(BodyName(serializedObject, "recoverAnimation", "DarkLord_Hand_SwordRecover"), 0, "recoverAnimation")
            .WithPolicy("Lifted sword waits briefly in the air before return movement starts."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Return", "Fly to return socket", returnDuration)
            .WithBody(BodyName(serializedObject, "recoverAnimation", "DarkLord_Hand_SwordRecover"), 0, "recoverAnimation")
            .WithVfx(
                EgoSwordVfxName(selectedEgoSword, "swordSpinVfx", "SwordSpin4FrameVfx"),
                DemonKingVfxSocketId.SwordThrowEffectOrigin,
                prefabReference: EgoSwordVfxPrefab(selectedEgoSword, "swordSpinVfx"))
            .WithPolicy("EgoSword returns to SwordThrowReturnOrigin while spin follows position only."));
        definition.AddCue(DemonKingPatternPreviewCue.EgoSword(
            0f,
            recallLiftSeconds + recallLiftHoldSeconds + returnDuration,
            DemonKingEgoSwordPreviewMode.RecallSpin));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Complete", "Recover final frame", Float(serializedObject, "recoverEndPoseHoldSeconds", 0.16f))
            .WithBody(BodyName(serializedObject, "recoverCompleteAnimation", "DarkLord_Hand_SwordRecover"), BodyFrame(serializedObject, "recoverCompleteAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "recoverCompleteAnimation")
            .WithPolicy("Final frame shows the sword returned before sword idle recovery."));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildWallBounceRush(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "WallBounceRush",
            "HP50 set piece after sword throw: hand-only warning, linear rushes with endpoint pause, charge loop/disappear VFX, then final jump landing.");
        definition.AddGroup("Rush", "wallBounceCount", "warningSeconds", "retreatSeconds", "fallbackRushDistance", "minimumVisibleRushDistance", "minimumRushSeconds", "shortRushRetargetMaxAngle", "shortRushRetargetStepDegrees", "rushSpeedMultiplier", "chargeDisappearStartProgress", "chargeVfxFlipX", "rushEndPoseHoldSeconds");
        definition.AddGroup("Hit", "hitWidth", "damage", "knockback");
        definition.AddGroup("Final Jump", "finalJumpSeconds", "finalImpactDiameter", "finalImpactDamage", "finalJumpArcHeight", "finalJumpMotionProfile", "finalLandingPoseHoldSeconds", "finalLandingFrameSwitchRatio");
        definition.AddGroup("Animation / VFX", "handRushAnimation", "endpointPauseAnimation", "chargeLoopVfx", "finalJumpTravelAnimation", "finalJumpLandingAnimation", "finalLandingImpactVfx", "finalLandingExplosionVfx");
        definition.AddGroup("SFX / Shake", "rushStartSound", "rushEndpointSound", "finalLandingSound", "rushEndpointCameraShake", "finalLandingCameraShake");
        definition.LegacyNoRuntimeEffectProperties.Add("finalLandingFrameSwitchRatio");
        definition.AddMapping("Hand Rush Body / Charge VFX", bodyPropertyPath: "handRushAnimation", vfxPropertyPath: "chargeLoopVfx");
        definition.AddMapping("Charge Disappear / Endpoint Pause Body", bodyPropertyPath: "endpointPauseAnimation");
        definition.AddMapping("Final Jump Travel Body", bodyPropertyPath: "finalJumpTravelAnimation");
        definition.AddMapping("Final Landing Body / Impact VFX", bodyPropertyPath: "finalJumpLandingAnimation", vfxPropertyPath: "finalLandingImpactVfx");
        definition.AddMapping("Final Landing Extra Explosion VFX", vfxPropertyPath: "finalLandingExplosionVfx");

        float time = 0f;
        float warning = Float(serializedObject, "warningSeconds", 0.6f);
        float hitWidth = Float(serializedObject, "hitWidth", 1.6f);
        float endpointHold = Mathf.Max(0.1f, Float(serializedObject, "rushEndPoseHoldSeconds", 0.1f));
        int count = Mathf.Clamp(Int(serializedObject, "wallBounceCount", 4), 1, 10);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Warning", "Retreat and rush warning", warning + Float(serializedObject, "retreatSeconds", 0.16f))
            .WithBody(BodyName(serializedObject, "handRushAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "handRushAnimation", 0), "handRushAnimation")
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Rush warning", ForwardLineCenter, new Vector2(5f, hitWidth), LeftRotationDeg))
            .WithPolicy("Charge is hand-only because WallBounceRush is selected after ThrowEgoSword drops the sword."));
        for (int i = 0; i < count; i++)
        {
            AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Rush", $"Wall rush {i + 1}", 0.2f)
                .WithBody(BodyName(serializedObject, "handRushAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "handRushAnimation", 0), "handRushAnimation")
                .WithVfx(
                    VfxName(serializedObject, "chargeLoopVfx", "DemonChargeEffectVfx"),
                    VfxSocket(serializedObject, "chargeLoopVfx", DemonKingVfxSocketId.ChargeLoop),
                    VfxFallbackOffset(serializedObject, "chargeLoopVfx", Vector2.zero),
                    VfxRotation(serializedObject, "chargeLoopVfx"),
                    VfxScale(serializedObject, "chargeLoopVfx"),
                    VfxPrefab(serializedObject, "chargeLoopVfx"))
                .WithShape(DemonKingPatternPreviewShape.Rectangle("Rush hit", ForwardLineCenter, new Vector2(5f, hitWidth), LeftRotationDeg))
                .WithPolicy("Linear rush movement uses no easing; Charge VFX follows in Loop and switches to Disappear at chargeDisappearStartProgress."));
            AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Endpoint", $"Endpoint pause {i + 1}", endpointHold)
                .WithBody(BodyName(serializedObject, "endpointPauseAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "endpointPauseAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "endpointPauseAnimation")
                .WithVfx(
                    VfxName(serializedObject, "chargeLoopVfx", "DemonChargeEffectVfx"),
                    VfxSocket(serializedObject, "chargeLoopVfx", DemonKingVfxSocketId.ChargeLoop),
                    VfxFallbackOffset(serializedObject, "chargeLoopVfx", Vector2.zero),
                    VfxRotation(serializedObject, "chargeLoopVfx"),
                    VfxScale(serializedObject, "chargeLoopVfx"),
                    VfxPrefab(serializedObject, "chargeLoopVfx"))
                .WithPolicy("Endpoint keeps the body pause, endpoint sound/shake, and at least 0.1s pause after the earlier Charge Disappear transition."));
        }

        float finalImpactDiameter = Float(serializedObject, "finalImpactDiameter", 3.4f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Final Jump", "Final jump travel", Float(serializedObject, "finalJumpSeconds", 0.5f))
            .WithBody(BodyName(serializedObject, "finalJumpTravelAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "finalJumpTravelAnimation", 0), "finalJumpTravelAnimation")
            .WithShape(DemonKingPatternPreviewShape.Circle("Final landing warning", Vector2.zero, finalImpactDiameter))
            .WithPolicy("Final jump uses finalJumpMotionProfile."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Commit", "Final landing impact", Float(serializedObject, "finalLandingPoseHoldSeconds", 0.14f))
            .WithBody(BodyName(serializedObject, "finalJumpLandingAnimation", "DarkLord_Hand_JumpAttack"), BodyFrame(serializedObject, "finalJumpLandingAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "finalJumpLandingAnimation")
            .WithVfx(
                VfxName(serializedObject, "finalLandingImpactVfx", "DemonKingImpactVfx"),
                VfxSocket(serializedObject, "finalLandingImpactVfx", DemonKingVfxSocketId.FootLandingImpact),
                VfxFallbackOffset(serializedObject, "finalLandingImpactVfx", Vector2.zero),
                VfxRotation(serializedObject, "finalLandingImpactVfx"),
                VfxScale(serializedObject, "finalLandingImpactVfx"),
                VfxPrefab(serializedObject, "finalLandingImpactVfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("Final landing impact", Vector2.zero, finalImpactDiameter)));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildGroggyRecoverCounter(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "GroggyRecoverCounter",
            "Groggy pose is held during dim/eye flash, then Sword or Hand GroggyCounter commits to different VFX.");
        definition.AddGroup("Timing", "attackDelaySeconds", "dimFadeOutRatio", "eyeFlashHoldRatio", "counterEndPoseHoldSeconds", "minimumImpactPoseHoldSeconds");
        definition.AddGroup("Hit", "explosionDiameter", "damage", "knockback");
        definition.AddGroup("Sword Visual", "swordVisual");
        definition.AddGroup("Hand Visual", "handVisual");
        definition.AddGroup("Legacy Eye Flash Fallback", "dimTargetAlpha", "eyeFlashLocalOffset", "eyeFlashSize");
        definition.AddGroup("SFX / Shake", "warningPingSound", "counterImpactSound", "counterImpactCameraShake");
        definition.AddMapping("Sword Groggy Pose", bodyPropertyPath: "swordVisual.groggyPoseAnimation");
        definition.AddMapping("Sword Counter / VFX", bodyPropertyPath: "swordVisual.counterAnimation", vfxPropertyPath: "swordVisual.counterImpactVfx");
        definition.AddMapping("Hand Groggy Pose", bodyPropertyPath: "handVisual.groggyPoseAnimation");
        definition.AddMapping("Hand Counter / VFX", bodyPropertyPath: "handVisual.counterAnimation", vfxPropertyPath: "handVisual.counterImpactVfx");

        float attackDelay = Float(serializedObject, "attackDelaySeconds", 0.4f);
        float dim = attackDelay * Float(serializedObject, "dimFadeOutRatio", 0.45f);
        float eye = attackDelay * Float(serializedObject, "eyeFlashHoldRatio", 0.2f);
        float impactDiameter = Float(serializedObject, "explosionDiameter", 5.4f);
        float hold = Mathf.Max(
            Float(serializedObject, "counterEndPoseHoldSeconds", 0.12f),
            Float(serializedObject, "minimumImpactPoseHoldSeconds", 0.5f));
        float time = 0f;
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Groggy", "Groggy dim hold", dim)
            .WithBody(BodyName(serializedObject, "handVisual.groggyPoseAnimation", "DarkLord_Hand_Groggy"), BodyFrame(serializedObject, "handVisual.groggyPoseAnimation", 0), "handVisual.groggyPoseAnimation")
            .WithPolicy("Current Sword/Hand Groggy pose is held; it should not oscillate."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Eye", "Eye flash hold", eye)
            .WithBody(BodyName(serializedObject, "handVisual.groggyPoseAnimation", "DarkLord_Hand_Groggy"), BodyFrame(serializedObject, "handVisual.groggyPoseAnimation", 0), "handVisual.groggyPoseAnimation")
            .WithVfx("DemonKingEyeLightVfx", DemonKingVfxSocketId.EyeFlash, new Vector2(0f, 0.75f))
            .WithPolicy("Eye flash VFX and warning ping happen while Groggy pose remains held."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Sword Branch", "Sword GroggyCounter impact", hold)
            .WithBody(BodyName(serializedObject, "swordVisual.counterAnimation", "DarkLord_Sword_GroggyCounter"), BodyFrame(serializedObject, "swordVisual.counterAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "swordVisual.counterAnimation")
            .WithVfx(
                VfxName(serializedObject, "swordVisual.counterImpactVfx", "DarkLordGroggyReleaseVfx"),
                VfxSocket(serializedObject, "swordVisual.counterImpactVfx", DemonKingVfxSocketId.SwordCounterOrigin),
                VfxFallbackOffset(serializedObject, "swordVisual.counterImpactVfx", Vector2.zero),
                VfxRotation(serializedObject, "swordVisual.counterImpactVfx"),
                VfxScale(serializedObject, "swordVisual.counterImpactVfx"),
                VfxPrefab(serializedObject, "swordVisual.counterImpactVfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("Sword counter", Vector2.zero, impactDiameter))
            .WithPolicy("Sword-held counter keeps the sword release VFX."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Hand Branch", "Hand GroggyCounter impact", hold)
            .WithBody(BodyName(serializedObject, "handVisual.counterAnimation", "DarkLord_Hand_GroggyCounter"), BodyFrame(serializedObject, "handVisual.counterAnimation", DemonKingPatternPreviewPhase.LastBodyFrameIndex), "handVisual.counterAnimation")
            .WithVfx(
                VfxName(serializedObject, "handVisual.counterImpactVfx", "DemonKingImpactVfx"),
                VfxSocket(serializedObject, "handVisual.counterImpactVfx", DemonKingVfxSocketId.HandCounterImpact),
                VfxFallbackOffset(serializedObject, "handVisual.counterImpactVfx", Vector2.zero),
                VfxRotation(serializedObject, "handVisual.counterImpactVfx"),
                VfxScale(serializedObject, "handVisual.counterImpactVfx"),
                VfxPrefab(serializedObject, "handVisual.counterImpactVfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("Hand counter", Vector2.zero, impactDiameter))
            .WithPolicy("Hand-state counter uses DemonKingImpact, then restores combat idle."));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildFinalDesperation(SerializedObject serializedObject)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "FinalDesperation",
            "10% pose, opening knockback, repeated DarkLordExplosion2 bombs, and alternating laser warning/fire rows.");
        definition.AddGroup("Opening", "moveToCenterSeconds", "openingKnockbackDiameter", "openingKnockbackDamage", "openingKnockback");
        definition.AddGroup("Animation", "openingMoveAnimation", "finalPoseAnimation");
        definition.AddGroup("Bombs", "bombIntervalSeconds", "bombWarningSeconds", "bombDiameter", "bombDamage", "bombOffsetRange", "bombExplosionVfx");
        definition.AddGroup("Laser", "laserWarningSeconds", "laserAttackSeconds", "laserWidth", "laserVfxRayOriginOffset", "fallbackLaserLength", "laserDamage", "laserVfxPrefabOverride", "laserVfxResourcePath");
        definition.AddGroup("SFX / Shake", "startSound", "bombExplosionSound", "laserWarningSound", "laserFireSound", "openingCameraShake", "bombExplosionCameraShake");
        definition.AddMapping("Opening Move Body", bodyPropertyPath: "openingMoveAnimation");
        definition.AddMapping("10% Final Pose", bodyPropertyPath: "finalPoseAnimation");
        definition.AddMapping("Bomb Explosion VFX", vfxPropertyPath: "bombExplosionVfx");
        definition.AddMapping("Laser VFX", objectVfxPropertyPath: "laserVfxPrefabOverride", resourcePathPropertyPath: "laserVfxResourcePath");

        float time = 0f;
        float bombDiameter = Float(serializedObject, "bombDiameter", 2.1f);
        float laserWidth = Float(serializedObject, "laserWidth", 0.75f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Opening", "Center move", Float(serializedObject, "moveToCenterSeconds", 0.6f))
            .WithBody(BodyName(serializedObject, "openingMoveAnimation", string.Empty), BodyFrame(serializedObject, "openingMoveAnimation", -1), "openingMoveAnimation")
            .WithShape(DemonKingPatternPreviewShape.Circle("Opening knockback", Vector2.zero, Float(serializedObject, "openingKnockbackDiameter", 40f)))
            .WithPolicy("Optional opening move body cue; DarkLord_10Percent starts after center settlement."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Opening", "10% pose commit", 0.12f)
            .WithBody(BodyName(serializedObject, "finalPoseAnimation", "DarkLord_10Percent"), BodyFrame(serializedObject, "finalPoseAnimation", 0), "finalPoseAnimation")
            .WithPolicy("Final pose, start sound, opening shake, and health clamp release commit together."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Bomb", "10% bomb explosion", Float(serializedObject, "bombWarningSeconds", 0.4f))
            .WithBody(BodyName(serializedObject, "finalPoseAnimation", "DarkLord_10Percent"), BodyFrame(serializedObject, "finalPoseAnimation", 0), "finalPoseAnimation")
            .WithVfx(VfxPrefab(serializedObject, "bombExplosionVfx"), VfxName(serializedObject, "bombExplosionVfx", "DarkLordExplosion2Vfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("Bomb", new Vector2(-1.5f, 0.5f), bombDiameter))
            .WithPolicy("Bombs use DarkLordExplosion2 and their own explosion sound/shake."));
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Laser Warning", "Laser warning pair", Float(serializedObject, "laserWarningSeconds", 1f))
            .WithBody(BodyName(serializedObject, "finalPoseAnimation", "DarkLord_10Percent"), BodyFrame(serializedObject, "finalPoseAnimation", 0), "finalPoseAnimation")
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Laser warning", ForwardLineCenter, new Vector2(6f, laserWidth), LeftRotationDeg)));
        float laserFireStart = time;
        float laserAttackSeconds = Float(serializedObject, "laserAttackSeconds", 1f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Laser Fire", "Laser fire pair", laserAttackSeconds)
            .WithBody(BodyName(serializedObject, "finalPoseAnimation", "DarkLord_10Percent"), BodyFrame(serializedObject, "finalPoseAnimation", 0), "finalPoseAnimation")
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Laser damage", ForwardLineCenter, new Vector2(6f, laserWidth), LeftRotationDeg))
            .WithPolicy("Animated EgoSword laser VFX owns active damage where available."));
        string laserResourcePath = String(serializedObject, "laserVfxResourcePath", "DemonKing/DemonKingEgoLaserVfx");
        GameObject laserPrefabOverride = ObjectPrefab(serializedObject, "laserVfxPrefabOverride");
        definition.AddCue(DemonKingPatternPreviewCue.Vfx(
            laserFireStart,
            laserAttackSeconds,
            laserPrefabOverride != null ? laserPrefabOverride.name : "DemonKingEgoLaserVfx",
            laserResourcePath,
            Vector2.zero,
            Vector3.one,
            0f,
            DemonKingPatternPreviewShape.Rectangle("Laser damage", ForwardLineCenter, new Vector2(6f, laserWidth), LeftRotationDeg),
            laserPrefabOverride));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildEgoSwordVerticalStrike(EgoSwordActor selectedEgoSword)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "EgoSwordVerticalStrike",
            "Dropped EgoSword subpattern. Detailed movement values remain on EgoSwordActor, not this wrapper asset.");
        definition.PreferEgoSword();
        definition.AddMapping(
            "Vertical Attack VFX",
            vfxPropertyPath: "verticalAttackVfx",
            source: DemonKingPatternPreviewMappingSource.EgoSwordActor);
        definition.AddMapping(
            "Vertical Impact VFX",
            vfxPropertyPath: "verticalImpactVfx",
            source: DemonKingPatternPreviewMappingSource.EgoSwordActor);
        float time = 0f;
        float trackSeconds = EgoSwordFloat(selectedEgoSword, "verticalTrackSeconds", 1.5f);
        float liftSeconds = EgoSwordFloat(selectedEgoSword, "verticalStrikeLiftSeconds", 0.1f);
        float liftHeight = EgoSwordFloat(selectedEgoSword, "verticalStrikeLiftHeight", 0.45f);
        float dropSeconds = EgoSwordFloat(selectedEgoSword, "verticalStrikeDropSeconds", 0.16f);
        float diameter = EgoSwordFloat(selectedEgoSword, "verticalStrikeDiameter", 2.3f);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Track", "Sword hover tracking", trackSeconds)
            .WithPolicy("Tune travel/hover/planting offsets from the EgoSword tab."));
        definition.AddCue(DemonKingPatternPreviewCue.EgoSword(
            0f,
            trackSeconds,
            DemonKingEgoSwordPreviewMode.VerticalTrack));
        float commitStart = time;
        float commitDuration = Mathf.Max(0.01f, (liftHeight > 0f ? liftSeconds : 0f) + dropSeconds);
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Commit", "Vertical strike impact", commitDuration)
            .WithVfx(
                EgoSwordVfxName(selectedEgoSword, "verticalAttackVfx", "EgoSwordAttackVfx"),
                DemonKingVfxSocketId.SwordThrowEffectOrigin,
                prefabReference: EgoSwordVfxPrefab(selectedEgoSword, "verticalAttackVfx"))
            .WithShape(DemonKingPatternPreviewShape.Circle("EgoSword impact", Vector2.zero, diameter)));
        definition.AddCue(DemonKingPatternPreviewCue.EgoSword(
            commitStart,
            commitDuration,
            DemonKingEgoSwordPreviewMode.VerticalCommit));
        return definition;
    }

    private static DemonKingPatternPreviewDefinition BuildEgoSwordCrossLaser(EgoSwordActor selectedEgoSword)
    {
        DemonKingPatternPreviewDefinition definition = new(
            "EgoSwordCrossLaser",
            "Dropped EgoSword laser subpattern. Laser cadence and origin offsets are tuned on EgoSwordActor.");
        definition.PreferEgoSword();
        definition.AddMapping(
            "Sword Spin VFX",
            vfxPropertyPath: "swordSpinVfx",
            source: DemonKingPatternPreviewMappingSource.EgoSwordActor);
        definition.AddMapping(
            "Laser VFX",
            objectVfxPropertyPath: "laserVfxPrefab",
            resourcePathPropertyPath: "laserVfxResourcePath",
            source: DemonKingPatternPreviewMappingSource.EgoSwordActor);
        float time = 0f;
        float warningSeconds = EgoSwordFloat(selectedEgoSword, "laserWarningSeconds", 1f)
            * Mathf.Clamp(EgoSwordFloat(selectedEgoSword, "laserTempoMultiplier", 0.75f), 0.1f, 1f);
        float attackSeconds = EgoSwordFloat(selectedEgoSword, "laserAttackDurationSeconds", 1f)
            * Mathf.Clamp(EgoSwordFloat(selectedEgoSword, "laserTempoMultiplier", 0.75f), 0.1f, 1f);
        float laserWidth = EgoSwordFloat(selectedEgoSword, "laserWidth", 0.75f);
        float laserLength = Mathf.Min(EgoSwordFloat(selectedEgoSword, "fallbackMapLaserLength", 40f), 8f);
        string laserResourcePath = EgoSwordString(selectedEgoSword, "laserVfxResourcePath", "DemonKing/DemonKingEgoLaserVfx");
        GameObject laserPrefab = EgoSwordObjectPrefab(selectedEgoSword, "laserVfxPrefab");
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Warning", "Cross laser warning", warningSeconds)
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Laser warning", ForwardLineCenter, new Vector2(laserLength, laserWidth), LeftRotationDeg)));
        definition.AddCue(DemonKingPatternPreviewCue.EgoSword(
            0f,
            warningSeconds,
            DemonKingEgoSwordPreviewMode.CrossLaserWarning));
        float fireStart = time;
        AddPhase(definition, ref time, new DemonKingPatternPreviewPhase("Fire", "Cross laser fire", attackSeconds)
            .WithShape(DemonKingPatternPreviewShape.Rectangle("Laser damage", ForwardLineCenter, new Vector2(laserLength, laserWidth), LeftRotationDeg)));
        definition.AddCue(DemonKingPatternPreviewCue.EgoSword(
            fireStart,
            attackSeconds,
            DemonKingEgoSwordPreviewMode.CrossLaserFire));
        definition.AddCue(DemonKingPatternPreviewCue.Vfx(
            fireStart,
            attackSeconds,
            laserPrefab != null ? laserPrefab.name : "DemonKingEgoLaserVfx",
            laserResourcePath,
            Vector2.zero,
            Vector3.one,
            0f,
            DemonKingPatternPreviewShape.Rectangle("Laser damage", ForwardLineCenter, new Vector2(laserLength, laserWidth), LeftRotationDeg),
            laserPrefab));
        definition.AddCue(DemonKingPatternPreviewCue.Vfx(
            fireStart,
            attackSeconds,
            laserPrefab != null ? laserPrefab.name : "DemonKingEgoLaserVfx",
            laserResourcePath,
            Vector2.zero,
            Vector3.one,
            90f,
            DemonKingPatternPreviewShape.Rectangle("Laser damage vertical", Vector2.zero, new Vector2(laserLength, laserWidth), 90f),
            laserPrefab));
        return definition;
    }

    private void AddGroup(string title, params string[] propertyNames)
    {
        FieldGroups.Add(new DemonKingPatternPreviewFieldGroup(title, propertyNames));
    }

    private void AddMapping(
        string label,
        string bodyPropertyPath = null,
        string vfxPropertyPath = null,
        string objectVfxPropertyPath = null,
        string resourcePathPropertyPath = null,
        DemonKingPatternPreviewMappingSource source = DemonKingPatternPreviewMappingSource.PatternAsset)
    {
        MappingRows.Add(new DemonKingPatternPreviewMappingRow(
            label,
            source,
            bodyPropertyPath,
            vfxPropertyPath,
            objectVfxPropertyPath,
            resourcePathPropertyPath));

        if (source != DemonKingPatternPreviewMappingSource.PatternAsset)
            return;

        AddMappingPropertyPath(bodyPropertyPath);
        AddMappingPropertyPath(vfxPropertyPath);
        AddMappingPropertyPath(objectVfxPropertyPath);
        AddMappingPropertyPath(resourcePathPropertyPath);
    }

    private void AddMappingPropertyPath(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return;

        MappingPropertyPaths.Add(propertyPath);
        int nestedSeparator = propertyPath.IndexOf('.');
        if (nestedSeparator > 0)
            MappingPropertyPaths.Add(propertyPath.Substring(0, nestedSeparator));
    }

    public int ResolvePhaseIndex(float timelineSeconds)
    {
        if (Phases.Count == 0)
            return -1;

        for (int i = 0; i < Phases.Count; i++)
        {
            DemonKingPatternPreviewPhase phase = Phases[i];
            if (timelineSeconds >= phase.StartSeconds && timelineSeconds < phase.EndSeconds)
                return i;
        }

        return Phases.Count - 1;
    }

    private static void AddPhase(
        DemonKingPatternPreviewDefinition definition,
        ref float startSeconds,
        DemonKingPatternPreviewPhase phase)
    {
        phase.StartSeconds = startSeconds;
        definition.Phases.Add(phase);
        if (!string.IsNullOrEmpty(phase.BodyClipName))
            definition.AddCue(DemonKingPatternPreviewCue.Body(phase));
        if (!string.IsNullOrEmpty(phase.VfxPrefabName))
            definition.AddCue(DemonKingPatternPreviewCue.Vfx(phase));
        if (phase.Shape != null)
            definition.AddCue(DemonKingPatternPreviewCue.ShapeCue(phase));
        startSeconds += phase.DurationSeconds;
    }

    private static bool Matches(ScriptableObject asset, string typeName)
    {
        Type type = asset.GetType();
        while (type != null)
        {
            if (type.Name == typeName)
                return true;

            type = type.BaseType;
        }

        return false;
    }

    private static int Int(SerializedObject serializedObject, string propertyName, int fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return fallback;

        return property.propertyType == SerializedPropertyType.Integer ? property.intValue : fallback;
    }

    private static float Float(SerializedObject serializedObject, string propertyName, float fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return fallback;

        return property.propertyType switch
        {
            SerializedPropertyType.Float => property.floatValue,
            SerializedPropertyType.Integer => property.intValue,
            _ => fallback
        };
    }

    private static float EgoSwordFloat(EgoSwordActor egoSword, string propertyName, float fallback)
    {
        if (egoSword == null)
            return fallback;

        SerializedObject serializedSword = new(egoSword);
        SerializedProperty property = serializedSword.FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Float
            ? property.floatValue
            : fallback;
    }

    private static string EgoSwordString(EgoSwordActor egoSword, string propertyName, string fallback)
    {
        if (egoSword == null)
            return fallback;

        SerializedObject serializedSword = new(egoSword);
        SerializedProperty property = serializedSword.FindProperty(propertyName);
        return !string.IsNullOrWhiteSpace(property?.stringValue)
            ? property.stringValue
            : fallback;
    }

    private static string String(SerializedObject serializedObject, string propertyName, string fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return !string.IsNullOrWhiteSpace(property?.stringValue)
            ? property.stringValue
            : fallback;
    }

    private static string EgoSwordVfxName(EgoSwordActor egoSword, string propertyPath, string fallback)
    {
        if (egoSword == null)
            return fallback;

        SerializedObject serializedSword = new(egoSword);
        return VfxName(serializedSword, propertyPath, fallback);
    }

    private static GameObject EgoSwordVfxPrefab(EgoSwordActor egoSword, string propertyPath)
    {
        if (egoSword == null)
            return null;

        SerializedObject serializedSword = new(egoSword);
        return VfxPrefab(serializedSword, propertyPath);
    }

    private static GameObject EgoSwordObjectPrefab(EgoSwordActor egoSword, string propertyName)
    {
        if (egoSword == null)
            return null;

        SerializedObject serializedSword = new(egoSword);
        SerializedProperty property = serializedSword.FindProperty(propertyName);
        return property?.objectReferenceValue switch
        {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            _ => null
        };
    }

    private static string BodyName(SerializedObject serializedObject, string propertyPath, string fallback)
    {
        SerializedProperty clipProperty = serializedObject.FindProperty($"{propertyPath}.clip");
        if (clipProperty?.objectReferenceValue is AnimationClip clip && clip != null)
            return clip.name;

        SerializedProperty fallbackProperty = serializedObject.FindProperty($"{propertyPath}.fallbackStateName");
        return !string.IsNullOrWhiteSpace(fallbackProperty?.stringValue)
            ? fallbackProperty.stringValue
            : fallback;
    }

    private static int BodyFrame(SerializedObject serializedObject, string propertyPath, int fallback)
    {
        SerializedProperty sampleModeProperty = serializedObject.FindProperty($"{propertyPath}.sampleMode");
        if (sampleModeProperty != null
            && sampleModeProperty.enumValueIndex == (int)DemonKingBodyFrameSampleMode.HoldLastFrame)
        {
            return DemonKingPatternPreviewPhase.LastBodyFrameIndex;
        }

        SerializedProperty frameProperty = serializedObject.FindProperty($"{propertyPath}.frameIndex");
        return frameProperty != null && frameProperty.propertyType == SerializedPropertyType.Integer
            ? frameProperty.intValue
            : fallback;
    }

    private static string VfxName(SerializedObject serializedObject, string propertyPath, string fallback)
    {
        SerializedProperty prefabProperty = serializedObject.FindProperty($"{propertyPath}.prefabOverride");
        if (prefabProperty?.objectReferenceValue is GameObject prefab && prefab != null)
            return prefab.name;

        SerializedProperty fallbackKindProperty = serializedObject.FindProperty($"{propertyPath}.fallbackKind");
        if (fallbackKindProperty == null)
            return fallback;

        return (DemonKingBuiltInVfxKind)fallbackKindProperty.enumValueIndex switch
        {
            DemonKingBuiltInVfxKind.Explosion => "DemonKingExplosionVfx",
            DemonKingBuiltInVfxKind.DarkLordExplosion2 => "DarkLordExplosion2Vfx",
            DemonKingBuiltInVfxKind.Impact => "DemonKingImpactVfx",
            DemonKingBuiltInVfxKind.GroggyRelease => "DarkLordGroggyReleaseVfx",
            DemonKingBuiltInVfxKind.EyeFlash => "DemonKingEyeLightVfx",
            DemonKingBuiltInVfxKind.Stab => "DemonKingStabVfx",
            DemonKingBuiltInVfxKind.Slash => "DarkLordSlashVfx",
            DemonKingBuiltInVfxKind.ChargeLoop => "DemonChargeEffectVfx",
            DemonKingBuiltInVfxKind.ChargeDisappear => "DemonChargeEffectVfx",
            DemonKingBuiltInVfxKind.SwordSpin => "SwordSpin4FrameVfx",
            DemonKingBuiltInVfxKind.EgoSwordAttack => "EgoSwordAttackVfx",
            DemonKingBuiltInVfxKind.HomingStock => "HomingMagicBaltStockVfx",
            DemonKingBuiltInVfxKind.HomingProjectile => "HomingMagicBaltProjectileVfx",
            _ => fallback
        };
    }

    private static GameObject VfxPrefab(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty prefabProperty = serializedObject.FindProperty($"{propertyPath}.prefabOverride");
        return prefabProperty?.objectReferenceValue as GameObject;
    }

    private static string ObjectPrefabName(SerializedObject serializedObject, string propertyPath, string fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        return property?.objectReferenceValue is GameObject prefab && prefab != null
            ? prefab.name
            : fallback;
    }

    private static GameObject ObjectPrefab(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        return property?.objectReferenceValue as GameObject;
    }

    private static DemonKingVfxSocketId VfxSocket(SerializedObject serializedObject, string propertyPath, DemonKingVfxSocketId fallback)
    {
        SerializedProperty socketProperty = serializedObject.FindProperty($"{propertyPath}.socketId");
        return socketProperty != null
            ? (DemonKingVfxSocketId)socketProperty.enumValueIndex
            : fallback;
    }

    private static Vector2 VfxFallbackOffset(SerializedObject serializedObject, string propertyPath, Vector2 fallback)
    {
        SerializedProperty offsetProperty = serializedObject.FindProperty($"{propertyPath}.fallbackLeftOffset");
        return offsetProperty != null && offsetProperty.propertyType == SerializedPropertyType.Vector2
            ? offsetProperty.vector2Value
            : fallback;
    }

    private static Vector3 VfxScale(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty scaleProperty = serializedObject.FindProperty($"{propertyPath}.scale");
        if (scaleProperty == null || scaleProperty.propertyType != SerializedPropertyType.Vector3)
            return Vector3.one;

        Vector3 scale = scaleProperty.vector3Value;
        return scale == Vector3.zero ? Vector3.one : scale;
    }

    private static float VfxRotation(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty rotationProperty = serializedObject.FindProperty($"{propertyPath}.rotationOffsetDeg");
        return rotationProperty != null && rotationProperty.propertyType == SerializedPropertyType.Float
            ? rotationProperty.floatValue
            : 0f;
    }
}
