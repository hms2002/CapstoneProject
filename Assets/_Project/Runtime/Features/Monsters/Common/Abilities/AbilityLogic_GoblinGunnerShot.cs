using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 고블린 사수의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 경고/발사 cleanup은 Runner에 위임한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_GoblinGunnerShot", menuName = "GAS/Ability Logic/Common Monsters/Goblin Gunner Shot")]
public sealed class AbilityLogic_GoblinGunnerShot : AbilityLogic
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

    [Header("Muzzle Effect")]
    [SerializeField] private GameObject muzzleEffectPrefab;
    [SerializeField, Min(0.01f)] private float muzzleEffectScale = 1f;
    [SerializeField, Min(0.01f)] private float muzzleEffectFallbackLifetime = 0.6f;

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
    public GameObject MuzzleEffectPrefab => muzzleEffectPrefab;
    public float MuzzleEffectScale => muzzleEffectScale;
    public float MuzzleEffectFallbackLifetime => muzzleEffectFallbackLifetime;
    public bool LogWallClipProbe => logWallClipProbe;
    public float WallClipProbeLogInterval => wallClipProbeLogInterval;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        GoblinGunnerShotRunner runner = system.GetComponent<GoblinGunnerShotRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<GoblinGunnerShotRunner>()?.Cancel();
    }
}
