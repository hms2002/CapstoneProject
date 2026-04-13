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
