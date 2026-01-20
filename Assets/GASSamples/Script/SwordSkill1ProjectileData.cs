using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill1_ProjectileData", menuName = "GAS/Samples/Sword Skill1 Projectile Data")]
    public class SwordSkill1ProjectileData : ScriptableObject
    {
        public GameObject projectilePrefab;
        public float projectileSpeed = 12f;
        public float lifetime = 2.5f;

        public LayerMask wallLayers;
        public LayerMask damageLayers;

        public GameplayEffect damageEffect; // GE_Damage_Spec 권장
        public float damage = 20f;

        public Vector3 spawnOffset = new Vector3(0.8f, 0.2f, 0f);
    }
}
