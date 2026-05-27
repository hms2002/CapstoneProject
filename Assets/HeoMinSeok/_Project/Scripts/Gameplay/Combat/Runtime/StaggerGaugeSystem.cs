using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 대상의 스태거 게이지 누적/트리거를 관리한다.
    /// - 스태거 관련 월드 UI가 공통 기준점을 잡을 수 있도록 선택적 표현 오프셋 정보를 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaggerGaugeSystem : MonoBehaviour
    {
        private const string StaggerImmuneTagResourcePath = "Tags/State.Status.StaggerImmune";
        private static GameplayTag s_staggerImmuneTag;

        [Header("Gauge Attributes")]
        public AttributeDefinition currentGaugeAttribute;      // 예: StaggerGauge
        public AttributeDefinition maxGaugeAttribute;          // 예: MaxStaggerGauge
        public AttributeDefinition resistancePercentAttribute; // 예: StaggerResistance (0.2 = 20%)

        [Header("Trigger")]
        public GameplayEffect staggeredEffect;
        public bool allowOverflow = true;

        [Header("Presentation")]
        [SerializeField] private bool allowPresentationOffset;
        [SerializeField] private Vector3 presentationWorldOffset = new(0f, 1.6f, 0f);
        [SerializeField] private Transform presentationAnchor;
        [SerializeField] private SpriteRenderer presentationBoundsSource;
        [SerializeField] private BossGroggyHeadTimer groggyTimerPrefab;
        [SerializeField] private Transform groggyTimerParent;

        public event Action<float, float> OnGaugeChanged; // old,new
        public event Action OnTriggered;

        private GameplayEffectRunner _runner;
        private AttributeSet _attr;
        private BossGroggyHeadTimer _spawnedGroggyTimer;

        public bool AllowPresentationOffset => allowPresentationOffset;
        public Vector3 PresentationWorldOffset => presentationWorldOffset;
        public Transform PresentationAnchor => presentationAnchor != null ? presentationAnchor : transform;
        public SpriteRenderer PresentationBoundsSource => presentationBoundsSource;

        private void Awake()
        {
            _runner = GetComponent<GameplayEffectRunner>();
            _attr = GetComponent<AttributeSet>();
        }

        private void Start()
        {
            EnsureGroggyTimerInstance();
        }

        public void Clear()
        {
            if (_attr == null || currentGaugeAttribute == null) return;

            float old = GetCurrentGauge();
            SetCurrentGauge(0f);
            OnGaugeChanged?.Invoke(old, 0f);
        }

        public void AddBuildUp(float amount, GameObject instigator, GameObject causer)
        {
            if (_attr == null) return;
            if (currentGaugeAttribute == null || maxGaugeAttribute == null) return;
            if (amount <= 0f) return;
            if (IsBuildUpSuppressed()) return;

            if (resistancePercentAttribute != null)
            {
                float resist = Mathf.Clamp01(_attr.GetAttributeValue(resistancePercentAttribute));
                amount *= (1f - resist);
                if (amount <= 0f) return;
            }

            float old = GetCurrentGauge();
            float max = Mathf.Max(0f, _attr.GetAttributeValue(maxGaugeAttribute));
            float next = old + amount;

            if (max <= 0f)
            {
                SetCurrentGauge(next);
                OnGaugeChanged?.Invoke(old, next);
                return;
            }

            int triggerCount = 0;
            while (next >= max)
            {
                triggerCount++;

                if (allowOverflow)
                    next -= max;
                else
                {
                    next = 0f;
                    break;
                }
            }

            SetCurrentGauge(next);
            OnGaugeChanged?.Invoke(old, next);

            if (triggerCount <= 0)
                return;

            for (int i = 0; i < triggerCount; i++)
                OnTriggered?.Invoke();

            if (staggeredEffect != null && _runner != null)
            {
                var src = instigator != null ? instigator : causer;
                for (int i = 0; i < triggerCount; i++)
                    _runner.ApplyEffect(staggeredEffect, gameObject, src);
            }
        }

        /// <summary>
        /// 책임 :
        /// - 스태거 게이지 직접 호출 경로에서도 StaggerImmune 태그를 일관되게 존중한다.
        /// - CombatDamageAction을 거치지 않는 특수 상호작용이 면역 중 누적/트리거를 우회하지 못하게 한다.
        /// </summary>
        private bool IsBuildUpSuppressed()
        {
            if (s_staggerImmuneTag == null)
                s_staggerImmuneTag = Resources.Load<GameplayTag>(StaggerImmuneTagResourcePath);

            if (s_staggerImmuneTag == null)
                return false;

            TagSystem tagSystem = GetComponent<TagSystem>();
            return tagSystem != null && tagSystem.HasTag(s_staggerImmuneTag);
        }

        /// <summary>
        /// 책임 :
        /// - 외부 패턴 보상이나 특수 상호작용이 현재 스태거 누적치를 안전하게 낮출 수 있는 공용 회복 창구를 제공한다.
        /// - 게이지 변경 이벤트를 동일하게 발행해 HUD/Presentation이 별도 연결 없이 갱신되도록 한다.
        /// </summary>
        public float ReduceBuildUp(float amount)
        {
            if (_attr == null || currentGaugeAttribute == null)
                return 0f;
            if (amount <= 0f)
                return 0f;

            float old = GetCurrentGauge();
            float next = Mathf.Max(0f, old - amount);
            if (Mathf.Approximately(old, next))
                return 0f;

            SetCurrentGauge(next);
            OnGaugeChanged?.Invoke(old, next);
            return old - next;
        }

        /// <summary>
        /// 책임 :
        /// - 최대 스태거 게이지 비율을 기준으로 현재 누적치를 회복한다.
        /// - 기획에서 "최대치의 N%"처럼 표현되는 회복량을 호출부가 Attribute 세부를 몰라도 적용하게 한다.
        /// </summary>
        public float ReduceBuildUpByMaxRatio(float ratio)
        {
            if (_attr == null || maxGaugeAttribute == null)
                return 0f;

            float max = Mathf.Max(0f, _attr.GetAttributeValue(maxGaugeAttribute));
            return max > 0f ? ReduceBuildUp(max * Mathf.Clamp01(ratio)) : 0f;
        }

        private float GetCurrentGauge()
        {
            return _attr != null && currentGaugeAttribute != null
                ? _attr.GetAttributeValue(currentGaugeAttribute)
                : 0f;
        }

        private void SetCurrentGauge(float value)
        {
            if (_attr == null || currentGaugeAttribute == null)
                return;

            _attr.TrySetBaseValue(currentGaugeAttribute, Mathf.Max(0f, value), this);
        }

        /// <summary>
        /// 책임 :
        /// - 스태거 시스템과 함께 쓰이는 그로기 타이머 프리팹을 자동 생성하고 기본 바인딩을 주입한다.
        /// - 보스별 presentation authoring 지점을 StaggerGaugeSystem 하나로 모아 수동 연결 부담을 줄인다.
        /// </summary>
        private void EnsureGroggyTimerInstance()
        {
            if (groggyTimerPrefab == null || _spawnedGroggyTimer != null)
                return;

            BossControllerBase boss = GetComponent<BossControllerBase>();
            if (boss == null)
                return;

            Transform parent = groggyTimerParent != null ? groggyTimerParent : transform;
            _spawnedGroggyTimer = Instantiate(groggyTimerPrefab, parent);
            _spawnedGroggyTimer.ConfigureForBoss(boss, this, staggeredEffect, _runner);
        }
    }
}
