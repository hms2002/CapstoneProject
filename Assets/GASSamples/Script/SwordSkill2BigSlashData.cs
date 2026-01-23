using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill2_BigSlashData", menuName = "GAS/Samples/Sword Skill2 BigSlash Data")]
    public class SwordSkill2BigSlashData : ScriptableObject
    {
        public Vector2 hitboxSize = new Vector2(4f, 4f);
        public float forwardOffset = 1.0f;
        public LayerMask hitLayers;

        public GameplayEffect damageEffect; // GE_Damage_Spec 권장
        public float damage = 50f;

        // 애니 이벤트 동기화(선택)
        public GameplayTag hitEventTag;
        public float hitEventTimeout = 0.4f;

        public float recoveryOverride = 0.2f;
    }
}
