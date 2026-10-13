using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Projects keywords in the visible item description into authored companion cards.
/// Gameplay state and keyword effects remain owned by their existing systems.
/// </summary>
public class ItemKeywordTooltipView : MonoBehaviour
{
    [Header("Authored Cards")]
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private RectTransform electrocutedCard;
    [SerializeField] private RectTransform burnCard;
    [SerializeField] private TMP_Text electrocutedTitle;
    [SerializeField] private TMP_Text electrocutedBody;
    [SerializeField] private TMP_Text burnTitle;
    [SerializeField] private TMP_Text burnBody;

    private static readonly Regex GlossaryLinkRegex = new("<link=\"glossary:([^\"]+)\">");
    private readonly List<TMP_Text> sourceTexts = new();
    private readonly Vector3[] worldCorners = new Vector3[4];
    private bool dirty;

    private const string ElectrocutedDescription =
        "감전 시 피해를 주고, 감전 상태는 4초 동안 지속됩니다.\n" +
        "주변 감전 적을 따라 기본 최대 3명에게 피해가 연쇄됩니다.";
    private const string BurnDescription =
        "기본적으로 1초마다 1중첩을 소모해 화염 피해 수치의 50%만큼 피해를 줍니다.\n\n" +
        "최대 99중첩까지 쌓을 수 있습니다.\n\n기본 화상 피해에는 치명타가 발생하지 않습니다.";

    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        dirty = true;
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        Clear();
    }

    private void LateUpdate()
    {
        if (dirty)
            Refresh();
    }

    private void HandleTextChanged(UnityEngine.Object changed)
    {
        if (changed is TMP_Text text && IsSourceText(text))
            dirty = true;
    }

    private void HandleLocaleChanged(Locale locale) => dirty = true;

    public void Refresh()
    {
        if (!isActiveAndEnabled || stackRoot == null)
        {
            Clear();
            return;
        }

        bool hasElectrocuted = false;
        bool hasBurn = false;
        GetComponentsInChildren(false, sourceTexts);
        for (int i = 0; i < sourceTexts.Count; i++)
        {
            TMP_Text text = sourceTexts[i];
            if (!IsSourceText(text))
                continue;

            FindKeywords(text.text, ref hasElectrocuted, ref hasBurn);
            if (hasElectrocuted && hasBurn)
                break;
        }

        SetText(electrocutedTitle, GameText.Get("term.감전", "감전"));
        SetText(electrocutedBody, GameText.Get("ui.item_keyword.electrocuted.description", ElectrocutedDescription));
        SetText(burnTitle, GameText.Get("term.화상", "화상"));
        SetText(burnBody, GameText.Get("ui.item_keyword.burn.description", BurnDescription));

        bool showElectrocuted = hasElectrocuted && electrocutedCard != null;
        bool showBurn = hasBurn && burnCard != null;
        SetVisible(electrocutedCard, showElectrocuted);
        SetVisible(burnCard, showBurn);
        SetVisible(stackRoot, showElectrocuted || showBurn);
        if (stackRoot.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(stackRoot);
        dirty = false;
    }

    public void Clear()
    {
        SetVisible(electrocutedCard, false);
        SetVisible(burnCard, false);
        SetVisible(stackRoot, false);
        sourceTexts.Clear();
        dirty = false;
    }

    /// <summary>Includes the cards when the hover controller keeps the entire tooltip on screen.</summary>
    public Rect GetLocalBounds()
    {
        RectTransform ownerRect = transform as RectTransform;
        Rect bounds = ownerRect != null ? ownerRect.rect : default;
        if (stackRoot == null || !stackRoot.gameObject.activeInHierarchy)
            return bounds;

        stackRoot.GetWorldCorners(worldCorners);
        float xMin = bounds.xMin;
        float yMin = bounds.yMin;
        float xMax = bounds.xMax;
        float yMax = bounds.yMax;
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 corner = transform.InverseTransformPoint(worldCorners[i]);
            xMin = Mathf.Min(xMin, corner.x);
            yMin = Mathf.Min(yMin, corner.y);
            xMax = Mathf.Max(xMax, corner.x);
            yMax = Mathf.Max(yMax, corner.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private bool IsSourceText(TMP_Text text)
    {
        return text != null && text.isActiveAndEnabled && text.transform.IsChildOf(transform) &&
            (stackRoot == null || !text.transform.IsChildOf(stackRoot));
    }

    private static void FindKeywords(string content, ref bool hasElectrocuted, ref bool hasBurn)
    {
        if (string.IsNullOrEmpty(content))
            return;

        // DetailTextFormatter keeps the Korean glossary identity stable across locales.
        foreach (Match match in GlossaryLinkRegex.Matches(content))
        {
            switch (match.Groups[1].Value)
            {
                case "감전":
                case "전기 속성":
                    hasElectrocuted = true;
                    break;
                case "화상":
                case "화상 피해":
                    hasBurn = true;
                    break;
            }
        }

        // Existing untagged Korean descriptions may name the status directly.
        hasElectrocuted |= content.IndexOf("감전", StringComparison.Ordinal) >= 0;
        hasBurn |= content.IndexOf("화상", StringComparison.Ordinal) >= 0;

        // Weapon stat labels use localized plain captions instead of glossary links.
        hasElectrocuted |= ContainsCaption(content, GameText.Get("term.감전", "감전")) ||
            ContainsCaption(content, GameText.Get("term.전기 속성", "전기 속성"));
        hasBurn |= ContainsCaption(content, GameText.Get("term.화상", "화상")) ||
            ContainsCaption(content, GameText.Get("term.화상 피해", "화상 피해"));
    }

    private static bool ContainsCaption(string content, string caption)
    {
        if (string.IsNullOrEmpty(caption))
            return false;

        int searchFrom = 0;
        while (searchFrom < content.Length)
        {
            int index = content.IndexOf(caption, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;
            int end = index + caption.Length;
            bool startsWord = !IsLatinWordCharacter(caption[0]) || index == 0 || !IsLatinWordCharacter(content[index - 1]);
            bool endsWord = !IsLatinWordCharacter(caption[caption.Length - 1]) || end == content.Length || !IsLatinWordCharacter(content[end]);
            if (startsWord && endsWord)
                return true;
            searchFrom = index + 1;
        }
        return false;
    }

    private static bool IsLatinWordCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private static void SetText(TMP_Text text, string content)
    {
        if (text != null && text.text != content)
            text.text = content;
    }

    private static void SetVisible(RectTransform rect, bool visible)
    {
        if (rect != null && rect.gameObject.activeSelf != visible)
            rect.gameObject.SetActive(visible);
    }
}
