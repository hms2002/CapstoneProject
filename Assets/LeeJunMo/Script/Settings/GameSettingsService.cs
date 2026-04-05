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

    private GameWindowMode windowMode = GameWindowMode.Windowed;
    private int resolutionWidth = DefaultWindowWidth;
    private int resolutionHeight = DefaultWindowHeight;
    private bool screenShakeEnabled = true;
    private UiScalePreset uiScalePreset = UiScalePreset.Medium;
    private GameLanguageOption language = GameLanguageOption.Korean;
    private bool initialized;

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

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        LoadPreferences();
        BuildResolutionOptions();
        ApplyDisplaySettings(windowMode, resolutionWidth, resolutionHeight);
        ApplyUiScale();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyUiScale();
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
            string key = $"{resolution.width}x{resolution.height}";
            if (!seen.Add(key))
                continue;

            resolutionOptions.Add(new DisplayResolutionOption(resolution.width, resolution.height));
        }

        resolutionOptions.Sort((a, b) =>
        {
            int areaCompare = (a.width * a.height).CompareTo(b.width * b.height);
            if (areaCompare != 0)
                return areaCompare;

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
