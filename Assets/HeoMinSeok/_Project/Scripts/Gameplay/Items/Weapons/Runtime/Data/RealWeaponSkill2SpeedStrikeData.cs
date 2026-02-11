using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "RW_Skill2_SpeedStrike_Data", menuName = "GAS/Weapon/RealWeapon/Skill2 SpeedStrike Data")]
    public sealed class RealWeaponSkill2SpeedStrikeData : ScriptableObject
    {
        [Header("Hitbox")]
        public Vector2 hitboxSize = new Vector2(1.6f, 0.9f);
        public float forwardOffset = 1.0f;
        public LayerMask hitLayers;

        [Header("Damage")]
        public GameplayEffect damageEffect; // 권장: GE_Damage_Spec

        [Header("Stat Query (Recommended)")]
        [Tooltip("공격력(최종) StatId (권장: AttackFinal)")]
        public StatId attackStatId = StatId.AttackFinal;

        [Tooltip("이동속도 배수(최종) StatId (권장: MoveSpeedMultiplierFinal, x1 기반)")]
        public StatId moveSpeedMultiplierStatId = StatId.MoveSpeedFinal;

        [Tooltip("피해 스케일. baseHp = ATK * (MoveSpeedMult * scale)")]
        public float speedScale = 3f;

        [Tooltip("선택: 넉백 공식")]
        public ScaledStatFormula knockbackFormula;

        [Header("Legacy (optional - can be removed)")]
        [Tooltip("공격력(ATK) Attribute (구 방식)")]
        public AttributeDefinition attackAttribute;

        [Tooltip("이동속도 배수 Attribute (구 방식)")]
        public AttributeDefinition moveSpeedMultiplierAttribute;

        [Header("Optional")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;
    }
}
