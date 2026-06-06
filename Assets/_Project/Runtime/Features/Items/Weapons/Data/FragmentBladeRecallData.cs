using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 파편검 Skill1 회수 경로 피해의 정적 데이터를 authoring 한다.
    /// - 회수 가능성/조각 상태 전이는 runtime data가 맡고, 이 데이터는 피해 payload 생성에 필요한 값만 제공한다.
    /// </summary>
    [CreateAssetMenu(fileName = "FragmentBladeRecallData", menuName = "GAS/Weapon/Fragment Blade/Recall Data")]
    public sealed class FragmentBladeRecallData : ScriptableObject
    {
        [Header("Damage")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public ScaledStatFormula damageFormula;
        public ScaledStatFormula knockbackFormula;
        public float legacyDamage = 8f;
        public float legacyStaggerDamage;
        public LayerMask hitLayers;

        public DamagePayloadConfig DamageConfig => damageConfig;
    }
}
