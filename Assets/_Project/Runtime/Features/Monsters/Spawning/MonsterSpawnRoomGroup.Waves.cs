using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sequences authored room waves, owns future spawn reservations and persists progress.
/// Wave clear only observes its own tickets/lock units, not the room hold or Alarm Bell actors.
/// </summary>
public sealed partial class MonsterSpawnRoomGroup
{
    /// <summary>Owns one queued spawn and its split-aware lifetime independently of its spawn anchor.</summary>
    private sealed class WaveSpawnTicket
    {
        public MonsterSpawnRequest Request;
        public int WaveIndex;
        public bool HasRunGoldReward;
        public int RunGoldReward;
        public bool Completed;
        public bool Reserved;
        public MonsterLockTrackingUnit Unit;
    }

    [SerializeField] private List<RoomMonsterWaveDefinition> monsterWaves = new();
    private readonly List<WaveSpawnTicket> waveTickets = new();
    private readonly WaitForSeconds waveClearPoll = new(0.1f);
    private DungeonRoomWaveRuntimeStateData restoredWaveState;
    private Coroutine waveSequence;
    private int currentWaveIndex;
    private float waveDelayRemaining;
    private bool waveDelayElapsed;
    private bool waveSequencePrepared;
    private bool waveSequencePausing;
    private bool waveEncounterHoldOwned;
    private bool roomWavesCompleted;

    public int CurrentWaveNumber => roomEntrySpawnStarted && !RoomWavesCompleted ? currentWaveIndex + 1 : 0;
    public int WaveCount => monsterWaves == null || monsterWaves.Count == 0 ? 1 : monsterWaves.Count;
    public bool RoomWavesCompleted => roomWavesCompleted || (restoredWaveState?.completed ?? false);

    public void ConfigureWaves(IReadOnlyList<RoomMonsterWaveDefinition> definitions)
    {
        if (roomEntrySpawnStarted)
            throw new InvalidOperationException("Cannot change room waves after encounter entry.");
        monsterWaves = RoomMonsterWaveDefinition.CopyOrDefault(definitions);
    }

    public DungeonRoomWaveRuntimeStateData CaptureWaveState()
    {
        if (!waveSequencePrepared && restoredWaveState != null)
            return restoredWaveState.Copy();

        return new DungeonRoomWaveRuntimeStateData
        {
            hasStarted = roomEntrySpawnStarted,
            completed = roomWavesCompleted,
            currentWaveId = monsterWaves != null && currentWaveIndex < monsterWaves.Count
                ? monsterWaves[currentWaveIndex].id : RoomMonsterWaveDefinition.DefaultId,
            remainingDelaySeconds = waveDelayRemaining,
            delayElapsed = waveDelayElapsed
        };
    }

    public void RestoreWaveState(DungeonRoomWaveRuntimeStateData state)
    {
        if (roomEntrySpawnStarted)
            throw new InvalidOperationException("Restore room waves before encounter entry.");
        restoredWaveState = state?.Copy();
    }

    private void PrepareRoomWaves(List<MonsterSpawnRequest> requests)
    {
        monsterWaves = RoomMonsterWaveDefinition.CopyOrDefault(monsterWaves);
        currentWaveIndex = 0;
        if (restoredWaveState != null && restoredWaveState.hasStarted)
        {
            int restoredIndex = monsterWaves.FindIndex(w => w.id == restoredWaveState.currentWaveId);
            if (restoredIndex >= 0)
                currentWaveIndex = restoredIndex;
            else
                CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[RoomWaves] {name}: saved wave no longer exists; retaining object survival states.", this);
        }

        waveDelayElapsed = restoredWaveState?.hasStarted == true && restoredWaveState.delayElapsed;
        waveDelayRemaining = restoredWaveState?.hasStarted == true
            ? SafeDelay(restoredWaveState.remainingDelaySeconds)
            : SafeDelay(monsterWaves[currentWaveIndex].startDelaySeconds);
        restoredWaveState = null;
        waveTickets.Clear();
        int[] runGoldRewards = BuildRunGoldAllocations(requests);
        for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
        {
            MonsterSpawnRequest request = requests[requestIndex];
            string waveId = request.SourceContainer != null
                ? request.SourceContainer.MonsterWaveId : RoomMonsterWaveDefinition.DefaultId;
            int index = monsterWaves.FindIndex(w => w.id == waveId);
            if (index < 0)
            {
                CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[RoomWaves] {name}: unknown wave '{waveId}', using the first wave.", this);
                index = 0;
            }
            if (index < currentWaveIndex)
                continue;
            waveTickets.Add(new WaveSpawnTicket
            {
                Request = request,
                WaveIndex = index,
                HasRunGoldReward = hasConfiguredRunGoldBudget,
                RunGoldReward = runGoldRewards != null ? runGoldRewards[requestIndex] : 0
            });
        }
        waveSequencePrepared = true;
        ResumeRoomWavesIfNeeded();
    }

    private static float SafeDelay(float delay) => float.IsNaN(delay) || float.IsInfinity(delay)
        ? 0f : Mathf.Max(0f, delay);

    private void ResumeRoomWavesIfNeeded()
    {
        if (!isActiveAndEnabled || !waveSequencePrepared || roomWavesCompleted || waveSequence != null)
            return;

        waveSequencePausing = false;
        waveEncounterHoldOwned = true;
        PushEncounterHold();
        // Reserve all future linked monsters before the first wave can die/open its chest.
        foreach (WaveSpawnTicket ticket in waveTickets)
        {
            if (ticket.Completed || ticket.Reserved)
                continue;
            ticket.Reserved = true;
            ReservePendingSpawn(ticket.Request);
        }
        waveSequence = StartCoroutine(RunRoomWaves());
    }

    private IEnumerator RunRoomWaves()
    {
        while (currentWaveIndex < monsterWaves.Count)
        {
            while (!waveDelayElapsed && waveDelayRemaining > 0f)
            {
                yield return null;
                waveDelayRemaining = Mathf.Max(0f, waveDelayRemaining - Time.deltaTime);
            }
            waveDelayElapsed = true;

            foreach (WaveSpawnTicket ticket in waveTickets)
            {
                if (ticket.WaveIndex == currentWaveIndex && !ticket.Completed)
                    ScheduleRoomEntrySpawn(ticket);
            }
            while (!IsCurrentWaveClear())
                yield return waveClearPoll;

            activeSpawnRoutines.Clear();
            currentWaveIndex++;
            waveDelayElapsed = false;
            if (currentWaveIndex < monsterWaves.Count)
                waveDelayRemaining = SafeDelay(monsterWaves[currentWaveIndex].startDelaySeconds);
        }

        roomWavesCompleted = true;
        ReleaseWaveEncounterHold();
        waveSequence = null;
    }

    private bool IsCurrentWaveClear()
    {
        foreach (WaveSpawnTicket ticket in waveTickets)
        {
            if (ticket.WaveIndex == currentWaveIndex &&
                (!ticket.Completed || (ticket.Unit != null && ticket.Unit.HasAliveMember())))
                return false;
        }
        return true;
    }

    private void CompleteWaveTicket(WaveSpawnTicket ticket, GameObject monster)
    {
        ticket.Completed = true;
        if (monster != null)
        {
            if (ticket.HasRunGoldReward)
            {
                ExperienceRewardSource rewardSource =
                    monster.GetComponent<ExperienceRewardSource>() ??
                    monster.GetComponentInChildren<ExperienceRewardSource>();
                rewardSource?.SetRuntimeGoldReward(ticket.RunGoldReward);
            }

            ticket.Unit = Mob.ResolveOrCreateLockTrackingUnit(monster);
        }
        else if (ticket.Request.SourceContainer != null)
            ticket.Request.SourceContainer.NotifyRuntimeSpawned(null);

        if (ticket.Reserved)
        {
            ticket.Reserved = false;
            ReleasePendingSpawn(ticket.Request, releaseChestPending: monster == null);
        }
    }

    /// <summary>
    /// 방 예산을 경험치 비율로 모든 예정 스폰에 나눈다.
    /// 이전 웨이브를 복원할 때도 전체 요청을 기준으로 계산해 이미 끝난 웨이브 몫을 재지급하지 않는다.
    /// </summary>
    private int[] BuildRunGoldAllocations(IReadOnlyList<MonsterSpawnRequest> requests)
    {
        if (!hasConfiguredRunGoldBudget || requests == null)
            return null;

        int[] rewards = new int[requests.Count];
        int[] weights = new int[requests.Count];
        int totalWeight = 0;
        for (int i = 0; i < requests.Count; i++)
        {
            GameObject prefab = requests[i].MonsterPrefab;
            ExperienceRewardSource rewardSource = prefab != null
                ? prefab.GetComponent<ExperienceRewardSource>() ??
                  prefab.GetComponentInChildren<ExperienceRewardSource>()
                : null;
            if (rewardSource == null || !rewardSource.GrantsExperience || rewardSource.BaseExperience <= 0)
                continue;

            weights[i] = rewardSource.BaseExperience;
            totalWeight += weights[i];
        }

        if (totalWeight <= 0 || configuredRunGoldBudget <= 0)
            return rewards;

        int assigned = 0;
        for (int i = 0; i < rewards.Length; i++)
        {
            if (weights[i] <= 0)
                continue;

            rewards[i] = configuredRunGoldBudget * weights[i] / totalWeight;
            assigned += rewards[i];
        }

        int remainder = configuredRunGoldBudget - assigned;
        for (int i = 0; i < rewards.Length && remainder > 0; i++)
        {
            if (weights[i] <= 0)
                continue;

            rewards[i]++;
            remainder--;
        }

        return rewards;
    }

    private void PauseRoomWaves()
    {
        waveSequencePausing = true;
        if (waveSequence != null)
            StopCoroutine(waveSequence);
        waveSequence = null;
        ReleaseWaveEncounterHold();
        foreach (WaveSpawnTicket ticket in waveTickets)
            ticket.Reserved = false;
    }

    private void ReleaseWaveEncounterHold()
    {
        if (!waveEncounterHoldOwned)
            return;
        waveEncounterHoldOwned = false;
        PopEncounterHold();
    }
}
