using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleIntroView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootGroup;

    [Header("Slide")]
    [SerializeField] private Image slideImage;
    [SerializeField] private TMP_Text scriptText;

    [Header("Skip Prompt")]
    [SerializeField] private GameObject skipPromptRoot;
    [SerializeField] private CanvasGroup skipPromptGroup;
    [SerializeField] private Image skipKeyIconImage;
    [SerializeField] private TMP_Text skipKeyLabel;
    [SerializeField] private HoldFillButtonView skipHoldButtonView;
    [SerializeField] private Image skipHoldFillImage;
    [Tooltip("When disabled, the authored skip prompt icon and text remain untouched on show.")]
    [SerializeField] private bool autoApplySkipKeyGlyphOnShow;
    [Tooltip("When disabled, the authored skip hold fill color remains untouched on show.")]
    [SerializeField] private bool autoApplySkipFillColorOnShow;
    [SerializeField] private Sprite fallbackSpaceIcon;

    private readonly List<Graphic> skipPromptGraphics = new();
    private readonly List<Color> skipPromptGraphicBaseColors = new();
    private bool skipPromptGraphicsCaptured;
    private float skipPromptAlpha = 1f;

    public bool IsReady
    {
        get
        {
            ResolveReferences();
            return root != null && rootGroup != null && slideImage != null && scriptText != null;
        }
    }

    public float SlideAlpha
    {
        get
        {
            if (slideImage == null)
                return 0f;

            return slideImage.color.a;
        }
    }

    public float RootAlpha
    {
        get
        {
            ResolveReferences();
            return rootGroup != null ? rootGroup.alpha : 0f;
        }
    }

    public float SkipPromptAlpha => skipPromptAlpha;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Show()
    {
        Show(KeyCode.Space);
    }

    public void Show(KeyCode skipKey)
    {
        ResolveReferences();

        if (root != null)
            root.SetActive(true);

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = true;
        }

        if (skipPromptRoot != null)
            skipPromptRoot.SetActive(true);

        SetSkipPromptAlpha(0f);
        SetSkipFill(0f);
        SetText(string.Empty);
        SetSlideAlpha(0f);

        if (autoApplySkipKeyGlyphOnShow)
            ApplySkipKeyGlyph(skipKey);
    }

    public void HideImmediate()
    {
        ResolveReferences();

        SetText(string.Empty);
        SetSkipFill(0f);
        SetSlideAlpha(0f);
        SetSkipPromptAlpha(0f);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        if (root != null)
            root.SetActive(false);
    }

    public void SetRootAlpha(float alpha)
    {
        ResolveReferences();

        if (rootGroup != null)
            rootGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void SetSlideSprite(Sprite sprite)
    {
        ResolveReferences();

        if (slideImage == null)
            return;

        slideImage.sprite = sprite;
        slideImage.enabled = sprite != null;
        slideImage.preserveAspect = true;
    }

    public void SetSlideAlpha(float alpha)
    {
        ResolveReferences();

        if (slideImage == null)
            return;

        Color color = slideImage.color;
        color.a = Mathf.Clamp01(alpha);
        slideImage.color = color;
    }

    public void SetText(string text)
    {
        ResolveReferences();

        if (scriptText != null)
            scriptText.text = text ?? string.Empty;
    }

    public void SetSkipPromptAlpha(float alpha)
    {
        ResolveReferences();

        skipPromptAlpha = Mathf.Clamp01(alpha);

        if (skipPromptRoot == null)
            return;

        if (!skipPromptRoot.activeSelf)
            skipPromptRoot.SetActive(true);

        if (skipPromptGroup != null)
        {
            skipPromptGroup.alpha = skipPromptAlpha;
            skipPromptGroup.interactable = false;
            skipPromptGroup.blocksRaycasts = false;
            return;
        }

        CaptureSkipPromptGraphicBaseColorsIfNeeded();
        for (int i = 0; i < skipPromptGraphics.Count; i++)
        {
            Graphic graphic = skipPromptGraphics[i];
            if (graphic == null)
                continue;

            Color color = i < skipPromptGraphicBaseColors.Count
                ? skipPromptGraphicBaseColors[i]
                : graphic.color;
            color.a *= skipPromptAlpha;
            graphic.color = color;
        }
    }

    public void SetSkipFill(float normalized)
    {
        ResolveReferences();

        skipHoldButtonView?.SetProgress(normalized);

        if (skipHoldFillImage == null)
            return;

        skipHoldFillImage.fillAmount = Mathf.Clamp01(normalized);
        skipHoldFillImage.enabled = skipHoldFillImage.fillAmount > 0f;
    }

    public void ApplySkipFillColor(Color color)
    {
        ResolveReferences();

        if (!autoApplySkipFillColorOnShow)
            return;

        if (skipHoldFillImage != null)
        {
            skipHoldFillImage.color = color;
            UpdateSkipPromptBaseColor(skipHoldFillImage, color);
            SetSkipPromptAlpha(skipPromptAlpha);
        }
    }

    public void ApplySkipKeyGlyph(KeyCode key)
    {
        ResolveReferences();

        InputGlyphPresentation glyph = InputGlyphDatabase.Resolve(key);
        InputGlyphVisualUtility.Apply(
            skipKeyLabel,
            skipKeyIconImage,
            glyph,
            InputGlyphDatabase.GetDisplayLabel(key),
            fallbackSpaceIcon);
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = gameObject;

        if (rootGroup == null && root != null)
            rootGroup = root.GetComponent<CanvasGroup>();

        if (skipPromptGroup == null && skipPromptRoot != null)
            skipPromptGroup = skipPromptRoot.GetComponent<CanvasGroup>();

        if (slideImage == null && root != null)
            slideImage = FindChildComponent<Image>(root.transform, "SlideImage");

        if (scriptText == null && root != null)
            scriptText = FindChildComponent<TMP_Text>(root.transform, "ScriptText");
    }

    private void CaptureSkipPromptGraphicBaseColorsIfNeeded()
    {
        if (skipPromptGraphicsCaptured || skipPromptRoot == null)
            return;

        skipPromptGraphicsCaptured = true;
        skipPromptGraphics.Clear();
        skipPromptGraphicBaseColors.Clear();

        skipPromptRoot.GetComponentsInChildren(includeInactive: true, skipPromptGraphics);
        for (int i = 0; i < skipPromptGraphics.Count; i++)
        {
            Graphic graphic = skipPromptGraphics[i];
            skipPromptGraphicBaseColors.Add(graphic != null ? graphic.color : Color.white);
        }
    }

    private void UpdateSkipPromptBaseColor(Graphic target, Color color)
    {
        if (target == null)
            return;

        CaptureSkipPromptGraphicBaseColorsIfNeeded();
        for (int i = 0; i < skipPromptGraphics.Count; i++)
        {
            if (skipPromptGraphics[i] != target)
                continue;

            if (i < skipPromptGraphicBaseColors.Count)
                skipPromptGraphicBaseColors[i] = color;
            return;
        }
    }

    private static T FindChildComponent<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        T[] candidates = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate != null && candidate.gameObject.name == objectName)
                return candidate;
        }

        return null;
    }
}
