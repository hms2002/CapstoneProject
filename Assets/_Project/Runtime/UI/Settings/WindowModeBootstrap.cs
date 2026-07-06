using UnityEngine;

/// <summary>
/// 책임: 스플래시 이전에 저장된 창 모드와 해상도 설정을 적용해 첫 프레임 표시 상태를 맞춘다.
/// </summary>
public static class WindowModeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void ApplyWindowMode()
    {
        GameSettingsService.ApplyBootSettings();
    }
}
