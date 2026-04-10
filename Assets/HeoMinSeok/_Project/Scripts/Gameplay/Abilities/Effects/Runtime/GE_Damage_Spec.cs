using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// SetByCaller 기반 데미지 GameplayEffect.
    /// - GameplayEffectRunner.ApplyEffectSpec() 경로로 적용되는 것을 전제로 함.
    /// - spec.SetSetByCallerMagnitude(damageKey, damage) 로 데미지 값을 전달.
    ///
    /// Token Health 지원:
    /// - 타겟이 IDamageReceiver를 구현하면, Health Attribute 대신 해당 수신자에게 피해 처리를 위임합니다.
    /// - 기본 토큰 피해는 fallbackTokenDamage(기본 1).
    /// - 필요하면 tokenDamageKey(SetByCaller)로 토큰 피해를 오버라이드할 수 있습니다.
    ///
    /// Hit Feedback (Optional):
    /// - Stun / CameraShake 등 피격 피드백을 타겟(IHitFeedbackReceiver2D)에게 전달할 수 있습니다.
    /// - 실제 연출/조작 통제는 타겟 컴포넌트에서 처리합니다.
    ///
    /// 주의:
    /// - 넉백은 더 이상 이 Effect가 처리하지 않는다.
    /// - 넉백은 GE_Knockback_Spec 등 별도 외압 Effect로 분리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "GE_Damage_Spec", menuName = "GAS/Effects/Damage (Spec)")]
    public class GE_Damage_Spec : GameplayEffect, ISpecGameplayEffect
    {
        private const string DefaultDeadTagResourcePath = "Tags/State.Dead";
        private static GameplayTag s_defaultDeadTag;

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

        [Header("Token Damage (Optional)")]
        [Tooltip("타겟이 IDamageReceiver를 통해 토큰 피해를 처리하는 경우 사용할 SetByCaller 키입니다.")]
        public GameplayTag tokenDamageKey;

        [Tooltip("IDamageReceiver가 토큰 피해를 처리할 때 사용할 기본 토큰 피해")]
        public int fallbackTokenDamage = 1;

        private void OnValidate()
        {
            duration = 0f;
            if (fallbackTokenDamage < 0) fallbackTokenDamage = 0;
            if (fallbackStunSeconds < 0f) fallbackStunSeconds = 0f;
            if (fallbackCameraShake < 0f) fallbackCameraShake = 0f;
        }

        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            if (target == null) return;

            if (s_defaultDeadTag == null)
                s_defaultDeadTag = Resources.Load<GameplayTag>(DefaultDeadTagResourcePath);

            var tags = target.GetComponent<TagSystem>();
            if (tags != null && s_defaultDeadTag != null && tags.HasTag(s_defaultDeadTag))
                return;

            // 0) 무적 태그
            if (invulnerableTag != null)
            {
                if (tags != null && tags.HasTag(invulnerableTag))
                    return;
            }

            // 1) 1회 보호막(태그) 처리
            if (oneHitShieldTag != null)
            {
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

            void TrySendHitFeedback(GameObject t, float stun, float shake)
            {
                if (t == null) return;
                if (stun <= 0f && shake <= 0f) return;

                var receiver = t.GetComponent<IHitFeedbackReceiver2D>();
                if (receiver != null)
                    receiver.OnHitFeedback(new HitFeedbackPayload(ResolveCauser(), stun, shake));
            }

            float damage = fallbackDamage;
            if (spec != null && damageKey != null && spec.TryGetSetByCallerMagnitude(damageKey, out var dv))
                damage = dv;

            float stunSeconds = fallbackStunSeconds;
            if (spec != null && stunSecondsKey != null && spec.TryGetSetByCallerMagnitude(stunSecondsKey, out var sv))
                stunSeconds = sv;

            float cameraShake = fallbackCameraShake;
            if (spec != null && cameraShakeKey != null && spec.TryGetSetByCallerMagnitude(cameraShakeKey, out var cv))
                cameraShake = cv;

            int tokenDamage = Mathf.Max(0, fallbackTokenDamage);
            if (spec != null && tokenDamageKey != null && spec.TryGetSetByCallerMagnitude(tokenDamageKey, out var td))
                tokenDamage = Mathf.Max(0, Mathf.RoundToInt(td));

            // 특수 DamageReceiver 위임 시도
            var damageReceiver = FindDamageReceiver(target);
            if (damageReceiver != null)
            {
                var request = DamageRequest.Create(
                    hpDamage: Mathf.Max(0f, damage),
                    tokenDamage: tokenDamage,
                    instigator: spec != null ? spec.Context?.Instigator : null,
                    causer: ResolveCauser(),
                    sourceObject: this,
                    knockbackImpulse: 0f,
                    stunSeconds: Mathf.Max(0f, stunSeconds),
                    cameraShake: Mathf.Max(0f, cameraShake));

                if (damageReceiver.TryApplyDamage(request))
                {
                    TrySendHitFeedback(target, stunSeconds, cameraShake);
                    return;
                }
            }

            // HP 기반 처리
            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null) return;

            if (damage <= 0f) return;

            if (absorbShieldAttribute != null)
            {
                float shield = attributeSet.GetAttributeValue(absorbShieldAttribute);
                if (shield > 0f)
                {
                    float absorbed = Mathf.Min(shield, damage);
                    attributeSet.TryModifyAttributeValue(absorbShieldAttribute, -absorbed, this);
                    damage -= absorbed;

                    if (damage <= 0f) return;
                }
            }

            if (healthAttribute == null) return;
            attributeSet.TryModifyAttributeValue(healthAttribute, -damage, this);

            TrySendHitFeedback(target, stunSeconds, cameraShake);
        }

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            Apply(spec: null, target: target);
        }

        public override void Remove(GameObject target, GameObject instigator) { }

        private static IDamageReceiver FindDamageReceiver(GameObject target)
        {
            if (target == null) return null;

            var behaviours = target.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageReceiver receiver)
                    return receiver;
            }

            return null;
        }
    }
}
