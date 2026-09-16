using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Per-target registration and cleanup hub; effect owners retain stacks and timers.</summary>
[DisallowMultipleComponent]
public sealed class MonsterStatusRuntime : MonoBehaviour
{
    [SerializeField] private GameplayEffect electrocutedEffect;
    private readonly List<IMonsterStatusSource> sources = new();
    private ElectrocutedSource electrocuted;
    private Enemy enemy;
    public IReadOnlyList<IMonsterStatusSource> Sources => sources;

    public static MonsterStatusRuntime Resolve(GameObject target)
    {
        var runtime = target.GetComponent<MonsterStatusRuntime>();
        return runtime != null ? runtime : target.AddComponent<MonsterStatusRuntime>();
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        electrocuted = new ElectrocutedSource(GetComponent<GameplayEffectRunner>(), gameObject, electrocutedEffect);
    }

    private void OnEnable()
    {
        if (electrocuted != null && electrocutedEffect != null) Register(electrocuted);
    }

    public bool Register(IMonsterStatusSource source)
    {
        if (source == null || !isActiveAndEnabled || (enemy != null && enemy.IsDead)) return false;
        if (!sources.Contains(source)) sources.Add(source);
        return true;
    }

    public void Unregister(IMonsterStatusSource source) => sources.Remove(source);

    public bool TryGetActive(string id, out IMonsterStatusSource result)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            if (source.StatusId == id && source.IsActive) { result = source; return true; }
        }
        result = null;
        return false;
    }

    public void ClearAll()
    {
        // Remove before callback: Clear may unregister itself or trigger other cleanup synchronously.
        while (sources.Count > 0)
        {
            var source = sources[sources.Count - 1];
            sources.RemoveAt(sources.Count - 1);
            source.Clear();
        }
        if (isActiveAndEnabled && electrocutedEffect != null && (enemy == null || !enemy.IsDead))
            Register(electrocuted);
    }

    private void OnDisable() => ClearAll();

    private sealed class ElectrocutedSource : IMonsterStatusSource
    {
        private readonly GameplayEffectRunner runner;
        private readonly GameObject target;
        private readonly GameplayEffect effect;
        public ElectrocutedSource(GameplayEffectRunner runner, GameObject target, GameplayEffect effect)
        { this.runner = runner; this.target = target; this.effect = effect; }
        public string StatusId => "Electrocuted";
        public MonsterStatusValueKind ValueKind => MonsterStatusValueKind.Seconds;
        public float DisplayValue => runner != null && effect != null ? runner.GetRemainingTime(effect, target) : 0f;
        public Color DisplayColor => new(0.85f, 0.67f, 0f, 1f);
        public bool IsActive => DisplayValue > 0f;
        public event Action PulseRequested { add { } remove { } }
        public void Clear()
        {
            if (runner == null || effect == null) return;
            while (runner.HasActiveEffect(effect, target)) runner.RemoveEffect(effect, target);
        }
    }
}
