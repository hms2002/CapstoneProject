using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 고블린 전사의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - ASC는 이 ScriptableObject만 알고, 실제 패턴 생명주기는 Runner가 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_GoblinWarriorCharge", menuName = "GAS/Ability Logic/Common Monsters/Goblin Warrior Charge")]
public sealed class AbilityLogic_GoblinWarriorCharge : AbilityLogic
{
    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 0f;

    [Header("Charge")]
    [SerializeField, Min(0.01f)] private float warningSeconds = 2f;
    [SerializeField, Min(0.1f)] private float dashDistance = 1.875f;
    [SerializeField, Min(0.01f)] private float dashSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float warningWidth = 0.9f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.2f;

    [Header("Collision")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask dashObstacleLayers;
    [SerializeField, Min(0.01f)] private float dashCastRadius = 0.28f;
    [SerializeField, Min(0f)] private float dashWallSkinWidth = 0.03f;

    public GE_Damage_Spec DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public float DamageAmount => damageAmount;
    public float KnockbackImpulse => knockbackImpulse;
    public float WarningSeconds => warningSeconds;
    public float DashDistance => dashDistance;
    public float DashSeconds => dashSeconds;
    public float WarningWidth => warningWidth;
    public float RecoverSeconds => recoverSeconds;
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public LayerMask DashObstacleLayers => dashObstacleLayers.value != 0 ? dashObstacleLayers : LayerMask.GetMask("Wall");
    public float DashCastRadius => dashCastRadius;
    public float DashWallSkinWidth => dashWallSkinWidth;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        GoblinWarriorChargeRunner runner = system.GetComponent<GoblinWarriorChargeRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<GoblinWarriorChargeRunner>()?.Cancel();
    }
}
