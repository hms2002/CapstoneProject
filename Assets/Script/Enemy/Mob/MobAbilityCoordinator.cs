using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 일반 몬스터의 AbilitySystem과 패턴 runner 생명주기를 한 곳에서 조율한다.
/// - AI가 "지금 공격을 시작해도 되는가"를 ASC busy 상태와 runner 실행 상태를 합쳐 판단하게 돕는다.
/// - 그로기 같은 전역 제압 상태를 공통 실행 금지 신호로 해석해 FSM/runner/helper가 같은 규칙을 보게 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AbilitySystem))]
public class MobAbilityCoordinator : MonoBehaviour, IMobAbilityBridge, IMobAbilityHelperAccess
{
    private const string GroggyTagResourcePath = "Tags/State.Status.Groggy";

    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TagSystem tagSystem;

    private static GameplayTag s_groggyTag;
    private IMobPatternRunner activeRunner;

    public bool IsAbilityExecutionBusy =>
        (abilitySystem != null && abilitySystem.IsBusy) ||
        (activeRunner != null && activeRunner.IsRunning);

    /// <summary>
    /// 책임 :
    /// - 일반 몬스터가 그로기처럼 전역 제압 상태에 들어갔는지 공통 실행 금지 규칙으로 노출한다.
    /// - FSM, runner, helper가 태그 경로를 직접 알지 않고도 같은 제압 상태 판단을 공유하게 한다.
    /// </summary>
    public bool IsAbilityExecutionSuppressed
    {
        get
        {
            if (tagSystem == null)
                return false;

            if (s_groggyTag == null)
                s_groggyTag = Resources.Load<GameplayTag>(GroggyTagResourcePath);

            return s_groggyTag != null && tagSystem.HasTag(s_groggyTag);
        }
    }

    private void Awake()
    {
        if (abilitySystem == null)
            abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();
    }

    private void OnDisable()
    {
        CancelActiveAbility(true);
        activeRunner = null;
    }

    public bool TryStartAbility(AbilityDefinition ability, GameObject explicitTarget = null)
    {
        if (abilitySystem == null || ability == null)
            return false;

        if (IsAbilityExecutionSuppressed)
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

    public bool HasStateTag(GameplayTag tag)
    {
        return tagSystem != null && tagSystem.HasTag(tag);
    }

    float IMobAbilityHelperAccess.GetCooldownRemaining(AbilityDefinition ability)
    {
        if (abilitySystem == null || ability == null)
            return 0f;

        return abilitySystem.GetCooldownRemaining(ability);
    }

    bool IMobAbilityHelperAccess.TrySetCooldownRemaining(AbilityDefinition ability, float seconds)
    {
        if (abilitySystem == null || ability == null)
            return false;

        return abilitySystem.TrySetCooldownRemaining(ability, seconds);
    }

    bool IMobAbilityHelperAccess.TryAddStateTag(GameplayTag tag, int count)
    {
        if (tagSystem == null || tag == null)
            return false;

        tagSystem.AddTag(tag, count);
        return true;
    }

    bool IMobAbilityHelperAccess.TryRemoveStateTag(GameplayTag tag, int count)
    {
        if (tagSystem == null || tag == null)
            return false;

        tagSystem.RemoveTag(tag, count);
        return true;
    }

    bool IMobAbilityHelperAccess.TryGetAbilityExecutionContext(AbilityDefinition ability, out AbilitySystem system, out AbilitySpec spec)
    {
        system = abilitySystem;
        spec = null;

        if (abilitySystem == null || ability == null)
            return false;

        spec = abilitySystem.FindSpec(ability);
        return spec != null;
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
