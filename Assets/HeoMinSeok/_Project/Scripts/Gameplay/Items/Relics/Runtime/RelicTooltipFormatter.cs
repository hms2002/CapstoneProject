using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 유물 툴팁에서 자주 쓰는 레벨 값 평가와 수치 포맷 규칙을 공통 제공한다.
/// - RelicLogic 구현체가 view 도움 없이 일관된 "[+-값]" 형식과 강조 토큰을 만들 수 있게 한다.
/// </summary>
public static class RelicTooltipFormatter
{
    public static float EvaluateLeveledValue(float baseValue, List<float> valuesByLevel, int level)
    {
        if (level < 1)
            level = 1;

        if (valuesByLevel != null && valuesByLevel.Count > 0)
        {
            int index = Mathf.Clamp(level - 1, 0, valuesByLevel.Count - 1);
            return valuesByLevel[index];
        }

        return baseValue * level;
    }

    public static string FormatSignedValueToken(float value, bool isPercent)
    {
        string sign = value > 0f ? "+" : string.Empty;
        float displayValue = isPercent ? value * 100f : value;
        string suffix = isPercent ? "%" : string.Empty;
        string raw = $"[{sign}{displayValue:0.##}{suffix}]";

        if (value > 0f)
            return "{pos:" + raw + "}";

        if (value < 0f)
            return "{neg:" + raw + "}";

        return raw;
    }

    public static string FormatUnsignedValueToken(float value, bool isPercent)
    {
        float displayValue = isPercent ? value * 100f : value;
        string suffix = isPercent ? "%" : string.Empty;
        return "{val:" + displayValue.ToString("0.##") + suffix + "}";
    }

    public static string FormatSeconds(float seconds)
    {
        return "{val:" + Mathf.Max(0f, seconds).ToString("0.##") + "}초";
    }

    /// <summary>
    /// 책임 :
    /// - {token} 형식의 자리표시자를 로직이 계산한 최종 문자열로 치환한다.
    /// - 허용된 포맷 규칙은 그대로 유지하고, 본문 템플릿만 데이터에서 조정 가능하게 만든다.
    /// </summary>
    public static string ReplaceTokens(string template, IDictionary<string, string> tokens)
    {
        string result = template ?? string.Empty;
        if (tokens == null || tokens.Count == 0)
            return result.TrimEnd();

        foreach (KeyValuePair<string, string> pair in tokens)
        {
            result = result.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
        }

        return result.TrimEnd();
    }
}
