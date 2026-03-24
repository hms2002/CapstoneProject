using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - SpeedStrike 근접 공격의 정적 데이터를 보관한다.
    /// - 히트박스 actor 프리팹, 범위, 유지시간, 피해 계산 규칙을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RW_Skill2_SpeedStrike_Data", menuName = "GAS/Weapon/RealWeapon/Skill2 SpeedStrike Data")]
    public sealed class RealWeaponSkill2SpeedStrikeData : ScriptableObject
    {
        [Header("Actor")]
        public MeleeHitboxActor hitboxPrefab;
        [Min(0.01f)] public float activeTime = 0.08f;

        [Header("Hitbox")]
        public Vector2 hitboxSize = new Vector2(1.6f, 0.9f);
        public float forwardOffset = 1.0f;
        public LayerMask hitLayers;

        [Header("Damage")]
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;

        [Header("Stat Query (Recommended)")]
        [Tooltip("공격력(최종) StatId (권장: AttackFinal)")]
        public StatId attackStatId = StatId.AttackFinal;

        [Tooltip("이동속도 배수(최종) StatId (권장: MoveSpeedFinal, x1 기반)")]
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