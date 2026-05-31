using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 작은 슬라임 Pawn의 기본 스탯, 이동 속도 배율, 사망 처리를 정의한다.
/// - 접촉 피해와 orbit 이동 같은 실행 세부는 전용 컴포넌트에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
public class Pawn : Slime
{
    private const string DieTriggerName = "die";
    private const float MaxHealth = 2f;
    private const float VisualScale = 0.55f;
    private const float ChaseSpeedMultiplier = 2f;

    [SerializeField] private GE_Damage_Spec damageEffect;

    private bool hasDieTrigger;

    public GE_Damage_Spec ContactDamageEffect => damageEffect;
    public bool CanDealContactDamage => CanAct();

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();
        CacheAnimatorParameters();
        ApplyStats();
    }

    public override bool CanUseChaseMovement()
    {
        if (!base.CanUseChaseMovement()) return false;
        UpdateSpeed(ChaseSpeedMultiplier);
        return CanMove();
    }

    protected override void OnDeathStarted()
    {
        CancelAbility();
        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
        SetAnimatorTriggerIfAvailable(DieTriggerName, hasDieTrigger);
    }

    /// <summary>폰은 별도 공격 상태를 만들지 않고 충돌로 피해를 줍니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        return false;
    }

    /// <summary>폰의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Pawn", MaxHealth, VisualScale);
    }

    /// <summary>Animator Controller에 Pawn 전용 트리거가 있는지 캐시합니다.</summary>
    private void CacheAnimatorParameters()
    {
        hasDieTrigger = HasAnimatorParameter(DieTriggerName, AnimatorControllerParameterType.Trigger);
    }

    /// <summary>지정한 Animator 파라미터가 존재하고 타입이 맞는지 확인합니다.</summary>
    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    /// <summary>파라미터가 존재할 때만 Animator trigger를 전달해 authoring 중 콘솔 오류를 방지합니다.</summary>
    private void SetAnimatorTriggerIfAvailable(string triggerName, bool hasTrigger)
    {
        if (!hasTrigger || animator == null)
            return;

        animator.SetTrigger(triggerName);
    }
}
