using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects the scene-owned dash pause into its authored prompt using current key bindings.</summary>
public sealed class PrototypeTutorialPromptView : MonoBehaviour
{
    [SerializeField] private PrototypeTutorialUpgrade tutorial;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private Image dashGlyph;

    private void OnEnable() => Refresh();
    private void Update() => Refresh();
    private void OnDisable()
    {
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (prompt == null) return;
        // Keep the authored parent active for its key icon, without explanatory text.
        prompt.text = string.Empty;
        bool visible = tutorial != null && tutorial.isActiveAndEnabled && tutorial.IsDashPromptVisible;
        prompt.gameObject.SetActive(visible);
        if (!visible) return;
        InputBindingService bindings = InputBindingService.EnsureInstance();
        InputGlyphPresentation glyph = bindings.GetBindingGlyph(InputActionId.Dash);
        if (glyph.Key == KeyCode.None) glyph = bindings.GetBindingGlyph(InputActionId.Dash, true);
        if (dashGlyph != null)
        {
            dashGlyph.sprite = glyph.Icon;
            dashGlyph.gameObject.SetActive(glyph.HasIcon);
        }
    }
}
