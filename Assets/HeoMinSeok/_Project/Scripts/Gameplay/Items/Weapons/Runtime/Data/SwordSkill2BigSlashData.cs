using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill2_BigSlashData", menuName = "GAS/Samples/Sword Skill2 BigSlash Data")]
    public class SwordSkill2BigSlashData : ScriptableObject
    {
        [Header("Damage Formula")]
        public DamageFormulaStats formulaStats;
        public bool includeElementDamage = false;
        public bool includeStaggerDamage = false;
        public float baseStaggerDamage = 0f;

        public Vector2 hitboxSize = new Vector2(4f, 4f);
        public float forwardOffset = 1.0f;
        public LayerMask hitLayers;

        public GameplayEffect damageEffect; // GE_Damage_Spec (HP)
        public float damage = 50f;          // 무기 스킬 기본 피해량

        public GameplayTag hitEventTag;
        public float hitEventTimeout = 0.4f;

        public float recoveryOverride = 0.2f;
    }
}
