using System.Collections.Generic;
using UnityEngine;

public sealed class BossBlackboard
{
    private readonly Transform ownerTransform;
    private readonly Dictionary<BossPatternEntry, float> patternSelectionReadyTimes = new();
    private readonly Dictionary<BossPatternEntry, int> patternUseCounts = new();

    public BossBlackboard(Transform ownerTransform)
    {
        this.ownerTransform = ownerTransform;
    }

    public Transform CurrentTarget { get; private set; }
    public float DistanceToTarget { get; private set; }
    public Vector2 DirectionToTarget { get; private set; }
    public float CurrentHpRatio { get; private set; } = 1f;

    public string CurrentStateName { get; private set; }
    public float StateElapsedTime { get; private set; }

    public int CurrentPhaseIndex { get; private set; }

    public BossPatternEntry ReservedPattern { get; private set; }
    public BossPatternEntry CurrentPattern { get; private set; }
    public BossPatternEntry LastUsedPattern { get; private set; }
    public int ConsecutivePatternUseCount { get; private set; }

    public void Tick(float deltaTime, Transform target, float currentHpRatio)
    {
        StateElapsedTime += deltaTime;
        CurrentTarget = target;
        CurrentHpRatio = Mathf.Clamp01(currentHpRatio);

        if (ownerTransform != null && CurrentTarget != null)
        {
            Vector3 delta = CurrentTarget.position - ownerTransform.position;
            DistanceToTarget = delta.magnitude;
            DirectionToTarget = delta.sqrMagnitude > 0.0001f
                ? ((Vector2)delta).normalized
                : Vector2.zero;
        }
        else
        {
            DistanceToTarget = float.MaxValue;
            DirectionToTarget = Vector2.zero;
        }
    }

    public void NotifyStateChanged(string stateName)
    {
        CurrentStateName = stateName;
        StateElapsedTime = 0f;
    }

    public void SetPhaseIndex(int phaseIndex)
    {
        CurrentPhaseIndex = Mathf.Max(0, phaseIndex);
    }

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

        if (pattern == null) return;

        patternSelectionReadyTimes[pattern] = Time.time + pattern.AiSelectionLockTime;
        patternUseCounts[pattern] = GetUseCount(pattern) + 1;

        if (LastUsedPattern == pattern)
            ConsecutivePatternUseCount++;
        else
            ConsecutivePatternUseCount = 1;

        LastUsedPattern = pattern;
    }

    public void EndPattern()
    {
        CurrentPattern = null;
    }

    public void ClearPatternContext()
    {
        ReservedPattern = null;
        CurrentPattern = null;
    }

    public bool IsPatternSelectionReady(BossPatternEntry pattern)
    {
        if (pattern == null) return false;

        if (!patternSelectionReadyTimes.TryGetValue(pattern, out float readyTime)) return true;

        return Time.time >= readyTime;
    }

    /// <summary>패턴 사용 횟수를 반환합니다.</summary>
    public int GetUseCount(BossPatternEntry pattern)
    {
        if (pattern == null) return 0;

        if (!patternUseCounts.TryGetValue(pattern, out int useCount)) return 0;

        return useCount;
    }
}
