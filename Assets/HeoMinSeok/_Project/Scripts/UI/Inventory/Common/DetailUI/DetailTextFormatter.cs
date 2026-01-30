using System.Text.RegularExpressions;

public static class DetailTextFormatter
{
    // [[term]] -> TMP link
    private static readonly Regex TermRegex = new(@"\[\[(.+?)\]\]");

    public static string ApplyGlossaryLinks(string raw, string linkColorHex = "5EC8FF")
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        return TermRegex.Replace(raw, m =>
        {
            var term = m.Groups[1].Value;
            return $"<link=\"glossary:{term}\"><color=#{linkColorHex}>{term}</color></link>";
        });
    }
}
