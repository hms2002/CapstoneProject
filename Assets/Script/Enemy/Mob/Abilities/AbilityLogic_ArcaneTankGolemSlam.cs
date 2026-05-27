using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 마도 탱커 골렘의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 점프 착지와 후속 낙석 경고/피해 생명주기는 Runner가 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_ArcaneTankGolemSlam", menuName = "GAS/Ability Logic/Common Monsters/Arcane Tank Golem Slam")]
public sealed class AbilityLogic_ArcaneTankGolemSlam : AbilityLogic
{
    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 0f;

    [Header("Slam")]
    [SerializeField, Min(0f)] private float attackRange = 6f;
    [SerializeField, Min(0.01f)] private float landingWarningSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float jumpSeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float landingDiameter = 2.8f;
    [SerializeField, Min(0f)] private float jumpVisualHeight = 1.6f;
    [SerializeField, Min(0f)] private float bodyZHeight = 1f;
    [SerializeField, Min(0.1f)] private float rockOffsetDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float rockDiameter = 1.3f;
    [SerializeField, Min(0.01f)] private float rockWarningSeconds = 1.1f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.45f;
    [SerializeField] private LayerMask targetLayers;

    [Header("Landing Impact Effect")]
    [SerializeField, Min(0f)] private float landingImpactDelay = 0.05f;
    [SerializeField] private GameObject landingImpactEffectPrefab;
    [SerializeField] private Vector3 landingImpactEffectOffset;
    [SerializeField, Min(0.01f)] private float landingImpactEffectScale = 1f;
    [SerializeField, Min(0.01f)] private float landingImpactEffectFallbackLifetime = 0.8f;

    [Header("Rock Fall Visual")]
    [SerializeField] private GameObject rockFallVisualPrefab;
    [SerializeField] private Vector3 rockFallVisualOffset;
    [SerializeField, Min(0f)] private float rockFallSpawnHeight = 3f;
    [SerializeField, Min(0.01f)] private float rockFallSeconds = 0.35f;

    public GE_Damage_Spec DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public float DamageAmount => damageAmount;
    public float KnockbackImpulse => knockbackImpulse;
    public float AttackRange => attackRange;
    public float LandingWarningSeconds => landingWarningSeconds;
    public float JumpSeconds => jumpSeconds;
    public float LandingDiameter => landingDiameter;
    public float JumpVisualHeight => jumpVisualHeight;
    public float BodyZHeight => bodyZHeight;
    public float RockOffsetDistance => rockOffsetDistance;
    public float RockDiameter => rockDiameter;
    public float RockWarningSeconds => rockWarningSeconds;
    public float RecoverSeconds => recoverSeconds;
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public float LandingImpactDelay => landingImpactDelay;
    public GameObject LandingImpactEffectPrefab => landingImpactEffectPrefab;
    public Vector3 LandingImpactEffectOffset => landingImpactEffectOffset;
    public float LandingImpactEffectScale => landingImpactEffectScale;
    public float LandingImpactEffectFallbackLifetime => landingImpactEffectFallbackLifetime;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        ArcaneTankGolemSlamRunner runner = system.GetComponent<ArcaneTankGolemSlamRunner>();
        if (runner == null) yield break;

        yield return runner.Run(
            system,
            spec,
            initialTarget,
            landingImpactDelay,
            landingImpactEffectPrefab,
            landingImpactEffectOffset,
            landingImpactEffectScale,
            landingImpactEffectFallbackLifetime,
            rockFallVisualPrefab,
            rockFallVisualOffset,
            rockFallSpawnHeight,
            rockFallSeconds);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<ArcaneTankGolemSlamRunner>()?.Cancel();
    }
}
