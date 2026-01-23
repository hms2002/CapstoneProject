using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IWeaponContainer
{
    int SlotCount { get; }
    event Action OnChanged;

    WeaponDefinition Get(int index);

    /// <summary>
    /// 해당 슬롯에 weapon을 놓을 수 있는지(중복/규칙 체크).
    /// ignoreIndex는 “내부 구현에서 필요한 경우”만 쓰고 기본은 -1.
    /// </summary>
    bool CanPlace(WeaponDefinition weapon, int index, int ignoreIndex = -1);

    bool TrySet(int index, WeaponDefinition weapon);
    bool TrySwap(int a, int b);
}

public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;

    private IWeaponContainer container;
    private int index;

    private void OnDisable()
    {
        if (container != null)
            container.OnChanged -= Refresh;
    }

    public void Bind(IWeaponContainer container, int index)
    {
        // 기존 구독 해제
        if (this.container != null)
            this.container.OnChanged -= Refresh;

        this.container = container;
        this.index = index;

        if (this.container != null)
            this.container.OnChanged += Refresh;

        Refresh();
    }

    public void Refresh()
    {
        if (container == null || icon == null) return;

        var w = container.Get(index);
        if (w == null)
        {
            icon.enabled = false;
            icon.sprite = null;
        }
        else
        {
            icon.enabled = true;
            icon.sprite = w.icon;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (container == null) return;

        var w = container.Get(index);
        if (w == null) return;

        WeaponDragContext.Begin(container, index, w);

        DragIcon.Instance?.Show(w.icon);
        DragIcon.Instance?.Follow(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!WeaponDragContext.Active) return;
        DragIcon.Instance?.Follow(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragIcon.Instance?.Hide();
        WeaponDragContext.Clear();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (container == null) return;
        if (!WeaponDragContext.Active) return;

        // source -> target 처리 (Swap 방식)
        WeaponDragContext.TryDrop(container, index);
    }
}

// -----------------------
// Drag Context (Swap support)
// -----------------------
public static class WeaponDragContext
{
    public static bool Active => Source != null && Item != null;

    public static IWeaponContainer Source { get; private set; }
    public static int SourceIndex { get; private set; } = -1;
    public static WeaponDefinition Item { get; private set; }

    public static void Begin(IWeaponContainer src, int srcIndex, WeaponDefinition item)
    {
        Source = src;
        SourceIndex = srcIndex;
        Item = item;
    }

    public static void Clear()
    {
        Source = null;
        SourceIndex = -1;
        Item = null;
    }

    public static void TryDrop(IWeaponContainer target, int targetIndex)
    {
        if (!Active) return;
        if (target == null) return;

        // 같은 슬롯에 드롭 = 무시
        if (Source == target && SourceIndex == targetIndex)
            return;

        // 같은 컨테이너면 swap
        if (Source == target)
        {
            Source.TrySwap(SourceIndex, targetIndex);
            return;
        }

        // 서로 다른 컨테이너면 swap(원자적으로)
        var src = Source;
        int srcIdx = SourceIndex;

        var srcItem = src.Get(srcIdx);
        var dstItem = target.Get(targetIndex);

        // 방어: 드래그 중 소스가 바뀐 경우
        if (srcItem != Item) return;

        // 배치 가능성 체크(룰 위반이면 거부)
        if (!target.CanPlace(srcItem, targetIndex, ignoreIndex: targetIndex)) return;
        if (!src.CanPlace(dstItem, srcIdx, ignoreIndex: srcIdx)) return;

        // 2단계 적용 + 실패 시 롤백
        if (!src.TrySet(srcIdx, dstItem)) return;
        if (!target.TrySet(targetIndex, srcItem))
        {
            // rollback
            src.TrySet(srcIdx, srcItem);
        }
    }
}

// -----------------------
// Drag Icon (UI object)
// -----------------------
public class DragIcon : MonoBehaviour
{
    public static DragIcon Instance { get; private set; }

    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image image;

    private void Awake()
    {
        Instance = this;

        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (image == null) image = GetComponent<Image>();

        Hide();
    }

    public void Show(Sprite sprite)
    {
        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void Follow(Vector2 screenPos)
    {
        if (rectTransform != null)
            rectTransform.position = screenPos;
    }

    public void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (image != null) image.enabled = false;
    }
}
