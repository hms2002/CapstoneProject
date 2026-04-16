using System.Collections;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public class AbilityLogic_WitchLightAllCandles : AbilityLogic
{
    // 이 클래스의 책임:
    // 마녀 보스의 50% 패턴(촛불을 켜라) 실행과 전용 튜닝 데이터를 제공한다.

    [Header("Phase Transition Timing")]
    [SerializeField] private float moveToCenterDuration = 0.45f;
    [SerializeField] private float relightDeadlineSeconds = 8f;
    [SerializeField] private float shieldBreakWaitGraceSeconds = 1.5f;

    [Header("Phase Transition Data")]
    [SerializeField] private GameplayEffect groggyStatusEffect;
    [SerializeField] private AttackTelegraphStyle mapWideWarningStyleAsset;
    [SerializeField] private GE_Damage_Spec mapWideDamageEffect;
    [SerializeField] private float mapWideDamageAmount = 1f;

    [Header("Charge Orb")]
    [SerializeField] private GameObject chargeOrbPrefab;
    [SerializeField] private Vector3 chargeOrbLocalOffset = new Vector3(0f, 1.35f, -0.05f);
    [SerializeField] private Vector3 chargeOrbStartScale = new Vector3(0.25f, 0.25f, 1f);
    [SerializeField] private Vector3 chargeOrbEndScale = new Vector3(1.35f, 1.35f, 1f);
    [SerializeField] private bool followWitchDuringCharge = true;
    [SerializeField] [Min(0.01f)] private float chargeOrbFailureDropDuration = 0.22f;
    [SerializeField] private Vector3 chargeOrbImpactLocalOffset = new Vector3(0f, 0.1f, -0.05f);

    [Header("Charge Presentation")]
    [SerializeField] private SoundRef chargeLoopSound;
    [SerializeField] [Min(0f)] private float chargeLoopFadeOutSeconds = 0.1f;
    [SerializeField] private WorldPresentationHook chargePulsePresentation;
    [SerializeField] private WorldPresentationHook orbLaunchPresentation;
    [HideInInspector, FormerlySerializedAs("orbLaunchSound")]
    [SerializeField] private SoundRef legacyOrbLaunchSound;
    [HideInInspector, FormerlySerializedAs("chargeLoopCameraShake")]
    [SerializeField] private CameraShakeHook legacyChargeLoopCameraShake = CameraShakeHook.Create(0.03f, 1f, 0.08f, 0.08f);

    [Header("Failure Presentation")]
    [SerializeField] private WorldPresentationHook failureImpactPresentation;
    [HideInInspector, FormerlySerializedAs("orbImpactSound")]
    [SerializeField] private SoundRef legacyOrbImpactSound;
    [HideInInspector, FormerlySerializedAs("failureEffectPrefab")]
    [SerializeField] private GameObject legacyFailureEffectPrefab;
    [HideInInspector, FormerlySerializedAs("failureEffectLocalOffset")]
    [SerializeField] private Vector3 legacyFailureEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [HideInInspector, FormerlySerializedAs("failureEffectLifetimeSeconds")]
    [SerializeField] private float legacyFailureEffectLifetimeSeconds = 0.35f;
    [HideInInspector, FormerlySerializedAs("failureEffectScaleMultiplier")]
    [SerializeField] private Vector3 legacyFailureEffectScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("failureEffectRotationOffsetZ")]
    [SerializeField] private float legacyFailureEffectRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("failureParticlePrefab")]
    [SerializeField] private GameObject legacyFailureParticlePrefab;
    [HideInInspector, FormerlySerializedAs("failureParticleLocalOffset")]
    [SerializeField] private Vector3 legacyFailureParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [HideInInspector, FormerlySerializedAs("failureParticleLifetimeOverrideSeconds")]
    [SerializeField] private float legacyFailureParticleLifetimeOverrideSeconds;
    [HideInInspector, FormerlySerializedAs("useUnscaledFailureParticleTime")]
    [SerializeField] private bool legacyUseUnscaledFailureParticleTime;
    [HideInInspector, FormerlySerializedAs("failureParticleScaleMultiplier")]
    [SerializeField] private Vector3 legacyFailureParticleScaleMultiplier = Vector3.one;
    [HideInInspector, FormerlySerializedAs("failureParticleRotationOffsetZ")]
    [SerializeField] private float legacyFailureParticleRotationOffsetZ;
    [HideInInspector, FormerlySerializedAs("failureCameraShake")]
    [SerializeField] private CameraShakeHook legacyFailureCameraShake = CameraShakeHook.Create(0.24f, 1f, 0.36f, 0.05f);

    public float MoveToCenterDuration => Mathf.Max(0f, moveToCenterDuration);
    public float RelightDeadlineSeconds => Mathf.Max(0f, relightDeadlineSeconds);
    public float ShieldBreakWaitGraceSeconds => Mathf.Max(0f, shieldBreakWaitGraceSeconds);
    public GameplayEffect GroggyStatusEffect => groggyStatusEffect;
    public AttackTelegraphStyle MapWideWarningStyleAsset => mapWideWarningStyleAsset;
    public GE_Damage_Spec MapWideDamageEffect => mapWideDamageEffect;
    public float MapWideDamageAmount => mapWideDamageAmount;
    public GameObject ChargeOrbPrefab => chargeOrbPrefab;
    public Vector3 ChargeOrbLocalOffset => chargeOrbLocalOffset;
    public Vector3 ChargeOrbStartScale => chargeOrbStartScale;
    public Vector3 ChargeOrbEndScale => chargeOrbEndScale;
    public bool FollowWitchDuringCharge => followWitchDuringCharge;
    public float ChargeOrbFailureDropDuration => Mathf.Max(0.01f, chargeOrbFailureDropDuration);
    public Vector3 ChargeOrbImpactLocalOffset => chargeOrbImpactLocalOffset;
    public SoundRef ChargeLoopSound => chargeLoopSound;
    public float ChargeLoopFadeOutSeconds => Mathf.Max(0f, chargeLoopFadeOutSeconds);
    public WorldPresentationHook ChargePulsePresentation => chargePulsePresentation;
    public WorldPresentationHook OrbLaunchPresentation => orbLaunchPresentation;
    public WorldPresentationHook FailureImpactPresentation => failureImpactPresentation;

    private void OnValidate()
    {
        MigrateLegacyChargePresentations();
        MigrateLegacyFailurePresentation();
    }

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        if (witch == null || witch.LightAllCandlesPatternExecutor == null)
            yield break;

        MigrateLegacyChargePresentations();
        MigrateLegacyFailurePresentation();
        yield return witch.LightAllCandlesPatternExecutor.RunPattern(this, initialTarget);
    }

    public void StopChargeLoopFor(Witch witch)
    {
        witch?.LightAllCandlesPatternExecutor?.StopChargeLoopFor(this);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        base.CleanupForSceneTransition(system, spec, target);

        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        witch?.LightAllCandlesPatternExecutor?.CleanupForSceneTransition(this);
    }

    private void MigrateLegacyFailurePresentation()
    {
        if (legacyFailureEffectPrefab != null && !failureImpactPresentation.effect.HasContent)
        {
            failureImpactPresentation.effect.prefab = legacyFailureEffectPrefab;
            failureImpactPresentation.effect.localOffset = legacyFailureEffectLocalOffset;
            failureImpactPresentation.effect.rotationOffsetZ = legacyFailureEffectRotationOffsetZ;
            failureImpactPresentation.effect.scaleMultiplier = legacyFailureEffectScaleMultiplier;
            failureImpactPresentation.effect.lifetimeOverrideSeconds = legacyFailureEffectLifetimeSeconds;
        }

        if (legacyFailureParticlePrefab != null && !failureImpactPresentation.particle.HasContent)
        {
            failureImpactPresentation.particle.prefab = legacyFailureParticlePrefab;
            failureImpactPresentation.particle.localOffset = legacyFailureParticleLocalOffset;
            failureImpactPresentation.particle.rotationOffsetZ = legacyFailureParticleRotationOffsetZ;
            failureImpactPresentation.particle.scaleMultiplier = legacyFailureParticleScaleMultiplier;
            failureImpactPresentation.particle.lifetimeOverrideSeconds = legacyFailureParticleLifetimeOverrideSeconds;
            failureImpactPresentation.particle.useUnscaledTime = legacyUseUnscaledFailureParticleTime;
        }

        if (!failureImpactPresentation.HasSound && legacyOrbImpactSound.IsSet)
            failureImpactPresentation.sound = legacyOrbImpactSound;

        if (!failureImpactPresentation.HasShake && legacyFailureCameraShake.amplitude > 0f)
            failureImpactPresentation.cameraShake = legacyFailureCameraShake;
    }

    private void MigrateLegacyChargePresentations()
    {
        if (!orbLaunchPresentation.HasSound && legacyOrbLaunchSound.IsSet)
            orbLaunchPresentation.sound = legacyOrbLaunchSound;

        if (!chargePulsePresentation.HasShake && legacyChargeLoopCameraShake.amplitude > 0f)
            chargePulsePresentation.cameraShake = legacyChargeLoopCameraShake;
    }

}
