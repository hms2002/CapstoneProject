using UnityEngine;

public static class WindowModeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void ApplyWindowMode()
    {
        GameSettingsService.ApplyBootSettings();
    }
}
