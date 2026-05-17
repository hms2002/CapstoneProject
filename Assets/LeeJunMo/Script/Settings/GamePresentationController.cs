using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GamePresentationController : MonoBehaviour
{
    private const int LetterboxSortingOrder = 32767;

    private static readonly GlobalCanvasLayer[] UiPresentationLayers =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.Dialogue,
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
        GlobalCanvasLayer.GameOver,
    };

    private readonly Dictionary<Canvas, RenderMode> baseCanvasRenderModes = new();
    private readonly Dictionary<Canvas, Camera> baseCanvasWorldCameras = new();
    private readonly Dictionary<Canvas, float> baseCanvasPlaneDistances = new();

    private Canvas letterboxCanvas;
    private RectTransform letterboxRoot;
    private Image topLetterboxBar;
    private Image bottomLetterboxBar;
    private Image leftLetterboxBar;
    private Image rightLetterboxBar;
    private int lastContainerWidth = -1;
    private int lastContainerHeight = -1;
    private GameWindowMode lastWindowMode = (GameWindowMode)(-1);
    private int lastResolutionWidth = -1;
    private int lastResolutionHeight = -1;
    private int lastPresentationCameraInstanceId = -1;

    public void RefreshIfNeeded(GameWindowMode windowMode, int resolutionWidth, int resolutionHeight)
    {
        Camera presentationCamera = ResolvePresentationCamera();
        int presentationCameraInstanceId = presentationCamera != null ? presentationCamera.GetInstanceID() : 0;
        Vector2Int containerSize = PresentationViewportUtility.GetPresentationContainerSize(windowMode);
        if (lastContainerWidth == containerSize.x &&
            lastContainerHeight == containerSize.y &&
            lastWindowMode == windowMode &&
            lastResolutionWidth == resolutionWidth &&
            lastResolutionHeight == resolutionHeight &&
            lastPresentationCameraInstanceId == presentationCameraInstanceId)
            return;

        ApplyPresentation(windowMode, resolutionWidth, resolutionHeight);
    }

    public void ApplyPresentation(GameWindowMode windowMode, int resolutionWidth, int resolutionHeight)
    {
        Camera presentationCamera = ResolvePresentationCamera();
        Vector2Int containerSize = PresentationViewportUtility.GetPresentationContainerSize(windowMode);
        Rect viewportRect = PresentationViewportUtility.CalculateViewportRect(containerSize.x, containerSize.y);
        ApplyCameraViewport(viewportRect);
        ApplyUiCanvasPresentation(viewportRect, presentationCamera);
        ApplyLetterboxOverlay(viewportRect);
        Canvas.ForceUpdateCanvases();
        lastContainerWidth = containerSize.x;
        lastContainerHeight = containerSize.y;
        lastWindowMode = windowMode;
        lastResolutionWidth = resolutionWidth;
        lastResolutionHeight = resolutionHeight;
        lastPresentationCameraInstanceId = presentationCamera != null ? presentationCamera.GetInstanceID() : 0;
    }

    private static Camera ResolvePresentationCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            return mainCamera;

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.targetTexture != null)
                continue;

            return candidate;
        }

        return null;
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

    private void ApplyUiCanvasPresentation(Rect viewportRect, Camera presentationCamera)
    {
        bool useFullScreen = PresentationViewportUtility.IsFullViewport(viewportRect);

        for (int i = 0; i < UiPresentationLayers.Length; i++)
        {
            Canvas canvas = GlobalUIRoot.GetCanvas(UiPresentationLayers[i]);
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

        bool useFullScreen = PresentationViewportUtility.IsFullViewport(viewportRect);

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
        scaler.referenceResolution = new Vector2(
            PresentationViewportUtility.DefaultWindowWidth,
            PresentationViewportUtility.DefaultWindowHeight);
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
}
