using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponAbilityBlockView : MonoBehaviour
{
    [Serializable]
    private struct InputHintSpriteEntry
    {
        public string inputHint;
        public Sprite sprite;
    }

    [Header("Header")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;

    [Header("Meta")]
    [SerializeField] private Image inputHintImage;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text extraMetaText;
    [SerializeField] private List<InputHintSpriteEntry> inputHintSprites = new();

    [Header("Body")]
    [SerializeField] private GameObject bodyRoot;
    [SerializeField] private TMP_Text bodyText;

    public void Set(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        Action<string> onGlossaryClick = null)
    {
        Set(title, icon, inputHint, cooldownSeconds, extraMeta, body, null, onGlossaryClick);
    }

    public void Set(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick = null)
    {
        if (titleText != null)
            titleText.text = title ?? string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        ApplyInputHintSprite(inputHint, inputAction);

        if (cooldownText != null)
            cooldownText.text = cooldownSeconds > 0f ? $"{cooldownSeconds:0.##}s" : string.Empty;

        if (extraMetaText != null)
            extraMetaText.text = string.IsNullOrEmpty(extraMeta) ? "-" : extraMeta;

        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;

            if (bodyRoot != null)
                bodyRoot.SetActive(!string.IsNullOrWhiteSpace(bodyText.text));

            TmpLinkClickHandler handler = bodyText.GetComponent<TmpLinkClickHandler>();
            if (handler == null)
                handler = bodyText.gameObject.AddComponent<TmpLinkClickHandler>();

            handler.onGlossaryKeyClicked = onGlossaryClick;
        }
        else if (bodyRoot != null)
        {
            bodyRoot.SetActive(false);
        }
    }

    private void ApplyInputHintSprite(string inputHint, InputActionId? inputAction)
    {
        if (inputHintImage == null)
            return;

        Sprite resolvedSprite = ResolveInputHintSprite(inputHint, inputAction);
        inputHintImage.sprite = resolvedSprite;
        inputHintImage.enabled = resolvedSprite != null;
    }

    private Sprite ResolveInputHintSprite(string inputHint, InputActionId? inputAction)
    {
        if (inputAction.HasValue)
        {
            InputGlyphPresentation glyph = InputBindingService.EnsureInstance().GetBindingGlyph(inputAction.Value);
            Sprite glyphSprite = InputGlyphVisualUtility.ResolveIcon(
                glyph,
                inputHint,
                ResolveMappedInputHintSprite);
            if (glyphSprite != null)
                return glyphSprite;
        }

        return ResolveMappedInputHintSprite(inputHint);
    }

    private Sprite ResolveMappedInputHintSprite(string inputHint)
    {
        if (string.IsNullOrWhiteSpace(inputHint) || inputHintSprites == null)
            return null;

        string normalized = inputHint.Trim();
        for (int i = 0; i < inputHintSprites.Count; i++)
        {
            InputHintSpriteEntry entry = inputHintSprites[i];
            if (string.Equals(entry.inputHint?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }

        return null;
    }
}
