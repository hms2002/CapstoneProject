using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueChoiceHighlightPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Graphic selectedBackground;
    [SerializeField] private CanvasGroup canvasGroup;

    private Color defaultSelectedColor = Color.white;
    private Color selectedColor = Color.white;
    private RectTransform rootRect;
    private Vector2 layoutAnchoredPosition;
    private bool hasLayoutAnchoredPosition;
    private bool selected;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        if (selectedBackground != null)
        {
            defaultSelectedColor = selectedBackground.color;
            defaultSelectedColor.a = 1f;
            selectedColor = defaultSelectedColor;
        }

        ApplySelection();
    }

    private void OnDisable()
    {
        KillMotion();
        selected = false;
        ApplySelection();
    }

    public void SetSelected(bool isSelected, bool immediate = false)
    {
        ResolveReferences();
        selected = isSelected;
        ApplySelection();
    }

    public void SetThemeColor(Color color)
    {
        selectedColor = color;
        ApplySelection();
    }

    public void ResetThemeColor()
    {
        selectedColor = defaultSelectedColor;
        ApplySelection();
    }

    public Tween CreateEnterTween(float moveDistance, float duration)
    {
        ResolveReferences();
        CaptureLayoutPosition();
        KillMotion();

        if (rootRect != null)
            rootRect.anchoredPosition = layoutAnchoredPosition + Vector2.down * Mathf.Max(0f, moveDistance);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            SetInteractionEnabled(false);
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(true).Pause();
        float safeDuration = Mathf.Max(0f, duration);

        if (rootRect != null)
            sequence.Join(rootRect.DOAnchorPos(layoutAnchoredPosition, safeDuration).SetEase(Ease.OutCubic));

        if (canvasGroup != null)
            sequence.Join(canvasGroup.DOFade(1f, safeDuration).SetEase(Ease.OutCubic));

        return sequence;
    }

    public Tween CreateExitTween(bool isSelected, float selectedMoveDistance, float duration)
    {
        ResolveReferences();
        CaptureLayoutPosition();
        KillMotion();
        SetInteractionEnabled(false);

        Sequence sequence = DOTween.Sequence().SetUpdate(true).Pause();
        float safeDuration = Mathf.Max(0f, duration);

        if (canvasGroup != null)
            sequence.Join(canvasGroup.DOFade(0f, safeDuration).SetEase(Ease.InCubic));

        if (isSelected && rootRect != null)
        {
            Vector2 targetPosition = layoutAnchoredPosition + Vector2.up * Mathf.Max(0f, selectedMoveDistance);
            sequence.Join(rootRect.DOAnchorPos(targetPosition, safeDuration).SetEase(Ease.InCubic));
        }

        return sequence;
    }

    public void CaptureLayoutPosition()
    {
        ResolveReferences();
        if (rootRect == null)
            return;

        layoutAnchoredPosition = rootRect.anchoredPosition;
        hasLayoutAnchoredPosition = true;
    }

    public void ResetPresentation(bool visible)
    {
        ResolveReferences();
        KillMotion();

        if (hasLayoutAnchoredPosition && rootRect != null)
            rootRect.anchoredPosition = layoutAnchoredPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;

        SetInteractionEnabled(false);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        ResolveReferences();
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    public void KillMotion()
    {
        if (rootRect != null)
            rootRect.DOKill();

        if (canvasGroup != null)
            canvasGroup.DOKill();
    }

    private void ApplySelection()
    {
        if (selectedBackground == null)
            return;

        Color color = selectedColor;
        color.a = selected ? selectedColor.a : 0f;
        selectedBackground.color = color;
    }

    private void ResolveReferences()
    {
        if (selectedBackground == null)
            selectedBackground = GetComponent<Graphic>();

        if (rootRect == null)
            rootRect = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }
}
