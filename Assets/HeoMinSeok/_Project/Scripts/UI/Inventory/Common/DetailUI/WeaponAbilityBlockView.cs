using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// 책임 :
/// - 무기 툴팁의 능력 카드 한 블록을 렌더링한다.
/// - 아이콘, 제목, 입력 키 이미지, 쿨다운, 보조 메타 슬롯, 본문 설명을 각각 독립적으로 표시한다.
/// </summary>
public class WeaponAbilityBlockView : MonoBehaviour
{
    [Serializable]
    private struct InputHintSpriteEntry
    {
        /// <summary>
        /// 책임 :
        /// - 입력 문자열(Q/E 등)과 실제 키 이미지 Sprite를 1:1로 매핑한다.
        /// - WeaponAbilityBlockView가 텍스트 대신 키 이미지를 표시할 수 있게 한다.
        /// </summary>
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

    public void Set(string title, Sprite icon, string inputHint, float cooldownSeconds, string extraMeta, string body, System.Action<string> onGlossaryClick = null)
    {
        if (titleText != null) titleText.text = title ?? string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        ApplyInputHintSprite(inputHint);

        if (cooldownText != null)
        {
            if (cooldownSeconds > 0f)
                cooldownText.text = $"{cooldownSeconds:0.##}s";
            else
                cooldownText.text = "";
        }

        if (extraMetaText != null)
            extraMetaText.text = string.IsNullOrEmpty(extraMeta) ? "-" : extraMeta;

        if (bodyText != null)
        {
            bodyText.text = body ?? "";

            if (bodyRoot != null)
                bodyRoot.SetActive(!string.IsNullOrWhiteSpace(bodyText.text));

            // glossary link click support (DetailTextFormatter.ApplyGlossaryLinks)
            var handler = bodyText.GetComponent<TmpLinkClickHandler>();
            if (handler == null) handler = bodyText.gameObject.AddComponent<TmpLinkClickHandler>();
            handler.onGlossaryKeyClicked = onGlossaryClick;
        }
        else if (bodyRoot != null)
        {
            bodyRoot.SetActive(false);
        }
    }

    private void ApplyInputHintSprite(string inputHint)
    {
        if (inputHintImage == null)
            return;

        var resolvedSprite = ResolveInputHintSprite(inputHint);
        inputHintImage.sprite = resolvedSprite;
        inputHintImage.enabled = resolvedSprite != null;
    }

    private Sprite ResolveInputHintSprite(string inputHint)
    {
        if (string.IsNullOrWhiteSpace(inputHint) || inputHintSprites == null)
            return null;

        string normalized = inputHint.Trim();
        for (int i = 0; i < inputHintSprites.Count; i++)
        {
            var entry = inputHintSprites[i];
            if (string.Equals(entry.inputHint?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }

        return null;
    }
}
