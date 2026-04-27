using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class BossPatternRuntimeState
{
    // 이 클래스의 책임:
    // 공통 블랙보드 밖에서 패턴 선택/실행 이력과 예약 상태를 관리한다.
    // 패턴 시작 시점 잠금과 종료 후 후딜, 후속 연계 예약을 함께 반영해 다음 선택 가능 시점을 계산한다.

    private readonly Dictionary<BossPatternEntry, float> patternSelectionReadyTimes = new();
    private readonly Dictionary<BossPatternEntry, int> patternUseCounts = new();

    private BossPatternEntry selectedFollowUpPattern;
    private AbilityDefinition queuedFollowUpAbility;

    public BossPatternEntry ReservedPattern { get; private set; }
    public BossPatternEntry CurrentPattern { get; private set; }
    public BossPatternEntry LastUsedPattern { get; private set; }
    public int ConsecutivePatternUseCount { get; private set; }
    public bool ReservedPatternIsForcedFollowUp { get; private set; }
    public bool HasQueuedFollowUpAbility => queuedFollowUpAbility != null;

    public void ReservePattern(BossPatternEntry pattern)
    {
        ReservedPattern = pattern;
        ReservedPatternIsForcedFollowUp = pattern != null && pattern == selectedFollowUpPattern;
        selectedFollowUpPattern = null;
    }

    public void ClearReservedPattern()
    {
        ReservedPattern = null;
        ReservedPatternIsForcedFollowUp = false;
    }

    public void BeginPattern(BossPatternEntry pattern)
    {
        CurrentPattern = pattern;
        ReservedPattern = null;
        ReservedPatternIsForcedFollowUp = false;

        if (pattern == null)
            return;

        patternSelectionReadyTimes[pattern] = Time.time + pattern.AiSelectionLockTime;
        patternUseCounts[pattern] = GetUseCount(pattern) + 1;

        if (LastUsedPattern == pattern)
            ConsecutivePatternUseCount++;
        else
            ConsecutivePatternUseCount = 1;

        LastUsedPattern = pattern;
    }

    public void EndPattern(BossPatternEntry pattern)
    {
        if (pattern != null && pattern.PostPatternDelay > 0f)
            patternSelectionReadyTimes[pattern] = Time.time + pattern.PostPatternDelay;

        CurrentPattern = null;
    }

    public void ClearPatternContext()
    {
        ReservedPattern = null;
        CurrentPattern = null;
        ReservedPatternIsForcedFollowUp = false;
        selectedFollowUpPattern = null;
        queuedFollowUpAbility = null;
    }

    /// <summary>
    /// 책임:
    /// 정상 종료된 패턴이 요청한 후속 Ability를 다음 선택 사이클까지 보관한다.
    /// </summary>
    public void QueueFollowUpAbility(AbilityDefinition ability)
    {
        queuedFollowUpAbility = ability;
    }

    /// <summary>
    /// 책임:
    /// 큐에 있던 후속 Ability를 한 번만 소비해 다음 패턴 선택에 넘긴다.
    /// </summary>
    public bool TryConsumeQueuedFollowUpAbility(out AbilityDefinition ability)
    {
        ability = queuedFollowUpAbility;
        queuedFollowUpAbility = null;
        return ability != null;
    }

    /// <summary>
    /// 책임:
    /// 선택된 패턴이 일반 선택 결과가 아니라 후속 연계 결과임을 다음 ReservePattern 호출까지 전달한다.
    /// </summary>
    public void MarkSelectedPatternAsForcedFollowUp(BossPatternEntry pattern)
    {
        selectedFollowUpPattern = pattern;
    }

    public bool IsPatternSelectionReady(BossPatternEntry pattern)
    {
        if (pattern == null)
            return false;

        if (!patternSelectionReadyTimes.TryGetValue(pattern, out float readyTime))
            return true;

        return Time.time >= readyTime;
    }

    public int GetUseCount(BossPatternEntry pattern)
    {
        if (pattern == null)
            return 0;

        if (!patternUseCounts.TryGetValue(pattern, out int useCount))
            return 0;

        return useCount;
    }
}
