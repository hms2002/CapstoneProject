using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 고블린 탱커의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 원형 경고와 피해 판정 생명주기는 Runner가 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_GoblinTankSlam", menuName = "GAS/Ability Logic/Common Monsters/Goblin Tank Slam")]
public sealed class AbilityLogic_GoblinTankSlam : AbilityLogic
{
    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 0f;

    [Header("Slam")]
    [SerializeField, Min(0f)] private float attackRange = 2.2f;
    [SerializeField, Min(0.01f)] private float warningSeconds = 2f;
    [SerializeField, Min(0f)] private float postWarningImpactDelay = 0.05f;
    [SerializeField, Min(0.1f)] private float impactDiameter = 2.4f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.35f;
    [SerializeField] private LayerMask targetLayers;

    [Header("Impact VFX")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private Vector3 impactEffectOffset;
    [SerializeField, Min(0.01f)] private float impactEffectScale = 1f;
    [SerializeField, Min(0.01f)] private float impactEffectReferenceDiameter = 2.4f;
    [SerializeField, Min(0.01f)] private float impactEffectFallbackLifetime = 1.5f;

    public GE_Damage_Spec DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public float DamageAmount => damageAmount;
    public float KnockbackImpulse => knockbackImpulse;
    public float AttackRange => attackRange;
    public float WarningSeconds => warningSeconds;
    public float PostWarningImpactDelay => postWarningImpactDelay;
    public float ImpactDiameter => impactDiameter;
    public float RecoverSeconds => recoverSeconds;
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public GameObject ImpactEffectPrefab => impactEffectPrefab;
    public Vector3 ImpactEffectOffset => impactEffectOffset;
    public float ImpactEffectScale => impactEffectScale;
    public float ImpactEffectReferenceDiameter => impactEffectReferenceDiameter;
    public float ImpactEffectFallbackLifetime => impactEffectFallbackLifetime;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        GoblinTankSlamRunner runner = system.GetComponent<GoblinTankSlamRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<GoblinTankSlamRunner>()?.Cancel();
    }
}
