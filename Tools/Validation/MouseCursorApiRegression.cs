// Run in a disposable Unity 6000.4 project with the production cursor sources,
// default theme and its sprite assets copied with their metadata. Requires graphics.
using System;
using System.Collections;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

public static class MouseCursorApiRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    #if UNITY_EDITOR
    public static void Run()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.update += ExerciseWhenPlaying;
    }

    // Re-register after the play-mode domain reload.
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update -= ExerciseWhenPlaying;
        EditorApplication.update += ExerciseWhenPlaying;
    }

    private static void ExerciseWhenPlaying()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 2) return;
        EditorApplication.update -= ExerciseWhenPlaying;
        Exercise();
    }

    public static void BuildPlayer()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/CursorValidation.unity");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/CursorValidation.unity" },
            locationPathName = "Build/CursorValidation.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    #else
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RunPlayer() => Exercise();
    #endif

    private static void Exit(int code)
    {
        #if UNITY_EDITOR
        EditorApplication.Exit(code);
        #else
        Application.Quit(code);
        #endif
    }

    private static Texture2D Resolve(MouseCursorService service, MouseCursorSpriteDefinition definition, out Vector2 hotspot)
    {
        object[] args = { definition, null, Vector2.zero };
        Check((bool)Call(service, "TryResolveCursorTexture", args), "Texture resolution failed");
        hotspot = (Vector2)args[2];
        return (Texture2D)args[1];
    }

    private static void Exercise()
    {
        try
        {
            var service = MouseCursorService.EnsureInstance();
            Set(service, "scaleWithScreenHeight", false);
            VerifyPixelsAndCache(service);
            VerifyTheme(service);
            VerifyOwnership(service);
            VerifyEscapeRelease(service);
            Check(service.transform.Find("MouseCursorCanvas") == null, "Cursor created a UI hierarchy");
            Debug.Log("CURSOR_API_PASS: unreadable crop/orientation/alpha/color, scaling/PPU/hotspot/cache, all theme sprites, domains, hide/display/focus, disable/enable");
            #if CURSOR_DISPLAY_REGRESSION
            DisplaySettingsRegression.Run(Exit);
            #else
            Exit(0);
            #endif
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Exit(1);
        }
    }

    private static void VerifyPixelsAndCache(MouseCursorService service)
    {
        var source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        source.filterMode = FilterMode.Point;
        var pixels = new Color32[64];
        pixels[2 * 8 + 2] = new Color32(255, 0, 0, 255);
        pixels[2 * 8 + 3] = new Color32(0, 255, 0, 128);
        pixels[3 * 8 + 2] = new Color32(0, 0, 255, 255);
        pixels[3 * 8 + 3] = new Color32(128, 64, 32, 255);
        source.SetPixels32(pixels);
        source.Apply(false, true);
        var sprite = Sprite.Create(source, new Rect(2, 2, 2, 2), Vector2.zero, 100);
        var definition = new MouseCursorSpriteDefinition { sprite = sprite, scale = 2, hotspotPixels = Vector2.one };
        Texture2D texture = Resolve(service, definition, out Vector2 hotspot);
        Check(texture.width == 4 && texture.height == 4 && hotspot == new Vector2(2, 2), "Scale or hotspot mismatch");
        var actual = texture.GetPixels32();
        var expected = new[] { pixels[18], pixels[19], pixels[26], pixels[27] };
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            Color32 a = actual[y * 4 + x], e = expected[(y / 2) * 2 + x / 2];
            Check(Math.Abs(a.r - e.r) <= 2 && Math.Abs(a.g - e.g) <= 2 && Math.Abs(a.b - e.b) <= 2 && Math.Abs(a.a - e.a) <= 2,
                $"GPU crop/color mismatch at {x},{y}: {a} vs {e}");
        }
        Check(ReferenceEquals(texture, Resolve(service, definition, out _)), "Steady cursor allocated a new texture");
        int cacheCount = Get<IDictionary>(service, "generatedCursorTextures").Count;
        definition.scale = 1;
        Texture2D resized = Resolve(service, definition, out _);
        Check(resized.width == 2 && !ReferenceEquals(texture, resized), "Resize did not replace texture");
        Check(Get<IDictionary>(service, "generatedCursorTextures").Count == cacheCount, "Resize grew cache");
        definition.hotspotPixels = new Vector2(2, -1);
        Resolve(service, definition, out hotspot);
        Check(hotspot == new Vector2(1, 0), "Hotspot outside API bounds");
        var lowPpu = Sprite.Create(source, new Rect(2, 2, 2, 2), Vector2.zero, 50);
        definition.sprite = lowPpu;
        Check(Resolve(service, definition, out _).width == 4, "Former Image native PPU size lost");
        Set(service, "scaleWithScreenHeight", true);
        Set(service, "referenceScreenHeight", (float)Screen.height * 2);
        Check(Resolve(service, definition, out _).width == 2, "Resolution scaling lost");
        Set(service, "scaleWithScreenHeight", false);
        Set(service, "referenceScreenHeight", 1080f);
        UnityEngine.Object.Destroy(sprite);
        UnityEngine.Object.Destroy(lowPpu);
        UnityEngine.Object.Destroy(source);
    }

    private static void VerifyTheme(MouseCursorService service)
    {
        var theme = Resources.Load<MouseCursorTheme>("DefaultMouseCursorTheme");
        Check(theme != null, "Real project theme missing");
        int count = 0;
        foreach (MouseCursorDomain domain in Enum.GetValues(typeof(MouseCursorDomain)))
        foreach (MouseCursorVariant variant in Enum.GetValues(typeof(MouseCursorVariant)))
        {
            var definition = theme.GetDefinition(domain, variant);
            if (definition?.sprite == null) continue;
            Texture2D texture = Resolve(service, definition, out Vector2 hotspot);
            Check(texture.isReadable && texture.format == TextureFormat.RGBA32 && texture.mipmapCount == 1, "API texture format invalid");
            Check(Array.Exists(texture.GetPixels32(), pixel => pixel.a > 0), "Theme cursor is transparent");
            Check(hotspot.x >= 0 && hotspot.x < texture.width && hotspot.y >= 0 && hotspot.y < texture.height, "Theme hotspot invalid");
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            Debug.Log($"CURSOR_THEME {domain}/{variant} sprite={definition.sprite.name} size={texture.width}x{texture.height}");
            count++;
        }
        Check(count >= 15, "Theme sprite coverage unexpectedly low");
    }

    private static void VerifyEscapeRelease(MouseCursorService service)
    {
        Vector2 inside = new Vector2(10, 10);
        Call(service, "UpdateCursorReleaseIntent", true, true, false, inside);
        Call(service, "RefreshCursorConfinement", true);
        Check(Cursor.lockState == CursorLockMode.None, "Escape did not release cursor");
        for (int i = 0; i < 3; i++) Call(service, "RefreshCursorConfinement", true);
        service.NotifyDisplayConfigurationChanged();
        Call(service, "OnApplicationFocus", false);
        Call(service, "OnApplicationFocus", true);
        Check(Cursor.lockState == CursorLockMode.None, "Focus/display relocked released cursor");
        Call(service, "UpdateCursorReleaseIntent", true, false, true, new Vector2(-1, 10));
        Check(Get<bool>(service, "cursorReleasedByEscape"), "Outside click recaptured cursor");
        Call(service, "UpdateCursorReleaseIntent", false, false, true, inside);
        Check(Get<bool>(service, "cursorReleasedByEscape"), "Unfocused click recaptured cursor");
        Call(service, "UpdateCursorReleaseIntent", true, true, true, inside);
        Check(Get<bool>(service, "cursorReleasedByEscape"), "Click overrode same-frame Escape");
        Call(service, "UpdateCursorReleaseIntent", true, false, true, inside);
        Check(!Get<bool>(service, "cursorReleasedByEscape"), "Inside click did not recapture cursor");
        Call(service, "RefreshCursorConfinement", true);
        Debug.Log("CURSOR_ESCAPE_PASS: release persists across frames/focus/display; inside click recaptures");
    }

    private static void VerifyOwnership(MouseCursorService service)
    {
        var title = new GameObject("Title owner");
        var popup = new GameObject("Popup owner");
        service.SetDomain(title, MouseCursorDomain.SystemUi, 0);
        Call(service, "ApplyResolvedCursor");
        Check(service.CurrentDomain == MouseCursorDomain.SystemUi, "Title system cursor missing");
        service.SetDomain(popup, MouseCursorDomain.Inventory, 100);
        Call(service, "ApplyResolvedCursor");
        Check(service.CurrentDomain == MouseCursorDomain.Inventory, "Popup priority changed");
        service.ClearDomain(popup);
        Call(service, "ApplyResolvedCursor");
        Check(service.CurrentDomain == MouseCursorDomain.SystemUi, "Title cursor not restored");
        service.SetDragging(title, true);
        Call(service, "ApplyResolvedCursor");
        Check(service.CurrentVariant == MouseCursorVariant.Dragging, "Drag variant lost");
        service.SetDragging(title, false);
        service.SetHidden(title, true);
        Call(service, "ApplyResolvedCursor");
        Check(!Cursor.visible, "Hidden owner ignored");
        service.NotifyDisplayConfigurationChanged();
        Check(!Cursor.visible, "Display reset exposed hidden cursor");
        service.SetHidden(title, false);
        service.ClearDomain(title);
        Call(service, "ApplyResolvedCursor");
        Check(Cursor.visible && service.CurrentDomain == MouseCursorDomain.Combat, "Gameplay cursor not restored");
        Call(service, "OnApplicationFocus", true);
        Check(Get<bool>(service, "forceCursorTextureReapply"), "Focus did not invalidate cursor cache");
        service.enabled = false;
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Disable left cursor hidden/locked");
        service.enabled = true;
        Call(service, "ApplyResolvedCursor");
        Check(Cursor.visible && Get<Texture2D>(service, "appliedCursorTexture") != null, "Re-enable failed");
        UnityEngine.Object.Destroy(title);
        UnityEngine.Object.Destroy(popup);
    }
}
