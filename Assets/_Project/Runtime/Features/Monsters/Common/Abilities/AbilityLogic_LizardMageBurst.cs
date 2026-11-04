using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 리자드맨 마법사의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 이동 사격과 단발 제한 추적탄 설정을 소유하고 cleanup은 Runner가 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_LizardMageBurst", menuName = "GAS/Ability Logic/Common Monsters/Lizard Mage Burst")]
public sealed class AbilityLogic_LizardMageBurst : AbilityLogic
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float attackRange = 7.5f;
    [SerializeField, Min(0.01f)] private float warningSeconds = 1.2f;
    [SerializeField, Min(0.01f)] private float warningWidth = 0.12f;
    [SerializeField, Min(0.01f)] private float projectileSpeedMultiplier = 1.95f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 2.6f;
    [SerializeField, Min(0f)] private float homingSeconds = 0.8f;
    [SerializeField, Min(0f)] private float homingTurnDegreesPerSecond = 75f;
    [SerializeField, Range(1f, 89f)] private float homingHalfAngle = 60f;
    [SerializeField, Min(0.1f)] private float shotRestSeconds = 1.5f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.25f;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private LayerMask targetLayers;

    [Header("Moving Cast")]
    [SerializeField, Min(0.1f)] private float retreatStartDistance = 3f;
    [SerializeField, Min(0.1f)] private float retreatStopDistance = 4f;
    [SerializeField, Min(0.1f)] private float approachDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float retreatSpeedMultiplier = 0.5f;
    [SerializeField, Range(0f, 1f)] private float strafeSpeedMultiplier = 0.25f;

    public GameObject ProjectilePrefab => projectilePrefab;
    public GE_Damage_Spec DamageEffect => damageEffect;
    public float DamageAmount => damageAmount;
    public float AttackRange => attackRange;
    public float WarningSeconds => warningSeconds;
    public float WarningWidth => warningWidth;
    public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
    public float ProjectileLifetime => projectileLifetime;
    public float HomingSeconds => homingSeconds;
    public float HomingTurnDegreesPerSecond => homingTurnDegreesPerSecond;
    public float HomingHalfAngle => homingHalfAngle;
    public float ShotRestSeconds => shotRestSeconds;
    public float RecoverSeconds => recoverSeconds;
    public LayerMask WallLayers => wallLayers.value != 0 ? wallLayers : LayerMask.GetMask("Wall");
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public float RetreatStartDistance => retreatStartDistance;
    public float RetreatStopDistance => Mathf.Max(retreatStartDistance, retreatStopDistance);
    public float ApproachDistance => Mathf.Max(RetreatStopDistance, approachDistance);
    public float RetreatSpeedMultiplier => retreatSpeedMultiplier;
    public float StrafeSpeedMultiplier => strafeSpeedMultiplier;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        LizardMageBurstRunner runner = system.GetComponent<LizardMageBurstRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<LizardMageBurstRunner>()?.Cancel();
    }
}
