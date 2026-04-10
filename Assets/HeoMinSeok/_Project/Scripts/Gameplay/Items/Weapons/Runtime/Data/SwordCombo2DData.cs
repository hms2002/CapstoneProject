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
        [System.Serializable]
        public class ElementDamageGroup
        {
            public List<ElementDamageInput> elements = new();
        }

        [System.Serializable]
        public struct SwordComboStepData
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
    }
}
