using UnityEngine;

public static class WindowModeBootstrap
{
    private const int DefaultWindowWidth = 1280;
    private const int DefaultWindowHeight = 720;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void ApplyWindowMode()
    {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(DefaultWindowWidth, DefaultWindowHeight, FullScreenMode.Windowed);
#endif
    }
}
