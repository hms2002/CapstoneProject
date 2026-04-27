using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 파편검이 슬롯 단위로 유지해야 하는 결속 조각 수, 떨어진 조각 상태, 강화 지속 시간을 보관한다.
/// - 조각 actor는 presentation일 뿐이며, gameplay 판단은 이 runtime data가 가진 shard 상태를 기준으로 수행하게 한다.
/// </summary>
public sealed class FragmentBladeRuntimeData : WeaponRuntimeData
{
    /// <summary>
    /// 책임 :
    /// - 파편검 조각 하나가 gameplay 관점에서 어떤 상태인지 정의한다.
    /// - visual actor가 이동 중이어도 Returning/Piercing은 아직 detached로 취급하게 하는 기준 상태다.
    /// </summary>
    public enum ShardState
    {
        Bound,
        DetachedIdle,
        Returning,
        Piercing
    }

    /// <summary>
    /// 책임 :
    /// - 조각 actor와 runtime data를 안정적으로 매칭하기 위한 조각별 상태 레코드다.
    /// - 저장/씬 전환 대상이 아니라 전투 중 임시 shard 상태를 표현한다.
    /// </summary>
    public sealed class ShardRuntimeState
    {
        public int Id { get; }
        public ShardState State { get; private set; }
        public Vector3 WorldPosition { get; private set; }
        public float SpawnTime { get; private set; }

        public bool IsBound => State == ShardState.Bound;
        public bool IsDetached => State != ShardState.Bound;

        public ShardRuntimeState(int id)
        {
            Id = id;
            Rebind();
        }

        public void Detach(Vector3 worldPosition, float spawnTime)
        {
            State = ShardState.DetachedIdle;
            WorldPosition = worldPosition;
            SpawnTime = spawnTime;
        }

        public void MarkReturning(Vector3 worldPosition)
        {
            State = ShardState.Returning;
            WorldPosition = worldPosition;
        }

        public void MarkPiercing(Vector3 worldPosition)
        {
            State = ShardState.Piercing;
            WorldPosition = worldPosition;
        }

        public void MarkDetachedIdle(Vector3 worldPosition)
        {
            State = ShardState.DetachedIdle;
            WorldPosition = worldPosition;
        }

        public void UpdateWorldPosition(Vector3 worldPosition)
        {
            WorldPosition = worldPosition;
        }

        public void Rebind()
        {
            State = ShardState.Bound;
            WorldPosition = Vector3.zero;
            SpawnTime = 0f;
        }
    }

    private readonly List<ShardRuntimeState> shards = new();
    private int maxShardCount = 6;
    private float skill2RemainingSeconds;

    public int MaxShardCount => maxShardCount;
    public int BoundShardCount { get; private set; }
    public int DetachedShardCount => maxShardCount - BoundShardCount;
    public float Skill2RemainingSeconds => Mathf.Max(0f, skill2RemainingSeconds);
    public bool IsSkill2Active => skill2RemainingSeconds > 0f;
    public IReadOnlyList<ShardRuntimeState> Shards => shards;

    public float BoundRatio => maxShardCount > 0
        ? Mathf.Clamp01((float)BoundShardCount / maxShardCount)
        : 0f;

    /// <summary>
    /// 책임 :
    /// - loadout authoring 값을 새 runtime data의 기본 shard 규칙으로 주입한다.
    /// - 새 슬롯에 들어온 파편검이 항상 모든 조각이 결속된 안전한 상태에서 시작하게 만든다.
    /// </summary>
    public void ApplyDefaults(FragmentBladeLoadout loadout)
    {
        maxShardCount = loadout != null ? loadout.MaxShardCount : 6;
        maxShardCount = Mathf.Max(1, maxShardCount);
        EnsureShardCapacity(maxShardCount);
        RebindAllShards();
    }

    /// <summary>
    /// 책임 :
    /// - 전투 중 지속 시간을 갱신하고 만료된 강화 상태를 정리한다.
    /// - processor가 없는 1차 구현에서도 live runtime state가 호출할 수 있는 얕은 tick 표면을 제공한다.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (skill2RemainingSeconds <= 0f)
            return;

        skill2RemainingSeconds = Mathf.Max(0f, skill2RemainingSeconds - Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 책임 :
    /// - 기본 공격 성공 후 조각 하나를 detached 상태로 전환한다.
    /// - Skill2 강화 중에는 소모를 막고, 결속 조각이 0개여도 최소 공격 정책 때문에 실패를 반환하지 않는다.
    /// </summary>
    public bool TryDetachShard(Vector3 fallbackWorldPosition, float spawnTime, out ShardRuntimeState shard)
    {
        shard = null;

        if (IsSkill2Active)
            return false;

        for (int i = shards.Count - 1; i >= 0; i--)
        {
            if (!shards[i].IsBound)
                continue;

            shard = shards[i];
            shard.Detach(fallbackWorldPosition, spawnTime);
            BoundShardCount = Mathf.Max(0, BoundShardCount - 1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 책임 :
    /// - Skill1 회수 시점에 사용할 detached 조각 후보를 반환한다.
    /// - Returning/Piercing 조각도 아직 detached로 취급하되, 회수 명령이 이전 이동 명령을 덮어쓸 수 있게 한다.
    /// </summary>
    public List<ShardRuntimeState> CollectDetachedShardsForRecall()
    {
        List<ShardRuntimeState> result = new();
        for (int i = 0; i < shards.Count; i++)
        {
            if (shards[i].IsDetached)
                result.Add(shards[i]);
        }

        return result;
    }

    /// <summary>
    /// 책임 :
    /// - 회수 visual이 끝난 조각을 결속 상태로 확정한다.
    /// - gameplay 상태 변경과 visual 완료 시점을 분리하되, 완료 시점에는 결속 수를 정확히 복구한다.
    /// </summary>
    public void CompleteRecall(ShardRuntimeState shard)
    {
        if (shard == null || shard.IsBound)
            return;

        shard.Rebind();
        BoundShardCount = Mathf.Clamp(BoundShardCount + 1, 0, maxShardCount);
    }

    /// <summary>
    /// 책임 :
    /// - Skill2를 시작하고 지정 시간 동안 기본 공격 조각 소모를 막는다.
    /// - 떨어진 조각이 없어도 강화 상태 자체는 유지되며 HUD가 남은 시간을 표시할 수 있게 한다.
    /// </summary>
    public void StartSkill2(float durationSeconds)
    {
        skill2RemainingSeconds = Mathf.Max(0f, durationSeconds);
    }

    /// <summary>
    /// 책임 :
    /// - 무기 해제, 씬 전환, 저장/복원 초기화 정책에서 모든 조각을 검에 다시 결속한다.
    /// - 떨어진 조각 월드 위치를 영속 상태로 취급하지 않는 파편검의 전환 정책을 한 곳에 고정한다.
    /// </summary>
    public void RebindAllShards()
    {
        EnsureShardCapacity(maxShardCount);

        for (int i = 0; i < shards.Count; i++)
            shards[i].Rebind();

        BoundShardCount = maxShardCount;
        skill2RemainingSeconds = 0f;
    }

    /// <summary>
    /// 책임 :
    /// - 외부 강화나 유물로 최대 조각 수가 변할 때 runtime data와 actor pool이 같은 크기를 볼 수 있게 한다.
    /// - 새로 늘어난 조각은 결속 상태로 시작하고, 줄어든 조각은 안전하게 제거한다.
    /// </summary>
    public void ResizeMaxShardCount(int nextMaxShardCount)
    {
        maxShardCount = Mathf.Max(1, nextMaxShardCount);
        EnsureShardCapacity(maxShardCount);
        BoundShardCount = 0;

        for (int i = 0; i < shards.Count; i++)
        {
            if (shards[i].IsBound)
                BoundShardCount++;
        }
    }

    private void EnsureShardCapacity(int desiredCount)
    {
        desiredCount = Mathf.Max(1, desiredCount);

        while (shards.Count < desiredCount)
            shards.Add(new ShardRuntimeState(shards.Count));

        if (shards.Count <= desiredCount)
            return;

        shards.RemoveRange(desiredCount, shards.Count - desiredCount);
    }
}
