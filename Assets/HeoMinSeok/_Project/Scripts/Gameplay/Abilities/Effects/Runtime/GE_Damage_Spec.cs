using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// "SetByCaller" 기반 데미지 GameplayEffect.
    /// - GameplayEffectRunner.ApplyEffectSpec() 경로로 적용되는 것을 전제로 함.
    /// - spec.SetSetByCallerMagnitude(damageKey, damage) 로 데미지 값을 전달.
    ///
    /// Token Health 지원:
    /// - 타겟에 PlayerTokenHealth가 붙어있으면, Health Attribute 대신 "토큰"을 감소시킵니다.
    /// - 기본 토큰 피해는 fallbackTokenDamage(기본 1).
    /// - 필요하면 tokenDamageKey(SetByCaller)로 토큰 피해를 오버라이드할 수 있습니다.
    ///
    /// Hit Feedback (Optional):
    /// - Stun / CameraShake 등 피격 피드백을 타겟(IHitFeedbackReceiver2D)에게 전달할 수 있습니다.
    /// - 실제 연출/조작 통제는 타겟 컴포넌트에서 처리합니다.
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

        [Header("Knockback (Optional)")]
        [Tooltip("SetByCaller 키 (예: Data.Knockback). 값은 Rigidbody2D AddForce(Impulse)로 적용됩니다.")]
        public GameplayTag knockbackKey;

        [Tooltip("SetByCaller 키가 없을 때 적용할 기본 넉백(Impulse).")]
        public float fallbackKnockback = 0f;

        [Header("Hit Feedback (Optional)")]
        [Tooltip("SetByCaller 키 (예: Data.StunSeconds). 타겟이 IHitFeedbackReceiver2D를 구현/보유하면 전달됩니다.")]
        public GameplayTag stunSecondsKey;

        [Tooltip("SetByCaller 키가 없을 때 적용할 기본 경직 시간(초). 0이면 전달하지 않습니다.")]
        public float fallbackStunSeconds = 0.3f;

        [Tooltip("SetByCaller 키 (예: Data.CameraShake). 타겟이 IHitFeedbackReceiver2D를 구현/보유하면 전달됩니다.")]
        public GameplayTag cameraShakeKey;

        [Tooltip("SetByCaller 키가 없을 때 적용할 기본 카메라 쉐이크(amplitude). 0이면 전달하지 않습니다.")]
        public float fallbackCameraShake = 0.10f;

        [Header("Shield (Optional)")]
        [Tooltip("1회 피해를 막아주는 보호막 태그(있으면 1회 소비하고 데미지 0 처리)")]
        public GameplayTag oneHitShieldTag;

        [Tooltip("흡수형 보호막 Attribute (있으면 먼저 여기서 깎고 남은 데미지만 Health로)")]
        public AttributeDefinition absorbShieldAttribute;

        [Header("Token Health (Optional)")]
        [Tooltip("타겟이 PlayerTokenHealth일 때 사용할 SetByCaller 키 (예: Data.TokenDamage). 비워두면 fallbackTokenDamage 사용.")]
        public GameplayTag tokenDamageKey;

        [Tooltip("PlayerTokenHealth 타겟에 대한 기본 토큰 피해(기본 1).")]
        public int fallbackTokenDamage = 1;

        private void OnValidate()
        {
            duration = 0f;
            if (fallbackTokenDamage < 0) fallbackTokenDamage = 0;
            if (fallbackStunSeconds < 0f) fallbackStunSeconds = 0f;
            if (fallbackCameraShake < 0f) fallbackCameraShake = 0f;
        }

        /// <summary>
        /// Spec 기반 적용 (권장 루트)
        /// </summary>
        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            if (target == null) return;

            // 0) 무적 태그
            if (invulnerableTag != null)
            {
                var tags = target.GetComponent<TagSystem>();
                if (tags != null && tags.HasTag(invulnerableTag))
                    return;
            }

            // 1) 1회 보호막(태그) 처리 (토큰/HP 공통)
            if (oneHitShieldTag != null)
            {
                var tags = target.GetComponent<TagSystem>();
                if (tags != null && tags.GetTagCount(oneHitShieldTag) > 0)
                {
                    tags.RemoveTag(oneHitShieldTag, 1);
                    return;
                }
            }

            GameObject ResolveCauser()
            {
                GameObject causer = spec != null ? spec.Context?.Causer : null;
                if (causer == null) causer = spec != null ? spec.Context?.Instigator : null;
                return causer;
            }

            void TryApplyKnockback(GameObject t)
            {
                if (t == null) return;

                float kb = fallbackKnockback;
                if (spec != null && knockbackKey != null && spec.TryGetSetByCallerMagnitude(knockbackKey, out var kbv))
                    kb = kbv;

                if (kb <= 0f) return;

                var receiver = t.GetComponent<KnockbackReceiver2D>();
                if (receiver != null)
                    receiver.ApplyKnockback(ResolveCauser(), kb);
            }

            void TrySendHitFeedback(GameObject t)
            {
                if (t == null) return;

                // Resolve values (0 means "do not send" for that channel)
                float stun = fallbackStunSeconds;
                if (spec != null && stunSecondsKey != null && spec.TryGetSetByCallerMagnitude(stunSecondsKey, out var sv))
                    stun = sv;

                float shake = fallbackCameraShake;
                if (spec != null && cameraShakeKey != null && spec.TryGetSetByCallerMagnitude(cameraShakeKey, out var cv))
                    shake = cv;

                if (stun <= 0f && shake <= 0f) return;

                var receiver = t.GetComponent<IHitFeedbackReceiver2D>();
                if (receiver != null)
                    receiver.OnHitFeedback(new HitFeedbackPayload(ResolveCauser(), stun, shake));
            }

            // ✅ Token Health 우선 처리
            var tokenHealth = target.GetComponent<PlayerTokenHealth>();
            if (tokenHealth != null)
            {
                int tokenDamage = Mathf.Max(0, fallbackTokenDamage);

                if (spec != null && tokenDamageKey != null &&
                    spec.TryGetSetByCallerMagnitude(tokenDamageKey, out var td))
                {
                    tokenDamage = Mathf.Max(0, Mathf.RoundToInt(td));
                }

                if (tokenDamage <= 0) return;

                tokenHealth.ApplyTokenDamage(tokenDamage, source: this);

                TryApplyKnockback(target);
                TrySendHitFeedback(target);
                return;
            }

            // -------------------------
            // HP 기반 처리
            // -------------------------
            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null) return;

            float damage = fallbackDamage;

            if (spec != null && damageKey != null && spec.TryGetSetByCallerMagnitude(damageKey, out var v))
                damage = v;

            if (damage <= 0f) return;

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

            TryApplyKnockback(target);
            TrySendHitFeedback(target);
        }

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            Apply(spec: null, target: target);
        }

        public override void Remove(GameObject target, GameObject instigator) { }
    }
}
