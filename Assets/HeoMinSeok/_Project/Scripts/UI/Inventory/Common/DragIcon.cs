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
        // 서로 다른 컨테이너: 스왑(양쪽 규칙 검사 포함)
        var srcItem = Source.Get(SourceIndex);
        var dstItem = target.Get(targetIndex);

        // 레벨 미리 백업 (덮어쓰기 전에!)
        int srcLvl = ItemDragContext.RelicLevel; // ✅ 캐시 우선
        if (srcLvl <= 0 && srcItem is RelicDefinition && Source is IRelicLevelProvider sp)
            sp.TryGetRelicLevel(SourceIndex, out srcLvl);

        int dstLvl = 0;
        if (dstItem is RelicDefinition && target is IRelicLevelProvider tp)
            tp.TryGetRelicLevel(targetIndex, out dstLvl);

        // 규칙 검사
        if (!target.CanPlace(srcItem, targetIndex, ignoreIndex: -1)) { Clear(); return false; }
        if (!Source.CanPlace(dstItem, SourceIndex, ignoreIndex: -1)) { Clear(); return false; }

        // 1) target에 src 넣기(레벨 포함 가능)
        bool ok1;
        if (srcItem is RelicDefinition sr && target is IRelicSlotReceiver tr && srcLvl > 0)
            ok1 = tr.TrySetRelicWithLevel(targetIndex, sr, srcLvl);
        else
            ok1 = target.TrySet(targetIndex, srcItem);

        if (!ok1) { Clear(); return false; }

        if (ok1 && srcItem is RelicDefinition && target is IRelicSlotReceiver)
        {
            var after = target.Get(targetIndex);
            if (after != srcItem)
            {
                // ✅ 스왑이 아니라 "소스만 제거"로 처리해야 함
                bool consumed = Source.TrySet(SourceIndex, null);
                Clear();
                return consumed;
            }
        }

        // 2) source에 dst 넣기(레벨 포함 가능)
        bool ok2;
        if (dstItem is RelicDefinition dr && Source is IRelicSlotReceiver sr2 && dstLvl > 0)
            ok2 = sr2.TrySetRelicWithLevel(SourceIndex, dr, dstLvl);
        else
            ok2 = Source.TrySet(SourceIndex, dstItem);

        if (!ok2)
        {
            // rollback: target을 원래 dst로 되돌리기(레벨 포함)
            if (dstItem is RelicDefinition drb && target is IRelicSlotReceiver trb && dstLvl > 0)
                trb.TrySetRelicWithLevel(targetIndex, drb, dstLvl);
            else
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
