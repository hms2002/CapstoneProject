using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - Dead'sSkeleton 자폭 패턴 실행의 공식 ASC 진입점이 되어 executor 시작과 취소 정리를 연결한다.
/// - 실제 인트로, armed, 접촉 폭발 시퀀스는 executor에 위임하고 이 로직은 생명주기 연결만 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DeadsSkeletonSelfDestruct", menuName = "GAS/Ability Logic/DeadsSkeleton Self Destruct")]
public class AbilityLogic_DeadsSkeletonSelfDestruct : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null)
            yield break;

        DeadsSkeletonSelfDestructPatternExecutor executor = system.GetComponent<DeadsSkeletonSelfDestructPatternExecutor>();
        if (executor == null)
            yield break;

        yield return executor.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        DeadsSkeletonSelfDestructPatternExecutor executor = system.GetComponent<DeadsSkeletonSelfDestructPatternExecutor>();
        executor?.Cancel();
    }
}
