using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Witch_ExtinguishCandle", menuName = "GAS/Ability Logic/Witch Boss/AL_Witch_ExtinguishCandle")]
    public class AbilityLogic_WitchExtinguishCandle : AbilityLogic
    {
        // 이 클래스의 책임:
        // 마녀 보스의 촛불 끄기 패턴 진입점과 전용 튜닝 데이터를 제공한다.

        [Header("Explosion Presentation")]
        [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;
        [SerializeField] private GameObject explosionVisualPrefab;
        [SerializeField] private GameObject explosionParticlePrefab;
        [SerializeField] private Vector3 explosionVisualOffset;
        [SerializeField] private Vector3 explosionVisualScale = Vector3.one;
        [SerializeField] private Vector3 explosionParticleOffset;
        [SerializeField] private Vector3 explosionParticleScale = Vector3.one;
        [SerializeField] private SoundRef explosionSound;
        [SerializeField] private CameraShakeHook explosionCameraShake = CameraShakeHook.Create(0.18f, 1f, 0.28f, 0.04f);

        [Header("Fog / Attack")]
        [SerializeField] private GE_Damage_Spec damageEffect;
        [SerializeField] private float damageAmount = 1f;
        [SerializeField] private Vector3 fogSpawnScaleMultiplier = Vector3.one;
        [SerializeField] private float attackRadiusMultiplier = 6f;

        public AttackTelegraphStyle WarningTelegraphStyle => warningTelegraphStyle;
        public GameObject ExplosionVisualPrefab => explosionVisualPrefab;
        public GameObject ExplosionParticlePrefab => explosionParticlePrefab;
        public Vector3 ExplosionVisualOffset => explosionVisualOffset;
        public Vector3 ExplosionVisualScale => explosionVisualScale;
        public Vector3 ExplosionParticleOffset => explosionParticleOffset;
        public Vector3 ExplosionParticleScale => explosionParticleScale;
        public SoundRef ExplosionSound => explosionSound;
        public CameraShakeHook ExplosionCameraShake => explosionCameraShake;
        public GE_Damage_Spec DamageEffect => damageEffect;
        public float DamageAmount => damageAmount;
        public Vector3 FogSpawnScaleMultiplier => fogSpawnScaleMultiplier;
        public float AttackRadiusMultiplier => attackRadiusMultiplier;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
