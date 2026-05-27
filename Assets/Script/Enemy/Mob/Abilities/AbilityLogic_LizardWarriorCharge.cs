using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 리자드맨 전사의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 2연타 돌진 시퀀스의 cleanup은 Runner에 맡긴다.
/// </summary>
[CreateAssetMenu(fileName = "AL_LizardWarriorCharge", menuName = "GAS/Ability Logic/Common Monsters/Lizard Warrior Charge")]
public sealed class AbilityLogic_LizardWarriorCharge : AbilityLogic
{
    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 0f;
    [SerializeField, Min(0f)] private float recoverSeconds = 0.25f;

    [Header("Charge Steps")]
    [SerializeField] private LizardWarrior.ChargeStep firstStep = new() { warningSeconds = 2f, dashDistance = 1.875f, dashSeconds = 0.34f, warningWidth = 0.95f };
    [SerializeField] private LizardWarrior.ChargeStep secondStep = new() { warningSeconds = 1.4f, dashDistance = 1.875f, dashSeconds = 0.34f, warningWidth = 0.95f };

    [Header("Collision")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask dashObstacleLayers;
    [SerializeField, Min(0.01f)] private float dashCastRadius = 0.3f;
    [SerializeField, Min(0f)] private float dashWallSkinWidth = 0.03f;

    public GE_Damage_Spec DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public float DamageAmount => damageAmount;
    public float KnockbackImpulse => knockbackImpulse;
    public float RecoverSeconds => recoverSeconds;
    public LizardWarrior.ChargeStep FirstStep => firstStep;
    public LizardWarrior.ChargeStep SecondStep => secondStep;
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");
    public LayerMask DashObstacleLayers => dashObstacleLayers.value != 0 ? dashObstacleLayers : LayerMask.GetMask("Wall");
    public float DashCastRadius => dashCastRadius;
    public float DashWallSkinWidth => dashWallSkinWidth;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        LizardWarriorChargeRunner runner = system.GetComponent<LizardWarriorChargeRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        system?.GetComponent<LizardWarriorChargeRunner>()?.Cancel();
    }
}
