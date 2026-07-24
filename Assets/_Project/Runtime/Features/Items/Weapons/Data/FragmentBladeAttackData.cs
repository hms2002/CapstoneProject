using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 파편검 기본 공격의 히트박스, 피해, 조각 수 기반 약화 규칙을 authoring 한다.
    /// - 조각 수가 0개여도 최소 공격은 가능하되 범위/피해가 줄어드는 정책을 데이터로 제공한다.
    /// </summary>
    [CreateAssetMenu(fileName = "FragmentBladeAttackData", menuName = "GAS/Weapon/Fragment Blade/Attack Data")]
    public sealed class FragmentBladeAttackData : ScriptableObject
    {
        [Header("Hitbox")]
        public MeleeHitboxActor hitboxPrefab;
        public Vector2 fullHitboxSize = new(1.6f, 1.0f);
        [Range(0.05f, 1f)] public float minimumHitboxScale = 0.35f;
        public float forwardOffset = 0.9f;
        public float activeTime = 0.08f;
        public LayerMask hitLayers;
        public GameplayTag hitConfirmedTag;

        [Header("Damage")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public ScaledStatFormula damageFormula;
        public ScaledStatFormula knockbackFormula;
        public float legacyDamage = 10f;
        public float legacyStaggerDamage;
        [Range(0.05f, 1f)] public float minimumDamageScale = 0.35f;

        [Header("Skill2 Piercing Follow-up")]
        [Range(0.05f, 2f)] public float piercingDamageScale = 0.45f;
        [Min(0.01f)] public float piercingDurationSeconds = 0.22f;
        [Min(0f)] public float piercingOvershootDistance = 1.25f;

        public DamagePayloadConfig DamageConfig => damageConfig;
    }
}
