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
    // 책임: SetByCaller 피해량을 Attribute/토큰 체력 피해와 피격 피드백으로 적용하는 GameplayEffect 사양이다.
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

        [Header("Debug")]
        [Tooltip("속성 발현 피해 팝업의 태그/피해량/억제 예약 흐름을 콘솔에 출력합니다.")]
        [SerializeField] private bool logElementDamagePopup = true;

        [Tooltip("환경 피해 등 일반 피해의 보호막/HP 적용 흐름을 콘솔에 출력합니다.")]
        [SerializeField] private bool logDamageApplication;

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

            LogDamageSpecEntry(spec, target);

            if (s_defaultDeadTag == null)
                s_defaultDeadTag = Resources.Load<GameplayTag>(DefaultDeadTagResourcePath);

            var tags = target.GetComponent<TagSystem>();
            if (tags != null && s_defaultDeadTag != null && tags.HasTag(s_defaultDeadTag))
                return;

            // 0) 무적 태그
            if (invulnerableTag != null)
            {
                if (tags != null && tags.HasTag(invulnerableTag))
                {
                    LogDamageApplication($"blocked: invulnerable tag. target={target.name}, tag={invulnerableTag.name}", target);
                    return;
                }
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

            float preHp = healthAttribute != null
                ? attributeSet.GetAttributeValue(healthAttribute)
                : 0f;

            if (absorbShieldAttribute != null)
            {
                float shield = attributeSet.GetAttributeValue(absorbShieldAttribute);
                if (shield > 0f)
                {
                    float absorbed = Mathf.Min(shield, damage);
                    bool shieldModified = attributeSet.TryModifyAttributeValue(absorbShieldAttribute, -absorbed, this);
                    float postShield = attributeSet.GetAttributeValue(absorbShieldAttribute);
                    LogDamageApplication(
                        $"shield absorb. target={target.name}, shield={shield:0.###}->{postShield:0.###}, absorbed={absorbed:0.###}, remainingBefore={damage:0.###}, modifyResult={shieldModified}",
                        target);
                    damage -= absorbed;

                    if (damage <= 0f)
                    {
                        LogDamageApplication($"hp skipped: fully absorbed. target={target.name}", target);
                        return;
                    }
                }
                else
                {
                    LogDamageApplication($"shield skipped: no shield. target={target.name}, shield={shield:0.###}", target);
                }
            }

            if (healthAttribute == null)
            {
                LogDamageApplication($"hp skipped: healthAttribute is null. target={target.name}, remainingDamage={damage:0.###}", target);
                return;
            }

            TryReserveElementDamagePopupSuppression(spec, target, preHp, damage);
            bool hpModified = attributeSet.TryModifyAttributeValue(healthAttribute, -damage, this);
            float postHpAfterModify = attributeSet.GetAttributeValue(healthAttribute);
            LogDamageApplication(
                $"hp modify. target={target.name}, damage={damage:0.###}, hp={preHp:0.###}->{postHpAfterModify:0.###}, modifyResult={hpModified}",
                target);
            TryShowElementDamagePopup(spec, target, attributeSet, preHp, damage);

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

        /// <summary>
        /// 책임 :
        /// - AttributeSet 변경 이벤트가 동기적으로 발생하기 전에 fallback 데미지 팝업을 먼저 억제 예약한다.
        /// - 속성 발현 피해가 흰색 일반 팝업으로 먼저 출력되는 타이밍 문제를 막는다.
        /// </summary>
        private void TryReserveElementDamagePopupSuppression(
            GameplayEffectSpec spec,
            GameObject target,
            float preHp,
            float remainingDamage)
        {
            GameplayTag popupElementTag = spec != null ? spec.Context?.DamagePopupElementTag : null;
            if (popupElementTag == null || target == null || healthAttribute == null)
                return;

            float expectedDamage = Mathf.Max(0f, Mathf.Min(remainingDamage, preHp - healthAttribute.minValue));
            if (expectedDamage <= 0f)
                return;

            DamagePopupDuplicateSuppressor.Register(
                target,
                expectedDamage,
                kind: DamagePopupSuppressionKind.Element);
            LogElementPopup(
                $"reserved suppression expectedDamage={expectedDamage:0.###}, preHp={preHp:0.###}, remainingDamage={remainingDamage:0.###}",
                popupElementTag,
                target);
        }

        /// <summary>
        /// 책임 :
        /// - 속성 게이지 발현 효과가 실제 HP 피해를 입힌 경우에만 속성 피해 팝업을 표시한다.
        /// - 단순 게이지 축적 수치가 데미지처럼 보이지 않도록 팝업 표시 책임을 실제 피해 적용 지점으로 제한한다.
        /// </summary>
        private void TryShowElementDamagePopup(
            GameplayEffectSpec spec,
            GameObject target,
            AttributeSet attributeSet,
            float preHp,
            float remainingDamage)
        {
            GameplayTag popupElementTag = spec != null ? spec.Context?.DamagePopupElementTag : null;
            if (popupElementTag == null || target == null || attributeSet == null || healthAttribute == null)
            {
                LogElementPopup(
                    $"skip show: tag={(popupElementTag != null ? popupElementTag.Path : "null")}, target={(target != null ? target.name : "null")}, attributeSet={(attributeSet != null ? "ok" : "null")}, healthAttribute={(healthAttribute != null ? healthAttribute.name : "null")}",
                    popupElementTag,
                    target);
                return;
            }

            float postHp = attributeSet.GetAttributeValue(healthAttribute);
            float appliedDamage = ResolvePopupDamageAmount(preHp, postHp, remainingDamage);
            if (appliedDamage <= 0f)
            {
                LogElementPopup(
                    $"skip show: appliedDamage={appliedDamage:0.###}, preHp={preHp:0.###}, postHp={postHp:0.###}, remainingDamage={remainingDamage:0.###}",
                    popupElementTag,
                    target);
                return;
            }

            Vector3 popupPosition = ResolvePopupPosition(spec, target);
            LogElementPopup(
                $"show element popup appliedDamage={appliedDamage:0.###}, preHp={preHp:0.###}, postHp={postHp:0.###}, remainingDamage={remainingDamage:0.###}, position={popupPosition}",
                popupElementTag,
                target);
            DamagePopupPlayback.Show(DamagePopupRequest.Element(appliedDamage, popupPosition, popupElementTag));
            DamagePopupDuplicateSuppressor.Register(
                target,
                appliedDamage,
                kind: DamagePopupSuppressionKind.Element);
        }

        private float ResolvePopupDamageAmount(float preHp, float postHp, float remainingDamage)
        {
            float hpDelta = Mathf.Max(0f, preHp - postHp);
            if (hpDelta > 0f)
                return hpDelta;

            if (healthAttribute == null)
                return 0f;

            return Mathf.Max(0f, Mathf.Min(remainingDamage, preHp - healthAttribute.minValue));
        }

        private void LogElementPopup(string message, GameplayTag popupElementTag, GameObject target)
        {
            if (!logElementDamagePopup)
                return;

            string elementName = popupElementTag != null ? popupElementTag.Path : "none";
            string targetName = target != null ? target.name : "null";
            Debug.Log($"[GE_Damage_Spec] {name}: target={targetName}, element={elementName}, {message}", this);
        }

        private void LogDamageSpecEntry(GameplayEffectSpec spec, GameObject target)
        {
            if (!logElementDamagePopup)
                return;

            GameplayTag popupElementTag = spec != null ? spec.Context?.DamagePopupElementTag : null;
            bool likelyElementDamage = popupElementTag != null || name.Contains("Electric") || name.Contains("Bleed") || name.Contains("Blood");
            if (!likelyElementDamage)
                return;

            string targetName = target != null ? target.name : "null";
            string elementName = popupElementTag != null ? popupElementTag.Path : "none";
            string damageKeyName = damageKey != null ? damageKey.Path : "none";
            float resolvedDamage = fallbackDamage;
            bool hasSetByCaller = spec != null && damageKey != null && spec.TryGetSetByCallerMagnitude(damageKey, out resolvedDamage);

            Debug.Log(
                $"[GE_Damage_Spec] {name}: enter target={targetName}, element={elementName}, damageKey={damageKeyName}, damage={resolvedDamage:0.###}, hasSetByCaller={hasSetByCaller}",
                this);
        }

        /// <summary>
        /// 책임:
        /// - 일반 피해가 보호막과 HP 중 어느 단계에서 소비됐는지 선택적으로 출력한다.
        /// - 구덩이/장판처럼 AbilitySpec 밖에서 들어오는 환경 피해 디버깅을 보조한다.
        /// </summary>
        private void LogDamageApplication(string message, GameObject target)
        {
            if (!logDamageApplication)
                return;

            Debug.Log($"[GE_Damage_Spec] {name}: {message}", target);
        }

        private static Vector3 ResolvePopupPosition(GameplayEffectSpec spec, GameObject target)
        {
            if (spec != null && spec.Context != null)
            {
                if (spec.Context.HasWorldPosition)
                    return spec.Context.WorldPosition;

                if (spec.Context.Hit2D.HasValue)
                    return spec.Context.Hit2D.Value.point;

                if (spec.Context.Hit3D.HasValue)
                    return spec.Context.Hit3D.Value.point;
            }

            return target != null ? target.transform.position : Vector3.zero;
        }
    }
}
