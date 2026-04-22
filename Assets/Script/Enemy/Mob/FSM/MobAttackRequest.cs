using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 일반 몬스터 FSM의 AttackState가 실제 실행할 ASC 요청 최소 정보를 담는다.
/// - helper가 선택한 AbilityDefinition, 명시 타깃, AI 후딜 시간을 상태 기계가 세부 문맥 해석 없이 전달하게 돕는다.
/// </summary>
public readonly struct MobAttackRequest
{
    public readonly AbilityDefinition Ability;
    public readonly GameObject ExplicitTarget;
    public readonly float RecoverSeconds;

    public MobAttackRequest(AbilityDefinition ability, GameObject explicitTarget, float recoverSeconds = 0f)
    {
        Ability = ability;
        ExplicitTarget = explicitTarget;
        RecoverSeconds = Mathf.Max(0f, recoverSeconds);
    }

    public bool IsValid => Ability != null;
}
