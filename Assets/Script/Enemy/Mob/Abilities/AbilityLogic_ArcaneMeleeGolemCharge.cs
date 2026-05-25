using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 마도 근접 골렘의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 빠른 2연타 돌진 시퀀스의 cleanup은 Runner에 위임한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_ArcaneMeleeGolemCharge", menuName = "GAS/Ability Logic/Common Monsters/Arcane Melee Golem Charge")]
public sealed class AbilityLogic_ArcaneMeleeGolemCharge : AbilityLogic
{
    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 0f;
    [SerializeField, Min(0f)] private float attackRange = 6.2f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.25f;

    [Header("Charge Steps")]
    [SerializeField] private ArcaneMeleeGolem.ChargeStep firstStep = new() { warningSeconds = 1.8f, dashDistance = 2.0625f, dashSeconds = 0.28f, warningWidth = 1f };
    [SerializeField] private ArcaneMeleeGolem.ChargeStep secondStep = new() { warningSeconds = 1f, dashDistance = 2.0625f, dashSeconds = 0.25f, warningWidth = 1f };

    [Header("Collision")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask dashObstacleLayers;
    [SerializeField, Min(0.01f)] private float dashCastRadius = 0.32f;
    [SerializeField, Min(0f)] private float dashWallSkinWidth = 0.03f;

    public GE_Damage_Spec DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public float DamageAmount => damageAmount;
    public float KnockbackImpulse => knockbackImpulse;
    public float AttackRange => attackRange;
    public float RecoverSeconds => recoverSeconds;
    public ArcaneMeleeGolem.ChargeStep FirstStep => firstStep;
    public ArcaneMeleeGolem.ChargeStep SecondStep => secondStep;
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public LayerMask DashObstacleLayers => dashObstacleLayers.value != 0 ? dashObstacleLayers : LayerMask.GetMask("Wall");
    public float DashCastRadius => dashCastRadius;
    public float DashWallSkinWidth => dashWallSkinWidth;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        ArcaneMeleeGolemChargeRunner runner = system.GetComponent<ArcaneMeleeGolemChargeRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<ArcaneMeleeGolemChargeRunner>()?.Cancel();
    }
}
