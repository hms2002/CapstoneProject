using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HoverUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;

    [Header("Global Positioning")]
    [SerializeField] private float offset = 12f;
    [SerializeField] private float edgePadding = 8f;
    [SerializeField] private bool followAnchor = true;

    [Header("Global Hide Timing")]
    [SerializeField] private bool delayHideOneFrame = true;
    [SerializeField] private float extraHideDelay = 0f;

    private IHoverView _currentView;
    private RectTransform _targetSlotRect;
    private bool _isHovering;

    private Coroutine _hideRoutine;
    private int _serial;

    private void Awake()
    {
        RefreshCanvasReference();
    }

    private void LateUpdate()
    {
        RefreshCanvasReference(_targetSlotRect, _currentView);

        if (!followAnchor || !_isHovering || _targetSlotRect == null || _currentView == null || !_currentView.IsActive)
            return;

        PositionNextToTarget(_targetSlotRect);
    }

    public void ShowHover(IHoverView view, RectTransform targetRect, object data, object context)
    {
        if (view == null || targetRect == null)
            return;

        if (!RefreshCanvasReference(targetRect, view))
            return;

        _serial++;
        _isHovering = true;
        _targetSlotRect = targetRect;

        CancelHide();

        if (_currentView != null && _currentView != view)
            _currentView.HideHover();

        _currentView = view;

        if (_currentView is MonoBehaviour currentViewBehaviour)
        {
            Canvas viewCanvas = currentViewBehaviour.GetComponent<Canvas>();
            if (viewCanvas != null)
                viewCanvas.sortingOrder = 999;
        }

        _currentView.ShowHover(data, context);
        PositionNextToTarget(_targetSlotRect);
    }

    public void HideHover(IHoverView view, RectTransform targetRect)
    {
        if (_currentView == view && _targetSlotRect == targetRect)
            _isHovering = false;

        TryScheduleHide();
    }

    public void HideImmediate()
    {
        CancelHide();
        _isHovering = false;
        _targetSlotRect = null;

        if (_currentView != null)
        {
            _currentView.HideHover();
            _currentView = null;
        }
    }

    public bool RefreshCanvasReference()
    {
        return RefreshCanvasReference(null, null);
    }

    private bool RefreshCanvasReference(RectTransform targetRect, IHoverView view)
    {
        Canvas resolvedCanvas = null;

        if (targetRect != null)
            resolvedCanvas = targetRect.GetComponentInParent<Canvas>();

        if (resolvedCanvas == null && view is MonoBehaviour viewBehaviour && viewBehaviour != null)
            resolvedCanvas = viewBehaviour.GetComponentInParent<Canvas>();

        if (resolvedCanvas == null && canvas != null)
            resolvedCanvas = canvas;

        if (resolvedCanvas == null)
            resolvedCanvas = GetComponentInParent<Canvas>();

        if (resolvedCanvas == null)
            resolvedCanvas = FindFirstObjectByType<Canvas>();

        canvas = resolvedCanvas != null ? resolvedCanvas.rootCanvas : null;
        return canvas != null;
    }

    private void TryScheduleHide()
    {
        if (_isHovering)
            return;

        if (_currentView == null || !_currentView.IsActive)
            return;

        if (_hideRoutine != null)
            return;

        int mySerial = _serial;
        _hideRoutine = StartCoroutine(CoHideIfStillNotHover(mySerial));
    }

    private IEnumerator CoHideIfStillNotHover(int serialAtStart)
    {
        if (delayHideOneFrame)
            yield return null;

        if (extraHideDelay > 0f)
            yield return new WaitForSecondsRealtime(extraHideDelay);

        if (_serial != serialAtStart)
            yield break;

        if (_isHovering)
            yield break;

        HideImmediate();
    }

    private void CancelHide()
    {
        if (_hideRoutine == null)
            return;

        StopCoroutine(_hideRoutine);
        _hideRoutine = null;
    }

    private void PositionNextToTarget(RectTransform targetRect)
    {
        if (!RefreshCanvasReference(targetRect, _currentView))
            return;

        if (canvas == null || _currentView == null || targetRect == null)
            return;

        RectTransform viewRect = _currentView.Rect;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewRect);

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Rect targetLocalRect = GetLocalRect(targetRect, canvasRect, cam);
        Rect canvasLocalRect = canvasRect.rect;

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);

        Vector2 rightTopScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 rightBottomScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
        Vector2 leftTopScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
        Vector2 leftBottomScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);

        Vector2 centerRightScreen = (rightTopScreen + rightBottomScreen) * 0.5f;
        Vector2 centerLeftScreen = (leftTopScreen + leftBottomScreen) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerRightScreen, cam, out Vector2 rightLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerLeftScreen, cam, out Vector2 leftLocal);

        Vector2 size = viewRect.rect.size;
        Vector2 pivot = viewRect.pivot;

        float rightLeftEdge = rightLocal.x + offset;
        Vector2 posRight = new Vector2(rightLeftEdge + pivot.x * size.x, rightLocal.y);

        float leftRightEdge = leftLocal.x - offset;
        Vector2 posLeft = new Vector2(leftRightEdge - (1f - pivot.x) * size.x, leftLocal.y);

        Vector2 chosen = ChoosePosAvoidOverlap(canvasLocalRect, targetLocalRect, size, pivot, posRight, posLeft);

        float chosenX = chosen.x;
        float y = ChooseYAlignSlot(canvasLocalRect, targetLocalRect, size, pivot, chosenX);
        viewRect.anchoredPosition = new Vector2(chosenX, y);
    }

    private Vector2 ChoosePosAvoidOverlap(Rect canvasLocalRect, Rect targetRect, Vector2 panelSize, Vector2 pivot, Vector2 rightPos, Vector2 leftPos)
    {
        Rect targetExpanded = Expand(targetRect, 6f);
        Vector2 rightClamped = ClampToCanvas(canvasLocalRect, rightPos, panelSize, pivot);
        Vector2 leftClamped = ClampToCanvas(canvasLocalRect, leftPos, panelSize, pivot);
        Rect rightPanel = PanelRectAt(rightClamped, panelSize, pivot);
        Rect leftPanel = PanelRectAt(leftClamped, panelSize, pivot);

        bool rightOverlap = Intersects(targetExpanded, rightPanel);
        bool leftOverlap = Intersects(targetExpanded, leftPanel);

        if (!rightOverlap && leftOverlap)
            return rightClamped;

        if (!leftOverlap && rightOverlap)
            return leftClamped;

        if (!rightOverlap && !leftOverlap)
            return rightClamped;

        float rightArea = OverlapArea(targetExpanded, rightPanel);
        float leftArea = OverlapArea(targetExpanded, leftPanel);

        return rightArea <= leftArea ? rightClamped : leftClamped;
    }

    private float OverlapArea(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        float w = xMax - xMin;
        float h = yMax - yMin;

        if (w <= 0f || h <= 0f)
            return 0f;

        return w * h;
    }

    private Rect GetLocalRect(RectTransform rectTransform, RectTransform canvasRect, Camera cam)
    {
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);
        Vector2 p0 = ScreenToCanvasLocal(worldCorners[0], canvasRect, cam);
        Vector2 p2 = ScreenToCanvasLocal(worldCorners[2], canvasRect, cam);
        float xMin = Mathf.Min(p0.x, p2.x);
        float xMax = Mathf.Max(p0.x, p2.x);
        float yMin = Mathf.Min(p0.y, p2.y);
        float yMax = Mathf.Max(p0.y, p2.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private Vector2 ScreenToCanvasLocal(Vector3 world, RectTransform canvasRect, Camera cam)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }

    private Rect PanelRectAt(Vector2 pos, Vector2 size, Vector2 pivot)
    {
        float left = pos.x - pivot.x * size.x;
        float bottom = pos.y - pivot.y * size.y;
        return new Rect(left, bottom, size.x, size.y);
    }

    private bool Intersects(Rect a, Rect b)
    {
        return a.Overlaps(b);
    }

    private Rect Expand(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    private Vector2 ClampToCanvas(Rect canvasRect, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        float minX = canvasRect.xMin + edgePadding + pivot.x * size.x;
        float maxX = canvasRect.xMax - edgePadding - (1f - pivot.x) * size.x;
        float minY = canvasRect.yMin + edgePadding + pivot.y * size.y;
        float maxY = canvasRect.yMax - edgePadding - (1f - pivot.y) * size.y;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        return pos;
    }

    private float ChooseYAlignSlot(Rect canvasRect, Rect targetRect, Vector2 panelSize, Vector2 panelPivot, float xFixed)
    {
        float targetTop = targetRect.yMax - targetRect.height / 2f;
        float targetBottom = targetRect.yMin + targetRect.height / 2f;
        float yTopAlign = targetTop - (1f - panelPivot.y) * panelSize.y;
        float yBottomAlign = targetBottom + panelPivot.y * panelSize.y;

        float yTopClamped = ClampY(canvasRect, panelSize, panelPivot, yTopAlign);
        float yBottomClamped = ClampY(canvasRect, panelSize, panelPivot, yBottomAlign);

        Rect targetExpanded = Expand(targetRect, 4f);
        Rect panelTopRect = PanelRectAt(new Vector2(xFixed, yTopClamped), panelSize, panelPivot);
        Rect panelBottomRect = PanelRectAt(new Vector2(xFixed, yBottomClamped), panelSize, panelPivot);

        float topOverlap = OverlapArea(targetExpanded, panelTopRect);
        float bottomOverlap = OverlapArea(targetExpanded, panelBottomRect);

        if (topOverlap < bottomOverlap)
            return yTopClamped;

        if (bottomOverlap < topOverlap)
            return yBottomClamped;

        return yTopClamped;
    }

    private float ClampY(Rect canvasRect, Vector2 size, Vector2 pivot, float y)
    {
        float minY = canvasRect.yMin + edgePadding + pivot.y * size.y;
        float maxY = canvasRect.yMax - edgePadding - (1f - pivot.y) * size.y;
        return Mathf.Clamp(y, minY, maxY);
    }
}
