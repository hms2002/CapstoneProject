using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private Transform target;
    private Vector3 offset;
    private Tween typingTween;
    private Tween hideDelayTween;
    private Vector3 originalScale;
    private Action<SpeechBubble> releaseAction;
    private Material runtimeBackgroundMaterial;
    private Material runtimeTextMaterial;
    private bool supportsBackgroundTheme;
    private bool supportsTextFaceColor;
    private int backgroundBorderColorPropertyId = -1;

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
        Action<SpeechBubble> onRelease)
    {
        StopActiveTweens();

        this.target = target;
        this.offset = offset;
        releaseAction = onRelease;

        transform.position = target.position + offset;
        gameObject.SetActive(true);
        ApplyTheme(theme);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.DOKill();
        }

        transform.localScale = Vector3.zero;
        transform.DOKill();
        transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack);

        if (bubbleText != null)
        {
            bubbleText.text = string.Empty;

            if (useTyping)
                typingTween = bubbleText.DOText(text, text.Length * typingSpeed).SetEase(Ease.Linear);
            else
                bubbleText.text = text;
        }

        if (duration > 0f)
            hideDelayTween = DOVirtual.DelayedCall(duration, Hide);
    }

    public void Hide()
    {
        StopActiveTweens();

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
            {
                ReleaseToPool();
            });
        }
        else
        {
            ReleaseToPool();
        }

        transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
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
        releaseAction = null;
    }

    private void StopActiveTweens()
    {
        typingTween?.Kill();
        typingTween = null;

        hideDelayTween?.Kill();
        hideDelayTween = null;

        if (canvasGroup != null)
            canvasGroup.DOKill();

        transform.DOKill();
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
}
