using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 외부 시스템이 PlayerStatusRuntime에 상태 적용을 신청할 때 필요한 런타임 값과 표시 override를 한 번에 전달한다.
/// - 상태 소유자가 status definition, 남은 시간, 스택, 강조 여부, 선택적 상태 태그를 느슨하게 묶어 상태 허브에 넘기게 만든다.
/// </summary>
public readonly struct StatusApplyRequest
{
    public StatusHudDefinition Definition { get; }
    public string OwnerKey { get; }
    public GameplayTag StateTag { get; }
    public int StackCount { get; }
    public float RemainingTime { get; }
    public float MaxTime { get; }
    public bool IsHighlighted { get; }
    public bool IsVisible { get; }
    public string EffectTextOverride { get; }
    public Sprite IconOverride { get; }
    public bool? ShowStacksOverride { get; }
    public bool? ShowDurationOverride { get; }

    public StatusApplyRequest(
        StatusHudDefinition definition,
        string ownerKey,
        GameplayTag stateTag = null,
        int stackCount = 0,
        float remainingTime = 0f,
        float maxTime = 0f,
        bool isHighlighted = false,
        bool isVisible = true,
        string effectTextOverride = null,
        Sprite iconOverride = null,
        bool? showStacksOverride = null,
        bool? showDurationOverride = null)
    {
        Definition = definition;
        OwnerKey = ownerKey;
        StateTag = stateTag;
        StackCount = stackCount;
        RemainingTime = remainingTime;
        MaxTime = maxTime;
        IsHighlighted = isHighlighted;
        IsVisible = isVisible;
        EffectTextOverride = effectTextOverride;
        IconOverride = iconOverride;
        ShowStacksOverride = showStacksOverride;
        ShowDurationOverride = showDurationOverride;
    }
}
