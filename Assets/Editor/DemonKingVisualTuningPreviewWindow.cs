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
    private Vector2 serializedScroll;

    private Camera previewCamera;
    private RenderTexture previewTexture;
    private GameObject previewRoot;
    private GameObject previewInstance;
    private SpriteRenderer bodyPreviewRenderer;
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
        minSize = new Vector2(520f, 520f);
        RefreshAssetLists();
        EnsureDefaultSelections();
        EnsurePreviewCamera();
        EditorApplication.update += TickPreview;
        RestartPreview();
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
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
                selectedEgoSword = sword;

            DemonKingVfxSocketMap socketMap = gameObject.GetComponentInChildren<DemonKingVfxSocketMap>(true);
            if (socketMap != null)
                selectedSocketMap = socketMap;
        }
        else if (active is EgoSwordActor sword)
        {
            selectedEgoSword = sword;
        }
        else if (active is DemonKingVfxSocketMap socketMap)
        {
            selectedSocketMap = socketMap;
        }

        Repaint();
    }

    private void OnGUI()
    {
        DrawHeader();

        Rect previewRect = GUILayoutUtility.GetRect(
            DefaultPreviewTextureSize,
            DefaultPreviewTextureSize,
            GUILayout.ExpandWidth(true),
            GUILayout.Height(Mathf.Min(position.height * 0.42f, 360f)));
        lastPreviewRect = previewRect;
        EnsurePreviewTexture(previewRect);
        RenderPreview();
        DrawPreviewTexture(previewRect);

        selectedTab = (ToolTab)GUILayout.Toolbar((int)selectedTab, Enum.GetNames(typeof(ToolTab)));
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
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
            GUILayout.Label("Preview", GUILayout.Width(48f));
            EditorGUI.BeginChangeCheck();
            previewSubject = (PreviewSubject)EditorGUILayout.EnumPopup(previewSubject, GUILayout.Width(155f));
            if (EditorGUI.EndChangeCheck())
                RestartPreview();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(previewPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                previewPlaying = !previewPlaying;

            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(66f)))
                RestartPreview();
        }
    }

    private void DrawPreviewTexture(Rect previewRect)
    {
        if (previewTexture != null)
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
        else
            EditorGUI.HelpBox(previewRect, "Preview texture is not available.", MessageType.Warning);

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
        EditorGUILayout.LabelField("Pattern AbilityLogic Asset", EditorStyles.boldLabel);
        DrawAbilityLogicSelector();
        if (selectedAbilityLogicAsset == null)
        {
            EditorGUILayout.HelpBox("Select an AL_DemonKing_* asset.", MessageType.Info);
            return;
        }

        SerializedObject serializedAsset = new(selectedAbilityLogicAsset);
        DrawSerializedObjectEditor(serializedAsset, "Apply Pattern Asset", false);
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
        EditorGUI.BeginChangeCheck();
        selectedAbilityLogicAsset = (ScriptableObject)EditorGUILayout.ObjectField(
            "Asset",
            selectedAbilityLogicAsset,
            typeof(ScriptableObject),
            allowSceneObjects: false);
        if (EditorGUI.EndChangeCheck())
            serializedScroll = Vector2.zero;

        string[] names = abilityLogicAssets.Select(asset => asset != null ? asset.name : "(null)").ToArray();
        int currentIndex = Mathf.Max(0, abilityLogicAssets.IndexOf(selectedAbilityLogicAsset));
        if (names.Length > 0)
        {
            int nextIndex = EditorGUILayout.Popup("Known Assets", currentIndex, names);
            ScriptableObject nextAsset = abilityLogicAssets.ElementAtOrDefault(nextIndex);
            if (nextAsset != null && nextAsset != selectedAbilityLogicAsset)
            {
                selectedAbilityLogicAsset = nextAsset;
                serializedScroll = Vector2.zero;
            }
        }
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
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = lastEditorTime > 0d ? Mathf.Min((float)(now - lastEditorTime), 0.05f) : 0f;
        lastEditorTime = now;

        if (previewPlaying)
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

        RenderPreview();
        Repaint();
    }

    private void RestartPreview()
    {
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

        previewInstance = body != null ? body : vfx;
    }

    private GameObject CreateVfxPreviewObject(Vector3 localPosition, Vector3 localScaleMultiplier, float localRotationDeg)
    {
        if (selectedVfxPrefab == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(selectedVfxPrefab) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(selectedVfxPrefab);
        if (instance == null)
            return null;

        instance.name = $"{selectedVfxPrefab.name}_Preview";
        instance.transform.SetParent(previewRoot.transform, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(0f, 0f, localRotationDeg);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, localScaleMultiplier);
        SetHideFlagsRecursive(instance.transform, HideFlags.HideAndDontSave);
        instance.SetActive(true);

        CachePreviewPlaybackComponents();
        StartPreviewPlaybackComponents();
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
        Sprite sprite = ResolveSpriteAtTime(clip, previewTime);
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
            || previewSubject == PreviewSubject.Composite && compositeShowVfx)
            DrawHitWindowOverlay(previewRect);
        if (previewSubject == PreviewSubject.EgoSwordOffsets
            || previewSubject == PreviewSubject.Composite && compositeShowEgoSword)
            DrawEgoSwordOverlay(previewRect);
        if (previewSubject == PreviewSubject.SocketMap
            || previewSubject == PreviewSubject.Composite && compositeShowSockets)
            DrawSocketOverlay(previewRect);
        if (previewSubject == PreviewSubject.Composite && compositeShowVfx)
            DrawCompositeVfxSocketOverlay(previewRect);
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

        Vector2 center = WorldToPreviewGui(PreviewOrigin, previewRect);
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

        previewCamera.transform.position = PreviewOrigin + new Vector3(0f, 0f, PreviewDepth);
        previewCamera.orthographicSize = previewCameraSize;
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
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
