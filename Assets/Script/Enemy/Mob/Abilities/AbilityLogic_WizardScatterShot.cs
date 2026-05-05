using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_WizardScatterShot", menuName = "GAS/Ability Logic/Wizard Scatter Shot")]
public class AbilityLogic_WizardScatterShot : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        WizardScatterShotRunner runner = system.GetComponent<WizardScatterShotRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null) return;

        WizardScatterShotRunner runner = system.GetComponent<WizardScatterShotRunner>();
        runner?.Cancel();
    }
}
