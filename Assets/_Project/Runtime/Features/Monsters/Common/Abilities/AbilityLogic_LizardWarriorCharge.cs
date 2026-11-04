using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 리자드맨 전사의 AbilityDefinition 실행을 몬스터 전용 Runner로 연결한다.
/// - 광폭 추격의 설정을 소유하고 임시 이동/연출 cleanup은 Runner에 맡긴다.
/// </summary>
[CreateAssetMenu(fileName = "AL_LizardWarriorCharge", menuName = "GAS/Ability Logic/Common Monsters/Lizard Warrior Charge")]
public sealed class AbilityLogic_LizardWarriorCharge : AbilityLogic
{
    [Header("Damage")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 0f;
    [SerializeField, Min(0f)] private float recoverSeconds = 1.1f;

    [Header("Frenzy Chase")]
    [SerializeField, Min(0.1f)] private float attackRange = 3.74f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 0.8f;
    [SerializeField, Min(0.1f)] private float chaseSeconds = 2.2f;
    [SerializeField, Min(0.1f)] private float chaseSpeedMultiplier = 1.4f;
    [SerializeField, Range(0f, 1f)] private float warningSpeedMultiplier = 0.75f;
    [SerializeField, Min(1f)] private float turnDegreesPerSecond = 270f;
    [SerializeField, Min(0.1f)] private float rearmSeconds = 1f;
    [SerializeField] private Color frenzyTint = new(1f, 0.28f, 0.22f, 1f);

    [Header("Collision")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0.1f)] private float contactDiameter = 0.95f;

    public GE_Damage_Spec DamageEffect => damageEffect;
    public GE_Knockback_Spec KnockbackEffect => knockbackEffect;
    public float DamageAmount => damageAmount;
    public float KnockbackImpulse => knockbackImpulse;
    public float RecoverSeconds => recoverSeconds;
    public float AttackRange => attackRange;
    public float WarningSeconds => warningSeconds;
    public float ChaseSeconds => chaseSeconds;
    public float ChaseSpeedMultiplier => chaseSpeedMultiplier;
    public float WarningSpeedMultiplier => warningSpeedMultiplier;
    public float TurnDegreesPerSecond => turnDegreesPerSecond;
    public float RearmSeconds => rearmSeconds;
    public Color FrenzyTint => frenzyTint;
    public float ContactDiameter => contactDiameter;
    public LayerMask TargetLayers => targetLayers.value != 0 ? targetLayers : LayerMask.GetMask("Player");

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
