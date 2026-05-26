using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HoldFillButtonView : MonoBehaviour
{
    [Header("Images")]
    [SerializeField] private Image baseImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Sprite defaultBaseSprite;
    [SerializeField] private Sprite defaultFillSprite;

    [Header("Fill")]
    [SerializeField] private RectTransform filledClipRoot;
    [SerializeField] private RectTransform fillWidthSource;
    [SerializeField] private bool driveFillImageAmount = true;
    [SerializeField] private bool driveClipWidth = true;
    [SerializeField] private bool hideFillWhenEmpty = true;
    [SerializeField, Range(0f, 1f)] private float progress;

    [Header("Interactable Visual")]
    [SerializeField] private List<Graphic> normalGraphics = new();
    [SerializeField] private List<Graphic> filledGraphics = new();
    [SerializeField] private Color disabledColor = new(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private bool applyDisabledAlpha;
    [SerializeField, Range(0f, 1f)] private float disabledAlphaMultiplier = 0.45f;

    private readonly List<Color> normalGraphicColors = new();
    private readonly List<Color> filledGraphicColors = new();
    private Color baseImageColor = Color.white;
    private Color fillImageColor = Color.white;
    private Graphic capturedBaseImage;
    private Graphic capturedFillImage;
    private bool colorsCaptured;
    private bool interactableVisualState = true;

    public float Progress => progress;

    private void Reset()
    {
        baseImage = GetComponent<Image>();
        fillWidthSource = transform as RectTransform;
    }

    private void Awake()
    {
        CaptureGraphicColorsIfNeeded();
        ApplySprites();
        ApplyProgress();
        ApplyInteractableVisual();
    }

    private void OnEnable()
    {
        CaptureGraphicColorsIfNeeded();
        ApplySprites();
        ApplyProgress();
        ApplyInteractableVisual();
    }

    private void OnValidate()
    {
        progress = Mathf.Clamp01(progress);
        if (!Application.isPlaying)
            return;

        ApplySprites();
        ApplyProgress();
        ApplyInteractableVisual();
    }

    public void SetProgress(float normalized)
    {
        progress = Mathf.Clamp01(normalized);
        ApplyProgress();
    }

    public void SetSprites(Sprite baseSprite, Sprite fillSprite)
    {
        if (baseSprite != null)
            defaultBaseSprite = baseSprite;

        if (fillSprite != null)
            defaultFillSprite = fillSprite;

        ApplySprites();
        ApplyProgress();
    }

    public void SetInteractableVisual(bool interactable)
    {
        interactableVisualState = interactable;
        ApplyInteractableVisual();
    }

    private void ApplyInteractableVisual()
    {
        CaptureGraphicColorsIfNeeded();

        ApplyGraphicVisual(baseImage, baseImageColor, interactableVisualState);
        ApplyGraphicVisual(fillImage, fillImageColor, interactableVisualState);
        ApplyGraphicVisual(normalGraphics, normalGraphicColors, interactableVisualState);
        ApplyGraphicVisual(filledGraphics, filledGraphicColors, interactableVisualState);
    }

    private void ApplySprites()
    {
        if (baseImage != null && defaultBaseSprite != null)
            baseImage.sprite = defaultBaseSprite;

        if (fillImage != null && defaultFillSprite != null)
            fillImage.sprite = defaultFillSprite;
    }

    private void ApplyProgress()
    {
        bool showFill = !hideFillWhenEmpty || progress > 0.0001f;

        if (fillImage != null)
        {
            if (driveFillImageAmount)
                fillImage.fillAmount = progress;

            fillImage.enabled = fillImage.sprite != null && showFill;
        }

        if (filledClipRoot != null)
        {
            if (ShouldDriveClipWidth())
                filledClipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ResolveFillWidth() * progress);

            if (hideFillWhenEmpty &&
                filledClipRoot.gameObject != gameObject &&
                filledClipRoot.gameObject.activeSelf != showFill)
            {
                filledClipRoot.gameObject.SetActive(showFill);
            }
        }
    }

    private bool ShouldDriveClipWidth()
    {
        if (!driveClipWidth)
            return false;
        if (fillImage == null || filledClipRoot != fillImage.rectTransform)
            return true;

        return !driveFillImageAmount || fillImage.type != Image.Type.Filled;
    }

    private float ResolveFillWidth()
    {
        RectTransform source = fillWidthSource;
        if (source == null && baseImage != null)
            source = baseImage.rectTransform;
        if (source == null)
            source = transform as RectTransform;

        return source != null ? Mathf.Max(0f, source.rect.width) : 0f;
    }

    private void CaptureGraphicColorsIfNeeded()
    {
        if (colorsCaptured &&
            capturedBaseImage == baseImage &&
            capturedFillImage == fillImage &&
            normalGraphicColors.Count == normalGraphics.Count &&
            filledGraphicColors.Count == filledGraphics.Count)
        {
            return;
        }

        capturedBaseImage = baseImage;
        baseImageColor = baseImage != null ? baseImage.color : Color.white;

        capturedFillImage = fillImage;
        fillImageColor = fillImage != null ? fillImage.color : Color.white;

        normalGraphicColors.Clear();
        for (int i = 0; i < normalGraphics.Count; i++)
            normalGraphicColors.Add(normalGraphics[i] != null ? normalGraphics[i].color : Color.white);

        filledGraphicColors.Clear();
        for (int i = 0; i < filledGraphics.Count; i++)
            filledGraphicColors.Add(filledGraphics[i] != null ? filledGraphics[i].color : Color.white);

        colorsCaptured = true;
    }

    private void ApplyGraphicVisual(Graphic graphic, Color baseColor, bool interactable)
    {
        if (graphic == null)
            return;

        graphic.color = BuildVisualColor(baseColor, interactable);
    }

    private void ApplyGraphicVisual(IReadOnlyList<Graphic> graphics, IReadOnlyList<Color> baseColors, bool interactable)
    {
        int count = Mathf.Min(graphics.Count, baseColors.Count);
        for (int i = 0; i < count; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            graphic.color = BuildVisualColor(baseColors[i], interactable);
        }
    }

    private Color BuildVisualColor(Color baseColor, bool interactable)
    {
        if (interactable)
        {
            baseColor.a = 1f;
            return baseColor;
        }

        Color color = disabledColor;
        color.a = applyDisabledAlpha ? baseColor.a * disabledAlphaMultiplier : 1f;
        return color;
    }
}
