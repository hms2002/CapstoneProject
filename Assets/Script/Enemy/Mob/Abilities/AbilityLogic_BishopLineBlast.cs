using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_BishopLineBlast", menuName = "GAS/Ability Logic/Bishop Line Blast")]
public class AbilityLogic_BishopLineBlast : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        BishopLineBlastRunner runner = system.GetComponent<BishopLineBlastRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null) return;

        BishopLineBlastRunner runner = system.GetComponent<BishopLineBlastRunner>();
        runner?.Cancel();
    }
}
