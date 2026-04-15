using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - StrangeCandlestick 공격 실행의 공식 ASC 진입점이 되어 락온-발사 runner 실행을 연결한다.
/// - 실제 경고 유지, 취소, 발사 시퀀스는 runner에 위임하고 이 로직은 생명주기 연결만 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_StrangeCandlestickAttack", menuName = "GAS/Ability Logic/Strange Candlestick Attack")]
public class AbilityLogic_StrangeCandlestickAttack : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null)
            yield break;

        StrangeCandlestickAttackRunner runner = system.GetComponent<StrangeCandlestickAttackRunner>();
        if (runner == null)
            yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        StrangeCandlestickAttackRunner runner = system.GetComponent<StrangeCandlestickAttackRunner>();
        runner?.Cancel();
    }
}
