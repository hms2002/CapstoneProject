using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIHoverManager : MonoBehaviour
{
    public static UIHoverManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private ItemDetailPanel detailPanel;
    [SerializeField] private RectTransform detailPanelRect;

    [Header("Positioning")]
    [SerializeField] private float offset = 12f;
    [SerializeField] private float edgePadding = 8f;
    [SerializeField] private bool followAnchor = true;

    [Header("Hide Timing")]
    [SerializeField] private bool delayHideOneFrame = true;
    [SerializeField] private float extraHideDelay = 0f;

    private RectTransform _currentSlotRect;
    private ScriptableObject _currentItem;
    private bool _hoverSlot;

    private Coroutine _hideRoutine;
    private int _serial;

    [SerializeField] private PlayerDetailContextProvider contextProviderBehaviour;
    private IItemDetailContextProvider _contextProvider;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (detailPanel == null) detailPanel = FindFirstObjectByType<ItemDetailPanel>();
        if (detailPanelRect == null && detailPanel != null) detailPanelRect = detailPanel.transform as RectTransform;

        _contextProvider = contextProviderBehaviour as IItemDetailContextProvider;
    }

    private void LateUpdate()
    {
        if (!followAnchor) return;
        if (!_hoverSlot) return;
        if (_currentSlotRect == null) return;
        if (detailPanel == null || detailPanelRect == null) return;
        if (!detailPanel.gameObject.activeSelf) return;

        PositionNextToSlot(_currentSlotRect);
    }

    public void HoverSlot(RectTransform slotRect, ScriptableObject itemDef, IItemContainer container = null, int index = -1)
    {
        _serial++;
        _hoverSlot = true;
        _currentSlotRect = slotRect;
        _currentItem = itemDef;

        CancelHide();

        if (itemDef == null)
        {
            HideImmediate();
            return;
        }

        var ctx = _contextProvider != null ? _contextProvider.BuildContext() : null;
        if (ctx != null)
        {
            ctx.sourceContainer = container;
            ctx.sourceIndex = index;

            if (itemDef is RelicDefinition && container is IRelicLevelProvider p && index >= 0)
            {
                if (p.TryGetRelicLevel(index, out var lvl))
                    ctx.relicLevelOverride = lvl;
            }
        }

        detailPanel?.Show(itemDef, ctx);

        if (detailPanelRect == null && detailPanel != null) detailPanelRect = detailPanel.transform as RectTransform;
        PositionNextToSlot(slotRect);
    }

    public void UnhoverSlot(RectTransform slotRect)
    {
        // 지금 보고 있던 슬롯에서 나간 경우만 hover 해제
        if (_currentSlotRect == slotRect)
            _hoverSlot = false;

        TryScheduleHide();
    }

    public void HideImmediate()
    {
        CancelHide();
        _hoverSlot = false;
        _currentSlotRect = null;
        _currentItem = null;
        detailPanel?.Hide();
    }

    private void TryScheduleHide()
    {
        if (_hoverSlot) return;
        if (detailPanel == null || !detailPanel.gameObject.activeSelf) return;

        // 이미 예약돼 있으면 또 걸지 않음
        if (_hideRoutine != null) return;

        int mySerial = _serial;
        _hideRoutine = StartCoroutine(CoHideIfStillNotHover(mySerial));
        Debug.Log("코루틴 활성화 했지롱 ㅋㅋ : " +_hideRoutine);
    }

    private IEnumerator CoHideIfStillNotHover(int serialAtStart)
    {
        if (delayHideOneFrame) yield return null;
            Debug.Log("곧 끌거임!!");
        if (extraHideDelay > 0f)
            yield return new WaitForSecondsRealtime(extraHideDelay);

        // 다른 슬롯에 Enter가 발생했으면 취소
        if (_serial != serialAtStart) { Debug.Log("작전 변경이다. _serial != serialAtStart"); yield break; }
        if (_hoverSlot)
        {
            Debug.Log("작전 변경이다. _serial != _hoverSlot"); yield break;
        }

            Debug.Log("끔!!!!");
        HideImmediate();
    }

    private void CancelHide()
    {
        if (_hideRoutine != null)
        {
            Debug.Log("끊지 마!");
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }

    // PositionNextToSlot 이하(레이아웃/클램프/겹침 방지)는 너 기존 코드 그대로 유지


    private void PositionNextToSlot(RectTransform slotRect)
    {
        if (canvas == null || detailPanelRect == null || slotRect == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(detailPanelRect);

        var canvasRect = canvas.transform as RectTransform;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // 1) 슬롯/캔버스 로컬 Rect 구하기
        Rect slotLocalRect = GetLocalRect(slotRect, canvasRect, cam);
        Rect canvasLocalRect = canvasRect.rect;

        // 2) 슬롯 오른쪽/왼쪽 중심점(로컬)
        Vector3[] c = new Vector3[4];
        slotRect.GetWorldCorners(c); // 0 LB,1 LT,2 RT,3 RB

        Vector2 rt = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        Vector2 rb = RectTransformUtility.WorldToScreenPoint(cam, c[3]);
        Vector2 lt = RectTransformUtility.WorldToScreenPoint(cam, c[1]);
        Vector2 lb = RectTransformUtility.WorldToScreenPoint(cam, c[0]);

        Vector2 centerRightScreen = (rt + rb) * 0.5f;
        Vector2 centerLeftScreen = (lt + lb) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerRightScreen, cam, out var rightLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerLeftScreen, cam, out var leftLocal);

        Vector2 size = detailPanelRect.rect.size;
        Vector2 pivot = detailPanelRect.pivot;

        // 3) 후보 위치(패널 pivot 기준 anchoredPosition)
        // 오른쪽: 패널의 left edge가 (rightLocal.x + offset) 되도록
        float rightLeftEdge = rightLocal.x + offset;
        Vector2 posRight = new Vector2(rightLeftEdge + pivot.x * size.x, rightLocal.y);

        // 왼쪽: 패널의 right edge가 (leftLocal.x - offset) 되도록
        float leftRightEdge = leftLocal.x - offset;
        Vector2 posLeft = new Vector2(leftRightEdge - (1f - pivot.x) * size.x, leftLocal.y);

        // 4) “겹치지 않는” 후보 선택
        Vector2 chosen = ChoosePosAvoidOverlap(canvasLocalRect, slotLocalRect, size, pivot, posRight, posLeft);

        float chosenX = chosen.x;
        float y = ChooseYAlignSlot(canvasLocalRect, slotLocalRect, size, pivot, chosenX);
        detailPanelRect.anchoredPosition = new Vector2(chosenX, y);
    }


    /// <summary>
    ///  ChoosePosAvoidOverlap_ClampFirst
    /// </summary>
    /// <param name="canvasLocalRect"></param>
    /// <param name="slotRect"></param>
    /// <param name="panelSize"></param>
    /// <param name="pivot"></param>
    /// <param name="rightPos"></param>
    /// <param name="leftPos"></param>
    /// <returns></returns>
    private Vector2 ChoosePosAvoidOverlap(
    Rect canvasLocalRect, Rect slotRect, Vector2 panelSize, Vector2 pivot,
    Vector2 rightPos, Vector2 leftPos)
    {
        Rect slotExpanded = Expand(slotRect, 6f);

        // ✅ 1) 먼저 Clamp
        Vector2 rightClamped = ClampToCanvas(canvasLocalRect, rightPos, panelSize, pivot);
        Vector2 leftClamped = ClampToCanvas(canvasLocalRect, leftPos, panelSize, pivot);

        // ✅ 2) Clamp된 위치로 실제 패널 Rect 계산
        Rect rightPanel = PanelRectAt(rightClamped, panelSize, pivot);
        Rect leftPanel = PanelRectAt(leftClamped, panelSize, pivot);

        // ✅ 3) 겹침 검사도 Clamp된 걸로
        bool rightOverlap = Intersects(slotExpanded, rightPanel);
        bool leftOverlap = Intersects(slotExpanded, leftPanel);

        // ✅ 4) 선택 규칙
        if (!rightOverlap && leftOverlap) return rightClamped;
        if (!leftOverlap && rightOverlap) return leftClamped;
        if (!rightOverlap && !leftOverlap) return rightClamped; // 기본 우측 선호

        // 둘 다 겹치면: "덜 겹치는" 쪽(겹침 면적 작은 쪽) 선택
        float rightArea = OverlapArea(slotExpanded, rightPanel);
        float leftArea = OverlapArea(slotExpanded, leftPanel);

        return (rightArea <= leftArea) ? rightClamped : leftClamped;
    }
    private float OverlapArea(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        float w = xMax - xMin;
        float h = yMax - yMin;
        if (w <= 0f || h <= 0f) return 0f;
        return w * h;
    }
    private Rect GetLocalRect(RectTransform rt, RectTransform canvasRect, Camera cam)
    {
        Vector3[] w = new Vector3[4];
        rt.GetWorldCorners(w);

        Vector2 p0 = ScreenToCanvasLocal(w[0], canvasRect, cam);
        Vector2 p2 = ScreenToCanvasLocal(w[2], canvasRect, cam);

        float xMin = Mathf.Min(p0.x, p2.x);
        float xMax = Mathf.Max(p0.x, p2.x);
        float yMin = Mathf.Min(p0.y, p2.y);
        float yMax = Mathf.Max(p0.y, p2.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }


    private Vector2 ScreenToCanvasLocal(Vector3 world, RectTransform canvasRect, Camera cam)
    {
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, cam, out var lp);
        return lp;
    }

    private Rect PanelRectAt(Vector2 pos, Vector2 size, Vector2 pivot)
    {
        float left = pos.x - pivot.x * size.x;
        float bottom = pos.y - pivot.y * size.y;
        return new Rect(left, bottom, size.x, size.y);
    }

    private bool Intersects(Rect a, Rect b) => a.Overlaps(b);

    private Rect Expand(Rect r, float amount)
    {
        return new Rect(r.xMin - amount, r.yMin - amount, r.width + amount * 2f, r.height + amount * 2f);
    }


    private bool FitsInCanvas(Rect canvasRect, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        float left = pos.x - pivot.x * size.x;
        float right = left + size.x;
        float bottom = pos.y - pivot.y * size.y;
        float top = bottom + size.y;

        return left >= canvasRect.xMin + edgePadding &&
               right <= canvasRect.xMax - edgePadding &&
               bottom >= canvasRect.yMin + edgePadding &&
               top <= canvasRect.yMax - edgePadding;
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

    private float ChooseYAlignSlot(Rect canvasRect, Rect slotRect, Vector2 panelSize, Vector2 panelPivot, float xFixed)
    {
        float slotTop = slotRect.yMax - slotRect.height / 2;
        float slotBottom = slotRect.yMin + slotRect.height / 2;

        // 후보 Y (top align / bottom align)
        float yTopAlign = slotTop - (1f - panelPivot.y) * panelSize.y;
        float yBottomAlign = slotBottom + panelPivot.y * panelSize.y;

        // 1) 각각 clamp
        float yTopClamped = ClampY(canvasRect, panelSize, panelPivot, yTopAlign);
        float yBottomClamped = ClampY(canvasRect, panelSize, panelPivot, yBottomAlign);

        // 2) 겹침 면적 비교(슬롯을 덜 가리는 쪽 선택)
        Rect slotExpanded = Expand(slotRect, 4f);

        Rect panelTopRect = PanelRectAt(new Vector2(xFixed, yTopClamped), panelSize, panelPivot);
        Rect panelBotRect = PanelRectAt(new Vector2(xFixed, yBottomClamped), panelSize, panelPivot);

        float topOverlap = OverlapArea(slotExpanded, panelTopRect);
        float botOverlap = OverlapArea(slotExpanded, panelBotRect);

        if (topOverlap < botOverlap) return yTopClamped;
        if (botOverlap < topOverlap) return yBottomClamped;

        // 3) 겹침이 같으면 “원래 의도”를 우선(보통 top align이 보기 좋음)
        return yTopClamped;
    }

    private float ClampY(Rect canvasRect, Vector2 size, Vector2 pivot, float y)
    {
        float minY = canvasRect.yMin + edgePadding + pivot.y * size.y;
        float maxY = canvasRect.yMax - edgePadding - (1f - pivot.y) * size.y;
        return Mathf.Clamp(y, minY, maxY);
    }
}
