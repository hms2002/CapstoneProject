using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class BuildPreviewWindow : EditorWindow
{
    private readonly PreviewPreset[] presets =
    {
        new("1280 x 720 (16:9)", 1280, 720),
        new("1600 x 900 (16:9)", 1600, 900),
        new("1920 x 1080 (16:9)", 1920, 1080),
        new("1280 x 800 (16:10)", 1280, 800),
        new("1440 x 900 (16:10)", 1440, 900),
        new("1680 x 1050 (16:10)", 1680, 1050),
        new("1024 x 768 (4:3)", 1024, 768),
        new("1280 x 960 (4:3)", 1280, 960),
        new("2560 x 1080 (21:9)", 2560, 1080),
        new("3440 x 1440 (21:9)", 3440, 1440),
    };

    private int displayPresetIndex = 2;
    private int contentPresetIndex = 0;
    private int customDisplayWidth = 1920;
    private int customDisplayHeight = 1080;
    private int customContentWidth = 1280;
    private int customContentHeight = 720;
    private bool enterPlayModeAfterApply = true;
    private bool maximizeGameView;
    private GameWindowMode previewWindowMode = GameWindowMode.Borderless;

    [Serializable]
    private readonly struct PreviewPreset
    {
        public PreviewPreset(string label, int width, int height)
        {
            Label = label;
            Width = width;
            Height = height;
        }

        public string Label { get; }
        public int Width { get; }
        public int Height { get; }
    }

    [MenuItem("Tools/Preview/Build Preview")]
    public static void ShowWindow()
    {
        GetWindow<BuildPreviewWindow>("Build Preview");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Game View Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Preview build-like letterboxing by separating the display size (Game View) from the selected in-game resolution.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        DrawDisplayControls();

        EditorGUILayout.Space(10f);
        DrawContentControls();

        EditorGUILayout.Space(10f);
        previewWindowMode = (GameWindowMode)EditorGUILayout.EnumPopup("Preview Window Mode", previewWindowMode);
        enterPlayModeAfterApply = EditorGUILayout.ToggleLeft("Enter Play Mode after applying size", enterPlayModeAfterApply);
        maximizeGameView = EditorGUILayout.ToggleLeft("Maximize Game View window", maximizeGameView);
        DrawExpectationHelpBox();

        EditorGUILayout.Space(10f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Preset Preview", GUILayout.Height(28f)))
            {
                PreviewPreset displayPreset = presets[Mathf.Clamp(displayPresetIndex, 0, presets.Length - 1)];
                PreviewPreset contentPreset = presets[Mathf.Clamp(contentPresetIndex, 0, presets.Length - 1)];
                ApplyPreview(displayPreset.Width, displayPreset.Height, displayPreset.Label, contentPreset.Width, contentPreset.Height);
            }

            if (GUILayout.Button("Apply Custom Preview", GUILayout.Height(28f)))
            {
                ApplyPreview(
                    Mathf.Max(320, customDisplayWidth),
                    Mathf.Max(180, customDisplayHeight),
                    "Custom Display Preview",
                    Mathf.Max(320, customContentWidth),
                    Mathf.Max(180, customContentHeight));
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Stop Play Mode"))
                EditorApplication.isPlaying = false;

            if (GUILayout.Button("Open Game View"))
                FocusGameView();
        }
    }

    private void DrawPresetControls()
    {
        EditorGUILayout.LabelField("Display Size (Game View)", EditorStyles.boldLabel);
        string[] labels = new string[presets.Length];
        for (int i = 0; i < presets.Length; i++)
            labels[i] = presets[i].Label;

        displayPresetIndex = EditorGUILayout.Popup("Display Preset", displayPresetIndex, labels);
    }

    private void DrawDisplayControls()
    {
        DrawPresetControls();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Custom Display Size", EditorStyles.boldLabel);
        customDisplayWidth = EditorGUILayout.IntField("Display Width", customDisplayWidth);
        customDisplayHeight = EditorGUILayout.IntField("Display Height", customDisplayHeight);
    }

    private void DrawContentControls()
    {
        EditorGUILayout.LabelField("Selected In-Game Resolution", EditorStyles.boldLabel);
        string[] labels = new string[presets.Length];
        for (int i = 0; i < presets.Length; i++)
            labels[i] = presets[i].Label;

        contentPresetIndex = EditorGUILayout.Popup("Content Preset", contentPresetIndex, labels);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Custom In-Game Resolution", EditorStyles.boldLabel);
        customContentWidth = EditorGUILayout.IntField("Content Width", customContentWidth);
        customContentHeight = EditorGUILayout.IntField("Content Height", customContentHeight);
    }

    private void DrawExpectationHelpBox()
    {
        PreviewPreset displayPreset = presets[Mathf.Clamp(displayPresetIndex, 0, presets.Length - 1)];
        PreviewPreset contentPreset = presets[Mathf.Clamp(contentPresetIndex, 0, presets.Length - 1)];
        float displayAspect = displayPreset.Width / (float)displayPreset.Height;
        float contentAspect = contentPreset.Width / (float)contentPreset.Height;

        string expectation = Mathf.Approximately(displayAspect, contentAspect)
            ? "Expected: no letterbox."
            : displayAspect > contentAspect
                ? "Expected: left and right pillarbox bars."
                : "Expected: top and bottom letterbox bars.";

        EditorGUILayout.HelpBox(expectation, MessageType.None);
    }

    private void ApplyPreview(int displayWidth, int displayHeight, string label, int contentWidth, int contentHeight)
    {
        SavePreviewSettings(contentWidth, contentHeight, previewWindowMode);

        int selectedIndex = GameViewReflectionUtility.FindOrCreateSize(displayWidth, displayHeight, label);
        GameViewReflectionUtility.SelectSize(selectedIndex, maximizeGameView);

        if (!enterPlayModeAfterApply)
            return;

        if (!EditorApplication.isPlaying)
            EditorApplication.isPlaying = true;
    }

    private static void FocusGameView()
    {
        GameViewReflectionUtility.OpenGameView();
    }

    private static void SavePreviewSettings(int width, int height, GameWindowMode windowMode)
    {
        PlayerPrefs.SetInt("settings.display.width", width);
        PlayerPrefs.SetInt("settings.display.height", height);
        PlayerPrefs.SetInt("settings.display.windowmode", (int)windowMode);
        PlayerPrefs.Save();
    }

    private static class GameViewReflectionUtility
    {
        private static readonly Assembly EditorAssembly = typeof(Editor).Assembly;
        private static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");
        private static readonly Type GameViewSizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
        private static readonly Type GameViewSizeTypeEnum = EditorAssembly.GetType("UnityEditor.GameViewSizeType");
        private static readonly Type GameViewSizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
        private static readonly Type GameViewSizeGroupType = EditorAssembly.GetType("UnityEditor.GameViewSizeGroupType");

        public static int FindOrCreateSize(int width, int height, string label)
        {
            object group = GetCurrentGroup();
            if (group == null)
                throw new InvalidOperationException("Unable to resolve the Game View size group.");

            MethodInfo getTotalCount = group.GetType().GetMethod("GetTotalCount");
            MethodInfo getGameViewSize = group.GetType().GetMethod("GetGameViewSize");

            int totalCount = (int)getTotalCount.Invoke(group, null);
            for (int i = 0; i < totalCount; i++)
            {
                object size = getGameViewSize.Invoke(group, new object[] { i });
                if (size == null)
                    continue;

                int existingWidth = (int)GameViewSizeType.GetProperty("width").GetValue(size);
                int existingHeight = (int)GameViewSizeType.GetProperty("height").GetValue(size);
                if (existingWidth == width && existingHeight == height)
                    return i;
            }

            object sizeType = Enum.Parse(GameViewSizeTypeEnum, "FixedResolution");
            ConstructorInfo constructor = GameViewSizeType.GetConstructor(
                new[] { GameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            object newSize = constructor.Invoke(new object[] { sizeType, width, height, label });

            MethodInfo addCustomSize = group.GetType().GetMethod("AddCustomSize");
            addCustomSize.Invoke(group, new[] { newSize });

            totalCount = (int)getTotalCount.Invoke(group, null);
            return Mathf.Max(0, totalCount - 1);
        }

        public static void SelectSize(int index, bool maximize)
        {
            EditorWindow gameView = OpenGameView();
            if (gameView == null)
                return;

            PropertyInfo selectedSizeIndex = GameViewType.GetProperty(
                "selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            selectedSizeIndex?.SetValue(gameView, index);
            gameView.maximized = maximize;
            gameView.Repaint();
            gameView.Focus();
        }

        public static EditorWindow OpenGameView()
        {
            if (GameViewType == null)
                throw new InvalidOperationException("Unable to find UnityEditor.GameView.");

            EditorWindow window = EditorWindow.GetWindow(GameViewType);
            window.Show();
            return window;
        }

        private static object GetCurrentGroup()
        {
            if (GameViewSizesType == null || GameViewSizeGroupType == null)
                return null;

            Type singleType = typeof(ScriptableSingleton<>).MakeGenericType(GameViewSizesType);
            object instance = singleType.GetProperty("instance").GetValue(null, null);
            MethodInfo getGroup = GameViewSizesType.GetMethod("GetGroup");
            object groupType = Enum.ToObject(GameViewSizeGroupType, (int)GetCurrentGroupType());
            return getGroup.Invoke(instance, new[] { groupType });
        }

        private static object GetCurrentGroupType()
        {
            string name = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget).ToString();
            if (Enum.IsDefined(GameViewSizeGroupType, name))
                return Enum.Parse(GameViewSizeGroupType, name);

            return Enum.Parse(GameViewSizeGroupType, "Standalone");
        }
    }
}
