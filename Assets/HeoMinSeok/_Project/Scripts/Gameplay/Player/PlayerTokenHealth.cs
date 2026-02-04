using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 플레이어 전용 '토큰 체력' 컴포넌트.
    /// - 예: 5~7개의 토큰을 가지고 있고, 피격 시 토큰이 1(또는 N) 감소.
    /// - 필요하면 AttributeSet(Health/MaxHealth)과 동기화해서 기존 UI/시스템을 재사용할 수 있음.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerTokenHealth : MonoBehaviour
    {
        [Header("Tokens")]
        [Min(1)] public int maxTokens = 5;
        [Min(0)] public int currentTokens = 5;

        [Tooltip("오브젝트가 Enable 될 때 currentTokens를 maxTokens로 리셋할지 (풀링 스폰에 유용)")]
        public bool resetToFullOnEnable = true;

        [Header("Optional: Sync to AttributeSet")]
        [Tooltip("true면 AttributeSet의 MaxHealth/Health를 (maxTokens/currentTokens)로 동기화합니다.")]
        public bool syncToAttributes = true;

        [Tooltip("동기화에 사용할 '현재 체력' Attribute (예: Health)")]
        public AttributeDefinition healthAttribute;

        [Tooltip("동기화에 사용할 '최대 체력' Attribute (예: MaxHealth)")]
        public AttributeDefinition maxHealthAttribute;

        public event Action<int, int> OnTokensChanged; // (old,new)
        public event Action<int> OnTokenDamaged;       // (amount)

        private AttributeSet _attributeSet;
        private AttributeValue _health;
        private AttributeValue _maxHealth;

        public bool IsDead => currentTokens <= 0;

        private void Awake()
        {
            CacheAttributes();
            // 씬 시작 시에도 한번 정합성 맞추기
            currentTokens = Mathf.Clamp(currentTokens, 0, Mathf.Max(1, maxTokens));
            SyncAttributesImmediate();
        }

        private void OnEnable()
        {
            if (resetToFullOnEnable)
            {
                currentTokens = Mathf.Max(1, maxTokens);
            }
            CacheAttributes();
            SyncAttributesImmediate();
        }

        private void CacheAttributes()
        {
            if (!syncToAttributes) return;

            if (_attributeSet == null) _attributeSet = GetComponent<AttributeSet>();
            if (_attributeSet == null) return;

            _maxHealth = maxHealthAttribute != null ? _attributeSet.GetAttribute(maxHealthAttribute) : null;
            _health = healthAttribute != null ? _attributeSet.GetAttribute(healthAttribute) : null;
        }

        /// <summary>
        /// 토큰 최대치 설정. (리셋/클램프 정책 선택 가능)
        /// </summary>
        public void SetMaxTokens(int newMax, bool refillToFull = false, bool clampCurrent = true)
        {
            newMax = Mathf.Max(1, newMax);
            maxTokens = newMax;

            if (refillToFull) currentTokens = maxTokens;
            else if (clampCurrent) currentTokens = Mathf.Clamp(currentTokens, 0, maxTokens);

            SyncAttributesImmediate();
        }

        /// <summary>
        /// 토큰 피해 적용 (A안).
        /// </summary>
        public void ApplyTokenDamage(int tokenDamage, UnityEngine.Object source = null)
        {
            if (tokenDamage <= 0) return;

            int old = currentTokens;
            currentTokens = Mathf.Clamp(currentTokens - tokenDamage, 0, Mathf.Max(1, maxTokens));

            if (currentTokens != old)
            {
                OnTokenDamaged?.Invoke(tokenDamage);
                OnTokensChanged?.Invoke(old, currentTokens);
                SyncAttributesImmediate();
            }

            if (currentTokens <= 0)
            {
                // TODO: 사망 처리(리스폰/게임오버)는 프로젝트 규칙에 맞춰 여기에서 이벤트만 쏘는 걸 권장.
                // Debug.Log("[PlayerTokenHealth] Dead");
            }
        }

        public void HealTokens(int amount)
        {
            if (amount <= 0) return;
            int old = currentTokens;
            currentTokens = Mathf.Clamp(currentTokens + amount, 0, Mathf.Max(1, maxTokens));
            if (currentTokens != old)
            {
                OnTokensChanged?.Invoke(old, currentTokens);
                SyncAttributesImmediate();
            }
        }

        /// <summary>
        /// AttributeSet(Health/MaxHealth)와 즉시 동기화.
        /// MaxLink clamp 문제 때문에 항상 'MaxHealth -> Health' 순서로 갱신합니다.
        /// </summary>
        private void SyncAttributesImmediate()
        {
            if (!syncToAttributes) return;
            if (_attributeSet == null) _attributeSet = GetComponent<AttributeSet>();
            if (_attributeSet == null) return;

            CacheAttributes();
            if (_maxHealth == null || _health == null) return;

            // 순서 중요: MaxHealth 먼저
            _maxHealth.SetBaseValue(maxTokens);
            _maxHealth.ForceRecalculate();

            _health.SetBaseValue(currentTokens);
            _health.ForceRecalculate();
        }
    }
}
