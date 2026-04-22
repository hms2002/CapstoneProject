using UnityEngine;

[DisallowMultipleComponent]
public sealed class PresentationCanvasAdapter : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField, Min(0.01f)] private float targetAspectRatio = PresentationViewportUtility.BasePresentationAspectRatio;

    private RenderMode baseRenderMode;
    private Camera baseWorldCamera;
    private float basePlaneDistance;
    private bool hasBaseCanvasState;
    private int lastContainerWidth = -1;
    private int lastContainerHeight = -1;
    private int lastPresentationCameraInstanceId = -1;
    private GameWindowMode lastWindowMode = (GameWindowMode)(-1);
    private Rect lastViewportRect = new Rect(-1f, -1f, -1f, -1f);

    private void Reset()
    {
        targetCanvas = GetComponent<Canvas>();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseCanvasState();
    }

    private void OnEnable()
    {
        ApplyNow(true);
    }

    private void LateUpdate()
    {
        ApplyNow(false);
    }

    public void ApplyNow(bool force = false)
    {
        ResolveReferences();
        if (targetCanvas == null)
            return;

        CaptureBaseCanvasState();

        GameWindowMode windowMode = GameSettingsService.Instance != null
            ? GameSettingsService.Instance.CurrentWindowMode
            : GameWindowMode.Windowed;

        Vector2Int containerSize = PresentationViewportUtility.GetPresentationContainerSize(windowMode);
        Rect viewportRect = PresentationViewportUtility.CalculateViewportRect(
            containerSize.x,
            containerSize.y,
            targetAspectRatio);

        Camera presentationCamera = ResolvePresentationCamera();
        int cameraInstanceId = presentationCamera != null ? presentationCamera.GetInstanceID() : 0;
        if (!force &&
            lastContainerWidth == containerSize.x &&
            lastContainerHeight == containerSize.y &&
            lastWindowMode == windowMode &&
            lastPresentationCameraInstanceId == cameraInstanceId &&
            AreViewportsEqual(lastViewportRect, viewportRect))
        {
            return;
        }

        ApplyCanvasPresentation(viewportRect, presentationCamera);

        lastContainerWidth = containerSize.x;
        lastContainerHeight = containerSize.y;
        lastWindowMode = windowMode;
        lastPresentationCameraInstanceId = cameraInstanceId;
        lastViewportRect = viewportRect;
    }

    private static bool AreViewportsEqual(Rect left, Rect right)
    {
        return Mathf.Approximately(left.x, right.x) &&
               Mathf.Approximately(left.y, right.y) &&
               Mathf.Approximately(left.width, right.width) &&
               Mathf.Approximately(left.height, right.height);
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();
    }

    private void CaptureBaseCanvasState()
    {
        if (hasBaseCanvasState || targetCanvas == null)
            return;

        baseRenderMode = targetCanvas.renderMode;
        baseWorldCamera = targetCanvas.worldCamera;
        basePlaneDistance = targetCanvas.planeDistance;
        hasBaseCanvasState = true;
    }

    private void ApplyCanvasPresentation(Rect viewportRect, Camera presentationCamera)
    {
        if (targetCanvas == null)
            return;

        bool useFullScreen = PresentationViewportUtility.IsFullViewport(viewportRect);
        if (!useFullScreen && presentationCamera != null)
        {
            targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            targetCanvas.worldCamera = presentationCamera;
            targetCanvas.planeDistance = Mathf.Max(1f, basePlaneDistance);
            return;
        }

        targetCanvas.renderMode = baseRenderMode;
        targetCanvas.worldCamera = baseWorldCamera;
        targetCanvas.planeDistance = basePlaneDistance;
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
}
