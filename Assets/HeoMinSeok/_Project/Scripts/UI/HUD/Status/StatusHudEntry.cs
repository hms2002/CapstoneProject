using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - HUD가 상태 하나를 표시하는 데 필요한 공통 메타데이터를 담는다.
/// - 스택, 지속시간, 아이콘, 이름, 서사 설명, 효과 설명을 상태 소유 계층과 분리해 HUD projection 모델로 제공한다.
/// </summary>
[Serializable]
public readonly struct StatusHudEntry
{
    public string OwnerKey { get; }
    public string StatusId { get; }
    public string NameText { get; }
    public string StoryText { get; }
    public string EffectText { get; }
    public Sprite Icon { get; }
    public int StackCount { get; }
    public bool ShowStacks { get; }
    public float RemainingTime { get; }
    public float MaxTime { get; }
    public bool ShowDuration { get; }
    public StatusHudGroup Group { get; }
    public int Priority { get; }
    public bool IsHighlighted { get; }
    public bool IsVisible { get; }

    public StatusHudEntry(
        string ownerKey,
        string statusId,
        string nameText,
        string storyText,
        string effectText,
        Sprite icon,
        int stackCount,
        bool showStacks,
        float remainingTime,
        float maxTime,
        bool showDuration,
        StatusHudGroup group,
        int priority,
        bool isHighlighted,
        bool isVisible)
    {
        OwnerKey = ownerKey;
        StatusId = statusId;
        NameText = nameText;
        StoryText = storyText;
        EffectText = effectText;
        Icon = icon;
        StackCount = stackCount;
        ShowStacks = showStacks;
        RemainingTime = remainingTime;
        MaxTime = maxTime;
        ShowDuration = showDuration;
        Group = group;
        Priority = priority;
        IsHighlighted = isHighlighted;
        IsVisible = isVisible;
    }
}
