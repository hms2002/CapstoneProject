using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 일반 몬스터의 AbilitySystem과 패턴 runner 생명주기를 한 곳에서 조율한다.
/// - AI가 "지금 공격을 시작해도 되는가"를 ASC busy 상태와 runner 실행 상태를 합쳐 판단하게 돕는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AbilitySystem))]
public class MobAbilityCoordinator : MonoBehaviour, IMobAbilityBridge
{
    [SerializeField] private AbilitySystem abilitySystem;

    private IMobPatternRunner activeRunner;

    public bool IsAbilityExecutionBusy =>
        (abilitySystem != null && abilitySystem.IsBusy) ||
        (activeRunner != null && activeRunner.IsRunning);

    private void Awake()
    {
        if (abilitySystem == null)
            abilitySystem = GetComponent<AbilitySystem>();
    }

    public bool TryStartAbility(AbilityDefinition ability, GameObject explicitTarget = null)
    {
        if (abilitySystem == null || ability == null)
            return false;

        return abilitySystem.TryActivateAbility(ability, explicitTarget);
    }

    public void CancelActiveAbility(bool force)
    {
        activeRunner?.Cancel();

        if (abilitySystem == null)
            return;

        if (abilitySystem.IsCasting)
            abilitySystem.CancelCasting(force);

        if (abilitySystem.IsExecuting)
            abilitySystem.CancelExecution(force);
    }

    public bool TryBeginRunner(IMobPatternRunner runner)
    {
        if (runner == null)
            return false;

        if (activeRunner != null && activeRunner != runner && activeRunner.IsRunning)
            return false;

        activeRunner = runner;
        return true;
    }

    public void EndRunner(IMobPatternRunner runner)
    {
        if (runner == null)
            return;

        if (activeRunner == runner)
            activeRunner = null;
    }
}
