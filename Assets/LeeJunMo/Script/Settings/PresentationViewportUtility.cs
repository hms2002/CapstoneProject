using UnityEngine;

// Responsibility: resolves the live presentation container and 16:9 viewport used by cameras and UI canvases.
public static class PresentationViewportUtility
{
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;
    public const float BasePresentationAspectRatio = 16f / 9f;

    private const float AspectRatioTolerance = 0.0001f;

    public static Rect FullViewportRect => new Rect(0f, 0f, 1f, 1f);

    public static bool ShouldBypassDisplayLetterboxForEditorPlayMode()
    {
#if UNITY_EDITOR
        return Application.isPlaying;
#else
        return false;
#endif
    }

    public static Vector2Int GetPresentationContainerSize(GameWindowMode windowMode)
    {
        return GetPresentationContainerSize(windowMode, DefaultWindowWidth, DefaultWindowHeight);
    }

    public static Vector2Int GetPresentationContainerSize(
        GameWindowMode windowMode,
        int windowedResolutionWidth,
        int windowedResolutionHeight)
    {
        if (Application.isPlaying && Screen.width > 0 && Screen.height > 0)
            return new Vector2Int(Screen.width, Screen.height);

        if (windowMode == GameWindowMode.Windowed)
        {
            int width = windowedResolutionWidth > 0 ? windowedResolutionWidth : DefaultWindowWidth;
            int height = windowedResolutionHeight > 0 ? windowedResolutionHeight : DefaultWindowHeight;
            return new Vector2Int(width, height);
        }

        Resolution currentResolution = Screen.currentResolution;
        if (currentResolution.width > 0 && currentResolution.height > 0)
            return new Vector2Int(currentResolution.width, currentResolution.height);

        Display mainDisplay = Display.main;
        if (mainDisplay != null && mainDisplay.systemWidth > 0 && mainDisplay.systemHeight > 0)
            return new Vector2Int(mainDisplay.systemWidth, mainDisplay.systemHeight);

        if (Screen.width > 0 && Screen.height > 0)
            return new Vector2Int(Screen.width, Screen.height);

        return new Vector2Int(DefaultWindowWidth, DefaultWindowHeight);
    }

    public static Rect CalculateViewportRect(
        int containerWidth,
        int containerHeight,
        float targetAspectRatio = BasePresentationAspectRatio)
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

    public static bool IsFullViewport(Rect viewportRect)
    {
        return Mathf.Approximately(viewportRect.x, 0f) &&
               Mathf.Approximately(viewportRect.y, 0f) &&
               Mathf.Approximately(viewportRect.width, 1f) &&
               Mathf.Approximately(viewportRect.height, 1f);
    }
}
