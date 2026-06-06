using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - BigSlash 근접 공격의 정적 데이터를 보관한다.
    /// - 히트박스 actor 프리팹, 공격 범위, 이벤트 대기, 피해 계산 규칙을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SwordSkill2_BigSlashData", menuName = "GAS/Samples/Sword Skill2 BigSlash Data")]
    public class SwordSkill2BigSlashData : ScriptableObject
    {
        [Header("Actor")]
        public MeleeHitboxActor hitboxPrefab;
        [Min(0.01f)] public float activeTime = 0.10f;

        [Header("Damage Channels")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;

        [Tooltip("Legacy per-hit element damages (FINAL values). Optional if you use DamageConfig.elementFormulas instead.")]
        public List<ElementDamageInput> elementDamages = new();

        [Tooltip("Legacy stagger damage (FINAL value). Optional if you use DamageConfig.staggerFormula instead.")]
        public float baseStaggerDamage = 0f;

        [Header("Hitbox")]
        public Vector2 hitboxSize = new Vector2(4f, 4f);
        public float forwardOffset = 1.0f;
        public LayerMask hitLayers;

        [Header("Damage Effect")]
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;

        [Header("Damage / Knockback Formula")]
        [Tooltip("If set, base HP damage is computed from attacker stats via this formula. If null, legacy 'damage' is used.")]
        public ScaledStatFormula damageFormula;

        [Tooltip("If set, knockback impulse is computed from attacker stats via this formula.")]
        public ScaledStatFormula knockbackFormula;

        [Header("Legacy Base Damage (Deprecated)")]
        public float damage = 50f;

        [Header("Timing")]
        public GameplayTag hitEventTag;
        public float hitEventTimeout = 0.4f;
        public float recoveryOverride = 0.2f;
    }
}