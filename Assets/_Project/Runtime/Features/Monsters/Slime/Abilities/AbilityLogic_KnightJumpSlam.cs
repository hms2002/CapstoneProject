using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_KnightJumpSlam", menuName = "GAS/Ability Logic/Knight Jump Slam")]
public class AbilityLogic_KnightJumpSlam : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        KnightJumpSlamRunner runner = system.GetComponent<KnightJumpSlamRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null) return;

        KnightJumpSlamRunner runner = system.GetComponent<KnightJumpSlamRunner>();
        runner?.Cancel();
    }
}
