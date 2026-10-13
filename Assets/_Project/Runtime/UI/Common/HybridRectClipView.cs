using UnityEngine;
using UnityEngine.UI;

// Keeps the authored stencil during panel rotation, using rectangular clipping only
// while its actual viewport corners align with the root canvas.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class HybridRectClipView : MonoBehaviour
{
    [SerializeField] private Mask originalMask;
    [SerializeField] private RectMask2D rectMask;
    [SerializeField] private Image maskImage;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Color originalImageColor = Color.white;
    [SerializeField] private bool originalImageEnabled = true;
    [SerializeField] private bool originalMaskEnabled = true;
    [SerializeField] private bool originalCullTransparentMesh = true;

    private readonly Vector3[] corners = new Vector3[4];
    private bool configured;
    private bool usingRectClip;
    private bool hasAppliedState;
    private const float AlignmentTolerance = 0.0001f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DiagnosticUsesRectClip => usingRectClip;
    public int DiagnosticStateChangeCount { get; private set; }
#endif

    private void OnEnable()
    {
        configured = originalMask != null && rectMask != null && maskImage != null &&
            viewport != null && rootCanvas != null;
        hasAppliedState = false;
        if (configured) ApplyClipState(IsAxisAligned());
    }

    private void LateUpdate()
    {
        if (configured) ApplyClipState(IsAxisAligned());
    }

    private bool IsAxisAligned()
    {
        viewport.GetWorldCorners(corners);
        Transform canvasTransform = rootCanvas.transform;
        for (int i = 0; i < corners.Length; i++)
            corners[i] = canvasTransform.InverseTransformPoint(corners[i]);
        if (!IsFinite(corners[0]) || !IsFinite(corners[1]) || !IsFinite(corners[2]) || !IsFinite(corners[3]))
            return false;
        // This is the same corner pair that uGUI uses to construct its ClipRect.
        // Reflected, collapsed, tilted and skewed viewports retain the stencil.
        if (corners[2].x - corners[0].x <= AlignmentTolerance ||
            corners[2].y - corners[0].y <= AlignmentTolerance)
            return false;
        return Near(corners[0].x, corners[1].x) && Near(corners[1].y, corners[2].y) &&
            Near(corners[2].x, corners[3].x) && Near(corners[3].y, corners[0].y) &&
            Near(corners[0].z, corners[1].z) && Near(corners[0].z, corners[2].z) && Near(corners[0].z, corners[3].z);
    }

    private static bool Near(float first, float second) => Mathf.Abs(first - second) <= AlignmentTolerance;
    private static bool IsFinite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);

    private void ApplyClipState(bool useRect)
    {
        if (hasAppliedState && usingRectClip == useRect) return;
        usingRectClip = useRect;
        hasAppliedState = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticStateChangeCount++;
#endif
        if (useRect)
        {
            originalMask.enabled = false;
            Color hidden = originalImageColor;
            hidden.a = 0f;
            maskImage.color = hidden;
            maskImage.enabled = originalImageEnabled;
            // A transparent graphic still supplies the authored empty-viewport hit
            // surface when raycastTarget is set. Culling it would remove that hit.
            maskImage.canvasRenderer.cullTransparentMesh = maskImage.raycastTarget
                ? false : originalCullTransparentMesh;
            rectMask.enabled = true;
        }
        else
        {
            RestoreOriginalMask();
            // Lifecycle restoration invalidates the cache; applying the stencil
            // as the current live state must keep it cached between frames.
            hasAppliedState = true;
            usingRectClip = false;
        }
    }

    private void RestoreOriginalMask()
    {
        if (rectMask != null) rectMask.enabled = false;
        if (maskImage != null)
        {
            maskImage.color = originalImageColor;
            maskImage.enabled = originalImageEnabled;
            maskImage.canvasRenderer.cullTransparentMesh = originalCullTransparentMesh;
        }
        if (originalMask != null) originalMask.enabled = originalMaskEnabled;
        usingRectClip = false;
        hasAppliedState = false;
    }

    private void OnDisable() => RestoreOriginalMask();
    private void OnDestroy()
    {
        if (hasAppliedState || usingRectClip) RestoreOriginalMask();
    }

#if UNITY_EDITOR
    // Called by the approved authoring tool or an isolated regression fixture.
    // References are authored; the view never creates a component or hierarchy.
    public void ConfigureForAuthoring(Mask stencil, RectMask2D rectangle, Image image,
        RectTransform clipViewport, Canvas clipCanvas)
    {
        originalMask = stencil;
        rectMask = rectangle;
        maskImage = image;
        viewport = clipViewport;
        rootCanvas = clipCanvas;
        originalImageColor = image.color;
        originalImageEnabled = image.enabled;
        originalMaskEnabled = stencil.enabled;
        originalCullTransparentMesh = image.canvasRenderer.cullTransparentMesh;
    }
#endif
}
