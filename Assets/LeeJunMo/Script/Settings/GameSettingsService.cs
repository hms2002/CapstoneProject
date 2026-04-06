using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameWindowMode
{
    Windowed = 0,
    Borderless = 1,
    Fullscreen = 2,
}

public enum UiScalePreset
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

public enum GameLanguageOption
{
    Korean = 0,
}

[Serializable]
public struct DisplayResolutionOption
{
    public int width;
    public int height;

    public DisplayResolutionOption(int width, int height)
    {
        this.width = width;
        this.height = height;
    }

    public override string ToString()
    {
        return $"{width} x {height}";
    }
}

[DefaultExecutionOrder(-900)]
public sealed class GameSettingsService : MonoBehaviour
{
    private const string WindowModePrefKey = "settings.display.windowmode";
    private const string ResolutionWidthPrefKey = "settings.display.width";
    private const string ResolutionHeightPrefKey = "settings.display.height";
    private const string ScreenShakePrefKey = "settings.gameplay.screenshake";
    private const string UiScalePrefKey = "settings.ui.scale";
    private const string LanguagePrefKey = "settings.language";

    private const int DefaultWindowWidth = 1280;
    private const int DefaultWindowHeight = 720;
    private const float AspectRatioTolerance = 0.0001f;
    private const int LetterboxSortingOrder = 32767;

    private static readonly DisplayResolutionOption[] CuratedResolutionOptions =
    {
        new(2560, 1080),
        new(3440, 1440),
    };

    private static readonly GlobalCanvasLayer[] UiScaleLayers =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.Dialogue,
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };

    public static GameSettingsService Instance { get; private set; }

    private readonly List<DisplayResolutionOption> resolutionOptions = new();
    private readonly Dictionary<CanvasScaler, Vector2> baseReferenceResolutions = new();
    private readonly Dictionary<CanvasScaler, float> baseScaleFactors = new();
    private readonly Dictionary<Canvas, RenderMode> baseCanvasRenderModes = new();
    private readonly Dictionary<Canvas, Camera> baseCanvasWorldCameras = new();
    private readonly Dictionary<Canvas, float> baseCanvasPlaneDistances = new();

    private GameWindowMode windowMode = GameWindowMode.Windowed;
    private int resolutionWidth = DefaultWindowWidth;
    private int resolutionHeight = DefaultWindowHeight;
    private bool screenShakeEnabled = true;
    private UiScalePreset uiScalePreset = UiScalePreset.Medium;
    private GameLanguageOption language = GameLanguageOption.Korean;
    private bool initialized;
    private int lastPresentationWidth = -1;
    private int lastPresentationHeight = -1;

    private Canvas letterboxCanvas;
    private RectTransform letterboxRoot;
    private Image topLetterboxBar;
    private Image bottomLetterboxBar;
    private Image leftLetterboxBar;
    private Image rightLetterboxBar;

    public event Action SettingsChanged;

    public bool ScreenShakeEnabled => screenShakeEnabled;
    public GameWindowMode CurrentWindowMode => windowMode;
    public UiScalePreset CurrentUiScalePreset => uiScalePreset;
    public GameLanguageOption CurrentLanguage => language;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void ApplyBootSettings()
    {
        LoadSavedDisplaySettings(out GameWindowMode savedMode, out int savedWidth, out int savedHeight);
        ApplyDisplaySettings(savedMode, savedWidth, savedHeight);
    }

    public static GameSettingsService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        GameSettingsService existing = FindAnyObjectByType<GameSettingsService>();
#else
        GameSettingsService existing = FindObjectOfType<GameSettingsService>();
#endif
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureInitialized();
            return existing;
        }

        GameObject root = new GameObject(nameof(GameSettingsService));
        return root.AddComponent<GameSettingsService>();
    }

    public static bool IsScreenShakeEnabled()
    {
        GameSettingsService service = EnsureInstance();
        return service == null || service.screenShakeEnabled;
    }

    public IReadOnlyList<DisplayResolutionOption> GetResolutionOptions()
    {
        EnsureInitialized();
        return resolutionOptions;
    }

    public int GetCurrentResolutionIndex()
    {
        EnsureInitialized();

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            DisplayResolutionOption option = resolutionOptions[i];
            if (option.width == resolutionWidth && option.height == resolutionHeight)
                return i;
        }

        return resolutionOptions.Count > 0 ? 0 : -1;
    }

    public void SetWindowMode(GameWindowMode mode)
    {
        EnsureInitialized();
        ApplyDisplaySelection(mode, GetCurrentResolutionIndex());
    }

    public void SetResolutionByIndex(int index)
    {
        EnsureInitialized();
        ApplyDisplaySelection(windowMode, index);
    }

    public void ApplyDisplaySelection(GameWindowMode mode, int resolutionIndex)
    {
        EnsureInitialized();

        if (resolutionOptions.Count == 0)
            BuildResolutionOptions();

        if (resolutionOptions.Count == 0)
            return;

        int clampedIndex = Mathf.Clamp(
            resolutionIndex < 0 ? GetCurrentResolutionIndex() : resolutionIndex,
            0,
            resolutionOptions.Count - 1);

        DisplayResolutionOption option = resolutionOptions[clampedIndex];
        bool changed = windowMode != mode ||
                       resolutionWidth != option.width ||
                       resolutionHeight != option.height;

        windowMode = mode;
        resolutionWidth = option.width;
        resolutionHeight = option.height;
        PlayerPrefs.SetInt(WindowModePrefKey, (int)windowMode);
        SaveResolution();
        ApplyDisplaySettings(windowMode, resolutionWidth, resolutionHeight);

        if (changed)
            NotifySettingsChanged();
    }

    public void SetScreenShakeEnabled(bool enabled)
    {
        EnsureInitialized();
        if (screenShakeEnabled == enabled)
            return;

        screenShakeEnabled = enabled;
        PlayerPrefs.SetInt(ScreenShakePrefKey, screenShakeEnabled ? 1 : 0);
        NotifySettingsChanged();
    }

    public void SetUiScalePreset(UiScalePreset preset)
    {
        EnsureInitialized();
        if (uiScalePreset == preset)
        {
            ApplyUiScale();
            return;
        }

        uiScalePreset = preset;
        PlayerPrefs.SetInt(UiScalePrefKey, (int)uiScalePreset);
        ApplyUiScale();
        NotifySettingsChanged();
    }

    public void SetLanguage(GameLanguageOption newLanguage)
    {
        EnsureInitialized();
        if (language == newLanguage)
            return;

        language = newLanguage;
        PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
        NotifySettingsChanged();
    }

    public float GetMasterVolume()
    {
        return SoundManager.EnsureInstance().GetMasterVolume();
    }

    public void SetMasterVolume(float value)
    {
        SoundManager.EnsureInstance().SetMasterVolume(value);
        NotifySettingsChanged();
    }

    public float GetMusicVolume()
    {
        return SoundManager.EnsureInstance().GetMusicVolume();
    }

    public void SetMusicVolume(float value)
    {
        SoundManager.EnsureInstance().SetMusicVolume(value);
        NotifySettingsChanged();
    }

    public float GetSfxVolume()
    {
        return SoundManager.EnsureInstance().GetSfxVolume();
    }

    public void SetSfxVolume(float value)
    {
        SoundManager.EnsureInstance().SetSfxVolume(value);
        NotifySettingsChanged();
    }

    public string GetWindowModeLabel(GameWindowMode mode)
    {
        return mode switch
        {
            GameWindowMode.Borderless => "테두리 없음",
            GameWindowMode.Fullscreen => "전체화면",
            _ => "창모드",
        };
    }

    public string GetOnOffLabel(bool value)
    {
        return value ? "켜기" : "끄기";
    }

    public string GetUiScaleLabel(UiScalePreset preset)
    {
        return preset switch
        {
            UiScalePreset.Small => "작게",
            UiScalePreset.Large => "크게",
            _ => "중간",
        };
    }

    public string GetLanguageLabel(GameLanguageOption option)
    {
        return option switch
        {
            _ => "한국어",
        };
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        if (lastPresentationWidth != Screen.width || lastPresentationHeight != Screen.height)
            ApplyPresentationBounds();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        LoadPreferences();
        BuildResolutionOptions();
        ApplyDisplaySettings(windowMode, resolutionWidth, resolutionHeight);
        ApplyUiScale();
        ApplyPresentationBounds();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyUiScale();
        ApplyPresentationBounds();
    }

    private void LoadPreferences()
    {
        windowMode = (GameWindowMode)Mathf.Clamp(
            PlayerPrefs.GetInt(WindowModePrefKey, (int)GameWindowMode.Windowed),
            (int)GameWindowMode.Windowed,
            (int)GameWindowMode.Fullscreen);

        resolutionWidth = Mathf.Max(640, PlayerPrefs.GetInt(ResolutionWidthPrefKey, DefaultWindowWidth));
        resolutionHeight = Mathf.Max(360, PlayerPrefs.GetInt(ResolutionHeightPrefKey, DefaultWindowHeight));
        screenShakeEnabled = PlayerPrefs.GetInt(ScreenShakePrefKey, 1) != 0;
        uiScalePreset = (UiScalePreset)Mathf.Clamp(
            PlayerPrefs.GetInt(UiScalePrefKey, (int)UiScalePreset.Medium),
            (int)UiScalePreset.Small,
            (int)UiScalePreset.Large);
        language = (GameLanguageOption)Mathf.Clamp(
            PlayerPrefs.GetInt(LanguagePrefKey, (int)GameLanguageOption.Korean),
            (int)GameLanguageOption.Korean,
            (int)GameLanguageOption.Korean);
    }

    private void BuildResolutionOptions()
    {
        resolutionOptions.Clear();

        HashSet<string> seen = new();
        Resolution[] screenResolutions = Screen.resolutions;
        for (int i = 0; i < screenResolutions.Length; i++)
        {
            Resolution resolution = screenResolutions[i];
            if (resolution.width < 640 || resolution.height < 360)
                continue;

            string key = $"{resolution.width}x{resolution.height}";
            if (!seen.Add(key))
                continue;

            resolutionOptions.Add(new DisplayResolutionOption(resolution.width, resolution.height));
        }

        for (int i = 0; i < CuratedResolutionOptions.Length; i++)
        {
            DisplayResolutionOption option = CuratedResolutionOptions[i];
            if (option.width < 640 || option.height < 360)
                continue;

            string key = $"{option.width}x{option.height}";
            if (!seen.Add(key))
                continue;

            resolutionOptions.Add(option);
        }

        resolutionOptions.Sort((a, b) =>
        {
            int widthCompare = a.width.CompareTo(b.width);
            if (widthCompare != 0)
                return widthCompare;

            return a.height.CompareTo(b.height);
        });

        if (resolutionOptions.Count == 0)
            resolutionOptions.Add(new DisplayResolutionOption(resolutionWidth, resolutionHeight));

        bool hasSavedResolution = false;
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            DisplayResolutionOption option = resolutionOptions[i];
            if (option.width == resolutionWidth && option.height == resolutionHeight)
            {
                hasSavedResolution = true;
                break;
            }
        }

        if (!hasSavedResolution)
            resolutionOptions.Add(new DisplayResolutionOption(resolutionWidth, resolutionHeight));

        resolutionOptions.Sort((a, b) =>
        {
            int widthCompare = a.width.CompareTo(b.width);
            if (widthCompare != 0)
                return widthCompare;

            return a.height.CompareTo(b.height);
        });
    }

    private void SaveResolution()
    {
        PlayerPrefs.SetInt(ResolutionWidthPrefKey, resolutionWidth);
        PlayerPrefs.SetInt(ResolutionHeightPrefKey, resolutionHeight);
    }

    private void ApplyUiScale()
    {
        float multiplier = GetUiScaleMultiplier(uiScalePreset);

        for (int i = 0; i < UiScaleLayers.Length; i++)
        {
            Canvas canvas = GlobalUIRoot.GetCanvas(UiScaleLayers[i]);
            if (canvas == null)
                continue;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                continue;

            if (!baseReferenceResolutions.ContainsKey(scaler))
                baseReferenceResolutions[scaler] = scaler.referenceResolution;

            if (!baseScaleFactors.ContainsKey(scaler))
                baseScaleFactors[scaler] = scaler.scaleFactor;

            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                Vector2 baseReferenceResolution = baseReferenceResolutions[scaler];
                scaler.referenceResolution = baseReferenceResolution / multiplier;
            }
            else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                float baseScaleFactor = baseScaleFactors[scaler];
                scaler.scaleFactor = baseScaleFactor * multiplier;
            }
        }
    }

    private static float GetUiScaleMultiplier(UiScalePreset preset)
    {
        return preset switch
        {
            UiScalePreset.Small => 0.9f,
            UiScalePreset.Large => 1.1f,
            _ => 1f,
        };
    }

    private void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke();
    }

    private void ApplyPresentationBounds()
    {
        Vector2Int containerSize = GetPresentationContainerSize();
        float contentAspectRatio = GetSelectedResolutionAspectRatio();
        Rect viewportRect = CalculateViewportRect(containerSize.x, containerSize.y, contentAspectRatio);
        ApplyCameraViewport(viewportRect);
        ApplyUiCanvasPresentation(viewportRect);
        ApplyLetterboxOverlay(viewportRect);
        lastPresentationWidth = Screen.width;
        lastPresentationHeight = Screen.height;
    }

    private float GetSelectedResolutionAspectRatio()
    {
        if (resolutionWidth <= 0 || resolutionHeight <= 0)
            return (float)DefaultWindowWidth / DefaultWindowHeight;

        return resolutionWidth / (float)resolutionHeight;
    }

    private static Rect CalculateViewportRect(int containerWidth, int containerHeight, float targetAspectRatio)
    {
        if (containerWidth <= 0 || containerHeight <= 0 || targetAspectRatio <= 0f)
            return new Rect(0f, 0f, 1f, 1f);

        float currentAspectRatio = containerWidth / (float)containerHeight;
        if (Mathf.Abs(currentAspectRatio - targetAspectRatio) <= AspectRatioTolerance)
            return new Rect(0f, 0f, 1f, 1f);

        if (currentAspectRatio > targetAspectRatio)
        {
            float normalizedWidth = targetAspectRatio / currentAspectRatio;
            float insetX = (1f - normalizedWidth) * 0.5f;
            return new Rect(insetX, 0f, normalizedWidth, 1f);
        }

        float normalizedHeight = currentAspectRatio / targetAspectRatio;
        float insetY = (1f - normalizedHeight) * 0.5f;
        return new Rect(0f, insetY, 1f, normalizedHeight);
    }

    private Vector2Int GetPresentationContainerSize()
    {
        if (windowMode == GameWindowMode.Windowed)
            return new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

        Display mainDisplay = Display.main;
        if (mainDisplay != null)
            return new Vector2Int(Mathf.Max(1, mainDisplay.systemWidth), Mathf.Max(1, mainDisplay.systemHeight));

        return new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
    }

    private static void ApplyCameraViewport(Rect viewportRect)
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.targetTexture != null)
                continue;

            camera.rect = viewportRect;
        }
    }

    private void ApplyUiCanvasPresentation(Rect viewportRect)
    {
        bool useFullScreen = Mathf.Approximately(viewportRect.x, 0f) &&
                             Mathf.Approximately(viewportRect.y, 0f) &&
                             Mathf.Approximately(viewportRect.width, 1f) &&
                             Mathf.Approximately(viewportRect.height, 1f);

        Camera presentationCamera = Camera.main;
        for (int i = 0; i < UiScaleLayers.Length; i++)
        {
            Canvas canvas = GlobalUIRoot.GetCanvas(UiScaleLayers[i]);
            if (canvas == null)
                continue;

            if (!baseCanvasRenderModes.ContainsKey(canvas))
                baseCanvasRenderModes[canvas] = canvas.renderMode;

            if (!baseCanvasWorldCameras.ContainsKey(canvas))
                baseCanvasWorldCameras[canvas] = canvas.worldCamera;

            if (!baseCanvasPlaneDistances.ContainsKey(canvas))
                baseCanvasPlaneDistances[canvas] = canvas.planeDistance;

            if (!useFullScreen && presentationCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = presentationCamera;
                canvas.planeDistance = Mathf.Max(1f, baseCanvasPlaneDistances[canvas]);
                continue;
            }

            canvas.renderMode = baseCanvasRenderModes[canvas];
            canvas.worldCamera = baseCanvasWorldCameras[canvas];
            canvas.planeDistance = baseCanvasPlaneDistances[canvas];
        }
    }

    private void ApplyLetterboxOverlay(Rect viewportRect)
    {
        EnsureLetterboxOverlay();
        if (letterboxRoot == null)
            return;

        bool useFullScreen = Mathf.Approximately(viewportRect.x, 0f) &&
                             Mathf.Approximately(viewportRect.y, 0f) &&
                             Mathf.Approximately(viewportRect.width, 1f) &&
                             Mathf.Approximately(viewportRect.height, 1f);

        SetLetterboxBar(topLetterboxBar, Vector2.zero, Vector2.zero, !useFullScreen && viewportRect.y > 0f);
        SetLetterboxBar(bottomLetterboxBar, Vector2.zero, Vector2.zero, !useFullScreen && viewportRect.y > 0f);
        SetLetterboxBar(leftLetterboxBar, Vector2.zero, Vector2.zero, !useFullScreen && viewportRect.x > 0f);
        SetLetterboxBar(rightLetterboxBar, Vector2.zero, Vector2.zero, !useFullScreen && viewportRect.x > 0f);

        if (useFullScreen)
            return;

        if (viewportRect.y > 0f)
        {
            SetLetterboxBar(topLetterboxBar, new Vector2(0f, viewportRect.y + viewportRect.height), new Vector2(1f, 1f), true);
            SetLetterboxBar(bottomLetterboxBar, new Vector2(0f, 0f), new Vector2(1f, viewportRect.y), true);
            return;
        }

        if (viewportRect.x > 0f)
        {
            SetLetterboxBar(leftLetterboxBar, new Vector2(0f, 0f), new Vector2(viewportRect.x, 1f), true);
            SetLetterboxBar(rightLetterboxBar, new Vector2(viewportRect.x + viewportRect.width, 0f), new Vector2(1f, 1f), true);
        }
    }

    private void EnsureLetterboxOverlay()
    {
        if (letterboxCanvas != null)
            return;

        GameObject root = new GameObject("LetterboxOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        letterboxRoot = root.GetComponent<RectTransform>();
        letterboxRoot.anchorMin = Vector2.zero;
        letterboxRoot.anchorMax = Vector2.one;
        letterboxRoot.offsetMin = Vector2.zero;
        letterboxRoot.offsetMax = Vector2.zero;

        letterboxCanvas = root.GetComponent<Canvas>();
        letterboxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        letterboxCanvas.sortingOrder = LetterboxSortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DefaultWindowWidth, DefaultWindowHeight);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        topLetterboxBar = CreateLetterboxBar("TopBar");
        bottomLetterboxBar = CreateLetterboxBar("BottomBar");
        leftLetterboxBar = CreateLetterboxBar("LeftBar");
        rightLetterboxBar = CreateLetterboxBar("RightBar");
    }

    private Image CreateLetterboxBar(string name)
    {
        GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(letterboxRoot, false);

        RectTransform rectTransform = barObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = barObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        barObject.SetActive(false);
        return image;
    }

    private static void SetLetterboxBar(Image image, Vector2 anchorMin, Vector2 anchorMax, bool visible)
    {
        if (image == null)
            return;

        if (!visible)
        {
            image.gameObject.SetActive(false);
            return;
        }

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        image.gameObject.SetActive(true);
    }

    private static void LoadSavedDisplaySettings(
        out GameWindowMode savedMode,
        out int savedWidth,
        out int savedHeight)
    {
        savedMode = (GameWindowMode)Mathf.Clamp(
            PlayerPrefs.GetInt(WindowModePrefKey, (int)GameWindowMode.Windowed),
            (int)GameWindowMode.Windowed,
            (int)GameWindowMode.Fullscreen);
        savedWidth = Mathf.Max(640, PlayerPrefs.GetInt(ResolutionWidthPrefKey, DefaultWindowWidth));
        savedHeight = Mathf.Max(360, PlayerPrefs.GetInt(ResolutionHeightPrefKey, DefaultWindowHeight));
    }

    private static void ApplyDisplaySettings(GameWindowMode mode, int width, int height)
    {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        FullScreenMode fullScreenMode = mode switch
        {
            GameWindowMode.Borderless => FullScreenMode.FullScreenWindow,
            GameWindowMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.Windowed,
        };

        Screen.fullScreenMode = fullScreenMode;
        Screen.SetResolution(Mathf.Max(640, width), Mathf.Max(360, height), fullScreenMode);
#endif
    }
}
