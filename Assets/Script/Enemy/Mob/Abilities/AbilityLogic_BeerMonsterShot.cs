using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 맥주 몬스터의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 탄막 프리팹, 피해량, 조준선, 회전 보정 같은 authoring 데이터를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_BeerMonsterShot", menuName = "GAS/Ability Logic/Beer Monster/Shot")]
public sealed class AbilityLogic_BeerMonsterShot : AbilityLogic
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float attackRange = 7f;
    [SerializeField, Min(0.01f)] private float warningSeconds = 1.7f;
    [SerializeField, Min(0.01f)] private float warningWidth = 0.12f;
    [SerializeField, Min(0.01f)] private float projectileSpeedMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 5f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.25f;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private LayerMask targetLayers;

    [Header("Projectile Rotation")]
    [SerializeField] private bool alignProjectileRotationToDirection = true;
    [SerializeField] private float projectileRotationOffsetDegrees;

    [Header("Debug")]
    [SerializeField] private bool logWallClipProbe;
    [SerializeField, Min(0.05f)] private float wallClipProbeLogInterval = 0.5f;

    public GameObject ProjectilePrefab => projectilePrefab;
    public GE_Damage_Spec DamageEffect => damageEffect;
    public float DamageAmount => damageAmount;
    public float AttackRange => attackRange;
    public float WarningSeconds => warningSeconds;
    public float WarningWidth => warningWidth;
    public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
    public float ProjectileLifetime => projectileLifetime;
    public float RecoverSeconds => recoverSeconds;
    public LayerMask WallLayers => wallLayers.value != 0 ? wallLayers : LayerMask.GetMask("Wall");
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public bool AlignProjectileRotationToDirection => alignProjectileRotationToDirection;
    public float ProjectileRotationOffsetDegrees => projectileRotationOffsetDegrees;
    public bool LogWallClipProbe => logWallClipProbe;
    public float WallClipProbeLogInterval => wallClipProbeLogInterval;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null)
            yield break;

        BeerMonsterShotRunner runner = system.GetComponent<BeerMonsterShotRunner>();
        if (runner == null)
            yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<BeerMonsterShotRunner>()?.Cancel();
    }
}
