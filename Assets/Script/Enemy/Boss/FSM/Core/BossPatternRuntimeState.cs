using System.Collections.Generic;
using UnityEngine;

public sealed class BossPatternRuntimeState
{
    // 이 클래스의 책임:
    // 공통 블랙보드 밖에서 패턴 선택/실행 이력과 예약 상태를 관리한다.
    // 패턴 시작 시점 잠금과 종료 후 후딜 잠금을 함께 반영해 다음 선택 가능 시점을 계산한다.

    private readonly Dictionary<BossPatternEntry, float> patternSelectionReadyTimes = new();
    private readonly Dictionary<BossPatternEntry, int> patternUseCounts = new();

    public BossPatternEntry ReservedPattern { get; private set; }
    public BossPatternEntry CurrentPattern { get; private set; }
    public BossPatternEntry LastUsedPattern { get; private set; }
    public int ConsecutivePatternUseCount { get; private set; }

    public void ReservePattern(BossPatternEntry pattern)
    {
        ReservedPattern = pattern;
    }

    public void ClearReservedPattern()
    {
        ReservedPattern = null;
    }

    public void BeginPattern(BossPatternEntry pattern)
    {
        CurrentPattern = pattern;
        ReservedPattern = null;

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
