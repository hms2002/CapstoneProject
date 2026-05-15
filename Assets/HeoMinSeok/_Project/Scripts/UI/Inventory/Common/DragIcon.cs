using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CapstoneAudio;

// -----------------------
// Drag Context (Swap support, generic)
// -----------------------
/// <summary>
/// 책임 :
/// - 현재 진행 중인 인벤토리 drag 세션의 source/item 정보를 전역으로 보관한다.
/// - drag가 정상 종료되지 못하는 UI 닫힘 경로에서도 공통 취소 처리를 제공한다.
/// </summary>
public static class ItemDragContext
{
    /// <summary>
    /// 책임 :
    /// - 인벤토리 drag 시작 시 재생할 그랩 사운드 키를 한 곳에 고정한다.
    /// - UI drag 시작 로직이 사운드 카탈로그 문자열에 직접 의존하지 않게 한다.
    /// </summary>
    private static readonly SoundRef ItemGrabSound = SoundRef.FromKey("ui.inventory.grab");


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
        PlayItemGrabSound();
    }

    public static void Clear()
    {
        Source = null;
        SourceIndex = -1;
        Item = null;
        RelicLevel = 0;
    }

    /// <summary>
    /// 책임 :
    /// - ESC 닫힘처럼 OnEndDrag가 호출되지 않는 경로에서 drag 세션을 안전하게 종료한다.
    /// - drag icon, drop zone, drag context를 한 번에 정리해 UI 잔상을 남기지 않는다.
    /// </summary>
    public static void CancelActiveDragSession()
    {
        DropZoneUI.ActiveInstance?.Hide();
        DragIcon.Instance?.Hide();
        Clear();
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
        return TryDropWithResult(target, targetIndex).Succeeded;
    }

    public static InventoryTransferResult TryDropWithResult(IItemContainer target, int targetIndex)
    {
        if (!Active)
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.NoActiveDrag);

        if (target == null)
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.MissingTarget);

        var request = new InventoryTransferRequest(Source, SourceIndex, target, targetIndex, RelicLevel);
        InventoryTransferResult result = InventoryTransferService.TryTransfer(request);
        Clear();
        return result;
    }
    /// <summary>
    /// 책임 :
    /// - 인벤토리 UI에서 아이템을 집어 drag를 시작하는 순간 그랩 사운드를 재생한다.
    /// - 드롭/스왑 성공 여부와 무관한 "집기" 피드백을 시작 시점에 고정한다.
    /// </summary>
    private static void PlayItemGrabSound()
    {
        SoundManager.EnsureInstance().Play(ItemGrabSound, new SoundPlaybackContext
        {
            Position = Vector3.zero,
            SourceObject = DragIcon.Instance
        });
    }
}

public class DragIcon : MonoBehaviour
{
    public static DragIcon Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image image;
    [SerializeField] private RectTransform rectTransform;
    private ItemDisplayIconDefaultState iconDefaultState;
    private RectTransformDefaultState rootDefaultState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (rectTransform == null) rectTransform = transform as RectTransform;
        iconDefaultState = CanApplyIconTransform()
            ? ItemDisplayIconDefaultState.Stretch(image)
            : new ItemDisplayIconDefaultState(image);
        rootDefaultState = new RectTransformDefaultState(rectTransform);
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(Sprite sprite)
    {
        ResetRootPresentation();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        ItemDisplayIconUtility.ApplyRaw(image, sprite, iconDefaultState);
    }

    public void Show(ScriptableObject item)
    {
        ResetRootPresentation();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        ItemDisplayIconUtility.Apply(
            image,
            item,
            ItemDisplayIconContext.DragIcon,
            iconDefaultState,
            applyAnchoredPosition: true,
            applyCustomTransform: CanApplyIconTransform());
    }

    public void Follow(Vector2 screenPos)
    {
        if (rectTransform != null)
            rectTransform.position = screenPos;
    }

    public void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        ItemDisplayIconUtility.Clear(image, iconDefaultState);
        ResetRootPresentation();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void ResetRootPresentation()
    {
        rootDefaultState.ApplyTo(rectTransform);
    }

    private bool CanApplyIconTransform()
    {
        return image != null && image.rectTransform != null && image.rectTransform != rectTransform;
    }

    private readonly struct RectTransformDefaultState
    {
        private readonly bool hasValue;
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 anchoredPosition;
        private readonly Vector2 sizeDelta;
        private readonly Vector2 pivot;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;

        public RectTransformDefaultState(RectTransform rect)
        {
            hasValue = rect != null;
            anchorMin = rect != null ? rect.anchorMin : Vector2.zero;
            anchorMax = rect != null ? rect.anchorMax : Vector2.zero;
            anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
            sizeDelta = rect != null ? rect.sizeDelta : Vector2.zero;
            pivot = rect != null ? rect.pivot : new Vector2(0.5f, 0.5f);
            localRotation = rect != null ? rect.localRotation : Quaternion.identity;
            localScale = rect != null ? rect.localScale : Vector3.one;
        }

        public void ApplyTo(RectTransform rect)
        {
            if (!hasValue || rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.pivot = pivot;
            rect.localRotation = localRotation;
            rect.localScale = localScale;
        }
    }
}
