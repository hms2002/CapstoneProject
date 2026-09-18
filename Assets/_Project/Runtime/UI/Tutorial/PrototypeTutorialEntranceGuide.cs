using TMPro;
using UnityEngine;

/// <summary>Projects the current action binding onto a scene-authored world-space entrance sign.</summary>
public sealed class PrototypeTutorialEntranceGuide : MonoBehaviour
{
    [SerializeField] private InputActionId action;
    [SerializeField] private SpriteRenderer glyph;
    [SerializeField] private TMP_Text fallbackLabel;
    private InputBindingService bindings;

    private void OnEnable()
    {
        bindings = InputBindingService.EnsureInstance();
        bindings.BindingChanged += OnBindingChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (bindings != null) bindings.BindingChanged -= OnBindingChanged;
        bindings = null;
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
            glyph.transform.localScale = Vector3.one * (.8f / Mathf.Max(size.x, size.y, .001f));
        }
        fallbackLabel.gameObject.SetActive(!presentation.HasIcon);
        fallbackLabel.text = presentation.DisplayLabel;
    }
}
