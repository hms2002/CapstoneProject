using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 투척/파기 스킬의 투사체, 피해, 회전, 파괴 피드백 authoring 값을 보관한다.
    /// - 현재 슬롯 제거 정책은 ability logic이 처리하고, 이 데이터는 월드 공격체 설정에 집중한다.
    /// </summary>
    [CreateAssetMenu(fileName = "OddIronThrowData", menuName = "GAS/Weapon/Odd Iron/Throw Data")]
    public sealed class OddIronThrowData : ScriptableObject
    {
        [Header("Projectile")]
        public GameObject projectilePrefab;
        public float throwSpeed = 15f;
        public float lifetime = 1.2f;
        public float angularSpeedDegrees = 720f;
        public Vector3 spawnOffset = new(0.8f, 0.15f, 0f);
        public Vector3 projectileScale = Vector3.one;
        public LayerMask wallLayers;
        public LayerMask damageLayers;

        [Header("Fixed Damage")]
        public DamagePayloadConfig damageConfig = new();
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public float fixedDamage = 60f;
        public float fixedStaggerDamage = 0f;
        public float fixedKnockbackImpulse = 8f;

        [Header("Feedback")]
        public GameObject impactVfxPrefab;
        public SoundRef throwSound;
        public SoundRef impactSound;
    }
}
