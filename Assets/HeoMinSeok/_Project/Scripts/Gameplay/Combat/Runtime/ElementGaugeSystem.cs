using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public sealed class ElementGaugeSystem : MonoBehaviour
    {
        [Serializable]
        public sealed class ElementGaugeState
        {
            [Header("Definition")]
            public ElementGaugeDefinition definition;

            [Header("Runtime")]
            public float currentBuildUp;
            public float lastBuildUpTime = -999f;

            [Header("Runtime VFX")]
            public GameObject sustainVfxInstance;

            [Header("Runtime UI")]
            public bool uiVisible;
        }

        [Header("Catalog")]
        [SerializeField] private ElementGaugeCatalog catalog;

        [Header("Decay")]
        [SerializeField] private bool useDecay = true;
        [SerializeField] private float decayDelaySeconds = 3f;
        [SerializeField, Range(0f, 1f)] private float decayPercentPerSecond = 0.02f;

        [Header("Trigger Policy")]
        [SerializeField] private bool allowOverflowCarry = true;

        [Header("Debug")]
        [SerializeField] private bool logWhenTriggered = false;
        [SerializeField] private bool logMissingDefinition = true;

        [Header("Runtime (Debug)")]
        [SerializeField] private List<ElementGaugeState> runtimeStates = new();

        public event Action<GameplayTag, float, float> OnGaugeChanged; // element, old, new
        public event Action<GameplayTag> OnGaugeTriggered;

        private readonly Dictionary<GameplayTag, ElementGaugeState> stateByTag = new();

        private GameplayEffectRunner _runner;
        private AttributeSet _attr;

        private void Awake()
        {
            _runner = GetComponent<GameplayEffectRunner>();
            _attr = GetComponent<AttributeSet>();
            RebuildRuntimeStates();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (decayDelaySeconds < 0f) decayDelaySeconds = 0f;
        }
#endif

        private void Update()
        {
            TickDecay();
        }

        public float GetValue(GameplayTag elementType)
        {
            var state = FindState(elementType);
            return state != null ? Mathf.Max(0f, state.currentBuildUp) : 0f;
        }

        public float GetThreshold(GameplayTag elementType)
        {
            var state = FindState(elementType);
            return state != null ? Mathf.Max(0f, GetMaxGauge(state.definition)) : 0f;
        }

        public void ClearAll()
        {
            if (runtimeStates == null) return;

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                var state = runtimeStates[i];
                if (state == null || state.definition == null || state.definition.elementTag == null)
                    continue;

                float old = state.currentBuildUp;
                state.currentBuildUp = 0f;
                state.lastBuildUpTime = -999f;

                if (state.sustainVfxInstance != null)
                {
                    Destroy(state.sustainVfxInstance);
                    state.sustainVfxInstance = null;
                }

                state.uiVisible = false;

                if (!Mathf.Approximately(old, 0f))
                    OnGaugeChanged?.Invoke(state.definition.elementTag, old, 0f);
            }
        }

        public void AddBuildUp(GameplayTag elementType, float amount, GameObject instigator, GameObject causer)
        {
            if (elementType == null || amount <= 0f)
                return;

            var state = FindState(elementType);
            if (state == null || state.definition == null)
            {
#if UNITY_EDITOR
                if (logMissingDefinition)
                {
                    Debug.LogWarning(
                        $"[ElementGaugeSystem] Missing gauge definition for element '{elementType.CachedPath}' on '{name}'. Ignoring build-up.",
                        this);
                }
#endif
                return;
            }

            float maxGauge = Mathf.Max(0f, GetMaxGauge(state.definition));
            float resistance = Mathf.Clamp01(GetResistance(state.definition));
            float finalAmount = amount * (1f - resistance);

            if (finalAmount <= 0f)
                return;

            float old = state.currentBuildUp;
            state.lastBuildUpTime = Time.time;
            state.uiVisible = true;

            // maxGauge <= 0 이면 임계점 없는 누적통으로 취급
            if (maxGauge <= 0f)
            {
                state.currentBuildUp += finalAmount;
                OnGaugeChanged?.Invoke(elementType, old, state.currentBuildUp);
                return;
            }

            float next = old + finalAmount;
            int triggerCount = 0;

            while (next >= maxGauge)
            {
                triggerCount++;

                if (allowOverflowCarry)
                    next -= maxGauge;
                else
                {
                    next = 0f;
                    break;
                }
            }

            state.currentBuildUp = Mathf.Max(0f, next);
            OnGaugeChanged?.Invoke(elementType, old, state.currentBuildUp);

            if (triggerCount <= 0)
                return;

            for (int i = 0; i < triggerCount; i++)
            {
                TriggerGaugeFull(state, instigator, causer);
                OnGaugeTriggered?.Invoke(elementType);
            }

            if (logWhenTriggered)
            {
                string srcName = instigator != null ? instigator.name : (causer != null ? causer.name : "null");
                Debug.Log(
                    $"[ElementGaugeSystem] TRIGGER {elementType.CachedPath} x{triggerCount} on '{name}' (source='{srcName}')",
                    this);
            }
        }

        public void RebuildRuntimeStates()
        {
            runtimeStates.Clear();
            stateByTag.Clear();

            if (catalog == null || catalog.definitions == null)
                return;

            for (int i = 0; i < catalog.definitions.Length; i++)
            {
                var def = catalog.definitions[i];
                if (def == null || def.elementTag == null)
                    continue;

                if (stateByTag.ContainsKey(def.elementTag))
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[ElementGaugeSystem] Duplicate element tag '{def.elementTag.CachedPath}' in catalog '{catalog.name}'.",
                        catalog);
#endif
                    continue;
                }

                var state = new ElementGaugeState
                {
                    definition = def,
                    currentBuildUp = 0f,
                    lastBuildUpTime = -999f,
                    sustainVfxInstance = null,
                    uiVisible = false
                };

                runtimeStates.Add(state);
                stateByTag.Add(def.elementTag, state);
            }
        }

        private void TickDecay()
        {
            if (!useDecay) return;
            if (runtimeStates == null || runtimeStates.Count == 0) return;

            float now = Time.time;

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                var state = runtimeStates[i];
                if (state == null || state.definition == null) continue;
                if (state.currentBuildUp <= 0f) continue;

                if (now - state.lastBuildUpTime < decayDelaySeconds)
                    continue;

                float maxGauge = Mathf.Max(0f, GetMaxGauge(state.definition));
                if (maxGauge <= 0f)
                    continue;

                float decayAmount = maxGauge * decayPercentPerSecond * Time.deltaTime;
                if (decayAmount <= 0f)
                    continue;

                float old = state.currentBuildUp;
                state.currentBuildUp = Mathf.Max(0f, state.currentBuildUp - decayAmount);

                if (!Mathf.Approximately(old, state.currentBuildUp))
                    OnGaugeChanged?.Invoke(state.definition.elementTag, old, state.currentBuildUp);

                if (state.currentBuildUp <= 0f)
                    state.uiVisible = false;
            }
        }

        private void TriggerGaugeFull(ElementGaugeState state, GameObject instigator, GameObject causer)
        {
            if (state == null || state.definition == null)
                return;

            var def = state.definition;
            var src = instigator != null ? instigator : causer;

            // 1) Trigger VFX
            if (def.triggerVfxPrefab != null)
            {
                Instantiate(def.triggerVfxPrefab, transform.position, Quaternion.identity, transform);
            }

            // 2) Sustain VFX
            if (def.sustainVfxPrefab != null)
            {
                if (state.sustainVfxInstance == null)
                {
                    state.sustainVfxInstance = Instantiate(
                        def.sustainVfxPrefab,
                        transform.position,
                        Quaternion.identity,
                        transform);
                }
                else
                {
                    state.sustainVfxInstance.transform.SetPositionAndRotation(
                        transform.position,
                        Quaternion.identity);
                    state.sustainVfxInstance.SetActive(false);
                    state.sustainVfxInstance.SetActive(true);
                }
            }

            // 3) Trigger effect
            if (_runner == null || def.triggerEffect == null)
                return;

            var ctx = new GameplayEffectContext(src, src)
            {
                SourceObject = def.triggerEffect
            };

            var spec = new GameplayEffectSpec(def.triggerEffect, ctx);
            _runner.ApplyEffectSpec(spec, gameObject);
        }

        private ElementGaugeState FindState(GameplayTag elementType)
        {
            if (elementType == null)
                return null;

            if (stateByTag.TryGetValue(elementType, out var state))
                return state;

            return null;
        }

        private float GetMaxGauge(ElementGaugeDefinition def)
        {
            if (_attr == null || def == null || def.maxGaugeAttribute == null)
                return 0f;

            return Mathf.Max(0f, _attr.GetAttributeValue(def.maxGaugeAttribute));
        }

        private float GetResistance(ElementGaugeDefinition def)
        {
            if (_attr == null || def == null || def.resistanceAttribute == null)
                return 0f;

            return _attr.GetAttributeValue(def.resistanceAttribute);
        }

        public int GetGaugeUiModels(List<ElementGaugeUiModel> buffer, bool visibleOnly = false)
        {
            if (buffer == null)
                return 0;

            buffer.Clear();

            if (runtimeStates == null || runtimeStates.Count == 0)
                return 0;

            for (int i = 0; i < runtimeStates.Count; i++)
            {
                var state = runtimeStates[i];
                if (state == null || state.definition == null || state.definition.elementTag == null)
                    continue;

                float current = Mathf.Max(0f, state.currentBuildUp);
                float threshold = Mathf.Max(0f, GetMaxGauge(state.definition));

                // 표시 여부는 실제 누적값 기준으로만 판단
                bool visible = current > 0.0001f;

                // threshold가 0이면 퍼센트 계산 보호용 기본값 사용
                if (threshold <= 0f)
                    threshold = 1f;

                if (visibleOnly && !visible)
                    continue;

                buffer.Add(new ElementGaugeUiModel(
                    state.definition.elementTag,
                    state.definition.icon,
                    current,
                    threshold,
                    visible));
            }

            return buffer.Count;
        }
    }
}