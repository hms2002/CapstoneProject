using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// "SetByCaller" 기반 데미지 GameplayEffect.
    /// - GameplayEffectRunner.ApplyEffectSpec() 경로로 적용되는 것을 전제로 함.
    /// - spec.SetSetByCallerMagnitude(damageKey, damage) 로 데미지 값을 전달.
    /// </summary>
    [CreateAssetMenu(fileName = "GE_Damage_Spec", menuName = "GAS/Effects/Damage (Spec)")]
    public class GE_Damage_Spec : GameplayEffect, ISpecGameplayEffect
    {
        [Header("Damage")]
        [Tooltip("깎을 대상 Attribute (보통 Health)")]
        public AttributeDefinition healthAttribute;

        [Header("Invulnerability (Optional)")]
        [Tooltip("타겟이 이 태그를 가지고 있으면 이번 피해는 무효(대쉬 무적 등).")]
        public GameplayTag invulnerableTag;

        [Tooltip("SetByCaller 키 (예: Data.Damage)")]
        public GameplayTag damageKey;

        [Tooltip("SetByCaller 키가 없을 때 적용할 기본 데미지(0이면 사실상 무시)")]
        public float fallbackDamage = 0f;

        [Header("Shield (Optional)")]
        [Tooltip("1회 피해를 막아주는 보호막 태그(있으면 1회 소비하고 데미지 0 처리)")]
        public GameplayTag oneHitShieldTag;

        [Tooltip("흡수형 보호막 Attribute (있으면 먼저 여기서 깎고 남은 데미지만 Health로)")]
        public AttributeDefinition absorbShieldAttribute;

        private void OnValidate()
        {
            // 데미지 GE는 보통 Instant로 쓰는 게 안전함
            // (Duration/Periodic은 별도 DOT GE로 분리 추천)
            duration = 0f;
        }

        /// <summary>
        /// Spec 기반 적용 (권장 루트)
        /// </summary>
        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            if (target == null) return;

            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null) return;

            float damage = fallbackDamage;

            if (spec != null && damageKey != null && spec.TryGetSetByCallerMagnitude(damageKey, out var v))
                damage = v;

            if (damage <= 0f) return;

            // 0) 무적 태그(대쉬 무적 등)
            if (invulnerableTag != null)
            {
                var tags = target.GetComponent<TagSystem>();
                if (tags != null && tags.HasTag(invulnerableTag))
                    return;
            }

            // 1) 1회 보호막(태그) 처리
            if (oneHitShieldTag != null)
            {
                var tags = target.GetComponent<TagSystem>();
                if (tags != null && tags.GetTagCount(oneHitShieldTag) > 0)
                {
                    tags.RemoveTag(oneHitShieldTag, 1);
                    return; // 이번 데미지는 완전히 무효
                }
            }

            // 2) 흡수 보호막(실드HP Attribute) 처리
            if (absorbShieldAttribute != null)
            {
                float shield = attributeSet.GetAttributeValue(absorbShieldAttribute);
                if (shield > 0f)
                {
                    float absorbed = Mathf.Min(shield, damage);
                    attributeSet.ModifyAttributeValue(absorbShieldAttribute, -absorbed, this);
                    damage -= absorbed;

                    if (damage <= 0f) return;
                }
            }

            // 3) Health 감소
            if (healthAttribute == null) return;
            attributeSet.ModifyAttributeValue(healthAttribute, -damage, this);

            // (선택) 여기서 Hit 이벤트/게이지 이벤트를 쏘고 싶으면,
            // spec.Context / spec.Context.SourceObject 등을 활용하면 됨.
        }

        /// <summary>
        /// 기존 ApplyEffect(effect, target, instigator) 경로로 호출되더라도 최소 동작은 하게 만든 폴백.
        /// (가능하면 프로젝트 규칙으로 Spec 경로만 쓰는 걸 권장)
        /// </summary>
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            // Spec이 없으면 fallbackDamage만 적용
            Apply(spec: null, target: target);
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            // Instant damage는 제거 로직 없음
        }
    }
}
