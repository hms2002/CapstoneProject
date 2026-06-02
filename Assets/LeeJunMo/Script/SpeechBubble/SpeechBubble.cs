using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CapstoneAudio;

public enum SpeechBubbleTailSide
{
    Left,
    Right
}

public class SpeechBubble : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private TextMeshProUGUI bubbleText;

    [Header("Default Theme")]
    [SerializeField] private Color defaultBorderColor = Color.black;
    [SerializeField] private Color defaultFillColor = new Color(1f, 1f, 1f, 0.52f);
    [SerializeField] private Color defaultFontColor = Color.black;
    [SerializeField] private float backgroundTextureInfluence = 0f;

    [Header("Typing Audio")]
    [SerializeField] private bool playTypingSound = true;
    [SerializeField, Min(0f)] private float typingSoundInterval = 0.035f;

    private Transform target;
    private Func<Vector3> targetPositionResolver;
    private Func<Quaternion> targetRotationResolver;
    private Vector3 offset;
    private Vector3 layoutOffset;
    private SpeechBubbleTailSide tailSide = SpeechBubbleTailSide.Left;
    private Tween typingTween;
    private Coroutine typingRoutine;
    private Coroutine textEffectRoutine;
    private Tween hideDelayTween;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Action<SpeechBubble> releaseAction;
    private Action hiddenAction;
    private Material runtimeBackgroundMaterial;
    private Material runtimeTextMaterial;
    private RectTransform backgroundRect;
    private RectTransform textRect;
    private HorizontalLayoutGroup backgroundLayoutGroup;
    private LayoutElement textLayoutElement;
    private Vector3 defaultBackgroundLocalScale = Vector3.one;
    private Vector3 defaultTextLocalScale = Vector3.one;
    private Vector2 defaultBackgroundAnchoredPosition;
    private RectOffset defaultBackgroundPadding;
    private bool supportsBackgroundTheme;
    private bool supportsTextFaceColor;
    private bool hasDefaultBackgroundLocalScale;
    private bool hasDefaultTextLocalScale;
    private bool hasDefaultBackgroundAnchoredPosition;
    private string currentFullText = string.Empty;
    private bool isTyping;
    private bool isHiding;
    private bool hasCachedTextDefaults;
    private bool defaultWordWrapping;
    private TextOverflowModes defaultOverflowMode;
    private HorizontalAlignmentOptions defaultHorizontalAlignment;
    private int backgroundBorderColorPropertyId = -1;
    private int lastTypedCharacterCount;
    private float nextTypingSoundTime;
    private DialogueTextRevealPlan currentRevealPlan;
    private int currentRevealVisibleCharacterCount;
    private bool hasCurrentRevealPlan;
    private readonly Vector3[] worldCorners = new Vector3[4];

    private const float UnwrappedPreferredWidthProbe = 10000f;

    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int BorderId = Shader.PropertyToID("_Border");
    private static readonly int TextureInfluenceId = Shader.PropertyToID("_TextureInfluence");
    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");

    public SpeechBubbleTailSide TailSide => tailSide;
    public Vector3 LayoutOffset => layoutOffset;

    private void Awake()
    {
        originalScale = transform.localScale;
        originalRotation = transform.rotation;
        ResolveVisualReferences();
        InitializeRuntimeMaterials();
    }

    public void SetupAndShow(
        Func<Vector3> targetPositionResolver,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease)
    {
        SetupAndShow(
            targetPositionResolver,
            offset,
            text,
            duration,
            useTyping,
            typingSpeed,
            theme,
            onHidden,
            onRelease,
            false,
            0f,
            0f,
            0f);
    }

    public void SetupAndShow(
        Transform target,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease)
    {
        SetupAndShow(
            target,
            offset,
            text,
            duration,
            useTyping,
            typingSpeed,
            theme,
            onHidden,
            onRelease,
            false,
            0f,
            0f,
            0f);
    }

    public void SetupAndShow(
        Transform target,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        SetupAndShowInternal(
            target,
            null,
            null,
            offset,
            text,
            duration,
            useTyping,
            typingSpeed,
            theme,
            onHidden,
            onRelease,
            preSizeLayout,
            minTextWidth,
            maxTextWidth,
            minTextHeight,
            false,
            DialogueAnimType.Normal);
    }

    public void SetupAndShow(
        Func<Vector3> targetPositionResolver,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        SetupAndShowInternal(
            null,
            targetPositionResolver,
            null,
            offset,
            text,
            duration,
            useTyping,
            typingSpeed,
            theme,
            onHidden,
            onRelease,
            preSizeLayout,
            minTextWidth,
            maxTextWidth,
            minTextHeight,
            false,
            DialogueAnimType.Normal);
    }

    public void SetupAndShow(
        Func<Vector3> targetPositionResolver,
        Func<Quaternion> targetRotationResolver,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        SetupAndShowInternal(
            null,
            targetPositionResolver,
            targetRotationResolver,
            offset,
            text,
            duration,
            useTyping,
            typingSpeed,
            theme,
            onHidden,
            onRelease,
            preSizeLayout,
            minTextWidth,
            maxTextWidth,
            minTextHeight,
            false,
            DialogueAnimType.Normal);
    }

    public void SetupAndShowAnimated(
        Transform target,
        Vector3 offset,
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        DialogueAnimType animType)
    {
        SetupAndShowAnimated(
            target,
            offset,
            text,
            duration,
            theme,
            onHidden,
            onRelease,
            animType,
            false,
            0f,
            0f,
            0f);
    }

    public void SetupAndShowAnimated(
        Func<Vector3> targetPositionResolver,
        Vector3 offset,
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        DialogueAnimType animType)
    {
        SetupAndShowAnimated(
            targetPositionResolver,
            offset,
            text,
            duration,
            theme,
            onHidden,
            onRelease,
            animType,
            false,
            0f,
            0f,
            0f);
    }

    public void SetupAndShowAnimated(
        Transform target,
        Vector3 offset,
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        DialogueAnimType animType,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        SetupAndShowInternal(
            target,
            null,
            null,
            offset,
            text,
            duration,
            true,
            0f,
            theme,
            onHidden,
            onRelease,
            preSizeLayout,
            minTextWidth,
            maxTextWidth,
            minTextHeight,
            true,
            animType);
    }

    public void SetupAndShowAnimated(
        Func<Vector3> targetPositionResolver,
        Vector3 offset,
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        DialogueAnimType animType,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        SetupAndShowInternal(
            null,
            targetPositionResolver,
            null,
            offset,
            text,
            duration,
            true,
            0f,
            theme,
            onHidden,
            onRelease,
            preSizeLayout,
            minTextWidth,
            maxTextWidth,
            minTextHeight,
            true,
            animType);
    }

    private void SetupAndShowInternal(
        Transform target,
        Func<Vector3> targetPositionResolver,
        Func<Quaternion> targetRotationResolver,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Action<SpeechBubble> onRelease,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight,
        bool useAnimatedReveal,
        DialogueAnimType animType)
    {
        StopActiveTweens();

        DialogueTextRevealPlan revealPlan = DialogueTextRevealUtility.BuildPlan(text);
        string displayText = useAnimatedReveal ? revealPlan.DisplayText : text;
        currentRevealPlan = revealPlan;
        hasCurrentRevealPlan = useAnimatedReveal;
        currentRevealVisibleCharacterCount = 0;

        this.target = target;
        this.targetPositionResolver = targetPositionResolver;
        this.targetRotationResolver = targetRotationResolver;
        this.offset = offset;
        layoutOffset = Vector3.zero;
        SetTailFlipX(false);
        hiddenAction = onHidden;
        releaseAction = onRelease;
        currentFullText = displayText;
        isTyping = false;
        isHiding = false;

        if (targetRotationResolver == null)
            transform.rotation = originalRotation;
        ApplyAnchorPose();
        gameObject.SetActive(true);
        ApplyTheme(theme);
        PrepareLayoutForText(displayText, preSizeLayout, minTextWidth, maxTextWidth, minTextHeight);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.DOKill();
        }

        transform.localScale = Vector3.zero;
        transform.DOKill();
        transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

        if (bubbleText != null)
        {
            bubbleText.text = string.Empty;
            bubbleText.maxVisibleCharacters = int.MaxValue;
            ResetTypingAudioTracking();

            if (useAnimatedReveal)
            {
                bubbleText.richText = true;
                bubbleText.text = displayText;
                bubbleText.maxVisibleCharacters = 0;
                bubbleText.ForceMeshUpdate();

                int visibleCharacterCount = bubbleText.textInfo != null
                    ? bubbleText.textInfo.characterCount
                    : 0;
                currentRevealVisibleCharacterCount = visibleCharacterCount;

                if (visibleCharacterCount > 0)
                {
                    isTyping = true;
                    typingRoutine = StartCoroutine(TypeTextRoutine(revealPlan, animType, visibleCharacterCount));
                }
                else
                {
                    bubbleText.maxVisibleCharacters = int.MaxValue;
                }
            }
            else
            {
                float typingDuration = Mathf.Max(0f, displayText.Length * typingSpeed);
                if (useTyping && typingDuration > 0f)
                {
                    isTyping = true;
                    typingTween = bubbleText.DOText(displayText, typingDuration)
                        .SetUpdate(true)
                        .SetEase(Ease.Linear)
                        .OnUpdate(HandleTypingTweenUpdated)
                        .OnComplete(() =>
                        {
                            HandleTypingTweenUpdated();
                            isTyping = false;
                        });
                }
                else
                {
                    bubbleText.text = displayText;
                    lastTypedCharacterCount = string.IsNullOrEmpty(displayText) ? 0 : displayText.Length;
                }
            }
        }

        if (duration > 0f)
            hideDelayTween = DOVirtual.DelayedCall(duration, Hide).SetUpdate(true);
    }

    private IEnumerator TypeTextRoutine(
        DialogueTextRevealPlan revealPlan,
        DialogueAnimType animType,
        int visibleCharacterCount)
    {
        DialogueTextRevealProfile profile = DialogueTextRevealUtility.ResolveProfile(animType);

        for (int i = 0; i < visibleCharacterCount; i++)
        {
            float explicitPause = DialogueTextRevealUtility.GetPauseBeforeCharacter(revealPlan, i);
            if (explicitPause > 0f)
                yield return WaitForTextRevealDelay(revealPlan, i, explicitPause);

            if (bubbleText == null)
                yield break;

            bubbleText.maxVisibleCharacters = i + 1;
            HandleTypingTweenUpdated();
            DialogueTextRevealUtility.ApplyTextEffects(
                bubbleText,
                revealPlan,
                i + 1,
                Time.unscaledTime);

            float delay = DialogueTextRevealUtility.GetPostCharacterDelay(
                profile,
                bubbleText.textInfo,
                i);

            if (delay > 0f)
                yield return WaitForTextRevealDelay(revealPlan, i + 1, delay);
        }

        float settleSeconds = DialogueTextRevealUtility.GetTextEffectSettleSeconds(revealPlan);
        if (settleSeconds > 0f)
            yield return WaitForTextRevealDelay(revealPlan, visibleCharacterCount, settleSeconds);

        typingRoutine = null;
        isTyping = false;

        if (bubbleText != null)
        {
            bubbleText.maxVisibleCharacters = visibleCharacterCount;
            StartTextEffectRoutine(revealPlan, visibleCharacterCount);
            HandleTypingTweenUpdated();
        }
    }

    private IEnumerator WaitForTextRevealDelay(
        DialogueTextRevealPlan revealPlan,
        int visibleCharacterCount,
        float seconds)
    {
        if (seconds <= 0f)
            yield break;

        if (!DialogueTextRevealUtility.HasTextEffects(revealPlan))
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (bubbleText == null)
                yield break;

            DialogueTextRevealUtility.ApplyTextEffects(
                bubbleText,
                revealPlan,
                visibleCharacterCount,
                Time.unscaledTime);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public bool TryAdvance()
    {
        if (isHiding)
            return false;

        if (isTyping)
        {
            CompleteTyping();
            return true;
        }

        Hide();
        return true;
    }

    public void SetLayoutOffset(Vector3 value)
    {
        layoutOffset = value;
        ApplyAnchorPose();
    }

    public void SetTailFlipX(bool value)
    {
        SetTailSide(value ? SpeechBubbleTailSide.Right : SpeechBubbleTailSide.Left);
    }

    public void SetTailSide(SpeechBubbleTailSide value)
    {
        tailSide = value;
        ResolveVisualReferences();
        ApplyTailFlip();
    }

    public void SetPlacement(SpeechBubbleTailSide value, Vector3 newLayoutOffset)
    {
        tailSide = value;
        layoutOffset = newLayoutOffset;
        ResolveVisualReferences();
        ApplyTailFlip();
        ApplyAnchorPose();
    }

    public bool TryGetAnchorWorldPosition(out Vector3 position)
    {
        position = default;

        if (!gameObject.activeInHierarchy)
            return false;

        position = ResolveAnchorPosition();
        return true;
    }

    public bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = default;

        if (!gameObject.activeInHierarchy)
            return false;

        ResolveVisualReferences();
        RectTransform sourceRect = backgroundRect != null
            ? backgroundRect
            : transform as RectTransform;
        if (sourceRect == null)
            return false;

        sourceRect.GetWorldCorners(worldCorners);
        bounds = new Bounds(worldCorners[0], Vector3.zero);
        for (int i = 1; i < worldCorners.Length; i++)
            bounds.Encapsulate(worldCorners[i]);

        return bounds.size.sqrMagnitude > 0.000001f;
    }

    public bool TryGetPlacementBounds(
        SpeechBubbleTailSide candidateTailSide,
        Vector3 candidateLayoutOffset,
        out Bounds bounds,
        out Vector3 desiredRootPosition,
        out Vector3 tailPivotPosition)
    {
        bounds = default;
        desiredRootPosition = default;
        tailPivotPosition = default;

        if (!gameObject.activeInHierarchy)
            return false;

        SpeechBubbleTailSide previousTailSide = tailSide;
        Vector3 previousLayoutOffset = layoutOffset;

        try
        {
            SetPlacement(candidateTailSide, candidateLayoutOffset);
            Canvas.ForceUpdateCanvases();

            if (!TryGetWorldBounds(out bounds))
                return false;

            desiredRootPosition = transform.position;
            tailPivotPosition = CalculateTailPivotPosition(candidateTailSide, bounds);
            return true;
        }
        finally
        {
            SetPlacement(previousTailSide, previousLayoutOffset);
            Canvas.ForceUpdateCanvases();
        }
    }

    public void Hide()
    {
        if (this == null)
            return;

        if (isHiding)
            return;

        isHiding = true;
        StopActiveTweens();

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    NotifyHidden();
                    ReleaseToPool();
                });
        }
        else
        {
            NotifyHidden();
            ReleaseToPool();
        }

        transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).SetUpdate(true);
    }

    private void LateUpdate()
    {
        if (target == null && targetPositionResolver == null)
            return;

        ApplyAnchorPose();
    }

    private void OnDisable()
    {
        StopActiveTweens();
        target = null;
        targetPositionResolver = null;
        targetRotationResolver = null;
        transform.rotation = originalRotation;
        hiddenAction = null;
        releaseAction = null;
        currentFullText = string.Empty;
        hasCurrentRevealPlan = false;
        currentRevealVisibleCharacterCount = 0;
        isTyping = false;
        isHiding = false;
        layoutOffset = Vector3.zero;
        tailSide = SpeechBubbleTailSide.Left;
        ApplyTailFlip();
    }

    private Vector3 ResolveAnchorPosition()
    {
        if (targetPositionResolver != null)
            return targetPositionResolver();

        if (target != null)
            return target.position;

        return transform.position - offset - layoutOffset;
    }

    private void ApplyAnchorPose()
    {
        Quaternion anchorRotation = ResolveAnchorRotation();
        Vector3 resolvedOffset = targetRotationResolver != null ? anchorRotation * offset : offset;
        transform.position = ResolveAnchorPosition() + resolvedOffset + layoutOffset;
        if (targetRotationResolver != null)
            transform.rotation = anchorRotation * originalRotation;
    }

    private Quaternion ResolveAnchorRotation()
    {
        return targetRotationResolver != null ? targetRotationResolver() : Quaternion.identity;
    }

    private static Vector3 CalculateTailPivotPosition(SpeechBubbleTailSide side, Bounds bounds)
    {
        float tailX = side == SpeechBubbleTailSide.Left ? bounds.min.x : bounds.max.x;
        return new Vector3(tailX, bounds.min.y, bounds.center.z);
    }

    private void StopActiveTweens()
    {
        StopTextEffectRoutine(true);

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        typingTween?.Kill();
        typingTween = null;
        ResetTypingAudioTracking();

        hideDelayTween?.Kill();
        hideDelayTween = null;

        if (canvasGroup != null)
            canvasGroup.DOKill();

        transform.DOKill();
    }

    private void CompleteTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        typingTween?.Kill();
        typingTween = null;
        isTyping = false;

        if (bubbleText != null)
        {
            bubbleText.text = currentFullText;
            bubbleText.maxVisibleCharacters = int.MaxValue;
            if (hasCurrentRevealPlan && currentRevealVisibleCharacterCount > 0)
                StartTextEffectRoutine(currentRevealPlan, currentRevealVisibleCharacterCount);
            else
                DialogueTextRevealUtility.ResetTextEffects(bubbleText);
        }

        lastTypedCharacterCount = string.IsNullOrEmpty(currentFullText) ? 0 : currentFullText.Length;
    }

    private void StartTextEffectRoutine(DialogueTextRevealPlan revealPlan, int visibleCharacterCount)
    {
        StopTextEffectRoutine(true);

        if (!DialogueTextRevealUtility.HasTextEffects(revealPlan) || bubbleText == null)
            return;

        textEffectRoutine = StartCoroutine(PlayTextEffectRoutine(revealPlan, visibleCharacterCount));
    }

    private IEnumerator PlayTextEffectRoutine(DialogueTextRevealPlan revealPlan, int visibleCharacterCount)
    {
        while (bubbleText != null)
        {
            DialogueTextRevealUtility.ApplyTextEffects(
                bubbleText,
                revealPlan,
                visibleCharacterCount,
                Time.unscaledTime);

            yield return null;
        }
    }

    private void StopTextEffectRoutine(bool resetText)
    {
        if (textEffectRoutine != null)
        {
            StopCoroutine(textEffectRoutine);
            textEffectRoutine = null;
        }

        if (resetText)
            DialogueTextRevealUtility.ResetTextEffects(bubbleText);
    }

    private void OnDestroy()
    {
        if (runtimeBackgroundMaterial != null)
            Destroy(runtimeBackgroundMaterial);

        if (runtimeTextMaterial != null)
            Destroy(runtimeTextMaterial);
    }

    private void ResolveVisualReferences()
    {
        if (bubbleText == null)
            bubbleText = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);

        if (bubbleBackground == null && bubbleText != null)
            bubbleBackground = bubbleText.GetComponentInParent<Image>();

        backgroundRect = bubbleBackground != null ? bubbleBackground.rectTransform : null;
        backgroundLayoutGroup = bubbleBackground != null
            ? bubbleBackground.GetComponent<HorizontalLayoutGroup>()
            : null;

        if (bubbleText != null)
        {
            textRect = bubbleText.rectTransform;
            textLayoutElement = bubbleText.GetComponent<LayoutElement>();
            CacheTextDefaults();
        }

        CacheTailFlipDefaults();
    }

    private void CacheTailFlipDefaults()
    {
        if (backgroundRect != null && !hasDefaultBackgroundLocalScale)
        {
            defaultBackgroundLocalScale = backgroundRect.localScale;
            hasDefaultBackgroundLocalScale = true;
        }

        if (backgroundRect != null && !hasDefaultBackgroundAnchoredPosition)
        {
            defaultBackgroundAnchoredPosition = backgroundRect.anchoredPosition;
            hasDefaultBackgroundAnchoredPosition = true;
        }

        if (backgroundLayoutGroup != null && defaultBackgroundPadding == null)
            defaultBackgroundPadding = ClonePadding(backgroundLayoutGroup.padding);

        if (textRect != null && !hasDefaultTextLocalScale)
        {
            defaultTextLocalScale = textRect.localScale;
            hasDefaultTextLocalScale = true;
        }
    }

    private void ApplyTailFlip()
    {
        bool flipX = tailSide == SpeechBubbleTailSide.Right;

        if (backgroundRect != null && hasDefaultBackgroundLocalScale)
        {
            Vector3 nextScale = defaultBackgroundLocalScale;
            if (flipX)
                nextScale.x = -nextScale.x;

            backgroundRect.localScale = nextScale;
        }

        if (backgroundRect != null && hasDefaultBackgroundAnchoredPosition)
            backgroundRect.anchoredPosition = defaultBackgroundAnchoredPosition;

        ApplyTailPadding(flipX);

        if (textRect != null && hasDefaultTextLocalScale)
        {
            Vector3 nextScale = defaultTextLocalScale;
            if (flipX && backgroundRect != null)
                nextScale.x = -nextScale.x;

            textRect.localScale = nextScale;
        }

        if (backgroundRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
    }

    private void ApplyTailPadding(bool flipX)
    {
        if (backgroundLayoutGroup == null || defaultBackgroundPadding == null)
            return;

        RectOffset padding = backgroundLayoutGroup.padding;
        if (padding == null)
            return;

        padding.left = flipX ? defaultBackgroundPadding.right : defaultBackgroundPadding.left;
        padding.right = flipX ? defaultBackgroundPadding.left : defaultBackgroundPadding.right;
        padding.top = defaultBackgroundPadding.top;
        padding.bottom = defaultBackgroundPadding.bottom;
    }

    private static RectOffset ClonePadding(RectOffset source)
    {
        return source != null
            ? new RectOffset(source.left, source.right, source.top, source.bottom)
            : null;
    }

    private void CacheTextDefaults()
    {
        if (hasCachedTextDefaults || bubbleText == null)
            return;

        defaultWordWrapping = bubbleText.enableWordWrapping;
        defaultOverflowMode = bubbleText.overflowMode;
        defaultHorizontalAlignment = bubbleText.horizontalAlignment;
        hasCachedTextDefaults = true;
    }

    private void PrepareLayoutForText(
        string text,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        ResolveVisualReferences();

        if (bubbleText == null)
            return;

        if (!preSizeLayout)
        {
            RestoreDefaultTextLayout();
            return;
        }

        EnsureTextLayoutElement();

        bubbleText.enableWordWrapping = true;
        bubbleText.overflowMode = TextOverflowModes.Overflow;
        bubbleText.horizontalAlignment = HorizontalAlignmentOptions.Left;
        bubbleText.text = text;

        float maxWidth = Mathf.Max(1f, maxTextWidth);
        float minWidth = Mathf.Clamp(minTextWidth, 1f, maxWidth);
        Vector2 unwrappedPreferred = bubbleText.GetPreferredValues(text, UnwrappedPreferredWidthProbe, 0f);
        float targetWidth = Mathf.Clamp(Mathf.Ceil(unwrappedPreferred.x), minWidth, maxWidth);
        Vector2 wrappedPreferred = bubbleText.GetPreferredValues(text, targetWidth, 0f);
        float targetHeight = Mathf.Max(minTextHeight, Mathf.Ceil(wrappedPreferred.y));

        ApplyPreparedTextLayout(targetWidth, targetHeight);
        bubbleText.text = string.Empty;

        if (backgroundRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
    }

    private void EnsureTextLayoutElement()
    {
        if (bubbleText == null)
            return;

        if (textLayoutElement == null)
            textLayoutElement = bubbleText.GetComponent<LayoutElement>();

        if (textLayoutElement == null)
            textLayoutElement = bubbleText.gameObject.AddComponent<LayoutElement>();
    }

    private void ApplyPreparedTextLayout(float width, float height)
    {
        if (textLayoutElement != null)
        {
            textLayoutElement.ignoreLayout = false;
            textLayoutElement.minWidth = width;
            textLayoutElement.preferredWidth = width;
            textLayoutElement.flexibleWidth = 0f;
            textLayoutElement.minHeight = height;
            textLayoutElement.preferredHeight = height;
            textLayoutElement.flexibleHeight = 0f;
        }

        if (textRect == null)
            return;

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void RestoreDefaultTextLayout()
    {
        if (hasCachedTextDefaults && bubbleText != null)
        {
            bubbleText.enableWordWrapping = defaultWordWrapping;
            bubbleText.overflowMode = defaultOverflowMode;
            bubbleText.horizontalAlignment = defaultHorizontalAlignment;
        }

        if (textLayoutElement == null)
            return;

        textLayoutElement.minWidth = -1f;
        textLayoutElement.preferredWidth = -1f;
        textLayoutElement.flexibleWidth = -1f;
        textLayoutElement.minHeight = -1f;
        textLayoutElement.preferredHeight = -1f;
        textLayoutElement.flexibleHeight = -1f;
    }

    private void InitializeRuntimeMaterials()
    {
        if (bubbleBackground != null)
        {
            Material sourceBackgroundMaterial = bubbleBackground.material;
            bool hasFill = sourceBackgroundMaterial != null && sourceBackgroundMaterial.HasProperty(FillColorId);
            bool hasBorder = sourceBackgroundMaterial != null && sourceBackgroundMaterial.HasProperty(BorderColorId);
            bool hasOutline = sourceBackgroundMaterial != null && sourceBackgroundMaterial.HasProperty(OutlineColorId);

            if (hasFill && (hasBorder || hasOutline))
            {
                runtimeBackgroundMaterial = new Material(sourceBackgroundMaterial)
                {
                    name = $"{sourceBackgroundMaterial.name} (SpeechBubble Runtime)"
                };
                backgroundBorderColorPropertyId = hasBorder ? BorderColorId : OutlineColorId;
                bubbleBackground.material = runtimeBackgroundMaterial;
                bubbleBackground.color = Color.white;
                supportsBackgroundTheme = true;
            }
        }

        if (bubbleText != null)
        {
            Material sourceTextMaterial = bubbleText.fontSharedMaterial;
            if (sourceTextMaterial != null)
            {
                runtimeTextMaterial = new Material(sourceTextMaterial)
                {
                    name = $"{sourceTextMaterial.name} (SpeechBubble Runtime)"
                };
                bubbleText.fontMaterial = runtimeTextMaterial;
                supportsTextFaceColor = runtimeTextMaterial.HasProperty(FaceColorId);
                if (supportsTextFaceColor)
                    bubbleText.color = Color.white;
            }
        }
    }

    private Vector4 CalculateNormalizedBorder()
    {
        if (bubbleBackground == null || bubbleBackground.sprite == null)
            return new Vector4(0.06f, 0.06f, 0.06f, 0.06f);

        Sprite sprite = bubbleBackground.sprite;
        Rect rect = sprite.rect;
        Vector4 border = sprite.border;
        float width = Mathf.Max(1f, rect.width);
        float height = Mathf.Max(1f, rect.height);

        return new Vector4(
            border.x / width,
            border.y / height,
            border.z / width,
            border.w / height);
    }

    private void ApplyTheme(SpeechBubbleThemeSettings theme)
    {
        Color borderColor = defaultBorderColor;
        Color fillColor = defaultFillColor;
        Color fontColor = defaultFontColor;

        if (theme != null && theme.UseCustomColors)
        {
            borderColor = theme.BorderColor;
            fillColor = theme.FillColor;
            fontColor = theme.FontColor;
        }

        if (supportsBackgroundTheme && runtimeBackgroundMaterial != null)
        {
            runtimeBackgroundMaterial.SetColor(backgroundBorderColorPropertyId, borderColor);
            runtimeBackgroundMaterial.SetColor(FillColorId, fillColor);
            if (runtimeBackgroundMaterial.HasProperty(TextureInfluenceId))
                runtimeBackgroundMaterial.SetFloat(TextureInfluenceId, backgroundTextureInfluence);
            if (runtimeBackgroundMaterial.HasProperty(BorderId))
                runtimeBackgroundMaterial.SetVector(BorderId, CalculateNormalizedBorder());
        }
        else if (bubbleBackground != null)
        {
            bubbleBackground.color = fillColor;
        }

        if (supportsTextFaceColor && runtimeTextMaterial != null)
        {
            runtimeTextMaterial.SetColor(FaceColorId, fontColor);
            bubbleText.color = Color.white;
        }
        else if (bubbleText != null)
        {
            bubbleText.color = fontColor;
        }
    }

    private void ReleaseToPool()
    {
        target = null;
        SetTailFlipX(false);

        Action<SpeechBubble> callback = releaseAction;
        releaseAction = null;
        callback?.Invoke(this);
    }

    private void NotifyHidden()
    {
        Action callback = hiddenAction;
        hiddenAction = null;
        callback?.Invoke();
    }

    private void ResetTypingAudioTracking()
    {
        lastTypedCharacterCount = 0;
        nextTypingSoundTime = 0f;
    }

    private void HandleTypingTweenUpdated()
    {
        if (!playTypingSound || bubbleText == null)
            return;

        int currentCharacterCount;
        if (bubbleText.maxVisibleCharacters != int.MaxValue)
        {
            int meshCharacterCount = bubbleText.textInfo != null ? bubbleText.textInfo.characterCount : 0;
            currentCharacterCount = Mathf.Min(bubbleText.maxVisibleCharacters, meshCharacterCount);
        }
        else
        {
            string currentText = bubbleText.text;
            currentCharacterCount = string.IsNullOrEmpty(currentText) ? 0 : currentText.Length;
        }

        if (currentCharacterCount <= lastTypedCharacterCount)
            return;

        if (Time.unscaledTime >= nextTypingSoundTime)
        {
            TypingAudioUtility.PlayBossTalking(this, target != null ? target.gameObject : gameObject);
            nextTypingSoundTime = Time.unscaledTime + typingSoundInterval;
        }

        lastTypedCharacterCount = currentCharacterCount;
    }
}
