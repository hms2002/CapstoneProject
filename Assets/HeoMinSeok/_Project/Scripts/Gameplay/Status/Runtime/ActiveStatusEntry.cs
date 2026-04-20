using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - PlayerStatusRuntime 안에서 활성 상태 하나의 런타임 값을 보관하고 HUD 엔트리로 투영한다.
/// - 상태 정의 자산과 남은 시간, 스택, 강조 여부, 선택적 상태 태그를 묶어 현재 표시 가능한 상태 한 개를 표현한다.
/// </summary>
public sealed class ActiveStatusEntry
{
    public int RuntimeId { get; }
    public StatusHudDefinition Definition { get; private set; }
    public string OwnerKey { get; private set; }
    public GameplayTag StateTag { get; private set; }
    public int StackCount { get; private set; }
    public float RemainingTime { get; private set; }
    public float MaxTime { get; private set; }
    public bool IsHighlighted { get; private set; }
    public bool IsVisible { get; private set; }
    public string EffectTextOverride { get; private set; }
    public Sprite IconOverride { get; private set; }
    public bool? ShowStacksOverride { get; private set; }
    public bool? ShowDurationOverride { get; private set; }

    public ActiveStatusEntry(int runtimeId, in StatusApplyRequest request)
    {
        RuntimeId = runtimeId;
        Apply(request);
    }

    public void Apply(in StatusApplyRequest request)
    {
        if (request.Definition != null)
            Definition = request.Definition;

        if (!string.IsNullOrWhiteSpace(request.OwnerKey))
            OwnerKey = request.OwnerKey;

        StateTag = request.StateTag;
        StackCount = request.StackCount;
        RemainingTime = request.RemainingTime;
        MaxTime = request.MaxTime;
        IsHighlighted = request.IsHighlighted;
        IsVisible = request.IsVisible;
        EffectTextOverride = request.EffectTextOverride;
        IconOverride = request.IconOverride;
        ShowStacksOverride = request.ShowStacksOverride;
        ShowDurationOverride = request.ShowDurationOverride;
    }

    public StatusHudEntry ToHudEntry()
    {
        if (Definition == null)
            return default;

        string fallbackOwnerKey = string.IsNullOrWhiteSpace(OwnerKey)
            ? $"player.status.{Definition.StatusId}.{RuntimeId}"
            : OwnerKey;

        return Definition.CreateEntry(
            fallbackOwnerKey,
            StackCount,
            RemainingTime,
            MaxTime,
            IsHighlighted,
            IsVisible,
            EffectTextOverride,
            IconOverride,
            ShowStacksOverride,
            ShowDurationOverride);
    }
}
