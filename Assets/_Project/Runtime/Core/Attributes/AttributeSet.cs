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
                valueAttr.SetClampNormalizationPolicy(true);

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

        /// <summary>
        /// 책임 : 현재 AttributeSet이 보유한 모든 AttributeDefinition을 순회 가능하게 제공한다.
        /// 씬 이동 저장 시 어떤 Attribute를 캡처할지 자동 수집하는 공식 진입점이다.
        /// </summary>
        public IEnumerable<AttributeDefinition> EnumerateDefinitions()
        {
            EnsureInitialized();
            return attributes.Keys;
        }

        /// <summary>
        /// 책임 : 특정 Attribute의 현재 base 값을 공식적으로 조회한다.
        /// 저장/비교/복원 정책 판단의 기준값으로 사용한다.
        /// </summary>
        public float GetBaseValue(AttributeDefinition definition)
        {
            EnsureInitialized();
            return GetAttribute(definition)?.BaseValue ?? 0f;
        }

        /// <summary>
        /// 책임 : 특정 Attribute의 현재 current 값을 공식적으로 조회한다.
        /// HP 같은 상태값 저장의 공식 읽기 창구다.
        /// </summary>
        public float GetCurrentValue(AttributeDefinition definition)
        {
            EnsureInitialized();
            return GetAttribute(definition)?.CurrentValue ?? 0f;
        }

        /// <summary>
        /// 책임 : Dynamic MaxLink 설정을 외부 정책 시스템이 읽을 수 있게 제공한다.
        /// MaxHealth/Health처럼 max-current 관계를 코드 하드코딩 없이 해석하는 공식 조회 경로다.
        /// </summary>
        public IEnumerable<MaxLink> EnumerateMaxLinks()
        {
            EnsureInitialized();
            return maxLinks;
        }

        /// <summary>
        /// 책임 : 주어진 max Attribute를 current Attribute로 사용하는 MaxLink를 찾는다.
        /// 최대값 변화 보상 정책이 어떤 현재값을 같이 조정해야 하는지 판정할 때 사용한다.
        /// </summary>
        public bool TryGetLinkedValueForMax(AttributeDefinition maxDefinition, out AttributeDefinition valueDefinition)
        {
            EnsureInitialized();
            valueDefinition = null;

            if (maxDefinition == null || maxLinks == null)
                return false;

            for (int i = 0; i < maxLinks.Count; i++)
            {
                var link = maxLinks[i];
                if (link.max != maxDefinition || link.value == null)
                    continue;

                valueDefinition = link.value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 책임 : 특정 source modifier 제거와 임시 modifier 추가를 가정한 Attribute current 값을 계산한다.
        /// 유물 해제/교체 사전 검증처럼 실제 상태를 바꾸기 전에 결과값을 예측해야 하는 경로에서 사용한다.
        /// </summary>
        public float CalculateProjectedCurrentValue(
            AttributeDefinition definition,
            UnityEngine.Object removedSource,
            IReadOnlyList<AttributeModifier> addedModifiers)
        {
            EnsureInitialized();
            return GetAttribute(definition)?.CalculateProjectedCurrentValue(removedSource, addedModifiers) ?? 0f;
        }

        /// <summary>
        /// 책임 : 씬 복원 시 current 값을 직접 되살려야 하는 상태형 Attribute인지 판정한다.
        /// 장착형 modifier로 계산되는 파생 스탯은 제외하고, HP처럼 실제 상태값만 복원 대상으로 삼는다.
        /// </summary>
        public bool ShouldRestoreCurrentValue(AttributeDefinition definition)
        {
            EnsureInitialized();

            if (definition == null)
                return false;

            if (definition.IsBaseOnly())
                return true;

            if (maxLinks == null || maxLinks.Count == 0)
                return false;

            for (int i = 0; i < maxLinks.Count; i++)
            {
                var link = maxLinks[i];
                if (link.value == definition)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 책임 : 현재 modifier/clamp 규칙을 유지한 채 목표 current 값을 복원한다.
        /// 씬 이동 후 현재 HP/MP 같은 상태값을 안전하게 되살리는 공식 창구다.
        /// </summary>
        public bool TrySetCurrentValue(AttributeDefinition definition, float value, UnityEngine.Object source)
        {
            EnsureInitialized();

            if (definition == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            return attr.TrySetCurrentValue(value);
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
                if (attribute.RemoveModifiersFromSource(source))
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
