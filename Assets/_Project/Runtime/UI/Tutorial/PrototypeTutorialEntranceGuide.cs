using TMPro;
using UnityEngine;

/// <summary>Projects the current action binding onto a scene-authored world-space entrance sign.</summary>
public sealed class PrototypeTutorialEntranceGuide : MonoBehaviour
{
    [SerializeField] private InputActionId action;
    [SerializeField] private SpriteRenderer glyph;
    [SerializeField] private TMP_Text fallbackLabel;
    [SerializeField] private PrototypeTutorialUpgrade tutorial;
    [SerializeField] private int visibleStage = -1;
    [SerializeField] private Transform presentationRoot;
    [SerializeField] private bool animate;
    [SerializeField] private float glyphSize = .8f;
    private InputBindingService bindings;
    private SpriteRenderer[] sprites;
    private TMP_Text[] labels;
    private Color[] spriteColors, labelColors;
    private Vector3 restPosition;
    private bool showing;
    private float visibility, fromVisibility, elapsed;

    private void Awake()
    {
        if (presentationRoot == null) return;
        restPosition = presentationRoot.localPosition;
        sprites = presentationRoot.GetComponentsInChildren<SpriteRenderer>(true);
        labels = presentationRoot.GetComponentsInChildren<TMP_Text>(true);
        spriteColors = System.Array.ConvertAll(sprites, s => s.color);
        labelColors = System.Array.ConvertAll(labels, t => t.color);
    }

    private void OnEnable()
    {
        bindings = InputBindingService.EnsureInstance();
        bindings.BindingChanged += OnBindingChanged;
        Refresh();
        showing = false;
        visibility = fromVisibility = elapsed = 0f;
        TickPresentation(0f);
    }

    private void OnDisable()
    {
        if (bindings != null) bindings.BindingChanged -= OnBindingChanged;
        bindings = null;
        if (presentationRoot != null)
        {
            presentationRoot.gameObject.SetActive(false);
            presentationRoot.localPosition = restPosition;
        }
    }

    private void LateUpdate() => TickPresentation(Time.unscaledDeltaTime);

    private void TickPresentation(float delta)
    {
        if (presentationRoot == null) return;
        bool wanted = visibleStage < 0 || (tutorial != null && tutorial.isActiveAndEnabled && tutorial.Stage == visibleStage);
        if (wanted != showing)
        {
            showing = wanted;
            fromVisibility = visibility;
            elapsed = 0f;
            // Render the starting pose even if spawning the encounter caused a long frame.
            delta = 0f;
        }
        elapsed += delta;
        float t = animate ? Mathf.Clamp01(elapsed / (showing ? .25f : .15f)) : 1f;
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        visibility = Mathf.Lerp(fromVisibility, showing ? 1f : 0f, eased);
        presentationRoot.gameObject.SetActive(showing || visibility > 0f);
        // ItemDetailPanel-style fade/slide, enlarged for readability in world space.
        presentationRoot.localPosition = restPosition + Vector3.down * (.45f * (1f - visibility));
        for (int i = 0; i < sprites.Length; i++)
        {
            Color color = spriteColors[i]; color.a *= visibility; sprites[i].color = color;
        }
        for (int i = 0; i < labels.Length; i++)
        {
            Color color = labelColors[i]; color.a *= visibility; labels[i].color = color;
        }
    }

    private void OnBindingChanged(InputActionId changed)
    {
        if (changed == action) Refresh();
    }

    private void Refresh()
    {
        InputGlyphPresentation presentation = bindings.GetBindingGlyph(action);
        if (presentation.Key == KeyCode.None)
            presentation = bindings.GetBindingGlyph(action, true);
        glyph.sprite = presentation.Icon;
        glyph.enabled = presentation.HasIcon;
        if (presentation.HasIcon)
        {
            Vector2 size = presentation.Icon.bounds.size;
            glyph.transform.localScale = Vector3.one * (glyphSize / Mathf.Max(size.x, size.y, .001f));
        }
        fallbackLabel.gameObject.SetActive(!presentation.HasIcon);
        fallbackLabel.text = presentation.DisplayLabel;
    }
}
