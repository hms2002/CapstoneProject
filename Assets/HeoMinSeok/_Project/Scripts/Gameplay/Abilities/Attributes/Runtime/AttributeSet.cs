using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    // “한 게임 오브젝트의 AttributeValue들을 생성/보관하고,
    // (초기화/델타/모디파이어/클램프/틱/이벤트) 규칙을 한 곳에서 일관되게 적용하는 스탯 관문이다.
    // 외부 코드는 Attribute를 변경하려면 이 클래스를 통해야 한다.”
    public class AttributeSet : MonoBehaviour
    {
        [Serializable]
        public struct MaxLink
        {
            public AttributeDefinition value; // 예: Health
            public AttributeDefinition max;   // 예: MaxHealth

            [Tooltip("초기화 시 value를 max.CurrentValue로 채웁니다.")]
            public bool fillToMaxOnInitialize;
        }

        [Header("Initial Attribute Sources")]
        [SerializeField] private AttributeCatalogSO attributeCatalog;
        [SerializeField] private AttributeInitProfileSO baseInitProfile;
        [SerializeField] private AttributeInitProfileSO[] overrideInitProfiles = Array.Empty<AttributeInitProfileSO>();

        [Header("Optional: Dynamic Max Links (value is clamped by max.CurrentValue)")]
        [SerializeField] private List<MaxLink> maxLinks = new List<MaxLink>();

        private readonly Dictionary<AttributeDefinition, AttributeValue> attributes = new Dictionary<AttributeDefinition, AttributeValue>();
        private bool _initialized;
        private bool _maxLinksBound;

        public delegate void AttributeChangedDelegate(AttributeDefinition attribute, float oldValue, float newValue);
        public event AttributeChangedDelegate OnAttributeChanged;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (!_initialized) return;
            if (attributes.Count == 0) return;

            float dt = Time.deltaTime;
            foreach (var attributeValue in attributes.Values)
                attributeValue.Update(dt);
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            EnsureAllInitialAttributeEntriesExist();

            _initialized = true;

            SetupMaxLinks();
            InitializeFromInitialData();
        }

        private void EnsureAllInitialAttributeEntriesExist()
        {
            EnsureCatalogAttributesExist();
            EnsureProfileAttributesExist(baseInitProfile);

            if (overrideInitProfiles != null)
            {
                for (int i = 0; i < overrideInitProfiles.Length; i++)
                    EnsureProfileAttributesExist(overrideInitProfiles[i]);
            }

            EnsureMaxLinkAttributesExist();
        }

        private void EnsureCatalogAttributesExist()
        {
            if (attributeCatalog == null || attributeCatalog.Attributes == null)
                return;

            var defs = attributeCatalog.Attributes;
            for (int i = 0; i < defs.Length; i++)
            {
                EnsureAttributeExists(defs[i]);
            }
        }

        private void EnsureProfileAttributesExist(AttributeInitProfileSO profile)
        {
            if (profile == null || profile.Entries == null)
                return;

            var entries = profile.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                EnsureAttributeExists(entries[i].attribute);
            }
        }

        private void EnsureMaxLinkAttributesExist()
        {
            if (maxLinks == null || maxLinks.Count == 0)
                return;

            for (int i = 0; i < maxLinks.Count; i++)
            {
                var link = maxLinks[i];
                EnsureAttributeExists(link.value);
                EnsureAttributeExists(link.max);
            }
        }

        private void EnsureAttributeExists(AttributeDefinition definition)
        {
            if (definition == null) return;
            if (attributes.ContainsKey(definition)) return;

            var av = new AttributeValue(definition);
            attributes[definition] = av;

            var capturedDef = definition;
            av.OnValueChanged += (oldVal, newVal) =>
            {
                OnAttributeChanged?.Invoke(capturedDef, oldVal, newVal);
            };
        }

        private void SetupMaxLinks()
        {
            if (_maxLinksBound) return;
            if (maxLinks == null || maxLinks.Count == 0)
            {
                _maxLinksBound = true;
                return;
            }

            for (int i = 0; i < maxLinks.Count; i++)
            {
                var link = maxLinks[i];
                if (link.value == null || link.max == null)
                    continue;

                var valueAttr = GetAttribute(link.value);
                var maxAttr = GetAttribute(link.max);

                if (valueAttr == null || maxAttr == null)
                    continue;

                valueAttr.SetMaxValueGetter(() => maxAttr.CurrentValue);

                var capturedValue = valueAttr;
                maxAttr.OnValueChanged += (_, __) =>
                {
                    capturedValue.MarkDirty();
                    capturedValue.ForceRecalculate();
                };

                capturedValue.MarkDirty();
                capturedValue.ForceRecalculate();
            }

            _maxLinksBound = true;
        }

        [ContextMenu("Initialize From Initial Data")]
        public void InitializeFromInitialData()
        {
            EnsureCoreInitializedWithoutReapplying();

            ApplyDefinitionDefaults();

            if (baseInitProfile != null)
                ApplyInitProfile(baseInitProfile);

            if (overrideInitProfiles != null)
            {
                for (int i = 0; i < overrideInitProfiles.Length; i++)
                {
                    var profile = overrideInitProfiles[i];
                    if (profile == null) continue;
                    ApplyInitProfile(profile);
                }
            }

            ApplyFillToMaxOnInitialize();
        }

        [ContextMenu("Reset To Initial Data")]
        public void ResetToInitialData()
        {
            EnsureCoreInitializedWithoutReapplying();

            foreach (var pair in attributes)
            {
                var attr = pair.Value;
                if (attr == null) continue;

                attr.ClearAllModifiers();
                attr.SetBaseValue(pair.Key != null ? pair.Key.defaultBaseValue : 0f);
                attr.ForceRecalculate();
            }

            InitializeFromInitialData();
        }

        private void EnsureCoreInitializedWithoutReapplying()
        {
            if (_initialized) return;

            EnsureAllInitialAttributeEntriesExist();
            _initialized = true;
            SetupMaxLinks();
        }

        private void ApplyDefinitionDefaults()
        {
            foreach (var pair in attributes)
            {
                var def = pair.Key;
                var attr = pair.Value;

                if (def == null || attr == null)
                    continue;

                attr.SetBaseValue(def.defaultBaseValue);
                attr.ForceRecalculate();
            }
        }

        private void ApplyInitProfile(AttributeInitProfileSO profile)
        {
            if (profile == null || profile.Entries == null)
                return;

            var entries = profile.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.attribute == null)
                    continue;

                TrySetBaseValue(entry.attribute, entry.baseValue, this);
            }
        }

        private void ApplyFillToMaxOnInitialize()
        {
            if (maxLinks == null || maxLinks.Count == 0)
                return;

            for (int i = 0; i < maxLinks.Count; i++)
            {
                var link = maxLinks[i];
                if (!link.fillToMaxOnInitialize)
                    continue;

                if (link.value == null || link.max == null)
                    continue;

                var valueAttr = GetAttribute(link.value);
                var maxAttr = GetAttribute(link.max);

                if (valueAttr == null || maxAttr == null)
                    continue;

                valueAttr.SetBaseValue(maxAttr.CurrentValue);
                valueAttr.ForceRecalculate();
            }
        }

        public AttributeValue GetAttribute(AttributeDefinition definition)
        {
            EnsureInitialized();
            if (definition == null) return null;
            return attributes.TryGetValue(definition, out var v) ? v : null;
        }

        public IReadOnlyAttributeValue GetReadOnly(AttributeDefinition definition)
        {
            EnsureInitialized();
            return GetAttribute(definition);
        }

        public bool TryGetReadOnly(AttributeDefinition definition, out IReadOnlyAttributeValue value)
        {
            EnsureInitialized();
            value = null;
            if (definition == null) return false;

            if (attributes.TryGetValue(definition, out var v))
            {
                value = v;
                return true;
            }
            return false;
        }

        public float GetAttributeValue(AttributeDefinition definition)
        {
            EnsureInitialized();
            return GetAttribute(definition)?.CurrentValue ?? 0f;
        }

        public bool TrySetBaseValue(AttributeDefinition definition, float newValue, UnityEngine.Object source)
        {
            EnsureInitialized();

            if (definition == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.SetBaseValue(newValue);
            attr.ForceRecalculate();
            return true;
        }

        public bool TryModifyAttributeValue(AttributeDefinition definition, float amount, UnityEngine.Object source)
        {
            EnsureInitialized();

            if (definition == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.AddBaseValue(amount);
            attr.ForceRecalculate();
            return true;
        }

        public void RemoveModifiersFromSource(UnityEngine.Object source)
        {
            EnsureInitialized();

            foreach (var attribute in attributes.Values)
            {
                attribute.RemoveModifiersFromSource(source);
                attribute.ForceRecalculate();
            }
        }

        public bool TryAddModifier(AttributeDefinition definition, AttributeModifier modifier)
        {
            EnsureInitialized();

            if (definition == null || modifier == null)
                return false;

            if (definition.IsBaseOnly())
            {
                Debug.LogWarning($"[AttributeSet] '{definition.attributeName}' 은(는) BaseOnly 속성이므로 Modifier를 적용할 수 없습니다.");
                return false;
            }

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.AddModifier(modifier);
            attr.ForceRecalculate();
            return true;
        }

        public bool TryRemoveModifier(AttributeDefinition definition, AttributeModifier modifier)
        {
            EnsureInitialized();

            if (definition == null || modifier == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.RemoveModifier(modifier);
            attr.ForceRecalculate();
            return true;
        }

        [ContextMenu("Validate Initial Data")]
        public void ValidateInitialData()
        {
            ValidateCatalog();
            ValidateProfile(baseInitProfile, "BaseInitProfile");

            if (overrideInitProfiles != null)
            {
                for (int i = 0; i < overrideInitProfiles.Length; i++)
                    ValidateProfile(overrideInitProfiles[i], $"OverrideInitProfiles[{i}]");
            }
        }

        private void ValidateCatalog()
        {
            if (attributeCatalog == null || attributeCatalog.Attributes == null)
                return;

            var seen = new HashSet<AttributeDefinition>();

            for (int i = 0; i < attributeCatalog.Attributes.Length; i++)
            {
                var def = attributeCatalog.Attributes[i];
                if (def == null)
                {
                    Debug.LogWarning($"[AttributeSet] Catalog attribute at index {i} is null.", this);
                    continue;
                }

                if (!seen.Add(def))
                    Debug.LogWarning($"[AttributeSet] Duplicate catalog attribute: {def.name}", this);
            }
        }

        private void ValidateProfile(AttributeInitProfileSO profile, string label)
        {
            if (profile == null || profile.Entries == null)
                return;

            var local = new HashSet<AttributeDefinition>();

            for (int i = 0; i < profile.Entries.Length; i++)
            {
                var entry = profile.Entries[i];

                if (entry.attribute == null)
                {
                    Debug.LogWarning($"[AttributeSet] {label} entry {i} has null attribute.", this);
                    continue;
                }

                if (!local.Add(entry.attribute))
                    Debug.LogWarning($"[AttributeSet] {label} has duplicate attribute: {entry.attribute.name}", this);
            }
        }
    }
}