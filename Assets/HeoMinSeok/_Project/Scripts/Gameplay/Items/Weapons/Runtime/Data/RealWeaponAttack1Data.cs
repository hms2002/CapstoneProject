using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// "진짜 무기" - 일반 공격(1타)용 데이터.
    /// - 피해량: ScaledStatFormula로 공격자 스탯 기반 계산
    /// - 넉백: ScaledStatFormula로 공격자 스탯 기반 계산
    /// - 타격 범위: OverlapBox
    /// </summary>
    [CreateAssetMenu(fileName = "RW_Attack1_Data", menuName = "GAS/Weapon/RealWeapon/Attack1 Data")]
    public sealed class RealWeaponAttack1Data : ScriptableObject
    {
        [Header("Hitbox")]
        public Vector2 hitboxSize = new Vector2(1.4f, 0.8f);
        public float forwardOffset = 0.9f;
        public LayerMask hitLayers;

        [Header("Damage")]
        public GameplayEffect damageEffect; // 권장: GE_Damage_Spec
        public ScaledStatFormula damageFormula; // 예: ATK * 1.0
        public ScaledStatFormula knockbackFormula; // 예: KnockbackPower * 1.0

        [Header("Optional")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;
    }
}
