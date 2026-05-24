using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - "진짜 무기" 일반 공격(1타)의 정적 데이터를 보관한다.
    /// - 근접 히트박스 actor 프리팹, 히트박스 크기/오프셋, 피해 공식과 payload 설정을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RW_Attack1_Data", menuName = "GAS/Weapon/RealWeapon/Attack1 Data")]
    public sealed class RealWeaponAttack1Data : ScriptableObject
    {
        [Header("Actor")]
        public MeleeHitboxActor hitboxPrefab;
        [Min(0.01f)] public float activeTime = 0.08f;

        [Header("Hitbox")]
        public Vector2 hitboxSize = new Vector2(1.4f, 0.8f);
        public float forwardOffset = 0.9f;
        public LayerMask hitLayers;

        [Header("Hit Impact")]
        public HitImpactCueKind hitImpactCueKind;

        [Header("Damage")]
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public ScaledStatFormula damageFormula;
        public ScaledStatFormula knockbackFormula;

        [Header("Optional")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;
    }
}
