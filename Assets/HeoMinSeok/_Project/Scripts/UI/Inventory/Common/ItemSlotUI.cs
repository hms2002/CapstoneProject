using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;

public interface IItemContainer
{
    int SlotCount { get; }
    event Action OnChanged;
    ScriptableObject Get(int index);
    bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1);
    bool TrySet(int index, ScriptableObject item);
    bool TrySwap(int a, int b);
}
public interface IRelicLevelProvider
{
    bool TryGetRelicLevel(int index, out int level);
}

public interface IRelicSlotReceiver
{
    bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level);
}

/// <summary>
/// 책임 : 개별 인벤토리 슬롯 UI를 표시하고 drag, drop, quick move 입력을 컨테이너 동작으로 변환한다.
/// </summary>
public class ItemSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private GameObject hoverHighlightRoot;
    [SerializeField] private Sprite lockedSlotSprite;

    [Header("Highlight Presentation")]
    [SerializeField, Range(0f, 1f)] private float hoverHighlightAlpha = 0.65f;
    [SerializeField, Range(0f, 1f)] private float actionHighlightAlpha = 1f;
    [SerializeField, Min(0f)] private float highlightFadeInDuration = 0.08f;
    [SerializeField, Min(0f)] private float highlightFadeOutDuration = 0.08f;
    [SerializeField] private bool useUnscaledHighlightTime = true;

    private IItemContainer container;
    private int index;
    [SerializeField] private RectTransform slotRect;
    private ItemDisplayIconDefaultState iconDefaultState;
    private Sprite defaultBackgroundSprite;
    private bool defaultBackgroundEnabled;
    private bool defaultBackgroundPreserveAspect;
    private Image.Type defaultBackgroundType;
    private bool hasDefaultBackgroundState;
    private bool loggedMissingBackgroundImage;
    private bool loggedMissingLockedSlotSprite;
    private CanvasGroup hoverHighlightCanvasGroup;
    private Coroutine hoverHighlightRoutine;
    private bool isPointerOver;
    private bool isPointerPressed;
    private bool isDraggingThisSlot;
    private InventoryScreen ownerInventoryScreen;

    public RectTransform SlotRect
    {
        get
        {
            if (slotRect == null)
                slotRect = transform as RectTransform;

            return slotRect;
        }
    }

    public ScriptableObject CurrentItem => container != null ? container.Get(index) : null;
    public bool HasItem => CurrentItem != null;
    public bool HasEpicItem => CurrentItem is RelicDefinition relic && relic.rarity == ItemRarity.Epic;
    private bool IsLocked => container != null && index >= container.SlotCount;

    private void Awake()
    {
        if (slotRect == null)
            slotRect = transform as RectTransform;

        iconDefaultState = ItemDisplayIconDefaultState.Stretch(icon);
        CaptureDefaultBackgroundState();
        SetHoverHighlightImmediate(false);
    }
    private void OnDisable()
    {
        isPointerOver = false;
        isPointerPressed = false;
        isDraggingThisSlot = false;
        ClearIconAndLevel();
        SetHoverHighlightImmediate(false);
        ItemDragContext.CancelActiveDragSession();
        MouseCursorService.Instance?.SetDragging(this, false);
        MouseCursorService.Instance?.SetInteractable(this, false);

        if (container != null)
            container.OnChanged -= Refresh;
    }

    public void Bind(IItemContainer container, int index)
    {
        if (this.container != null)
            this.container.OnChanged -= Refresh;

        this.container = container;
        this.index = index;
        isPointerOver = false;
        isPointerPressed = false;
        isDraggingThisSlot = false;
        SetHoverHighlightImmediate(false);
        RestoreDefaultBackground();

        if (this.container != null)
            this.container.OnChanged += Refresh;

        Refresh();
    }

    public void Refresh()
    {
        if (container == null)
        {
            ClearIconAndLevel();
            SetHoverHighlight(false);
            return;
        }

        if (IsLocked)
        {
            RefreshLockedSlot();
            return;
        }

        var so = container.Get(index);
        RestoreDefaultBackground();
        RefreshHoverHighlight(so);

        if (icon != null)
            ItemDisplayIconUtility.Apply(icon, so, ItemDisplayIconContext.InventorySlot, iconDefaultState);

        if (so is RelicDefinition && container is IRelicLevelProvider p && p.TryGetRelicLevel(index, out var lvl))
        {
            if (levelText != null)
            {
                levelText.gameObject.SetActive(true);
                levelText.text = $"Lv {lvl}";
            }
        }
        else if (levelText != null)
        {
            levelText.gameObject.SetActive(false);
        }
    }

    private void ClearIconAndLevel()
    {
        RestoreDefaultBackground();

        if (icon != null)
            ItemDisplayIconUtility.Clear(icon, iconDefaultState);

        if (levelText != null)
            levelText.gameObject.SetActive(false);
    }

    private void RefreshLockedSlot()
    {
        SetHoverHighlight(false);
        ClearIconAndLevel();

        ApplyLockedBackground();

        if (levelText != null)
            levelText.gameObject.SetActive(false);
    }

    private void CaptureDefaultBackgroundState()
    {
        if (backgroundImage == null)
            return;

        defaultBackgroundSprite = backgroundImage.sprite;
        defaultBackgroundEnabled = backgroundImage.enabled;
        defaultBackgroundPreserveAspect = backgroundImage.preserveAspect;
        defaultBackgroundType = backgroundImage.type;
        hasDefaultBackgroundState = true;
    }

    private void RestoreDefaultBackground()
    {
        if (backgroundImage == null || !hasDefaultBackgroundState)
            return;

        backgroundImage.sprite = defaultBackgroundSprite;
        backgroundImage.enabled = defaultBackgroundEnabled;
        backgroundImage.preserveAspect = defaultBackgroundPreserveAspect;
        backgroundImage.type = defaultBackgroundType;
    }

    private void ApplyLockedBackground()
    {
        if (backgroundImage == null)
        {
            LogMissingBackgroundImage();
            return;
        }

        if (lockedSlotSprite == null)
        {
            LogMissingLockedSlotSprite();
            return;
        }

        backgroundImage.sprite = lockedSlotSprite;
        backgroundImage.enabled = true;
    }

    private void LogMissingBackgroundImage()
    {
        if (loggedMissingBackgroundImage)
            return;

        loggedMissingBackgroundImage = true;
        Debug.LogWarning($"{nameof(ItemSlotUI)} on {name} has no background image reference for locked slot presentation.", this);
    }

    private void LogMissingLockedSlotSprite()
    {
        if (loggedMissingLockedSlotSprite)
            return;

        loggedMissingLockedSlotSprite = true;
        Debug.LogWarning($"{nameof(ItemSlotUI)} on {name} has no locked slot sprite assigned.", this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (container == null || IsLocked || IsInventoryInspectionOnly()) return;

        // [수정] 드래그 시작 시 호버 패널 숨김 (UIManager 활용)
        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();

        var so = container.Get(index);
        if (so == null)
        {
            SetHoverHighlight(false);
            return;
        }

        var def = so.AsDef();
        if (def == null)
        {
            SetHoverHighlight(false);
            return;
        }

        isPointerPressed = false;
        isDraggingThisSlot = true;
        RefreshHoverHighlight(so);

        int relicLevel = 0;
        if (so is RelicDefinition && container is IRelicLevelProvider p)
            p.TryGetRelicLevel(index, out relicLevel);

        ItemDragContext.Begin(container, index, so, relicLevel);
        MouseCursorService.EnsureInstance().SetInteractable(this, false);
        MouseCursorService.EnsureInstance().SetDragging(this, true);

        DropZoneUI.ActiveInstance?.Show();
        DragIcon.Instance?.Show(so);
        DragIcon.Instance?.Follow(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsInventoryInspectionOnly())
        {
            ItemDragContext.CancelActiveDragSession();
            return;
        }

        if (!ItemDragContext.Active) return;
        DragIcon.Instance?.Follow(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isPointerPressed = false;
        isDraggingThisSlot = false;
        ItemDragContext.CancelActiveDragSession();
        MouseCursorService.EnsureInstance().SetDragging(this, false);
        Refresh();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (IsLocked || IsInventoryInspectionOnly())
        {
            ItemDragContext.CancelActiveDragSession();
            return;
        }

        InventorySlotTransferInteractionService.ExecuteDrop(container, index, Refresh);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (container == null || IsLocked || IsInventoryInspectionOnly()) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            InventorySlotTransferInteractionService.ExecuteQuickMove(container, index, Refresh);
            return;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (container == null || IsLocked || IsInventoryInspectionOnly() || ItemDragContext.Active)
            return;

        if (container.Get(index) == null)
            return;

        isPointerPressed = true;
        RefreshHoverHighlight();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (IsInventoryInspectionOnly())
        {
            isPointerPressed = false;
            RefreshHoverHighlight();
            return;
        }

        if (!isPointerPressed)
            return;

        isPointerPressed = false;
        RefreshHoverHighlight();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;

        if (container == null)
        {
            SetHoverHighlight(false);
            return;
        }

        if (IsLocked)
        {
            SetHoverHighlight(false);
            MouseCursorService.EnsureInstance().SetInteractable(this, false);
            if (UIManager.Instance != null)
                UIManager.Instance.HideHoverImmediate();
            return;
        }

        if (ItemDragContext.Active && !IsInventoryInspectionOnly())
        {
            RefreshHoverHighlight();
            return;
        }

        var so = container.Get(index);
        if (so == null)
        {
            SetHoverHighlight(false);
            MouseCursorService.EnsureInstance().SetInteractable(this, false);
            if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
            return;
        }

        RefreshHoverHighlight(so);
        MouseCursorService.EnsureInstance().SetInteractable(this, true);

        // [수정] HoverController에게 띄우라고 요청
        if (ItemHoverController.Instance != null)
        {
            ItemHoverController.Instance.HoverSlot(slotRect, so, container, index);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (!isDraggingThisSlot)
            isPointerPressed = false;

        RefreshHoverHighlight();
        MouseCursorService.EnsureInstance().SetInteractable(this, false);

        if (ItemDragContext.Active && !IsInventoryInspectionOnly()) return;

        // [수정] HoverController에게 끄라고 요청
        if (ItemHoverController.Instance != null)
        {
            ItemHoverController.Instance.UnhoverSlot(slotRect);
        }
    }

    private void RefreshHoverHighlight()
    {
        ScriptableObject item = container != null ? container.Get(index) : null;
        RefreshHoverHighlight(item);
    }

    private void RefreshHoverHighlight(ScriptableObject item)
    {
        bool shouldShow = ShouldShowHoverHighlight(item);
        SetHoverHighlight(shouldShow, GetHoverHighlightTargetAlpha());
    }

    private bool ShouldShowHoverHighlight(ScriptableObject item)
    {
        if (item == null)
            return false;

        if (IsInventoryInspectionOnly())
            return isPointerOver;

        if (isDraggingThisSlot)
            return true;

        if (ItemDragContext.Active)
            return false;

        return isPointerOver;
    }

    private float GetHoverHighlightTargetAlpha()
    {
        if (IsInventoryInspectionOnly())
            return hoverHighlightAlpha;

        return isPointerPressed || isDraggingThisSlot
            ? actionHighlightAlpha
            : hoverHighlightAlpha;
    }

    private bool IsInventoryInspectionOnly()
    {
        if (ItemContainerGroupRegistry.IsInspectionOnly)
            return true;

        InventoryScreen owner = ResolveOwnerInventoryScreen();
        return owner != null && owner.IsInspectionOnly;
    }

    private InventoryScreen ResolveOwnerInventoryScreen()
    {
        if (ownerInventoryScreen == null)
            ownerInventoryScreen = GetComponentInParent<InventoryScreen>(true);

        return ownerInventoryScreen;
    }

    private void SetHoverHighlight(bool active)
    {
        SetHoverHighlight(active, GetHoverHighlightTargetAlpha());
    }

    private void SetHoverHighlight(bool active, float targetAlpha)
    {
        if (hoverHighlightRoot == null)
            return;

        ResolveHoverHighlightCanvasGroup();

        if (active)
        {
            if (!hoverHighlightRoot.activeSelf)
                hoverHighlightRoot.SetActive(true);

            PlayHoverHighlightFade(targetAlpha, highlightFadeInDuration, false);
            return;
        }

        if (!hoverHighlightRoot.activeSelf)
        {
            SetHoverHighlightAlpha(0f);
            return;
        }

        PlayHoverHighlightFade(0f, highlightFadeOutDuration, true);
    }

    private void SetHoverHighlightImmediate(bool active)
    {
        StopHoverHighlightRoutine();

        if (hoverHighlightRoot == null)
            return;

        ResolveHoverHighlightCanvasGroup();
        hoverHighlightRoot.SetActive(active);
        SetHoverHighlightAlpha(active ? GetHoverHighlightTargetAlpha() : 0f);
    }

    private void ResolveHoverHighlightCanvasGroup()
    {
        if (hoverHighlightRoot == null)
            return;

        if (hoverHighlightCanvasGroup == null || hoverHighlightCanvasGroup.gameObject != hoverHighlightRoot)
            hoverHighlightCanvasGroup = hoverHighlightRoot.GetComponent<CanvasGroup>();

        if (hoverHighlightCanvasGroup == null)
            hoverHighlightCanvasGroup = hoverHighlightRoot.AddComponent<CanvasGroup>();

        hoverHighlightCanvasGroup.interactable = false;
        hoverHighlightCanvasGroup.blocksRaycasts = false;
    }

    private void PlayHoverHighlightFade(float targetAlpha, float duration, bool deactivateOnComplete)
    {
        StopHoverHighlightRoutine();

        if (duration <= 0f)
        {
            SetHoverHighlightAlpha(targetAlpha);
            if (deactivateOnComplete)
                hoverHighlightRoot.SetActive(false);
            return;
        }

        hoverHighlightRoutine = StartCoroutine(CoHoverHighlightFade(targetAlpha, duration, deactivateOnComplete));
    }

    private IEnumerator CoHoverHighlightFade(float targetAlpha, float duration, bool deactivateOnComplete)
    {
        float startAlpha = hoverHighlightCanvasGroup != null ? hoverHighlightCanvasGroup.alpha : targetAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledHighlightTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetHoverHighlightAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetHoverHighlightAlpha(targetAlpha);

        if (deactivateOnComplete && hoverHighlightRoot != null)
            hoverHighlightRoot.SetActive(false);

        hoverHighlightRoutine = null;
    }

    private void SetHoverHighlightAlpha(float alpha)
    {
        if (hoverHighlightCanvasGroup != null)
            hoverHighlightCanvasGroup.alpha = alpha;
    }

    private void StopHoverHighlightRoutine()
    {
        if (hoverHighlightRoutine == null)
            return;

        StopCoroutine(hoverHighlightRoutine);
        hoverHighlightRoutine = null;
    }
}
