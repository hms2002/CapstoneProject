using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_RookCharge", menuName = "GAS/Ability Logic/Rook Charge")]
public class AbilityLogic_RookCharge : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null)
            yield break;

        RookChargeRunner runner = system.GetComponent<RookChargeRunner>();
        if (runner == null)
            yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        RookChargeRunner runner = system.GetComponent<RookChargeRunner>();
        runner?.Cancel();
    }
}
