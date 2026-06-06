using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UpgradeTooltip : MonoBehaviour, IHoverView, IHoverPositionOffsetProvider
{
    public static UpgradeTooltip Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Presentation")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, -24f);
    [SerializeField, Min(0f)] private float openDuration = 0.12f;
    [SerializeField, Min(0f)] private float closeDuration = 0.1f;
    [SerializeField, Range(0f, 1f)] private float closedAlpha = 0f;
    [SerializeField] private bool useUnscaledTime = true;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Coroutine presentationRoutine;
    private Vector2 hoverPositionOffset;
    private bool isAnimating;

    public RectTransform Rect => rectTransform;
    public Vector2 HoverPositionOffset => hoverPositionOffset;
    public bool IsActive => gameObject.activeSelf && (isAnimating || (canvasGroup != null && canvasGroup.alpha > 0.01f));

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        OpenUI();
        SnapHidden();
    }

    private void OnDestroy()
    {
        StopPresentationRoutine();

        if (Instance == this)
            Instance = null;
    }

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        HideHover();
    }

    public void ShowHover(object data, object context = null)
    {
        if (!(data is UpgradeNodeSO node))
        {
            HideHover();
            return;
        }

        if (titleText != null)
            titleText.text = node.upgradeName;

        if (contentText != null)
        {
            contentText.text = node.description;
            contentText.enableWordWrapping = true;
        }

        ApplyStatusText(node);
        gameObject.SetActive(true);
        PlayOpenPresentation();
    }

    public void HideHover()
    {
        if (canvasGroup == null)
            return;

        PlayClosePresentation();
    }

    private void ApplyStatusText(UpgradeNodeSO node)
    {
        if (statusText == null)
            return;

        LockType status = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.GetNodeStatus(node.nodeID)
            : LockType.Locked;

        string statusLabel = status switch
        {
            LockType.Locked => "\uC7A0\uAE40",
            LockType.Purchased => "\uAD6C\uB9E4\uB428",
            _ => string.Empty
        };

        statusText.text = statusLabel;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(statusLabel));
    }

    private void PlayOpenPresentation()
    {
        StopPresentationRoutine();

        if (canvasGroup == null)
            return;

        ForceNonInteractive();

        bool alreadyVisible = canvasGroup.alpha > 0.99f && hoverPositionOffset.sqrMagnitude < 0.01f;
        if (alreadyVisible || openDuration <= 0f)
        {
            ApplyPresentation(Vector2.zero, 1f);
            isAnimating = false;
            return;
        }

        Vector2 fromOffset = canvasGroup.alpha > 0.01f ? hoverPositionOffset : openOffset;
        presentationRoutine = StartCoroutine(CoPresentation(fromOffset, Vector2.zero, canvasGroup.alpha, 1f, openDuration, false, null));
    }

    private void PlayClosePresentation()
    {
        StopPresentationRoutine();

        if (canvasGroup == null)
            return;

        ForceNonInteractive();

        if (!gameObject.activeSelf || (canvasGroup.alpha <= 0.01f && !isAnimating))
        {
            SnapHidden();
            return;
        }

        presentationRoutine = StartCoroutine(CoPresentation(
            hoverPositionOffset,
            openOffset,
            canvasGroup.alpha,
            closedAlpha,
            closeDuration,
            true,
            () =>
            {
                if (statusText != null)
                    statusText.gameObject.SetActive(false);
            }));
    }

    private IEnumerator CoPresentation(
        Vector2 fromOffset,
        Vector2 toOffset,
        float fromAlpha,
        float toAlpha,
        float duration,
        bool driveRectPosition,
        System.Action onComplete)
    {
        isAnimating = true;
        Vector2 baseAnchoredPosition = rectTransform != null
            ? rectTransform.anchoredPosition - fromOffset
            : Vector2.zero;

        if (duration <= 0f)
        {
            ApplyPresentation(toOffset, toAlpha, driveRectPosition, baseAnchoredPosition);
            FinishPresentation(onComplete);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            ApplyPresentation(
                Vector2.LerpUnclamped(fromOffset, toOffset, eased),
                Mathf.LerpUnclamped(fromAlpha, toAlpha, eased),
                driveRectPosition,
                baseAnchoredPosition);
            yield return null;
        }

        ApplyPresentation(toOffset, toAlpha, driveRectPosition, baseAnchoredPosition);
        FinishPresentation(onComplete);
    }

    private void FinishPresentation(System.Action onComplete)
    {
        presentationRoutine = null;
        isAnimating = false;
        ForceNonInteractive();
        onComplete?.Invoke();
    }

    private void ApplyPresentation(Vector2 offset, float alpha, bool driveRectPosition = false, Vector2 baseAnchoredPosition = default)
    {
        hoverPositionOffset = offset;

        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        if (driveRectPosition && rectTransform != null)
            rectTransform.anchoredPosition = baseAnchoredPosition + offset;
    }

    private void SnapHidden()
    {
        StopPresentationRoutine();
        ApplyPresentation(openOffset, closedAlpha);
        ForceNonInteractive();

        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private void ForceNonInteractive()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void StopPresentationRoutine()
    {
        if (presentationRoutine == null)
            return;

        StopCoroutine(presentationRoutine);
        presentationRoutine = null;
        isAnimating = false;
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }
}
