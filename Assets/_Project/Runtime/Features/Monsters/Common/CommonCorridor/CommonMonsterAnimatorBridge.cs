using UnityEngine;

/// <summary>
/// 책임:
/// - 공통 복도 몬스터의 FSM/Runner가 요구하는 애니메이션 큐를 Animator 파라미터 호출로 변환한다.
/// - 컨트롤러별로 없는 트리거는 조용히 무시해, 임시/최종 애니메이터를 같은 코드 경로로 사용할 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CommonMonsterAnimatorBridge : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string attackReadyTrigger = "attackReady";
    [SerializeField] private string attackTrigger = "attack";
    [SerializeField] private string recoverTrigger = "recover";
    [SerializeField] private string dieTrigger = "die";
    [SerializeField] private string jumpTrigger = "jump";
    [SerializeField] private string landTrigger = "land";
    [SerializeField] private string landEndTrigger = "landEnd";

    public Animator Animator => animator;

    private void Awake()
    {
        ResolveAnimatorIfNeeded();
    }

    private void OnValidate()
    {
        ResolveAnimatorIfNeeded();
    }

    public void Configure(Animator targetAnimator)
    {
        animator = targetAnimator;
    }

    public void Configure(
        Animator targetAnimator,
        string attackReady,
        string attack,
        string recover,
        string die,
        string jump,
        string land)
    {
        Configure(targetAnimator, attackReady, attack, recover, die, jump, land, null);
    }

    public void Configure(
        Animator targetAnimator,
        string attackReady,
        string attack,
        string recover,
        string die,
        string jump,
        string land,
        string landEnd)
    {
        animator = targetAnimator;
        attackReadyTrigger = attackReady;
        attackTrigger = attack;
        recoverTrigger = recover;
        dieTrigger = die;
        jumpTrigger = jump;
        landTrigger = land;
        landEndTrigger = landEnd;
    }

    public void TriggerAttackReady() => TrySetTrigger(attackReadyTrigger);

    public void TriggerAttack() => TrySetTrigger(attackTrigger);

    public void TriggerRecover() => TrySetTrigger(recoverTrigger);

    public void TriggerDie() => TrySetTrigger(dieTrigger);

    public void TriggerJump() => TrySetTrigger(jumpTrigger);

    public void TriggerLand() => TrySetTrigger(landTrigger);

    public void TriggerLandEnd() => TrySetTrigger(landEndTrigger);

    private void ResolveAnimatorIfNeeded()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private bool TrySetTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName) || animator.runtimeAnimatorController == null)
            return false;

        if (!HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
            return false;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
        return true;
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == expectedType && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
