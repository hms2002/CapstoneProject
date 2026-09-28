// Isolated fixture: production GameSettingsService + cursor sources. Stub unrelated audio/presentation.
// Define CURSOR_DISPLAY_REGRESSION and run MouseCursorApiRegression.BuildPlayer / Run.
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public sealed class DisplaySettingsRegression : MonoBehaviour
{
    private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private Action<int> complete;

    public static void Run(Action<int> complete)
    {
        var runner = new GameObject("Display regression").AddComponent<DisplaySettingsRegression>();
        runner.complete = complete;
        runner.StartCoroutine(runner.CheckWindow());
    }

    private static object Call(string name, params object[] args) =>
        typeof(GameSettingsService).GetMethod(name, StaticPrivate).Invoke(null, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private IEnumerator CheckWindow()
    {
        Exception failure = null;
        try { CheckPolicyAndSavedSettings(); }
        catch (Exception exception) { failure = exception; }
        if (failure != null) { Debug.LogException(failure); complete(1); yield break; }

        foreach (var request in new[] { new DisplayResolutionOption(1680, 1050), new DisplayResolutionOption(3440, 1440) })
        {
            Call("ApplyDisplaySettings", GameWindowMode.Windowed, request.width, request.height);
            yield return new WaitForSecondsRealtime(1f);
            var limit = (Vector2Int)Call("GetWindowClientLimit");
            if (Screen.width > limit.x || Screen.height > limit.y || Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Debug.LogError($"DISPLAY_BOUNDS_FAIL requested={request} actual={Screen.width}x{Screen.height} limit={limit}");
                complete(1);
                yield break;
            }
            float aspectError = Mathf.Abs(Screen.width / (float)Screen.height - request.width / (float)request.height);
            if (aspectError > .01f)
            {
                Debug.LogError($"DISPLAY_ASPECT_FAIL requested={request} actual={Screen.width}x{Screen.height}");
                complete(1);
                yield break;
            }
            Debug.Log($"DISPLAY_BOUNDS_PASS requested={request} actual={Screen.width}x{Screen.height} clientLimit={limit} workArea={Screen.mainWindowDisplayInfo.workArea}");
        }
        Debug.Log("DISPLAY_REGRESSION_PASS");
        complete(0);
    }

    private static void CheckPolicyAndSavedSettings()
    {
        var limit = new Vector2Int(1904, 1001);
        foreach (var requested in new[] { new DisplayResolutionOption(1680, 1050), new DisplayResolutionOption(3440, 1440), new DisplayResolutionOption(1280, 720) })
        {
            var fitted = (DisplayResolutionOption)Call("FitWindowResolution", requested, limit);
            Check(fitted.width <= limit.x && fitted.height <= limit.y, "Fit exceeds client limit");
            Check(Mathf.Abs(fitted.width / (float)fitted.height - requested.width / (float)requested.height) < .01f, "Fit stretches aspect");
            if (requested.width == 1280) Check(fitted.width == 1280 && fitted.height == 720, "Safe size changed");
        }

        // This fixture has its own product identity/preferences, never the game's saved settings.
        PlayerPrefs.SetInt("settings.display.width", 3440);
        PlayerPrefs.SetInt("settings.display.height", 1440);
        PlayerPrefs.SetInt("settings.display.windowmode", 0);
        GameSettingsService.ApplyBootSettings();
        var service = GameSettingsService.EnsureInstance();
        foreach (string method in new[] { "LoadPreferences", "NormalizeSavedWindowResolution", "BuildResolutionOptions" })
            typeof(GameSettingsService).GetMethod(method, InstancePrivate).Invoke(service, null);
        var currentLimit = (Vector2Int)Call("GetWindowClientLimit");
        Check(service.CurrentResolutionWidth <= currentLimit.x && service.CurrentResolutionHeight <= currentLimit.y, "Saved size was not repaired");
        Check(PlayerPrefs.GetInt("settings.display.width") == service.CurrentResolutionWidth &&
              PlayerPrefs.GetInt("settings.display.height") == service.CurrentResolutionHeight, "Preferences and UI disagree");
        foreach (var option in service.GetResolutionOptions())
            Check(option.width <= currentLimit.x && option.height <= currentLimit.y, "Oversized resolution remains selectable");
        Check(service.GetCurrentResolutionIndex() >= 0, "Repaired option missing");
    }
}
