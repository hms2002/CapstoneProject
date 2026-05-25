using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 리자드맨 마법사의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 경고 후 다중 발사 시퀀스의 cleanup은 Runner가 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_LizardMageBurst", menuName = "GAS/Ability Logic/Common Monsters/Lizard Mage Burst")]
public sealed class AbilityLogic_LizardMageBurst : AbilityLogic
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float attackRange = 7.5f;
    [SerializeField, Min(0.01f)] private float warningSeconds = 1.8f;
    [SerializeField, Min(0.01f)] private float warningWidth = 0.12f;
    [SerializeField, Min(0.01f)] private float projectileSpeedMultiplier = 1.3f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 5f;
    [SerializeField, Min(1)] private int shotCount = 3;
    [SerializeField, Min(0f)] private float shotInterval = 0.3f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.25f;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private LayerMask targetLayers;

    public GameObject ProjectilePrefab => projectilePrefab;
    public GE_Damage_Spec DamageEffect => damageEffect;
    public float DamageAmount => damageAmount;
    public float AttackRange => attackRange;
    public float WarningSeconds => warningSeconds;
    public float WarningWidth => warningWidth;
    public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
    public float ProjectileLifetime => projectileLifetime;
    public int ShotCount => shotCount;
    public float ShotInterval => shotInterval;
    public float RecoverSeconds => recoverSeconds;
    public LayerMask WallLayers => wallLayers.value != 0 ? wallLayers : LayerMask.GetMask("Wall");
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");

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
