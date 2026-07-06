using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임: 게임 창 표시 방식의 사용자 설정 값을 나타낸다.
/// </summary>
public enum GameWindowMode
{
    Windowed = 0,
    Borderless = 1,
    Fullscreen = 2,
}

/// <summary>
/// 책임: 전역 UI 배율의 사용자 선택 프리셋을 나타낸다.
/// </summary>
public enum UiScalePreset
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

/// <summary>
/// 책임: 게임 언어 설정의 사용자 선택 값을 나타낸다.
/// </summary>
public enum GameLanguageOption
{
    Korean = 0,
}

/// <summary>
/// 책임: 설정 UI와 디스플레이 적용 코드가 공유하는 해상도 옵션 값을 보관한다.
/// </summary>
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

/// <summary>
/// 책임: 저장된 게임/디스플레이/UI 설정을 로드하고 런타임 서비스에 적용하는 전역 설정 서비스이다.
/// </summary>
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
    private static readonly DisplayResolutionOption[] CuratedResolutionOptions =
    {
        new(2560, 1080),
        new(3440, 1440),
    };

    public static GameSettingsService Instance { get; private set; }

    private static readonly IGameSettingsBackend s_settingsBackend = new GameSettingsBackend();

    private readonly List<DisplayResolutionOption> resolutionOptions = new();

    private GameWindowMode windowMode = GameWindowMode.Windowed;
    private int resolutionWidth = DefaultWindowWidth;
    private int resolutionHeight = DefaultWindowHeight;
    private bool screenShakeEnabled = true;
    private UiScalePreset uiScalePreset = UiScalePreset.Medium;
    private GameLanguageOption language = GameLanguageOption.Korean;
    private bool initialized;
    private GamePresentationController presentationController;
    private GameUiScaleController uiScaleController;

    public event Action SettingsChanged;

    public bool ScreenShakeEnabled => screenShakeEnabled;
    public GameWindowMode CurrentWindowMode => windowMode;
    public int CurrentResolutionWidth => resolutionWidth;
    public int CurrentResolutionHeight => resolutionHeight;
    public UiScalePreset CurrentUiScalePreset => uiScalePreset;
    public GameLanguageOption CurrentLanguage => language;

    /// <summary>
    /// 책임: Core의 GameSettingsQuery 요청을 현재 GameSettingsService 인스턴스 상태로 연결한다.
    /// </summary>
    private sealed class GameSettingsBackend : IGameSettingsBackend
    {
        public bool IsScreenShakeEnabled()
        {
            return GameSettingsService.IsScreenShakeEnabled();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSettingsBackend()
    {
        GameSettingsQuery.RegisterBackend(s_settingsBackend);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        RegisterSettingsBackend();
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

    public DisplayResolutionOption GetDisplayedResolutionOption(GameWindowMode mode, int resolutionIndex)
    {
        EnsureInitialized();

        if (mode != GameWindowMode.Windowed)
            return GetSystemDisplayResolution();

        if (resolutionOptions.Count == 0)
            BuildResolutionOptions();

        if (resolutionOptions.Count == 0)
            return new DisplayResolutionOption(resolutionWidth, resolutionHeight);

        int clampedIndex = Mathf.Clamp(
            resolutionIndex < 0 ? GetCurrentResolutionIndex() : resolutionIndex,
            0,
            resolutionOptions.Count - 1);

        return resolutionOptions[clampedIndex];
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

    private static DisplayResolutionOption GetSystemDisplayResolution()
    {
        Resolution currentResolution = Screen.currentResolution;
        if (currentResolution.width > 0 && currentResolution.height > 0)
            return new DisplayResolutionOption(currentResolution.width, currentResolution.height);

        Display mainDisplay = Display.main;
        if (mainDisplay != null && mainDisplay.systemWidth > 0 && mainDisplay.systemHeight > 0)
            return new DisplayResolutionOption(mainDisplay.systemWidth, mainDisplay.systemHeight);

        if (Screen.width > 0 && Screen.height > 0)
            return new DisplayResolutionOption(Screen.width, Screen.height);

        return new DisplayResolutionOption(DefaultWindowWidth, DefaultWindowHeight);
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

        if (presentationController != null)
            presentationController.RefreshIfNeeded(windowMode, resolutionWidth, resolutionHeight);
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        EnsurePresentationController();
        EnsureUiScaleController();
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

    private void EnsurePresentationController()
    {
        if (presentationController != null)
            return;

        presentationController = GetComponent<GamePresentationController>();
        if (presentationController == null)
            presentationController = gameObject.AddComponent<GamePresentationController>();
    }

    private void EnsureUiScaleController()
    {
        if (uiScaleController != null)
            return;

        uiScaleController = GetComponent<GameUiScaleController>();
        if (uiScaleController == null)
            uiScaleController = gameObject.AddComponent<GameUiScaleController>();
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
        EnsureUiScaleController();
        if (uiScaleController != null)
            uiScaleController.Apply(uiScalePreset);
    }

    private void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke();
    }

    private void ApplyPresentationBounds()
    {
        EnsurePresentationController();
        if (presentationController != null)
            presentationController.ApplyPresentation(windowMode, resolutionWidth, resolutionHeight);
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

        DisplayResolutionOption appliedResolution = mode == GameWindowMode.Windowed
            ? new DisplayResolutionOption(width, height)
            : GetSystemDisplayResolution();

        Screen.fullScreenMode = fullScreenMode;
        Screen.SetResolution(
            Mathf.Max(640, appliedResolution.width),
            Mathf.Max(360, appliedResolution.height),
            fullScreenMode);

        MouseCursorService.EnsureInstance().NotifyDisplayConfigurationChanged();
#endif
    }
}
