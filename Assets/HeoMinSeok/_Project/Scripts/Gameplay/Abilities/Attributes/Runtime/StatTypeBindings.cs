using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Maps <see cref="StatId"/> to AttributeDefinitions.
    ///
    /// Why:
    /// - Lets data assets (like ScaledStatFormula) reference stable enums instead of AD assets.
    /// - Centralizes AD wiring for a character/profile.
    ///
    /// Note:
    /// - Derived stats (e.g., AttackFinal) are usually computed in a provider, not mapped here.
    /// </summary>
    [CreateAssetMenu(fileName = "StatTypeBindings", menuName = "GAS/Stats/Stat Type Bindings")]
    public sealed class StatTypeBindings : ScriptableObject
    {
        [Serializable]
        public sealed class Binding
        {
            public StatId id = StatId.None;
            public AttributeDefinition attribute;

            [Tooltip("If true, this binding is treated as an x1 multiplier (e.g., 1.10). Missing value defaults to 1.")]
            public bool isMultiplier;
        }

        [Serializable]
        public sealed class Composite
        {
            [Tooltip("Final stat id (queried by formulas).")]
            public StatId finalId = StatId.None;

            public StatId baseId = StatId.None;
            public StatId addId = StatId.None;
            public StatId mulId = StatId.None;
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();
        [SerializeField] private List<Composite> composites = new List<Composite>();

        // Runtime cache
        private Dictionary<StatId, Binding> _cache;
        private Dictionary<StatId, Composite> _compositeCache;

        public IReadOnlyList<Binding> Bindings => bindings;
        public IReadOnlyList<Composite> Composites => composites;

        public bool TryGetBinding(StatId id, out Binding binding)
        {
            if (id == StatId.None)
            {
                binding = null;
                return false;
            }

            EnsureCache();
            return _cache.TryGetValue(id, out binding) && binding != null;
        }

        public bool TryGetComposite(StatId finalId, out Composite composite)
        {
            if (finalId == StatId.None)
            {
                composite = null;
                return false;
            }
            EnsureCompositeCache();
            return _compositeCache.TryGetValue(finalId, out composite) && composite != null;
        }

        private void OnValidate()
        {
            _cache = null;
            _compositeCache = null;
        }

        private void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<StatId, Binding>(bindings != null ? bindings.Count : 8);
            if (bindings == null) return;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null) continue;
                if (b.id == StatId.None) continue;
                // Last one wins.
                _cache[b.id] = b;
            }
        }

        private void EnsureCompositeCache()
        {
            if (_compositeCache != null) return;
            _compositeCache = new Dictionary<StatId, Composite>(composites != null ? composites.Count : 4);
            if (composites == null) return;
            for (int i = 0; i < composites.Count; i++)
            {
                var c = composites[i];
                if (c == null) continue;
                if (c.finalId == StatId.None) continue;
                _compositeCache[c.finalId] = c;
            }
        }
    }
}
