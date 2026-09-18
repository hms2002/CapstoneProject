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
        string key = bindings.GetBindingDisplayLabel(InputActionId.Dash);
        if (bindings.GetBinding(InputActionId.Dash).secondary != KeyCode.None)
            key += " / " + bindings.GetBindingDisplayLabel(InputActionId.Dash, true);
        prompt.text = $"<b>[ {key} ] 대쉬</b>\n탄막 방향으로 이동 키를 누르며 대쉬하세요.\n대쉬 중에는 무적으로 탄막을 통과합니다.";
    }
}
