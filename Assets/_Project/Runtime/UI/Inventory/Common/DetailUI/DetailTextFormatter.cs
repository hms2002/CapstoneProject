using System.Text.RegularExpressions;

public static class DetailTextFormatter
{
    /// <summary>
    /// 책임 :
    /// - 아이템 툴팁 본문 문자열의 의미 태그와 glossary 링크를 TMP rich text로 변환한다.
    /// - 텍스트 작성 규칙을 중앙에서 일관되게 적용한다.
    /// </summary>

    // [[term]] -> TMP link
    private static readonly Regex TermRegex = new(@"\[\[(.+?)\]\]");
    private static readonly Regex PositiveRegex = new(@"\{pos:(.+?)\}");
    private static readonly Regex NegativeRegex = new(@"\{neg:(.+?)\}");
    private static readonly Regex EmphasisRegex = new(@"\{em:(.+?)\}");
    private static readonly Regex ValueRegex = new(@"\{val:(.+?)\}");

    public static string ApplyGlossaryLinks(string raw, string linkColorHex = "5EC8FF")
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        return TermRegex.Replace(raw, m =>
        {
            var term = m.Groups[1].Value;
            return $"<link=\"glossary:{term}\"><color=#{linkColorHex}>{term}</color></link>";
        });
    }

    public static string Format(string raw, TooltipColorPalette palette, string fallbackGlossaryColorHex = "5EC8FF")
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        string formatted = raw;
        formatted = ApplyColorTag(formatted, PositiveRegex, palette != null ? palette.PositiveColorHex : "66FF66");
        formatted = ApplyColorTag(formatted, NegativeRegex, palette != null ? palette.NegativeColorHex : "FF5050");
        formatted = ApplyColorTag(formatted, EmphasisRegex, palette != null ? palette.EmphasisColorHex : "FF3296");
        formatted = ApplyColorTag(formatted, ValueRegex, palette != null ? palette.ValueColorHex : "FFBE00");

        string glossaryColor = palette != null ? palette.GlossaryColorHex : fallbackGlossaryColorHex;
        return ApplyGlossaryLinks(formatted, glossaryColor);
    }

    private static string ApplyColorTag(string raw, Regex regex, string colorHex)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        return regex.Replace(raw, m =>
        {
            string body = m.Groups[1].Value;
            return $"<color=#{colorHex}>{body}</color>";
        });
    }
}
