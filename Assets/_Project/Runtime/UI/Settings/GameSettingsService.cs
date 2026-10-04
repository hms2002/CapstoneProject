using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Settings;

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
/// 책임: 게임 언어 설정의 사용자 선택 값을 나타낸다.
/// </summary>
public enum GameLanguageOption
{
    Korean = 0,
    English = 1,
    Japanese = 2,
    SimplifiedChinese = 3,
    TraditionalChinese = 4,
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
    private GameLanguageOption language = GameLanguageOption.Korean;
    private bool initialized;
    private GamePresentationController presentationController;

    public event Action SettingsChanged;

    public bool ScreenShakeEnabled => screenShakeEnabled;
    public GameWindowMode CurrentWindowMode => windowMode;
    public int CurrentResolutionWidth => resolutionWidth;
    public int CurrentResolutionHeight => resolutionHeight;
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

        return FitWindowResolution(resolutionOptions[clampedIndex], GetWindowClientLimit());
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

        DisplayResolutionOption option = FitWindowResolution(resolutionOptions[clampedIndex], GetWindowClientLimit());
        bool changed = windowMode != mode ||
                       resolutionWidth != option.width ||
                       resolutionHeight != option.height;

        windowMode = mode;
        resolutionWidth = option.width;
        resolutionHeight = option.height;
        PlayerPrefs.SetInt(WindowModePrefKey, (int)windowMode);
        SaveResolution();
        BuildResolutionOptions();
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

    public void SetLanguage(GameLanguageOption newLanguage)
    {
        if (!IsSupportedLanguage(newLanguage))
            throw new ArgumentOutOfRangeException(nameof(newLanguage), newLanguage, "Unsupported game language.");
        EnsureInitialized();
        if (language == newLanguage)
            return;

        language = newLanguage;
        PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
        PlayerPrefs.Save();
        ApplyLanguageLocale();
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
            GameWindowMode.Borderless => GameText.Get("settings.window.borderless", "테두리 없음"),
            GameWindowMode.Fullscreen => GameText.Get("settings.window.fullscreen", "전체화면"),
            _ => GameText.Get("settings.window.windowed", "창모드"),
        };
    }

    public string GetOnOffLabel(bool value)
    {
        return value ? GameText.Get("settings.toggle.on", "켜기") : GameText.Get("settings.toggle.off", "끄기");
    }

    public string GetLanguageLabel(GameLanguageOption option)
    {
        return option switch
        {
            GameLanguageOption.English => "English",
            GameLanguageOption.Japanese => "日本語",
            GameLanguageOption.SimplifiedChinese => "简体中文",
            GameLanguageOption.TraditionalChinese => "繁體中文",
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
        LoadPreferences();
        ApplyLanguageLocale();
        NormalizeSavedWindowResolution();
        BuildResolutionOptions();
        ApplyDisplaySettings(windowMode, resolutionWidth, resolutionHeight);
        ApplyPresentationBounds();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

    private void LoadPreferences()
    {
        windowMode = (GameWindowMode)Mathf.Clamp(
            PlayerPrefs.GetInt(WindowModePrefKey, (int)GameWindowMode.Windowed),
            (int)GameWindowMode.Windowed,
            (int)GameWindowMode.Fullscreen);

        resolutionWidth = Mathf.Max(640, PlayerPrefs.GetInt(ResolutionWidthPrefKey, DefaultWindowWidth));
        resolutionHeight = Mathf.Max(360, PlayerPrefs.GetInt(ResolutionHeightPrefKey, DefaultWindowHeight));
        screenShakeEnabled = PlayerPrefs.GetInt(ScreenShakePrefKey, 1) != 0;
        var savedLanguage = (GameLanguageOption)PlayerPrefs.GetInt(LanguagePrefKey, -1);
        if (PlayerPrefs.HasKey(LanguagePrefKey) && IsSupportedLanguage(savedLanguage))
        {
            language = savedLanguage;
        }
        else
        {
            string steamLanguage = SteamPlatformService.TryGetGameLanguage(out string detected) ? detected : null;
            language = ResolveInitialLanguage(steamLanguage, Application.systemLanguage);
            PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
            PlayerPrefs.Save();
        }
    }

    internal static bool IsSupportedLanguage(GameLanguageOption option) =>
        option >= GameLanguageOption.Korean && option <= GameLanguageOption.TraditionalChinese;

    internal static GameLanguageOption ResolveInitialLanguage(string steamLanguage, SystemLanguage systemLanguage)
    {
        return steamLanguage switch
        {
            "koreana" => GameLanguageOption.Korean,
            "english" => GameLanguageOption.English,
            "japanese" => GameLanguageOption.Japanese,
            "schinese" => GameLanguageOption.SimplifiedChinese,
            "tchinese" => GameLanguageOption.TraditionalChinese,
            _ => ResolveSystemLanguage(systemLanguage),
        };
    }

    internal static GameLanguageOption ResolveSystemLanguage(SystemLanguage systemLanguage)
    {
        return systemLanguage switch
        {
            SystemLanguage.Korean => GameLanguageOption.Korean,
            SystemLanguage.Japanese => GameLanguageOption.Japanese,
            SystemLanguage.ChineseSimplified => GameLanguageOption.SimplifiedChinese,
            SystemLanguage.ChineseTraditional => GameLanguageOption.TraditionalChinese,
            _ => GameLanguageOption.English,
        };
    }

    private Coroutine localeInitialization;

    private void ApplyLanguageLocale()
    {
        if (localeInitialization == null)
            localeInitialization = StartCoroutine(SelectLanguageLocale());
    }

    private IEnumerator SelectLanguageLocale()
    {
        yield return LocalizationSettings.InitializationOperation;
        // Read the latest preference after initialization, including changes made while it was loading.
        GameLanguageOption requestedLanguage = language;
        string code = language switch
        {
            GameLanguageOption.Korean => "ko",
            GameLanguageOption.Japanese => "ja",
            GameLanguageOption.SimplifiedChinese => "zh-Hans",
            GameLanguageOption.TraditionalChinese => "zh-Hant",
            _ => "en",
        };
        var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale != null)
        {
            yield return LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale);
            if (language != requestedLanguage)
            {
                localeInitialization = null;
                ApplyLanguageLocale();
                yield break;
            }
            LocalizationSettings.SelectedLocale = locale;
        }
        localeInitialization = null;
    }

    private void BuildResolutionOptions()
    {
        resolutionOptions.Clear();

        Vector2Int limit = GetWindowClientLimit();
        HashSet<string> seen = new();
        Resolution[] screenResolutions = Screen.resolutions;
        for (int i = 0; i < screenResolutions.Length; i++)
        {
            Resolution resolution = screenResolutions[i];
            if (resolution.width < 640 || resolution.height < 360 ||
                resolution.width > limit.x || resolution.height > limit.y)
                continue;

            string key = $"{resolution.width}x{resolution.height}";
            if (!seen.Add(key))
                continue;

            resolutionOptions.Add(new DisplayResolutionOption(resolution.width, resolution.height));
        }

        for (int i = 0; i < CuratedResolutionOptions.Length; i++)
        {
            DisplayResolutionOption option = CuratedResolutionOptions[i];
            if (option.width < 640 || option.height < 360 ||
                option.width > limit.x || option.height > limit.y)
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

    private void NormalizeSavedWindowResolution()
    {
        DisplayResolutionOption fitted = FitWindowResolution(
            new DisplayResolutionOption(resolutionWidth, resolutionHeight), GetWindowClientLimit());
        resolutionWidth = fitted.width;
        resolutionHeight = fitted.height;
        SaveResolution();
    }

    private static DisplayResolutionOption FitWindowResolution(DisplayResolutionOption requested, Vector2Int limit)
    {
        int width = Mathf.Max(640, requested.width);
        int height = Mathf.Max(360, requested.height);
        float scale = Mathf.Min(1f, Mathf.Min(Mathf.Max(1, limit.x) / (float)width, Mathf.Max(1, limit.y) / (float)height));
        return new DisplayResolutionOption(Mathf.Max(1, Mathf.FloorToInt(width * scale)),
            Mathf.Max(1, Mathf.FloorToInt(height * scale)));
    }

    private static Vector2Int GetWindowClientLimit()
    {
        RectInt workArea = Screen.mainWindowDisplayInfo.workArea;
        if (workArea.width <= 0 || workArea.height <= 0)
        {
            DisplayResolutionOption display = GetSystemDisplayResolution();
            workArea = new RectInt(0, 0, display.width, display.height);
        }

        // Reserve window decorations as well as the taskbar excluded by Unity's work area.
        int frameWidth = 32;
        int frameHeight = 64;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr window = GetActiveWindow();
        uint dpi = window != IntPtr.Zero ? GetDpiForWindow(window) : GetDpiForSystem();
        NativeWindowRect frame = default;
        // WS_OVERLAPPEDWINDOW is conservative even for a non-resizable player.
        if (AdjustWindowRectExForDpi(ref frame, 0x00CF0000, false, 0, dpi))
        {
            frameWidth = frame.right - frame.left;
            frameHeight = frame.bottom - frame.top;
        }
#endif
        return new Vector2Int(Mathf.Max(1, workArea.width - frameWidth), Mathf.Max(1, workArea.height - frameHeight));
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeWindowRect { public int left, top, right, bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AdjustWindowRectExForDpi(ref NativeWindowRect rect, uint style,
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool menu, uint extendedStyle, uint dpi);
#endif

    private void SaveResolution()
    {
        PlayerPrefs.SetInt(ResolutionWidthPrefKey, resolutionWidth);
        PlayerPrefs.SetInt(ResolutionHeightPrefKey, resolutionHeight);
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
        DisplayResolutionOption fitted = FitWindowResolution(new DisplayResolutionOption(savedWidth, savedHeight), GetWindowClientLimit());
        savedWidth = fitted.width;
        savedHeight = fitted.height;
        PlayerPrefs.SetInt(ResolutionWidthPrefKey, savedWidth);
        PlayerPrefs.SetInt(ResolutionHeightPrefKey, savedHeight);
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
            ? FitWindowResolution(new DisplayResolutionOption(width, height), GetWindowClientLimit())
            : GetSystemDisplayResolution();

        Screen.fullScreenMode = fullScreenMode;
        Screen.SetResolution(
            appliedResolution.width,
            appliedResolution.height,
            fullScreenMode);

        MouseCursorService.EnsureInstance().NotifyDisplayConfigurationChanged();
#endif
    }
}
