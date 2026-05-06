using CapstoneAudio;
using UnityEngine;
using UnityGAS;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 단발/전탄 사격에 필요한 투사체, 고정 피해, 사운드, VFX authoring 값을 보관한다.
    /// - damageFormula 없이 base damage 값을 제공해 기존 피해 적용 경로를 유지하되 공격력 스케일링을 피하게 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "OddIronShotData", menuName = "GAS/Weapon/Odd Iron/Shot Data")]
    public sealed class OddIronShotData : ScriptableObject
    {
        [Header("Projectile")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 25f;
        public float lifetime = 1.5f;
        public Vector3 spawnOffset = new(0.8f, 0.15f, 0f);
        public Vector3 projectileScale = Vector3.one;
        public LayerMask wallLayers;
        public LayerMask damageLayers;

        [Header("Fixed Damage")]
        public DamagePayloadConfig damageConfig = new();
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public float fixedDamage = 100f;
        public float fixedStaggerDamage = 0f;
        public float fixedKnockbackImpulse = 8f;

        [Header("Feedback")]
        public GameObject muzzleFlashPrefab;
        public SoundRef fireSound;

        [Header("Barrage")]
        public float barrageInterval = 0.08f;
        public float barrageSpreadAngle = 15f;
    }
}
