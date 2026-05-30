#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class DialogueTextAnimationTunerWindow : EditorWindow
{
    private const float PreviewHeight = 220f;
    private const float PreviewSettingsMinHeight = 160f;
    private const float PreviewSettingsMaxHeight = 280f;
    private const float WindowPadding = 4f;
    private const float SplitterSize = 5f;
    private const float TopMinHeight = 220f;
    private const float PreviewMinWidth = 320f;
    private const float InspectorMinWidth = 320f;
    private const float InspectorMaxWidth = 430f;
    private const float InputPaneDefaultWidth = 320f;
    private const float InputPaneMinWidth = 240f;
    private const float PlaybackPaneMinWidth = 300f;
    private const float TagPresetPaneMinWidth = 220f;
    private const float TagPresetPaneMaxWidth = 300f;
    private const float PreviewCameraPadding = 20f;
    private const float PreviewFallbackWidth = 1720f;
    private const float PreviewFallbackHeight = 250f;
    private const float PreviewTimeMax = 5f;
    private const int PreviewLayer = 30;
    private const string DefaultDialogueRootPrefabPath = "Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab";
    private const string DialogueTextFieldName = "dialogueText";
    private const string PreviewFontGuidPrefKey = "DialogueTextAnimationTuner.PreviewFontGuid";
    private const string OverridePreviewFontPrefKey = "DialogueTextAnimationTuner.OverridePreviewFont";
    private const string ProfilePaneWidthPrefKey = "DialogueTextAnimationTuner.ProfilePaneWidth";
    private const string BottomPaneHeightPrefKey = "DialogueTextAnimationTuner.BottomPaneHeight";
    private const string InputPaneWidthPrefKey = "DialogueTextAnimationTuner.InputPaneWidth";
    private const string TagPaneWidthPrefKey = "DialogueTextAnimationTuner.TagPaneWidth";
    private const string DefaultSampleText =
        "[slowshake][rand_size=95,110]Preview text moves softly[/rand_size][/slowshake].";

    private DialogueTextAnimationProfileSO selectedProfile;
    private TMP_FontAsset previewFont;
    private bool overridePreviewFont;
    private TextMeshProUGUI manualPreviewSource;
    private Editor profileEditor;
    private Camera previewCamera;
    private RenderTexture previewRenderTexture;
    private GameObject previewRoot;
    private RectTransform previewCanvasRect;
    private RectTransform previewContainerRect;
    private TextMeshProUGUI previewText;
    private Vector2 inspectorScrollPosition;
    private Vector2 previewSettingsScrollPosition;
    private int previewTextureWidth;
    private int previewTextureHeight;
    private bool layoutPrefsLoaded;
    private float profilePaneWidth;
    private float bottomPaneHeight;
    private float inputPaneWidth;
    private float tagPresetPaneWidth;
    private string sampleText =
        "취한 말투는 [slowshake][rand_size=95,110]이렇게 흔들린다[/rand_size][/slowshake].";
    private DialogueCameraShakePreset cameraShakePreset = DialogueCameraShakePreset.None;
    private bool isPlaying = true;
    private bool loopPreviewTime = true;
    private bool typewriterLoop;
    private float previewTime;
    private float typewriterTime;
    private float visibleRatio = 1f;
    private double lastEditorTime;

    [MenuItem("Tools/Dialogue/Text Animation Tuner")]
    public static void Open()
    {
        GetWindow<DialogueTextAnimationTunerWindow>("Text Animation Tuner");
    }

    private void OnEnable()
    {
        sampleText = DefaultSampleText;
        selectedProfile = Resources.Load<DialogueTextAnimationProfileSO>(
            DialogueTextAnimationProfileSO.DefaultResourcesPath);
        previewFont = LoadSavedPreviewFont();
        overridePreviewFont = EditorPrefs.GetBool(OverridePreviewFontPrefKey, false);
        LoadLayoutPrefs();
        lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += TickPreview;
        RebuildProfileEditor();
        EnsurePreview();
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
        DestroyProfileEditor();
        DestroyPreview();
    }

    private void OnGUI()
    {
        EnsureLayoutPrefsLoaded();

        Rect contentRect = new Rect(
            WindowPadding,
            WindowPadding,
            Mathf.Max(1f, position.width - WindowPadding * 2f),
            Mathf.Max(1f, position.height - WindowPadding * 2f));

        ClampMainPaneSizes(contentRect);

        Rect topRect = new Rect(
            contentRect.x,
            contentRect.y,
            contentRect.width,
            Mathf.Max(1f, contentRect.height - bottomPaneHeight - SplitterSize));
        Rect horizontalSplitterRect = new Rect(
            contentRect.x,
            topRect.yMax,
            contentRect.width,
            SplitterSize);
        Rect bottomRect = new Rect(
            contentRect.x,
            horizontalSplitterRect.yMax,
            contentRect.width,
            bottomPaneHeight);

        DrawTopPanes(topRect);
        DrawSplitter(
            horizontalSplitterRect,
            MouseCursor.ResizeVertical,
            delta => bottomPaneHeight -= delta.y,
            () => SaveLayoutPref(BottomPaneHeightPrefKey, bottomPaneHeight));
        DrawBottomPanes(bottomRect);
    }

    private void DrawTopPanes(Rect topRect)
    {
        ClampTopPaneSizes(topRect.width);

        Rect profileRect = new Rect(
            topRect.xMax - profilePaneWidth,
            topRect.y,
            profilePaneWidth,
            topRect.height);
        Rect splitterRect = new Rect(
            profileRect.x - SplitterSize,
            topRect.y,
            SplitterSize,
            topRect.height);
        Rect previewRect = new Rect(
            topRect.x,
            topRect.y,
            Mathf.Max(1f, splitterRect.x - topRect.x),
            topRect.height);

        GUILayout.BeginArea(previewRect, EditorStyles.helpBox);
        EditorGUILayout.LabelField("Preview Window", EditorStyles.boldLabel);
        DrawPreview();
        GUILayout.EndArea();

        DrawSplitter(
            splitterRect,
            MouseCursor.ResizeHorizontal,
            delta => profilePaneWidth -= delta.x,
            () => SaveLayoutPref(ProfilePaneWidthPrefKey, profilePaneWidth));

        GUILayout.BeginArea(profileRect, EditorStyles.helpBox);
        DrawInspectorPane();
        GUILayout.EndArea();
    }

    private void DrawBottomPanes(Rect bottomRect)
    {
        ClampBottomPaneSizes(bottomRect.width);

        Rect inputRect = new Rect(
            bottomRect.x,
            bottomRect.y,
            inputPaneWidth,
            bottomRect.height);
        Rect inputSplitterRect = new Rect(
            inputRect.xMax,
            bottomRect.y,
            SplitterSize,
            bottomRect.height);
        Rect tagRect = new Rect(
            bottomRect.xMax - tagPresetPaneWidth,
            bottomRect.y,
            tagPresetPaneWidth,
            bottomRect.height);
        Rect tagSplitterRect = new Rect(
            tagRect.x - SplitterSize,
            bottomRect.y,
            SplitterSize,
            bottomRect.height);
        Rect playbackRect = new Rect(
            inputSplitterRect.xMax,
            bottomRect.y,
            Mathf.Max(1f, tagSplitterRect.x - inputSplitterRect.xMax),
            bottomRect.height);

        GUILayout.BeginArea(inputRect, EditorStyles.helpBox);
        DrawPreviewInputControls();
        GUILayout.EndArea();

        DrawSplitter(
            inputSplitterRect,
            MouseCursor.ResizeHorizontal,
            delta => inputPaneWidth += delta.x,
            () => SaveLayoutPref(InputPaneWidthPrefKey, inputPaneWidth));

        GUILayout.BeginArea(playbackRect, EditorStyles.helpBox);
        DrawPlaybackControls();
        GUILayout.EndArea();

        DrawSplitter(
            tagSplitterRect,
            MouseCursor.ResizeHorizontal,
            delta => tagPresetPaneWidth -= delta.x,
            () => SaveLayoutPref(TagPaneWidthPrefKey, tagPresetPaneWidth));

        GUILayout.BeginArea(tagRect, EditorStyles.helpBox);
        DrawTagPresetPane();
        GUILayout.EndArea();
    }

    private static void DrawSplitter(Rect rect, MouseCursor cursor, System.Action<Vector2> onDrag, System.Action onDragEnd)
    {
        int controlId = GUIUtility.GetControlID("DialogueTextAnimationTunerSplitter".GetHashCode(), FocusType.Passive, rect);
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

    private void EnsureLayoutPrefsLoaded()
    {
        if (!layoutPrefsLoaded)
            LoadLayoutPrefs();
    }

    private void LoadLayoutPrefs()
    {
        layoutPrefsLoaded = true;
        profilePaneWidth = EditorPrefs.GetFloat(ProfilePaneWidthPrefKey, InspectorMaxWidth);
        bottomPaneHeight = EditorPrefs.GetFloat(BottomPaneHeightPrefKey, PreviewSettingsMaxHeight);
        inputPaneWidth = EditorPrefs.GetFloat(InputPaneWidthPrefKey, InputPaneDefaultWidth);
        tagPresetPaneWidth = EditorPrefs.GetFloat(TagPaneWidthPrefKey, TagPresetPaneMaxWidth);
    }

    private static void SaveLayoutPref(string key, float value)
    {
        EditorPrefs.SetFloat(key, value);
    }

    private void ClampMainPaneSizes(Rect contentRect)
    {
        float maxBottomHeight = Mathf.Max(BottomPaneMinHeight(), contentRect.height - TopMinHeight - SplitterSize);
        bottomPaneHeight = Mathf.Clamp(
            bottomPaneHeight > 0f ? bottomPaneHeight : PreviewSettingsMaxHeight,
            BottomPaneMinHeight(),
            maxBottomHeight);

        ClampTopPaneSizes(contentRect.width);
    }

    private void ClampTopPaneSizes(float totalWidth)
    {
        float maxProfileWidth = Mathf.Max(InspectorMinWidth, totalWidth - PreviewMinWidth - SplitterSize);
        profilePaneWidth = Mathf.Clamp(
            profilePaneWidth > 0f ? profilePaneWidth : InspectorMaxWidth,
            InspectorMinWidth,
            maxProfileWidth);
    }

    private void ClampBottomPaneSizes(float totalWidth)
    {
        float availableWidth = Mathf.Max(1f, totalWidth - SplitterSize * 2f);
        float minimumTotalWidth = InputPaneMinWidth + PlaybackPaneMinWidth + TagPresetPaneMinWidth;
        if (availableWidth <= minimumTotalWidth)
        {
            float ratio = availableWidth / minimumTotalWidth;
            inputPaneWidth = Mathf.Max(1f, InputPaneMinWidth * ratio);
            tagPresetPaneWidth = Mathf.Max(1f, TagPresetPaneMinWidth * ratio);
            return;
        }

        float maxInputWidth = availableWidth - PlaybackPaneMinWidth - TagPresetPaneMinWidth;
        inputPaneWidth = Mathf.Clamp(
            inputPaneWidth > 0f ? inputPaneWidth : InputPaneDefaultWidth,
            InputPaneMinWidth,
            maxInputWidth);

        float maxTagWidth = availableWidth - inputPaneWidth - PlaybackPaneMinWidth;
        tagPresetPaneWidth = Mathf.Clamp(
            tagPresetPaneWidth > 0f ? tagPresetPaneWidth : TagPresetPaneMaxWidth,
            TagPresetPaneMinWidth,
            maxTagWidth);
    }

    private static float BottomPaneMinHeight()
    {
        return PreviewSettingsMinHeight < 160f ? 160f : PreviewSettingsMinHeight;
    }

    private void DrawInspectorPane()
    {
        inspectorScrollPosition = EditorGUILayout.BeginScrollView(inspectorScrollPosition);
        DrawProfileHeader();
        EditorGUILayout.Space(8f);
        DrawTagResetButtons();
        EditorGUILayout.Space(8f);
        DrawProfileEditor();
        EditorGUILayout.EndScrollView();
    }

    private void DrawProfileHeader()
    {
        EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        selectedProfile = (DialogueTextAnimationProfileSO)EditorGUILayout.ObjectField(
            "Text Animation Profile",
            selectedProfile,
            typeof(DialogueTextAnimationProfileSO),
            false);
        if (EditorGUI.EndChangeCheck())
            RebuildProfileEditor();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Default", GUILayout.Height(24f)))
            {
                selectedProfile = Resources.Load<DialogueTextAnimationProfileSO>(
                    DialogueTextAnimationProfileSO.DefaultResourcesPath);
                RebuildProfileEditor();
            }

            if (GUILayout.Button("Create Default", GUILayout.Height(24f)))
            {
                selectedProfile = CreateDefaultProfileAsset();
                RebuildProfileEditor();
            }

            using (new EditorGUI.DisabledScope(selectedProfile == null))
            {
                if (GUILayout.Button("Reset Values", GUILayout.Height(24f)))
                    ResetSelectedProfileValues(
                        "Reset Dialogue Text Animation Profile",
                        profile => profile.ResetToDefaults());
            }
        }

        if (selectedProfile == null)
        {
            EditorGUILayout.HelpBox(
                "No profile is selected. DialogueView will use an in-memory fallback until the default Resources asset exists.",
                MessageType.Info);
        }
    }

    private void DrawTagResetButtons()
    {
        EditorGUILayout.LabelField("Reset Tag Values", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(selectedProfile == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTagResetButton("Shake", "Reset Shake Text Animation", profile => profile.ResetShakeToDefault());
                DrawTagResetButton("Tremble", "Reset Tremble Text Animation", profile => profile.ResetTrembleToDefault());
                DrawTagResetButton("SlowShake", "Reset SlowShake Text Animation", profile => profile.ResetSlowShakeToDefault());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTagResetButton("Wave", "Reset Wave Text Animation", profile => profile.ResetWaveToDefault());
                DrawTagResetButton("Float", "Reset Float Text Animation", profile => profile.ResetFloatToDefault());
                DrawTagResetButton("Punch", "Reset Punch Text Animation", profile => profile.ResetPunchToDefault());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTagResetButton("Rand Size", "Reset Random Size Text Animation", profile => profile.ResetRandomSizeToDefault());
                DrawTagResetButton("Cam Low", "Reset CameraShake Low Text Animation", profile => profile.ResetCameraShakeLowToDefault());
                DrawTagResetButton("Cam Mid", "Reset CameraShake Middle Text Animation", profile => profile.ResetCameraShakeMiddleToDefault());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTagResetButton("Cam High", "Reset CameraShake High Text Animation", profile => profile.ResetCameraShakeHighToDefault());
                DrawTagResetButton("Camera All", "Reset CameraShake Text Animation", profile => profile.ResetCameraShakeToDefault());
            }
        }
    }

    private void DrawTagResetButton(
        string label,
        string undoName,
        System.Action<DialogueTextAnimationProfileSO> resetAction)
    {
        if (GUILayout.Button(label, GUILayout.Height(22f)))
            ResetSelectedProfileValues(undoName, resetAction);
    }

    private void ResetSelectedProfileValues(
        string undoName,
        System.Action<DialogueTextAnimationProfileSO> resetAction)
    {
        if (selectedProfile == null || resetAction == null)
            return;

        Undo.RecordObject(selectedProfile, undoName);
        resetAction(selectedProfile);
        EditorUtility.SetDirty(selectedProfile);
        GUI.changed = true;
        Repaint();
    }

    private void DrawProfileEditor()
    {
        if (profileEditor == null)
            return;

        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        profileEditor.OnInspectorGUI();
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.LabelField("Preview Text Settings", EditorStyles.boldLabel);
        if (DrawPreviewSettingsLayout())
            return;

        sampleText = EditorGUILayout.TextArea(sampleText, GUILayout.MinHeight(58f));
        DrawPreviewSourceField();
        DrawPreviewFontField();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawInsertButton("[shake]", "[shake]강조[/shake]");
            DrawInsertButton("[tremble]", "[tremble]불안[/tremble]");
            DrawInsertButton("[punch]", "[punch]타격[/punch]");
            DrawInsertButton("[wave]", "[wave]흐름[/wave]");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawInsertButton("[float]", "[float]여운[/float]");
            DrawInsertButton("[slowshake]", "[slowshake]느린 흔들림[/slowshake]");
            DrawInsertButton("[rand_size]", "[rand_size=95,110]크기 흔들림[/rand_size]");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            cameraShakePreset = (DialogueCameraShakePreset)EditorGUILayout.EnumPopup(
                "CameraShake Preview",
                cameraShakePreset);

            if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Width(72f)))
            {
                isPlaying = !isPlaying;
                if (isPlaying && !loopPreviewTime && previewTime >= PreviewTimeMax)
                    previewTime = 0f;
                if (isPlaying && typewriterLoop && visibleRatio >= 1f)
                    typewriterTime = 0f;
                lastEditorTime = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button("Restart", GUILayout.Width(72f)))
                previewTime = 0f;
        }

        visibleRatio = EditorGUILayout.Slider("Visible Characters", visibleRatio, 0f, 1f);
        using (new EditorGUI.DisabledScope(isPlaying))
        {
            previewTime = EditorGUILayout.Slider("Preview Time", previewTime, 0f, 5f);
        }
    }

    private bool DrawPreviewSettingsLayout()
    {
        float tagPresetWidth = Mathf.Clamp(position.width * 0.18f, TagPresetPaneMinWidth, TagPresetPaneMaxWidth);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawPreviewInputControls();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(tagPresetWidth)))
            {
                DrawTagPresetPane();
            }
        }

        return true;
    }

    private void DrawPreviewInputControls()
    {
        EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
        sampleText = EditorGUILayout.TextArea(sampleText, GUILayout.MinHeight(42f), GUILayout.MaxHeight(64f));
        DrawPreviewSourceField();
        DrawPreviewFontField();
    }

    private void DrawPlaybackControls()
    {
        EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            cameraShakePreset = (DialogueCameraShakePreset)EditorGUILayout.EnumPopup(
                "CameraShake Preview",
                cameraShakePreset);

            if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Width(72f)))
            {
                isPlaying = !isPlaying;
                if (isPlaying && !loopPreviewTime && previewTime >= PreviewTimeMax)
                    previewTime = 0f;
                if (isPlaying && typewriterLoop && visibleRatio >= 1f)
                    typewriterTime = 0f;
                lastEditorTime = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button("Restart", GUILayout.Width(72f)))
                RestartPreview();
        }

        loopPreviewTime = EditorGUILayout.Toggle("Loop", loopPreviewTime);
        EditorGUI.BeginChangeCheck();
        typewriterLoop = EditorGUILayout.Toggle("Typewriter Loop", typewriterLoop);
        if (EditorGUI.EndChangeCheck())
            typewriterTime = visibleRatio * PreviewTimeMax;

        if (typewriterLoop)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Slider("Visible Characters", visibleRatio, 0f, 1f);
            }
        }
        else
        {
            visibleRatio = EditorGUILayout.Slider("Visible Characters", visibleRatio, 0f, 1f);
        }

        using (new EditorGUI.DisabledScope(isPlaying))
        {
            previewTime = EditorGUILayout.Slider("Preview Time", previewTime, 0f, PreviewTimeMax);
        }
    }

    private void DrawTagPresetPane()
    {
        EditorGUILayout.LabelField("Tag Presets", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawInsertButton("[shake]", "[shake]shake[/shake]");
                DrawInsertButton("[tremble]", "[tremble]tremble[/tremble]");
                DrawInsertButton("[punch]", "[punch]punch[/punch]");
                DrawInsertButton("[wave]", "[wave]wave[/wave]");
            }

            using (new EditorGUILayout.VerticalScope())
            {
                DrawInsertButton("[float]", "[float]float[/float]");
                DrawInsertButton("[slowshake]", "[slowshake]slow shake[/slowshake]");
                DrawInsertButton("[rand_size]", "[rand_size=95,110]random size[/rand_size]");
            }
        }
    }

    private void DrawPreviewFontField()
    {
        EditorGUI.BeginChangeCheck();
        overridePreviewFont = EditorGUILayout.Toggle("Override Preview Font", overridePreviewFont);
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetBool(OverridePreviewFontPrefKey, overridePreviewFont);

        using (new EditorGUI.DisabledScope(!overridePreviewFont))
        {
            EditorGUI.BeginChangeCheck();
            previewFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                "Preview Font",
                previewFont,
                typeof(TMP_FontAsset),
                false);
            if (EditorGUI.EndChangeCheck())
                SavePreviewFont(previewFont);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(EditorGUIUtility.labelWidth);
            using (new EditorGUI.DisabledScope(!overridePreviewFont || previewFont == null))
            {
                if (GUILayout.Button("Use Dialogue Font", GUILayout.Width(140f)))
                {
                    previewFont = null;
                    SavePreviewFont(null);
                }
            }
        }
    }

    private void DrawPreviewSourceField()
    {
        EditorGUI.BeginChangeCheck();
        manualPreviewSource = (TextMeshProUGUI)EditorGUILayout.ObjectField(
            "Preview Source",
            manualPreviewSource,
            typeof(TextMeshProUGUI),
            true);
        if (EditorGUI.EndChangeCheck())
            Repaint();

        TextMeshProUGUI source = ResolvePreviewSource(out string warning);
        if (source == null)
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Resolved Source", source, typeof(TextMeshProUGUI), true);
    }

    private void DrawPreview()
    {
        Rect previewRect = GUILayoutUtility.GetRect(
            GUIContent.none,
            GUIStyle.none,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true),
            GUILayout.MinHeight(PreviewHeight));
        EditorGUI.DrawRect(previewRect, new Color(0.07f, 0.07f, 0.08f, 1f));

        if (Event.current.type != EventType.Repaint)
            return;

        EnsurePreview();
        if (previewCamera == null || previewText == null)
            return;

        TextMeshProUGUI source = ResolvePreviewSource(out string warning);
        if (source == null)
        {
            DrawPreviewWarning(previewRect, warning);
            return;
        }

        DialogueTextAnimationProfileSO profile =
            selectedProfile != null ? selectedProfile : DialogueTextAnimationProfileSO.LoadDefaultOrFallback();
        DialogueTextRevealPlan plan = DialogueTextAnimationUtility.BuildPlan(sampleText, profile);
        CopyPreviewSourceSettings(source);
        previewText.richText = true;
        previewText.text = plan.DisplayText;
        if (overridePreviewFont && previewFont != null)
        {
            previewText.font = previewFont;
            previewText.fontSharedMaterial = previewFont.material;
        }
        previewText.maxVisibleCharacters = int.MaxValue;
        previewText.ForceMeshUpdate();

        int characterCount = previewText.textInfo != null ? previewText.textInfo.characterCount : 0;
        int visibleCount = Mathf.RoundToInt(characterCount * Mathf.Clamp01(visibleRatio));
        previewText.maxVisibleCharacters = visibleCount;
        previewText.ForceMeshUpdate();
        DialogueTextImpactState impactState = BuildImpactState(profile);
        DialogueTextAnimationUtility.ApplyTextEffects(
            previewText,
            plan,
            visibleCount,
            previewTime,
            impactState,
            profile);

        ConfigurePreviewCamera(source, previewRect);

        Canvas.ForceUpdateCanvases();

        EnsurePreviewRenderTexture(previewRect);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = previewCamera.targetTexture;
        bool previousEnabled = previewCamera.enabled;
        try
        {
            previewCamera.targetTexture = previewRenderTexture;
            previewCamera.enabled = true;
            previewCamera.Render();
        }
        finally
        {
            previewCamera.enabled = previousEnabled;
            previewCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
        }

        if (previewRenderTexture != null)
            GUI.DrawTexture(previewRect, previewRenderTexture, ScaleMode.StretchToFill, false);
    }

    private static void DrawPreviewWarning(Rect previewRect, string warning)
    {
        GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.95f, 0.82f, 0.45f, 1f) }
        };
        GUI.Label(previewRect, warning, style);
    }

    private void ConfigurePreviewCamera(TextMeshProUGUI source, Rect previewRect)
    {
        if (previewCamera == null)
            return;

        Vector2 previewSize = ResolvePreviewContainerSize(source);
        float aspect = Mathf.Max(0.01f, previewRect.width / Mathf.Max(1f, previewRect.height));
        previewCamera.orthographic = true;
        previewCamera.aspect = aspect;
        previewCamera.orthographicSize = ResolvePreviewCameraSize(previewSize, aspect);
        previewCamera.transform.position = new Vector3(0f, 0f, -10f);
        previewCamera.transform.rotation = Quaternion.identity;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.07f, 0.07f, 0.08f, 1f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.nearClipPlane = 0.1f;
        previewCamera.farClipPlane = 100f;
    }

    private void EnsurePreviewRenderTexture(Rect previewRect)
    {
        float scale = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
        int width = Mathf.Max(1, Mathf.RoundToInt(previewRect.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(previewRect.height * scale));
        if (previewRenderTexture != null &&
            previewTextureWidth == width &&
            previewTextureHeight == height)
        {
            return;
        }

        ReleasePreviewRenderTexture();
        previewTextureWidth = width;
        previewTextureHeight = height;
        previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "DialogueTextAnimationPreviewTexture",
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 1,
            useMipMap = false
        };
        previewRenderTexture.Create();
    }

    private void ReleasePreviewRenderTexture()
    {
        previewTextureWidth = 0;
        previewTextureHeight = 0;
        if (previewRenderTexture == null)
            return;

        if (previewCamera != null && previewCamera.targetTexture == previewRenderTexture)
            previewCamera.targetTexture = null;

        previewRenderTexture.Release();
        DestroyImmediate(previewRenderTexture);
        previewRenderTexture = null;
    }

    private DialogueTextImpactState BuildImpactState(DialogueTextAnimationProfileSO profile)
    {
        if (cameraShakePreset == DialogueCameraShakePreset.None ||
            profile == null ||
            !profile.TryResolveCameraShakeMotion(cameraShakePreset, out DialogueCameraShakeMotionSettings motion))
        {
            return default;
        }

        return new DialogueTextImpactState(
            0f,
            motion.Duration,
            motion.TextSettleDuration,
            motion.CharacterImpactOffset,
            motion.Vibrato,
            motion.Randomness);
    }

    private void DrawInsertButton(string label, string textToAppend)
    {
        if (!GUILayout.Button(label, GUILayout.Height(22f)))
            return;

        string resolvedText = ResolveInsertSample(label, textToAppend);
        sampleText = string.IsNullOrWhiteSpace(sampleText)
            ? resolvedText
            : $"{sampleText} {resolvedText}";
    }

    private static string ResolveInsertSample(string label, string fallback)
    {
        switch (label)
        {
            case "[shake]":
                return "[shake]shake[/shake]";
            case "[tremble]":
                return "[tremble]tremble[/tremble]";
            case "[punch]":
                return "[punch]punch[/punch]";
            case "[wave]":
                return "[wave]wave[/wave]";
            case "[float]":
                return "[float]float[/float]";
            case "[slowshake]":
                return "[slowshake]slow shake[/slowshake]";
            case "[rand_size]":
                return "[rand_size=95,110]random size[/rand_size]";
            default:
                return fallback;
        }
    }

    private void TickPreview()
    {
        double now = EditorApplication.timeSinceStartup;
        float delta = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.1f);
        lastEditorTime = now;

        if (isPlaying)
        {
            previewTime += delta;
            if (loopPreviewTime)
            {
                previewTime = Mathf.Repeat(previewTime, PreviewTimeMax);
            }
            else if (previewTime >= PreviewTimeMax)
            {
                previewTime = PreviewTimeMax;
                isPlaying = false;
            }

            if (typewriterLoop)
            {
                typewriterTime = Mathf.Repeat(typewriterTime + delta, PreviewTimeMax);
                visibleRatio = Mathf.Clamp01(typewriterTime / PreviewTimeMax);
            }
        }

        Repaint();
    }

    private void RestartPreview()
    {
        previewTime = 0f;
        typewriterTime = 0f;
        if (typewriterLoop)
            visibleRatio = 0f;

        lastEditorTime = EditorApplication.timeSinceStartup;
        Repaint();
    }

    private void EnsurePreview()
    {
        if (previewCamera == null)
        {
            GameObject cameraObject = new GameObject("DialogueTextAnimationPreviewCamera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.layer = PreviewLayer;
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.07f, 0.07f, 0.08f, 1f);
            previewCamera.cullingMask = 1 << PreviewLayer;
        }

        if (previewRoot != null && previewText != null)
            return;

        previewRoot = new GameObject("DialogueTextAnimationPreviewCanvas", typeof(RectTransform), typeof(Canvas));
        previewRoot.hideFlags = HideFlags.HideAndDontSave;
        previewRoot.layer = PreviewLayer;

        Canvas previewCanvas = previewRoot.GetComponent<Canvas>();
        previewCanvas.renderMode = RenderMode.WorldSpace;
        previewCanvas.worldCamera = previewCamera;
        previewCanvas.sortingOrder = 0;

        previewCanvasRect = previewRoot.GetComponent<RectTransform>();
        previewCanvasRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewCanvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewCanvasRect.pivot = new Vector2(0.5f, 0.5f);
        previewCanvasRect.localPosition = Vector3.zero;
        previewCanvasRect.localRotation = Quaternion.identity;
        previewCanvasRect.localScale = Vector3.one;

        GameObject containerObject = new GameObject("DialogueTextCon", typeof(RectTransform));
        containerObject.hideFlags = HideFlags.HideAndDontSave;
        containerObject.layer = PreviewLayer;
        previewContainerRect = containerObject.GetComponent<RectTransform>();
        previewContainerRect.SetParent(previewCanvasRect, false);
        previewContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewContainerRect.pivot = new Vector2(0.5f, 0.5f);
        previewContainerRect.anchoredPosition = Vector2.zero;
        previewContainerRect.localRotation = Quaternion.identity;
        previewContainerRect.localScale = Vector3.one;

        GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.hideFlags = HideFlags.HideAndDontSave;
        textObject.layer = PreviewLayer;
        textObject.transform.SetParent(previewContainerRect, false);
        previewText = textObject.AddComponent<TextMeshProUGUI>();
        previewText.hideFlags = HideFlags.HideAndDontSave;
    }

    private void DestroyPreview()
    {
        ReleasePreviewRenderTexture();
        previewText = null;
        previewContainerRect = null;
        previewCanvasRect = null;
        if (previewRoot != null)
            DestroyImmediate(previewRoot);

        previewRoot = null;
        if (previewCamera != null)
            DestroyImmediate(previewCamera.gameObject);

        previewCamera = null;
    }

    private void CopyPreviewSourceSettings(TextMeshProUGUI source)
    {
        if (source == null || previewText == null)
            return;

        Vector2 containerSize = ResolvePreviewContainerSize(source);
        Vector2 textSize = ResolvePreviewTextSize(source, containerSize);
        previewCanvasRect.sizeDelta = containerSize;
        previewContainerRect.sizeDelta = containerSize;
        CopyRectTransform(source.rectTransform, previewText.rectTransform);
        previewText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textSize.x);
        previewText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textSize.y);

        CopyPreviewFontSettings(source);
        previewText.spriteAsset = source.spriteAsset;
        previewText.styleSheet = source.styleSheet;
        previewText.color = source.color;
        previewText.fontSize = source.fontSize;
        previewText.enableAutoSizing = source.enableAutoSizing;
        previewText.fontSizeMin = source.fontSizeMin;
        previewText.fontSizeMax = source.fontSizeMax;
        previewText.fontStyle = source.fontStyle;
        previewText.fontWeight = source.fontWeight;
        previewText.alignment = source.alignment;
        previewText.enableWordWrapping = source.enableWordWrapping;
        previewText.overflowMode = source.overflowMode;
        previewText.characterSpacing = source.characterSpacing;
        previewText.wordSpacing = source.wordSpacing;
        previewText.lineSpacing = source.lineSpacing;
        previewText.paragraphSpacing = source.paragraphSpacing;
        previewText.margin = source.margin;
        previewText.richText = source.richText;
        previewText.parseCtrlCharacters = source.parseCtrlCharacters;
        previewText.extraPadding = source.extraPadding;
    }

    private void CopyPreviewFontSettings(TextMeshProUGUI source)
    {
        if (source == null || previewText == null)
            return;

        previewText.font = source.font;
        if (overridePreviewFont && previewFont != null)
            return;

        Material sourceMaterial = source.fontSharedMaterial;
        if (sourceMaterial == null && source.font != null)
            sourceMaterial = source.font.material;

        if (sourceMaterial != null)
            previewText.fontSharedMaterial = sourceMaterial;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private TextMeshProUGUI ResolvePreviewSource(out string warning)
    {
        warning = string.Empty;
        if (manualPreviewSource != null)
            return manualPreviewSource;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultDialogueRootPrefabPath);
        if (prefab == null)
        {
            warning = $"Preview source prefab was not found: {DefaultDialogueRootPrefabPath}";
            return null;
        }

        DialogueView dialogueView = prefab.GetComponentInChildren<DialogueView>(true);
        if (dialogueView != null)
        {
            SerializedObject serializedView = new SerializedObject(dialogueView);
            SerializedProperty dialogueTextProperty = serializedView.FindProperty(DialogueTextFieldName);
            if (dialogueTextProperty != null &&
                dialogueTextProperty.objectReferenceValue is TextMeshProUGUI dialogueText)
            {
                return dialogueText;
            }
        }

        TextMeshProUGUI namedDialogueText = FindTextByName(prefab.transform, "DialogueText");
        if (namedDialogueText != null)
            return namedDialogueText;

        warning =
            $"Could not resolve DialogueView.{DialogueTextFieldName} from {DefaultDialogueRootPrefabPath}. Assign a TextMeshProUGUI Preview Source manually.";
        return null;
    }

    private static TextMeshProUGUI FindTextByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == objectName)
                return texts[i];
        }

        return null;
    }

    private static Vector2 ResolvePreviewContainerSize(TextMeshProUGUI source)
    {
        RectTransform sourceParent = source != null ? source.rectTransform.parent as RectTransform : null;
        return ResolveRectSize(sourceParent, new Vector2(PreviewFallbackWidth, PreviewFallbackHeight));
    }

    private static Vector2 ResolvePreviewTextSize(TextMeshProUGUI source, Vector2 containerSize)
    {
        if (source == null || source.rectTransform == null)
            return ResolveFallbackSize(containerSize);

        Vector2 sourceRectSize = ResolveSourceRectSize(source);
        Vector2 sourceSizeDelta = source.rectTransform.sizeDelta;
        Vector2 fallbackSize = ResolveFallbackSize(containerSize);

        return new Vector2(
            ResolvePreviewDimension(sourceRectSize.x, sourceSizeDelta.x, fallbackSize.x, PreviewFallbackWidth),
            ResolvePreviewDimension(sourceRectSize.y, sourceSizeDelta.y, fallbackSize.y, PreviewFallbackHeight));
    }

    private static Vector2 ResolveSourceRectSize(TextMeshProUGUI source)
    {
        if (source == null || source.rectTransform == null)
            return Vector2.zero;

        return source.rectTransform.rect.size;
    }

    private static Vector2 ResolveFallbackSize(Vector2 containerSize)
    {
        return new Vector2(
            containerSize.x > 0.01f ? containerSize.x : PreviewFallbackWidth,
            containerSize.y > 0.01f ? containerSize.y : PreviewFallbackHeight);
    }

    private static float ResolvePreviewDimension(
        float sourceRectDimension,
        float sourceSizeDeltaDimension,
        float containerDimension,
        float hardFallback)
    {
        if (sourceRectDimension > 0.01f)
            return sourceRectDimension;

        if (sourceSizeDeltaDimension > 0.01f)
            return sourceSizeDeltaDimension;

        if (containerDimension > 0.01f)
            return containerDimension;

        return hardFallback;
    }

    private static Vector2 ResolveRectSize(RectTransform rectTransform, Vector2 fallback)
    {
        if (rectTransform == null)
            return fallback;

        Vector2 rectSize = rectTransform.rect.size;
        if (rectSize.x > 0.01f && rectSize.y > 0.01f)
            return rectSize;

        Vector2 sizeDelta = rectTransform.sizeDelta;
        if (Mathf.Abs(sizeDelta.x) > 0.01f && Mathf.Abs(sizeDelta.y) > 0.01f)
            return new Vector2(Mathf.Abs(sizeDelta.x), Mathf.Abs(sizeDelta.y));

        return fallback;
    }

    private static float ResolvePreviewCameraSize(Vector2 previewSize, float aspect)
    {
        float halfHeight = Mathf.Max(1f, previewSize.y * 0.5f);
        float halfWidthAsHeight = Mathf.Max(1f, previewSize.x * 0.5f / Mathf.Max(0.01f, aspect));
        return Mathf.Max(halfHeight, halfWidthAsHeight) + PreviewCameraPadding;
    }

    private void RebuildProfileEditor()
    {
        DestroyProfileEditor();
        if (selectedProfile != null)
            profileEditor = Editor.CreateEditor(selectedProfile);
    }

    private void DestroyProfileEditor()
    {
        if (profileEditor != null)
            DestroyImmediate(profileEditor);

        profileEditor = null;
    }

    private static TMP_FontAsset LoadSavedPreviewFont()
    {
        string guid = EditorPrefs.GetString(PreviewFontGuidPrefKey, string.Empty);
        if (string.IsNullOrEmpty(guid))
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static void SavePreviewFont(TMP_FontAsset font)
    {
        if (font == null)
        {
            EditorPrefs.DeleteKey(PreviewFontGuidPrefKey);
            return;
        }

        string path = AssetDatabase.GetAssetPath(font);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
            EditorPrefs.DeleteKey(PreviewFontGuidPrefKey);
        else
            EditorPrefs.SetString(PreviewFontGuidPrefKey, guid);
    }

    private static DialogueTextAnimationProfileSO CreateDefaultProfileAsset()
    {
        EnsureFolder("Assets/LeeJunMo/Datas/Resources");
        EnsureFolder("Assets/LeeJunMo/Datas/Resources/Dialogue");

        DialogueTextAnimationProfileSO existing =
            AssetDatabase.LoadAssetAtPath<DialogueTextAnimationProfileSO>(
                DialogueTextAnimationProfileSO.DefaultAssetPath);
        if (existing != null)
            return existing;

        DialogueTextAnimationProfileSO profile =
            CreateInstance<DialogueTextAnimationProfileSO>();
        profile.ResetToDefaults();
        AssetDatabase.CreateAsset(profile, DialogueTextAnimationProfileSO.DefaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
