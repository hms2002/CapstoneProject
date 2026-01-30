using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// -----------------------
// Drag Context (Swap support, generic)
// -----------------------
public static class ItemDragContext
{
    public static bool Active => Source != null && Item != null;

    public static IItemContainer Source { get; private set; }
    public static int SourceIndex { get; private set; } = -1;
    public static ScriptableObject Item { get; private set; }

    public static void Begin(IItemContainer source, int sourceIndex, ScriptableObject item)
    {
        Source = source;
        SourceIndex = sourceIndex;
        Item = item;
    }

    public static void Clear()
    {
        Source = null;
        SourceIndex = -1;
        Item = null;
    }

    /// <summary>
    /// source 슬롯의 아이템을 target 슬롯으로 드롭/스왑.
    /// </summary>
    public static bool TryDrop(IItemContainer target, int targetIndex)
    {
        if (!Active) return false;
        if (target == null) return false;

        // 같은 컨테이너 내 스왑
        if (target == Source)
        {
            bool ok = Source.TrySwap(SourceIndex, targetIndex);
            Clear();
            return ok;
        }

        // 서로 다른 컨테이너: 스왑(양쪽 규칙 검사 포함)
        var srcItem = Source.Get(SourceIndex);
        var dstItem = target.Get(targetIndex);

        // 타겟이 srcItem을 받을 수 있어야 함
        if (!target.CanPlace(srcItem, targetIndex, ignoreIndex: -1)) { Clear(); return false; }
        // 소스가 dstItem을 다시 받을 수 있어야 함(스왑)
        if (!Source.CanPlace(dstItem, SourceIndex, ignoreIndex: -1)) { Clear(); return false; }

        // 1) 임시로 target에 src를 넣기
        if (!target.TrySet(targetIndex, srcItem)) { Clear(); return false; }
        // 2) source에 dst를 넣기
        if (!Source.TrySet(SourceIndex, dstItem))
        {
            // rollback attempt
            target.TrySet(targetIndex, dstItem);
            Clear();
            return false;
        }

        Clear();
        return true;
    }
}

public class DragIcon : MonoBehaviour
{
    public static DragIcon Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image image;
    [SerializeField] private RectTransform rectTransform;

    private void Awake()
    {
        Instance = this;
        if (rectTransform == null) rectTransform = transform as RectTransform;
        Hide();
    }

    public void Show(Sprite sprite)
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (image != null)
        {
            image.enabled = true;
            image.sprite = sprite;
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
