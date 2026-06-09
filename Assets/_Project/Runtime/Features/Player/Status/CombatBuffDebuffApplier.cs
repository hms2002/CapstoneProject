using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 전투 대상 위에서 버프/디버프용 GameplayEffect를 적용하고, 필요한 결과를 플레이어 상태 HUD에 동기화한다.
/// - source owner가 GE 적용과 HUD 갱신을 각각 따로 호출하지 않도록 공통 연결 경로를 제공한다.
/// - 대상 기준으로 디버프 수명과 HUD 타이머 갱신을 조율해 source 종속형과 비종속형 효과를 같은 틀에서 다루게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatBuffDebuffApplier : MonoBehaviour
{
    /// <summary>
    /// 책임 :
    /// - source owner가 특정 대상에게 적용한 플레이어 HUD 동기화 상태 하나를 추적한다.
    /// - GE의 남은 시간과 스택 결과를 StatusHandle과 묶어 후속 업데이트/해제를 가능하게 만든다.
    /// </summary>
    private sealed class TrackedPlayerStatusSync
    {
        public GameObject Target;
        public AbilitySystem AbilitySystem;
        public PlayerStatusRuntime PlayerStatusRuntime;
        public CombatBuffDebuffApplicationDefinition Definition;
        public GameplayEffect Effect;
        public Object SourceObject;
        public string OwnerKey;
        public float AppliedDuration;
        public StatusHandle Handle;
    }

    [SerializeField] private bool logApplyFlow = true;

    private readonly List<TrackedPlayerStatusSync> trackedPlayerStatuses = new();

    public static CombatBuffDebuffApplier GetOrAdd(GameObject host)
    {
        if (host == null)
            return null;

        CombatBuffDebuffApplier existing = host.GetComponent<CombatBuffDebuffApplier>();
        return existing != null ? existing : host.AddComponent<CombatBuffDebuffApplier>();
    }

    /// <summary>
    /// 책임 :
    /// - source owner가 지정한 effect를 대상에 적용하고, 플레이어 대상이면 HUD 상태도 같은 owner 문맥으로 등록/갱신한다.
    /// - 대상 위에서 수명을 추적하므로 source가 사라져도 독립 지속형 효과는 계속 갱신할 수 있다.
    /// </summary>
    public bool ApplyFromSource(
        Object sourceObject,
        GameObject targetRoot,
        CombatBuffDebuffApplicationDefinition definition,
        string ownerKey,
        float durationOverride = -1f)
    {
        if (targetRoot == null)
        {
            if (logApplyFlow)
                Debug.LogWarning("[CombatBuffDebuffApplier] Ignored apply because targetRoot was null.", this);
            return false;
        }

        if (definition == null || definition.GameplayEffect == null)
        {
            if (logApplyFlow)
                Debug.LogWarning("[CombatBuffDebuffApplier] Ignored apply because definition or GameplayEffect was null.", this);
            return false;
        }

        AbilitySystem abilitySystem = ResolveAbilitySystem(targetRoot);
        if (abilitySystem == null || abilitySystem.EffectRunner == null)
        {
            if (logApplyFlow)
                Debug.LogWarning($"[CombatBuffDebuffApplier] Ignored apply because AbilitySystem/EffectRunner was missing on '{targetRoot.name}'.", this);
            return false;
        }

        GameplayEffectSpec spec = abilitySystem.MakeSpec(
            definition.GameplayEffect,
            causer: sourceObject as GameObject,
            sourceObject: sourceObject != null ? sourceObject : this);

        if (durationOverride >= 0f)
            spec.SetDuration(durationOverride);

        abilitySystem.EffectRunner.ApplyEffectSpec(spec, targetRoot);

        if (definition.ShowOnPlayerHud)
            SyncPlayerStatus(sourceObject, abilitySystem, targetRoot, definition, ownerKey, durationOverride);

        if (logApplyFlow)
        {
            float appliedDuration = durationOverride >= 0f ? durationOverride : definition.GameplayEffect.duration;
            Debug.Log(
                $"[CombatBuffDebuffApplier] Applied '{definition.GameplayEffect.name}' to '{targetRoot.name}' " +
                $"(ownerKey={ownerKey}, duration={appliedDuration:0.00}, source={sourceObject}, showOnPlayerHud={definition.ShowOnPlayerHud}).",
                this);
        }

        return true;
    }

    private void Update()
    {
        for (int i = trackedPlayerStatuses.Count - 1; i >= 0; i--)
        {
            if (!RefreshTrackedPlayerStatus(trackedPlayerStatuses[i], allowRelease: true))
                trackedPlayerStatuses.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        ReleaseTrackedPlayerStatuses();
    }

    private void OnDestroy()
    {
        ReleaseTrackedPlayerStatuses();
    }

    private void SyncPlayerStatus(
        Object sourceObject,
        AbilitySystem abilitySystem,
        GameObject targetRoot,
        CombatBuffDebuffApplicationDefinition definition,
        string ownerKey,
        float durationOverride)
    {
        PlayerStatusRuntime statusRuntime = ResolvePlayerStatusRuntime(targetRoot);
        if (statusRuntime == null)
        {
            if (logApplyFlow)
                Debug.LogWarning($"[CombatBuffDebuffApplier] Skipped HUD sync because PlayerStatusRuntime was missing on '{targetRoot.name}'.", this);
            return;
        }

        TrackedPlayerStatusSync tracked = FindTrackedPlayerStatus(targetRoot, ownerKey);
        if (tracked == null)
        {
            tracked = new TrackedPlayerStatusSync
            {
                Target = targetRoot,
                AbilitySystem = abilitySystem,
                PlayerStatusRuntime = statusRuntime,
                Definition = definition,
                Effect = definition.GameplayEffect,
                SourceObject = sourceObject != null ? sourceObject : this,
                OwnerKey = ownerKey,
                AppliedDuration = durationOverride >= 0f ? durationOverride : definition.GameplayEffect.duration
            };

            trackedPlayerStatuses.Add(tracked);
        }
        else
        {
            tracked.AbilitySystem = abilitySystem;
            tracked.PlayerStatusRuntime = statusRuntime;
            tracked.Definition = definition;
            tracked.Effect = definition.GameplayEffect;
            tracked.AppliedDuration = durationOverride >= 0f ? durationOverride : definition.GameplayEffect.duration;
        }

        if (!RefreshTrackedPlayerStatus(tracked, allowRelease: false) && logApplyFlow)
        {
            Debug.LogWarning(
                $"[CombatBuffDebuffApplier] Failed to sync HUD for '{targetRoot.name}' because no active GE result was found after apply.",
                this);
        }
    }

    private bool RefreshTrackedPlayerStatus(TrackedPlayerStatusSync tracked, bool allowRelease)
    {
        if (tracked == null || tracked.Target == null || tracked.AbilitySystem == null || tracked.PlayerStatusRuntime == null)
        {
            if (allowRelease)
                ReleaseTrackedPlayerStatus(tracked, endEffect: false);
            return false;
        }

        if (tracked.Definition != null &&
            tracked.Definition.LifetimePolicy == BuffDebuffLifetimePolicy.WhileSourceAlive &&
            tracked.SourceObject == null)
        {
            if (allowRelease)
                ReleaseTrackedPlayerStatus(tracked, endEffect: true);
            return false;
        }

        GameplayEffectRunner runner = tracked.AbilitySystem.EffectRunner;
        if (runner == null)
        {
            if (allowRelease)
                ReleaseTrackedPlayerStatus(tracked, endEffect: false);
            return false;
        }

        ActiveGameplayEffect activeEffect = tracked.SourceObject != null
            ? runner.FindActiveEffect(tracked.Effect, tracked.Target, tracked.SourceObject)
            : runner.FindActiveEffect(tracked.Effect, tracked.Target);
        if (activeEffect == null)
        {
            if (allowRelease)
                ReleaseTrackedPlayerStatus(tracked, endEffect: false);
            return false;
        }

        float remainingTime = Mathf.Max(0f, activeEffect.TimeRemaining);
        if (remainingTime <= 0f && allowRelease)
        {
            ReleaseTrackedPlayerStatus(tracked, endEffect: false);
            return false;
        }

        if (tracked.Definition == null || tracked.Definition.StatusHudDefinition == null)
            return true;

        StatusApplyRequest request = new(
            tracked.Definition.StatusHudDefinition,
            tracked.OwnerKey,
            stackCount: Mathf.Max(0, activeEffect.StackCount),
            remainingTime: remainingTime,
            maxTime: tracked.AppliedDuration > 0f ? tracked.AppliedDuration : tracked.Effect.duration,
            isVisible: true,
            showStacksOverride: activeEffect.StackCount > 1 ? (bool?)true : null,
            showDurationOverride: remainingTime > 0f ? (bool?)true : null);

        if (tracked.Handle.IsValid)
        {
            tracked.PlayerStatusRuntime.UpdateStatus(tracked.Handle, request);
            return true;
        }

        tracked.Handle = tracked.PlayerStatusRuntime.Apply(request);
        return tracked.Handle.IsValid;
    }

    private void ReleaseTrackedPlayerStatuses()
    {
        for (int i = trackedPlayerStatuses.Count - 1; i >= 0; i--)
            ReleaseTrackedPlayerStatus(trackedPlayerStatuses[i], endEffect: true);

        trackedPlayerStatuses.Clear();
    }

    private void ReleaseTrackedPlayerStatus(TrackedPlayerStatusSync tracked, bool endEffect)
    {
        if (tracked == null)
            return;

        if (endEffect &&
            tracked.Definition != null &&
            tracked.Definition.LifetimePolicy == BuffDebuffLifetimePolicy.WhileSourceAlive &&
            tracked.AbilitySystem != null &&
            tracked.AbilitySystem.EffectRunner != null &&
            tracked.Target != null &&
            tracked.Effect != null &&
            tracked.SourceObject != null)
        {
            tracked.AbilitySystem.EffectRunner.EndEffectsBySourceObject(tracked.Target, tracked.Effect, tracked.SourceObject);
        }

        if (tracked.Handle.IsValid)
            tracked.Handle.Release();

        tracked.Handle = default;
    }

    private TrackedPlayerStatusSync FindTrackedPlayerStatus(GameObject targetRoot, string ownerKey)
    {
        for (int i = 0; i < trackedPlayerStatuses.Count; i++)
        {
            TrackedPlayerStatusSync tracked = trackedPlayerStatuses[i];
            if (tracked == null)
                continue;

            if (tracked.Target == targetRoot && tracked.OwnerKey == ownerKey)
                return tracked;
        }

        return null;
    }

    private static AbilitySystem ResolveAbilitySystem(GameObject targetRoot)
    {
        if (targetRoot == null)
            return null;

        AbilitySystem abilitySystem = targetRoot.GetComponent<AbilitySystem>();
        if (abilitySystem != null)
            return abilitySystem;

        abilitySystem = targetRoot.GetComponentInParent<AbilitySystem>();
        if (abilitySystem != null)
            return abilitySystem;

        return targetRoot.GetComponentInChildren<AbilitySystem>(true);
    }

    private static PlayerStatusRuntime ResolvePlayerStatusRuntime(GameObject targetRoot)
    {
        if (targetRoot == null)
            return null;

        PlayerStatusRuntime statusRuntime = targetRoot.GetComponent<PlayerStatusRuntime>();
        if (statusRuntime != null)
            return statusRuntime;

        PlayerInteractor2D player = targetRoot.GetComponent<PlayerInteractor2D>();
        if (player == null)
            player = targetRoot.GetComponentInParent<PlayerInteractor2D>();

        if (player == null)
            player = targetRoot.GetComponentInChildren<PlayerInteractor2D>(true);

        return player != null
            ? PlayerStatusRuntime.GetOrAdd(player.gameObject)
            : null;
    }
}
