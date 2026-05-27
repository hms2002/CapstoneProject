using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueChoiceKeyGlyph : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform glyphRoot;
    [SerializeField] private Image glyphIcon;
    [SerializeField] private TMP_Text fallbackLabel;

    public void Bind(int choiceIndex)
    {
        ResolveReferences();

        KeyCode key = GetChoiceKey(choiceIndex);
        bool hasShortcut = key != KeyCode.None;

        if (glyphRoot != null)
            glyphRoot.gameObject.SetActive(hasShortcut);

        if (!hasShortcut)
            return;

        InputGlyphPresentation glyph = InputGlyphDatabase.Resolve(key);
        InputGlyphVisualUtility.Apply(fallbackLabel, glyphIcon, glyph, (choiceIndex + 1).ToString());

        if (glyphIcon != null)
            glyphIcon.raycastTarget = false;
    }

    public void Hide()
    {
        ResolveReferences();

        if (glyphRoot != null)
            glyphRoot.gameObject.SetActive(false);

        if (glyphIcon != null)
            glyphIcon.enabled = false;

        if (fallbackLabel != null)
            fallbackLabel.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (glyphRoot == null)
            glyphRoot = ResolveGlyphRoot();

        if (glyphIcon == null && glyphRoot != null)
            glyphIcon = glyphRoot.GetComponent<Image>();

        if (fallbackLabel == null && glyphRoot != null)
            fallbackLabel = glyphRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private RectTransform ResolveGlyphRoot()
    {
        Transform glyph = transform.Find("KeyGlyph");
        if (glyph != null && glyph != transform)
            return glyph as RectTransform;

        Transform named = transform.Find("Image");
        if (named != null && named != transform)
            return named as RectTransform;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.transform != transform)
                return image.transform as RectTransform;
        }

        return null;
    }

    private static KeyCode GetChoiceKey(int choiceIndex)
    {
        return choiceIndex switch
        {
            0 => KeyCode.Alpha1,
            1 => KeyCode.Alpha2,
            2 => KeyCode.Alpha3,
            _ => KeyCode.None,
        };
    }
}
