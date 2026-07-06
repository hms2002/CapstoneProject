using UnityEngine;

/// <summary>
/// 책임 : RunSpecialNpc 선택지 UI RectTransform을 월드 대상 위치에 맞춰 canvas 좌표로 따라가게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunSpecialNpcChoiceAnchorFollower : MonoBehaviour, IRunSpecialNpcChoiceAnchorFollower
{
    [Header("References")]
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new(0f, 1.6f, 0f);
    [SerializeField] private Vector2 canvasOffset = new(0f, 28f);
    [SerializeField] private bool clampToCanvas = true;
    [SerializeField] private Vector2 clampPadding = new(32f, 32f);
    [SerializeField] private bool hideWhenBehindCamera = true;

    private Transform followTarget;
    private RectTransform canvasRect;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        UpdatePosition();
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        UpdatePosition();
    }

    public void ClearFollowTarget()
    {
        followTarget = null;
    }

    private void ResolveReferences()
    {
        if (targetRect == null)
            targetRect = transform as RectTransform;

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>(includeInactive: true);

        if (targetCanvas != null)
            canvasRect = targetCanvas.transform as RectTransform;
    }

    private void UpdatePosition()
    {
        if (followTarget == null)
            return;

        ResolveReferences();
        if (targetRect == null || canvasRect == null)
            return;

        Camera resolvedWorldCamera = worldCamera != null ? worldCamera : Camera.main;
        Vector3 worldPoint = followTarget.position + worldOffset;
        Vector3 screenPoint = resolvedWorldCamera != null
            ? resolvedWorldCamera.WorldToScreenPoint(worldPoint)
            : RectTransformUtility.WorldToScreenPoint(null, worldPoint);

        bool visible = !hideWhenBehindCamera || screenPoint.z >= 0f;
        if (!visible)
            return;

        Camera uiCamera = ResolveUiCamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            return;

        Vector2 anchoredPosition = localPoint + canvasOffset;
        if (clampToCanvas)
            anchoredPosition = ClampToCanvas(anchoredPosition);

        targetRect.anchoredPosition = anchoredPosition;
    }

    private Camera ResolveUiCamera()
    {
        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return targetCanvas.worldCamera;
    }

    private Vector2 ClampToCanvas(Vector2 anchoredPosition)
    {
        Rect rect = canvasRect.rect;
        Vector2 padding = Vector2.Max(Vector2.zero, clampPadding);

        float minX = rect.xMin + padding.x;
        float maxX = rect.xMax - padding.x;
        float minY = rect.yMin + padding.y;
        float maxY = rect.yMax - padding.y;

        if (minX > maxX)
        {
            float centerX = (rect.xMin + rect.xMax) * 0.5f;
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = (rect.yMin + rect.yMax) * 0.5f;
            minY = centerY;
            maxY = centerY;
        }

        return new Vector2(
            Mathf.Clamp(anchoredPosition.x, minX, maxX),
            Mathf.Clamp(anchoredPosition.y, minY, maxY));
    }
}
