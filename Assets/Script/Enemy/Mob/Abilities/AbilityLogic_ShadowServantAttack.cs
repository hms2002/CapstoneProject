using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - ShadowServant 공격 실행의 공식 ASC 진입점이 되어 runner 시작과 취소 정리를 연결한다.
/// - 복잡한 경고/대기/폭발 시퀀스는 runner에 위임하고, 이 로직은 생명주기 연결만 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_ShadowServantAttack", menuName = "GAS/Ability Logic/Shadow Servant Attack")]
public class AbilityLogic_ShadowServantAttack : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null)
            yield break;

        ShadowServantAttackRunner runner = system.GetComponent<ShadowServantAttackRunner>();
        if (runner == null)
            yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        ShadowServantAttackRunner runner = system.GetComponent<ShadowServantAttackRunner>();
        runner?.Cancel();
    }
}
