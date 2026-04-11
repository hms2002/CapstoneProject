using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 3연속 콤보 근접 공격의 정적 데이터를 보관한다.
    /// - 콤보별 피해/넉백/스태거/활성시간/이동/히트박스 규칙을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SwordCombo2DData", menuName = "GAS/Samples/Data/Sword Combo 2D")]
    public class SwordCombo2DData : ScriptableObject
    {
        private const float MinAttackSpeedScaledDuration = 0.02f;

        [System.Serializable]
        public class ElementDamageGroup
        {
            public List<ElementDamageInput> elements = new();
        }

        [System.Serializable]
        public readonly struct RuntimeSwordComboStepData
        {
            /// <summary>
            /// 책임 :
            /// - 검 콤보 한 타의 원본 설정에 공격속도 보정을 반영한 런타임 전용 값을 제공한다.
            /// - 원본 SO 데이터를 수정하지 않고 실행 시점의 최종 타이밍만 안전하게 사용하게 만든다.
            /// </summary>
            public readonly string animationTrigger;
            public readonly float activeTime;
            public readonly float recoveryDuration;
            public readonly float nextAttackDelay;
            public readonly ScaledStatFormula damageFormula;
            public readonly ScaledStatFormula knockbackFormula;
            public readonly float legacyDamage;
            public readonly float legacyStaggerDamage;
            public readonly ElementDamageGroup elementDamages;
            public readonly Vector2 hitboxSize;
            public readonly float forwardOffset;
            public readonly float sideOffset;
            public readonly int sideSign;
            public readonly float lungeDistance;
            public readonly float lungeDuration;

            public RuntimeSwordComboStepData(
                string animationTrigger,
                float activeTime,
                float recoveryDuration,
                float nextAttackDelay,
                ScaledStatFormula damageFormula,
                ScaledStatFormula knockbackFormula,
                float legacyDamage,
                float legacyStaggerDamage,
                ElementDamageGroup elementDamages,
                Vector2 hitboxSize,
                float forwardOffset,
                float sideOffset,
                int sideSign,
                float lungeDistance,
                float lungeDuration)
            {
                this.animationTrigger = animationTrigger;
                this.activeTime = activeTime;
                this.recoveryDuration = recoveryDuration;
                this.nextAttackDelay = nextAttackDelay;
                this.damageFormula = damageFormula;
                this.knockbackFormula = knockbackFormula;
                this.legacyDamage = legacyDamage;
                this.legacyStaggerDamage = legacyStaggerDamage;
                this.elementDamages = elementDamages;
                this.hitboxSize = hitboxSize;
                this.forwardOffset = forwardOffset;
                this.sideOffset = sideOffset;
                this.sideSign = sideSign;
                this.lungeDistance = lungeDistance;
                this.lungeDuration = lungeDuration;
            }
        }

        [System.Serializable]
        public struct SwordComboStepData : IAttackSpeedScaledStep<RuntimeSwordComboStepData>
        {
            /// <summary>
            /// 책임 :
            /// - 검 콤보 한 타의 전투/타이밍/이동/히트박스 데이터를 한 덩어리로 보관한다.
            /// - 병렬 배열 인덱스 동기화 대신 "한 타의 설정"을 한 곳에서 읽게 만든다.
            /// </summary>
            public string animationTrigger;
            public float activeTime;
            public float recoveryDuration;
            public float nextAttackDelay;
            public ScaledStatFormula damageFormula;
            public ScaledStatFormula knockbackFormula;
            public float legacyDamage;
            public float legacyStaggerDamage;
            public ElementDamageGroup elementDamages;
            public Vector2 hitboxSize;
            public float forwardOffset;
            public float sideOffset;
            public int sideSign;
            public float lungeDistance;
            public float lungeDuration;

            public float ResolveNextAttackDelay()
            {
                return nextAttackDelay > 0f ? nextAttackDelay : Mathf.Max(0f, recoveryDuration);
            }

            public RuntimeSwordComboStepData CreateAttackSpeedScaled(float finalAttackSpeed)
            {
                float safeAttackSpeed = finalAttackSpeed > 0.0001f ? finalAttackSpeed : 1f;

                float scaledRecovery = Mathf.Max(MinAttackSpeedScaledDuration, recoveryDuration / safeAttackSpeed);
                float scaledNextAttackDelay = Mathf.Max(MinAttackSpeedScaledDuration, ResolveNextAttackDelay() / safeAttackSpeed);
                float scaledLungeDuration = Mathf.Max(MinAttackSpeedScaledDuration, lungeDuration / safeAttackSpeed);

                return new RuntimeSwordComboStepData(
                    animationTrigger,
                    activeTime,
                    scaledRecovery,
                    scaledNextAttackDelay,
                    damageFormula,
                    knockbackFormula,
                    legacyDamage,
                    legacyStaggerDamage,
                    elementDamages,
                    hitboxSize,
                    forwardOffset,
                    sideOffset,
                    sideSign,
                    lungeDistance,
                    scaledLungeDuration);
            }
        }

        [Header("Actor")]
        public MeleeHitboxActor hitboxPrefab;

        [Header("Combo Steps")]
        public SwordComboStepData[] steps = new SwordComboStepData[3];

        [Header("Damage Channels")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;

        [Header("Combo")]
        public float comboResetTime = 0.45f;

        [Header("Hit Timing (Animation Event)")]
        public GameplayTag hitEventTag;
        public GameplayTag hitConfirmedTag;
        public float hitEventTimeout = 0.35f;
        public LayerMask hitLayers;

        [Header("Damage Effect")]
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;

        /// <summary>
        /// 책임 :
        /// - 현재 콤보 데이터에서 유효한 step 수를 반환한다.
        /// - 신규 step 데이터가 비어 있으면 legacy 배열 길이를 기반으로 안전한 기본 길이를 제공한다.
        /// </summary>
        public int GetStepCount()
        {
            return steps != null && steps.Length > 0 ? steps.Length : 1;
        }

        /// <summary>
        /// 책임 :
        /// - 지정한 콤보 단계의 데이터를 신규 step 구조로 반환한다.
        /// - step 데이터가 비어 있는 자리는 legacy 배열 값으로 안전하게 보간한다.
        /// </summary>
        public SwordComboStepData GetStep(int comboIndex)
        {
            comboIndex = Mathf.Clamp(comboIndex, 0, GetStepCount() - 1);
            return steps[Mathf.Clamp(comboIndex, 0, steps.Length - 1)];
        }

        /// <summary>
        /// 책임 :
        /// - 지정한 콤보 단계의 원본 데이터를 읽고 공격속도 보정이 적용된 런타임 step 값을 반환한다.
        /// - 원본 SO 데이터는 그대로 유지하고 실행 시점에만 타이밍 스케일을 반영한다.
        /// </summary>
        public RuntimeSwordComboStepData GetRuntimeStep(int comboIndex, float finalAttackSpeed)
        {
            return GetStep(comboIndex).CreateAttackSpeedScaled(finalAttackSpeed);
        }
    }
}
