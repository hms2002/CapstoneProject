using System.Collections;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Witch_ExtinguishCandle", menuName = "GAS/Ability Logic/Witch Boss/AL_Witch_ExtinguishCandle")]
    public class AbilityLogic_WitchExtinguishCandle : AbilityLogic
    {
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

        public WorldPresentationHook GetExplosionPresentation()
        {
            return new WorldPresentationHook
            {
                sound = explosionSound,
                cameraShake = explosionCameraShake,
                effect = new SpawnedPresentationHook
                {
                    prefab = explosionVisualPrefab,
                    localOffset = explosionVisualOffset,
                    rotationOffsetZ = 0f,
                    scaleMultiplier = explosionVisualScale,
                    lifetimeMode = PresentationLifetimeMode.AutoDetect,
                    lifetimeOverrideSeconds = 0f,
                    useUnscaledTime = false
                },
                particle = new SpawnedPresentationHook
                {
                    prefab = explosionParticlePrefab,
                    localOffset = explosionParticleOffset,
                    rotationOffsetZ = 0f,
                    scaleMultiplier = explosionParticleScale,
                    lifetimeMode = PresentationLifetimeMode.AutoDetect,
                    lifetimeOverrideSeconds = 0f,
                    useUnscaledTime = false
                }
            };
        }

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
