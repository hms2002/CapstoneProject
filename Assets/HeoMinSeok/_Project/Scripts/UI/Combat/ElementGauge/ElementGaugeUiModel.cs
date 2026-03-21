using UnityEngine;
using UnityGAS;

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