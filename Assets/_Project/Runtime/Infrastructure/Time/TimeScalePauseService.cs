using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 여러 시스템이 동시에 전역 Time.timeScale 정지를 요청해도 마지막 owner가 해제될 때만 시간을 복구한다.
/// - 각 기능이 직접 previousTimeScale을 저장하며 0을 다시 복구하는 중첩 pause 버그를 방지한다.
/// </summary>
public static class TimeScalePauseService
{
    private static readonly ITimeScalePauseBackend PlaybackBackend = new TimeScalePauseBackend();

    /// <summary>
    /// 책임:
    /// - TimeScalePauseService가 보유한 owner 중 씬 전환 등으로 파괴된 owner를 매 프레임 청소한다.
    /// - owner가 Destroy되며 Release를 호출하지 못한 경우에도 전역 시간이 영구 정지되지 않게 한다.
    /// </summary>
    private sealed class TimeScalePauseServiceRunner : MonoBehaviour
    {
        private void Update()
        {
            CleanupDeadOwners();
        }
    }

    /// <summary>
    /// 책임:
    /// - TimeScalePauseService에 등록된 pause 요청 owner 참조를 보관한다.
    /// - UnityEngine.Object의 fake-null 상태를 감지할 수 있게 원본 Object를 유지한다.
    /// </summary>
    private sealed class PauseOwner
    {
        public PauseOwner(Object owner)
        {
            Owner = owner;
        }

        public Object Owner { get; }
    }

    private static readonly Dictionary<int, PauseOwner> owners = new();
    private static float restoreTimeScale = 1f;
    private static TimeScalePauseServiceRunner runner;

    public static bool IsPaused => owners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        TimeScalePausePlayback.RegisterBackend(PlaybackBackend);
        EnsureRunner();
    }

    public static bool IsHeldBy(Object owner)
    {
        if (owner == null)
            return false;

        CleanupDeadOwners();
        return owners.ContainsKey(owner.GetInstanceID());
    }

    public static bool Acquire(Object owner)
    {
        if (owner == null)
            return false;

        CleanupDeadOwners();

        int ownerId = owner.GetInstanceID();
        if (owners.ContainsKey(ownerId))
            return false;

        if (owners.Count == 0)
        {
            EnsureRunner();
            restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        owners[ownerId] = new PauseOwner(owner);
        return true;
    }

    public static bool Release(Object owner)
    {
        if (owner == null)
            return false;

        int ownerId = owner.GetInstanceID();
        bool removed = owners.Remove(ownerId);
        CleanupDeadOwners();

        if (owners.Count == 0)
        {
            Time.timeScale = restoreTimeScale > 0f ? restoreTimeScale : 1f;
            restoreTimeScale = 1f;
        }
        else
        {
            Time.timeScale = 0f;
        }

        return removed;
    }

    private static void EnsureRunner()
    {
        if (runner != null)
            return;

        GameObject runnerObject = new GameObject(nameof(TimeScalePauseService));
        Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<TimeScalePauseServiceRunner>();
    }

    private static void CleanupDeadOwners()
    {
        if (owners.Count == 0)
            return;

        List<int> deadOwnerIds = null;
        foreach (KeyValuePair<int, PauseOwner> pair in owners)
        {
            if (pair.Value?.Owner != null)
                continue;

            deadOwnerIds ??= new List<int>();
            deadOwnerIds.Add(pair.Key);
        }

        if (deadOwnerIds == null)
            return;

        for (int i = 0; i < deadOwnerIds.Count; i++)
            owners.Remove(deadOwnerIds[i]);

        if (owners.Count == 0)
        {
            Time.timeScale = restoreTimeScale > 0f ? restoreTimeScale : 1f;
            restoreTimeScale = 1f;
        }
    }

    /// <summary>
    /// 책임 : Core의 time-scale pause playback 요청을 기존 정적 TimeScalePauseService로 연결한다.
    /// </summary>
    private sealed class TimeScalePauseBackend : ITimeScalePauseBackend
    {
        public bool IsPaused => TimeScalePauseService.IsPaused;

        public bool IsHeldBy(Object owner)
        {
            return TimeScalePauseService.IsHeldBy(owner);
        }

        public bool Acquire(Object owner)
        {
            return TimeScalePauseService.Acquire(owner);
        }

        public bool Release(Object owner)
        {
            return TimeScalePauseService.Release(owner);
        }
    }
}
