using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 무기 툴팁의 요약 스탯 한 줄을 표시한다.
/// - 고정 포맷의 "스탯 이름 [+-값]" 구조를 한 줄 텍스트로 렌더링한다.
/// </summary>
public class WeaponStatLineView : MonoBehaviour
{
    [SerializeField] private TMP_Text bodyText;

    public void Set(string label, string value)
    {
        if (bodyText != null)
            bodyText.text = BuildInlineText(label, value);
    }

    private static string BuildInlineText(string label, string value)
    {
        string safeLabel = label ?? string.Empty;
        string safeValue = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safeValue))
            return $"● {safeLabel}";
        if (string.IsNullOrWhiteSpace(safeLabel))
            return $"● {safeValue}";
        return $"● {safeLabel} {safeValue}";
    }
}
