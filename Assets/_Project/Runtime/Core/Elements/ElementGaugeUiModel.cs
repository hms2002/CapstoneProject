using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : Core 속성 게이지 상태를 UI가 표시 가능한 불변 스냅샷으로 전달하는 값 타입이다.
/// </summary>
public readonly struct ElementGaugeUiModel
{
    public readonly GameplayTag ElementTag;
    public readonly Sprite Icon;
    public readonly float Current;
    public readonly float Threshold;
    public readonly float Ratio;
    public readonly bool Visible;

    public ElementGaugeUiModel(
        GameplayTag elementTag,
        Sprite icon,
        float current,
        float threshold,
        bool visible)
    {
        ElementTag = elementTag;
        Icon = icon;
        Current = current;
        Threshold = threshold;
        Ratio = threshold > 0f ? Mathf.Clamp01(current / threshold) : 0f;
        Visible = visible;
    }
}
