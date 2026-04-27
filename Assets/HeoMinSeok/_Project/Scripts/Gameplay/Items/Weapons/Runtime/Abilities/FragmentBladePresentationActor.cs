using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 파편검 조각 actor pool과 이동/trail presentation을 관리한다.
/// - runtime data의 shard 상태를 시각 actor로 반영하되, gameplay 판정과 저장 정책은 소유하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FragmentBladePresentationActor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private FragmentShardActor shardPrefab;
    [SerializeField] private Transform shardPoolRoot;
    [SerializeField] private Transform recallDestination;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float recallDurationSeconds = 0.35f;

    private readonly List<FragmentShardActor> pool = new();
    private readonly Dictionary<int, FragmentShardActor> activeActorsByShardId = new();

    private void Awake()
    {
        if (shardPoolRoot == null)
            shardPoolRoot = transform;

        if (recallDestination == null)
            recallDestination = transform;
    }

    /// <summary>
    /// 책임 :
    /// - runtime data의 최대 조각 수 변경에 맞춰 actor pool을 준비한다.
    /// - 풀 부족이 gameplay 중 instantiate 난사로 이어지지 않도록 명시적으로 크기를 맞춘다.
    /// </summary>
    public void EnsurePoolSize(int desiredCount)
    {
        if (shardPrefab == null)
            return;

        int targetCount = Mathf.Max(0, desiredCount);
        while (pool.Count < targetCount)
        {
            FragmentShardActor actor = Instantiate(shardPrefab, shardPoolRoot);
            actor.gameObject.SetActive(false);
            pool.Add(actor);
        }
    }

    /// <summary>
    /// 책임 :
    /// - 기본 공격으로 detached 확정된 shard를 월드 actor로 표시한다.
    /// - data가 가진 위치를 그대로 사용해 gameplay 상태와 presentation 위치를 맞춘다.
    /// </summary>
    public void ShowDetached(FragmentBladeRuntimeData.ShardRuntimeState shard)
    {
        if (shard == null)
            return;

        FragmentShardActor actor = GetOrCreateActor(shard.Id);
        if (actor == null)
            return;

        actor.ShowDetached(shard.WorldPosition);
    }

    /// <summary>
    /// 책임 :
    /// - skill1 회수 명령을 actor들에게 전달한다.
    /// - 이미 Returning/Piercing 중인 actor도 이전 명령을 중단하고 새 회수 명령으로 덮어쓴다.
    /// </summary>
    public void BeginRecall(
        IReadOnlyList<FragmentBladeRuntimeData.ShardRuntimeState> shards,
        CombatHitPayload recallPayload,
        LayerMask recallDamageLayers,
        GameObject ignoreTarget,
        Action<int, Vector3> onShardArrived)
    {
        if (shards == null)
            return;

        for (int i = 0; i < shards.Count; i++)
        {
            FragmentBladeRuntimeData.ShardRuntimeState shard = shards[i];
            if (shard == null)
                continue;

            FragmentShardActor actor = GetOrCreateActor(shard.Id);
            if (actor == null)
                continue;

            if (!actor.gameObject.activeSelf)
                actor.ShowDetached(shard.WorldPosition);

            actor.BeginRecall(
                recallDestination,
                recallDurationSeconds,
                recallPayload,
                recallDamageLayers,
                ignoreTarget,
                (shardId, arrivedPosition) => HandleShardRecallArrived(shardId, arrivedPosition, onShardArrived));
        }
    }

    /// <summary>
    /// 책임 :
    /// - Skill2 강화 중 기본 공격 적중에 반응해 detached 조각 actor들에게 관통 이동 명령을 전달한다.
    /// - 이미 Returning/Piercing 중인 actor도 새 관통 명령으로 덮어쓴다.
    /// </summary>
    public void BeginPierce(
        IReadOnlyList<FragmentBladeRuntimeData.ShardRuntimeState> shards,
        Vector3 targetPosition,
        Vector3 fallbackDirection,
        float overshootDistance,
        float durationSeconds,
        CombatHitPayload piercePayload,
        LayerMask pierceDamageLayers,
        GameObject ignoreTarget,
        Action<int, Vector3> onShardCompleted)
    {
        if (shards == null)
            return;

        for (int i = 0; i < shards.Count; i++)
        {
            FragmentBladeRuntimeData.ShardRuntimeState shard = shards[i];
            if (shard == null)
                continue;

            FragmentShardActor actor = GetOrCreateActor(shard.Id);
            if (actor == null)
                continue;

            if (!actor.gameObject.activeSelf)
                actor.ShowDetached(shard.WorldPosition);

            actor.BeginPierce(
                targetPosition,
                fallbackDirection,
                ResolveShardScatterDistance(overshootDistance, i),
                durationSeconds,
                piercePayload,
                pierceDamageLayers,
                ignoreTarget,
                onShardCompleted);
        }
    }

    private static float ResolveShardScatterDistance(
        float overshootDistance,
        int shardIndex)
    {
        float scatterDistance = Mathf.Lerp(1f, 2f, Mathf.Repeat(shardIndex * 0.6180339f, 1f));
        return Mathf.Max(0f, overshootDistance) + scatterDistance;
    }

    /// <summary>
    /// 책임 :
    /// - 무기 해제/씬 전환/강제 취소 시 모든 조각 actor를 풀로 되돌리고 trail을 지운다.
    /// - runtime data 정규화와 함께 호출되어 presentation 찌꺼기가 남지 않게 한다.
    /// </summary>
    public void CancelActiveShardMotions()
    {
        foreach (FragmentShardActor actor in activeActorsByShardId.Values)
            actor.CancelAndHide(shardPoolRoot);

        activeActorsByShardId.Clear();
    }

    private void HandleShardRecallArrived(
        int shardId,
        Vector3 arrivedPosition,
        Action<int, Vector3> onShardArrived)
    {
        if (!activeActorsByShardId.TryGetValue(shardId, out FragmentShardActor actor))
            return;

        actor.CancelAndHide(shardPoolRoot);
        activeActorsByShardId.Remove(shardId);

        onShardArrived?.Invoke(shardId, arrivedPosition);
    }

    private FragmentShardActor GetOrCreateActor(int shardId)
    {
        if (activeActorsByShardId.TryGetValue(shardId, out FragmentShardActor activeActor))
            return activeActor;

        FragmentShardActor actor = TakeInactiveActor();
        if (actor == null)
            return null;

        actor.BindShard(shardId);
        activeActorsByShardId[shardId] = actor;
        return actor;
    }

    private FragmentShardActor TakeInactiveActor()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeSelf)
                return pool[i];
        }

        if (shardPrefab == null)
            return null;

        FragmentShardActor actor = Instantiate(shardPrefab, shardPoolRoot);
        actor.gameObject.SetActive(false);
        pool.Add(actor);
        return actor;
    }
}
