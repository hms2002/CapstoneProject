using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CapstoneAudio;

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
    private Vector3 offset;
    private Tween typingTween;
    private Tween hideDelayTween;
    private Vector3 originalScale;
    private Action<SpeechBubble> releaseAction;
    private Action hiddenAction;
    private Material runtimeBackgroundMaterial;
    private Material runtimeTextMaterial;
    private RectTransform backgroundRect;
    private RectTransform textRect;
    private LayoutElement textLayoutElement;
    private bool supportsBackgroundTheme;
    private bool supportsTextFaceColor;
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

    private const float UnwrappedPreferredWidthProbe = 10000f;

    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int BorderId = Shader.PropertyToID("_Border");
    private static readonly int TextureInfluenceId = Shader.PropertyToID("_TextureInfluence");
    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");

    private void Awake()
    {
        originalScale = transform.localScale;
        ResolveVisualReferences();
        InitializeRuntimeMaterials();
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
        StopActiveTweens();

        this.target = target;
        this.offset = offset;
        hiddenAction = onHidden;
        releaseAction = onRelease;
        currentFullText = text;
        isTyping = false;
        isHiding = false;

        transform.position = target.position + offset;
        gameObject.SetActive(true);
        ApplyTheme(theme);
        PrepareLayoutForText(text, preSizeLayout, minTextWidth, maxTextWidth, minTextHeight);

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
            ResetTypingAudioTracking();

            float typingDuration = Mathf.Max(0f, text.Length * typingSpeed);
            if (useTyping && typingDuration > 0f)
            {
                isTyping = true;
                typingTween = bubbleText.DOText(text, typingDuration)
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
                bubbleText.text = text;
                lastTypedCharacterCount = string.IsNullOrEmpty(text) ? 0 : text.Length;
            }
        }

        if (duration > 0f)
            hideDelayTween = DOVirtual.DelayedCall(duration, Hide).SetUpdate(true);
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
        if (target == null)
            return;

        transform.position = target.position + offset;
    }

    private void OnDisable()
    {
        StopActiveTweens();
        target = null;
        hiddenAction = null;
        releaseAction = null;
        currentFullText = string.Empty;
        isTyping = false;
        isHiding = false;
    }

    private void StopActiveTweens()
    {
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
        typingTween?.Kill();
        typingTween = null;
        isTyping = false;

        if (bubbleText != null)
            bubbleText.text = currentFullText;

        lastTypedCharacterCount = string.IsNullOrEmpty(currentFullText) ? 0 : currentFullText.Length;
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

        if (bubbleText != null)
        {
            textRect = bubbleText.rectTransform;
            textLayoutElement = bubbleText.GetComponent<LayoutElement>();
            CacheTextDefaults();
        }
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

        string currentText = bubbleText.text;
        int currentCharacterCount = string.IsNullOrEmpty(currentText) ? 0 : currentText.Length;
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
