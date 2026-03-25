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
    public static int RelicLevel { get; private set; }  // 0이면 “레벨 없음/모름”
    public static bool HasRelicLevel => RelicLevel > 0;

    public static void Begin(IItemContainer source, int sourceIndex, ScriptableObject item, int relicLevel = 0)
    {
        Source = source;
        SourceIndex = sourceIndex;
        Item = item;
        RelicLevel = relicLevel;
    }

    public static void Clear()
    {
        Source = null;
        SourceIndex = -1;
        Item = null;
        RelicLevel = 0;
    }

    /// <summary>
    /// source 슬롯의 아이템을 target 슬롯으로 드롭/스왑.
    /// </summary>
    /// <summary>
    /// 책임 : source 슬롯의 아이템을 target 슬롯으로 드롭/스왑한다.
    /// 유물의 경우 중복 강화가 자연스럽게 일어나도록 targetIndex를 보정한다.
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

        var srcItem = Source.Get(SourceIndex);
        if (srcItem == null)
        {
            Clear();
            return false;
        }

        // 책임 : 같은 유물 슬롯 위에 드롭한 경우 merge가 일어나도록 내부 targetIndex를 보정한다.
        int resolvedTargetIndex = ResolveRelicDropTargetIndex(target, targetIndex, srcItem);

        var dstItem = target.Get(resolvedTargetIndex);

        int srcLvl = ItemDragContext.RelicLevel;
        if (srcLvl <= 0 && srcItem is RelicDefinition && Source is IRelicLevelProvider sp)
            sp.TryGetRelicLevel(SourceIndex, out srcLvl);

        int dstLvl = 0;
        if (dstItem is RelicDefinition && target is IRelicLevelProvider tp)
            tp.TryGetRelicLevel(resolvedTargetIndex, out dstLvl);

        if (!target.CanPlace(srcItem, resolvedTargetIndex, ignoreIndex: -1)) { Clear(); return false; }
        if (!Source.CanPlace(dstItem, SourceIndex, ignoreIndex: -1)) { Clear(); return false; }

        bool ok1;
        if (srcItem is RelicDefinition sr && target is IRelicSlotReceiver tr && srcLvl > 0)
            ok1 = tr.TrySetRelicWithLevel(resolvedTargetIndex, sr, srcLvl);
        else
            ok1 = target.TrySet(resolvedTargetIndex, srcItem);

        if (!ok1) { Clear(); return false; }

        if (ok1 && srcItem is RelicDefinition && target is IRelicSlotReceiver)
        {
            var after = target.Get(resolvedTargetIndex);
            if (after != srcItem)
            {
                bool consumed = Source.TrySet(SourceIndex, null);
                Clear();
                return consumed;
            }
        }

        bool ok2;
        if (dstItem is RelicDefinition dr && Source is IRelicSlotReceiver sr2 && dstLvl > 0)
            ok2 = sr2.TrySetRelicWithLevel(SourceIndex, dr, dstLvl);
        else
            ok2 = Source.TrySet(SourceIndex, dstItem);

        if (!ok2)
        {
            if (dstItem is RelicDefinition drb && target is IRelicSlotReceiver trb && dstLvl > 0)
                trb.TrySetRelicWithLevel(resolvedTargetIndex, drb, dstLvl);
            else
                target.TrySet(resolvedTargetIndex, dstItem);

            Clear();
            return false;
        }

        Clear();
        return true;
    }
    // 책임 : 유물 drag&drop 시 "같은 유물 슬롯 위에 직접 드롭"한 경우,
         // merge 로직이 자연스럽게 타도록 대체 대상 슬롯을 찾아준다.
    private static int ResolveRelicDropTargetIndex(IItemContainer target, int requestedIndex, ScriptableObject srcItem)
    {
        if (target == null) return requestedIndex;

        var movingRelic = srcItem as RelicDefinition;
        if (movingRelic == null) return requestedIndex;

        var dstRelic = target.Get(requestedIndex) as RelicDefinition;
        if (dstRelic == null) return requestedIndex;

        if (dstRelic.relicId != movingRelic.relicId) return requestedIndex;

        for (int i = 0; i < target.SlotCount; i++)
        {
            if (i == requestedIndex) continue;
            if (!target.CanPlace(srcItem, i, ignoreIndex: -1)) continue;
            return i;
        }

        return requestedIndex;
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
