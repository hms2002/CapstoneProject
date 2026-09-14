using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>대상이 소유하는 독립 화상 스택입니다. 기존 ElementGaugeSystem을 사용하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class BurnStatus2D : MonoBehaviour, IMonsterStackStatusSource
{
    public const int StackLimit = 99;
    private static readonly HashSet<BurnStatus2D> activeStatuses = new();
    private static readonly ElementDamageResult[] NoElementBuildUp = Array.Empty<ElementDamageResult>();

    private AbilitySystem sourceSystem;
    private GameplayEffect damageEffect;
    private GameObject causer;
    private BurnSourceRuntime sourceRules;
    private float tickElapsed;
    private int stacks;
    private bool viewAttached;
    private CrimsonBoundaryVisual2D tickVisualPrefab;
    private CrimsonBoundaryVisual2D sustainVisualPrefab;
    private CrimsonBoundaryVisual2D sustainVisual;

    public string StatusId => "Burn";
    public int CurrentStacks => stacks;
    public int MaxStacks => StackLimit;
    public Color DisplayColor => new(1f, 0.28f, 0.02f, 1f);
    public static IEnumerable<BurnStatus2D> ActiveStatuses => activeStatuses;

    public event Action StackChanged;
    public event Action PulseRequested;

    public static BurnStatus2D Apply(GameObject target, AbilitySystem source, GameplayEffect effect, GameObject sourceCauser, int baseStacks, CrimsonBoundaryVisual2D tickVisual = null, CrimsonBoundaryVisual2D sustainPrefab = null)
    {
        if (target == null || source == null || effect == null || baseStacks <= 0)
            return null;

        BurnStatus2D status = target.GetComponent<BurnStatus2D>();
        if (status == null)
            status = target.AddComponent<BurnStatus2D>();

        if (tickVisual != null) status.tickVisualPrefab = tickVisual;
        if (sustainPrefab != null && status.sustainVisualPrefab != sustainPrefab)
        {
            status.StopSustainVisual();
            status.sustainVisualPrefab = sustainPrefab;
        }
        status.sourceSystem = source;
        status.damageEffect = effect;
        status.causer = sourceCauser != null ? sourceCauser : source.gameObject;
        status.sourceRules = BurnSourceRuntime.Resolve(source);
        int resolvedStacks = status.sourceRules != null
            ? status.sourceRules.ResolveApplicationStacks(baseStacks, status.stacks == 0)
            : baseStacks;
        status.AddStacks(resolvedStacks);
        status.EnsureSustainVisual();
        return status;
    }

    public int ConsumeUpTo(int amount)
    {
        int consumed = Mathf.Min(stacks, Mathf.Max(0, amount));
        SetStacks(stacks - consumed);
        return consumed;
    }

    public int ConsumeAll() => ConsumeUpTo(stacks);

    private void OnEnable()
    {
        if (stacks > 0)
            ActivateView();
    }

    private void OnDisable()
    {
        activeStatuses.Remove(this);
        StopSustainVisual();
        DetachView();
    }

    private void Update()
    {
        if (stacks <= 0)
            return;

        tickElapsed += Time.deltaTime;
        float interval = sourceRules != null ? sourceRules.TickInterval : 1f;
        while (stacks > 0 && tickElapsed >= interval)
        {
            tickElapsed -= interval;
            TickBurn();
            interval = sourceRules != null ? sourceRules.TickInterval : 1f;
        }
    }

    private void AddStacks(int amount)
    {
        if (amount <= 0) return;
        SetStacks(Mathf.Min(StackLimit, stacks + amount));
    }

    private void SetStacks(int value)
    {
        int previous = stacks;
        stacks = Mathf.Clamp(value, 0, StackLimit);
        if (stacks == previous) return;

        if (stacks > 0) ActivateView();
        else DeactivateView();
        StackChanged?.Invoke();
    }

    private void TickBurn()
    {
        if (sourceSystem != null && damageEffect != null)
        {
            IStatProvider provider = AbilityStatProviderFactory.Create(sourceSystem);
            float fire = provider != null ? Mathf.Max(0f, provider.Get(StatId.FireFinal)) : 0f;
            if (sourceRules != null)
                fire = Mathf.Max(fire, sourceRules.MinimumFireForBurn);
            float ratio = sourceRules != null ? sourceRules.DamageRatio : 0.5f;
            bool allowCritical = sourceRules != null && sourceRules.AllowCritical;
            DamageResult result = allowCritical
                ? DamageFormulaUtil.PostProcess(provider, fire * ratio, 0f)
                : new DamageResult
                {
                    hpDamage = fire * ratio * (provider != null ? Mathf.Max(0f, provider.Get(StatId.FinalMul)) : 1f),
                    isCrit = false
                };
            float stackDamageMultiplier = sourceRules != null
                ? sourceRules.ResolveStackDamageMultiplier(stacks)
                : 1f;

            CombatDamageAction.ApplyDamageAndEmitHit(
                system: sourceSystem,
                spec: null,
                damageEffect: damageEffect,
                knockbackEffect: null,
                target: gameObject,
                finalHpDamage: Mathf.Round(result.hpDamage * stackDamageMultiplier),
                finalStaggerBuildUp: 0f,
                finalKnockbackImpulse: 0f,
                hitConfirmedTag: null,
                hitWorldPosition: transform.position,
                causer: causer,
                isCriticalHit: result.isCrit,
                elementBuildUps: NoElementBuildUp,
                hasResolvedElementBuildUps: true,
                emitHitConfirmed: false);
        }

        CrimsonBoundaryVisual2D.Spawn(tickVisualPrefab, transform.position, Quaternion.identity);
        PulseRequested?.Invoke();
        ConsumeUpTo(1);
    }

    private void ActivateView()
    {
        activeStatuses.Add(this);
        EnsureSustainVisual();
        if (viewAttached) return;
        viewAttached = true;
        MonsterStackStatusViewPlayback.Attach(gameObject, this);
    }

    private void DeactivateView()
    {
        activeStatuses.Remove(this);
        tickElapsed = 0f;
        StopSustainVisual();
        DetachView();
    }

    private void EnsureSustainVisual()
    {
        if (!isActiveAndEnabled || stacks <= 0 || sustainVisual != null || sustainVisualPrefab == null) return;
        sustainVisual = CrimsonBoundaryVisual2D.Spawn(sustainVisualPrefab, transform.position, Quaternion.identity);
        if (sustainVisual != null) sustainVisual.transform.SetParent(transform, true);
    }

    private void StopSustainVisual()
    {
        if (sustainVisual != null)
        {
            sustainVisual.gameObject.SetActive(false);
            Destroy(sustainVisual.gameObject);
        }
        sustainVisual = null;
    }

    private void DetachView()
    {
        if (!viewAttached) return;
        viewAttached = false;
        MonsterStackStatusViewPlayback.Detach(gameObject, this);
    }
}
