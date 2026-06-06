using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_FloweringBloom", menuName = "GAS/Weapon/Flowering/Logic Bloom")]
public sealed class AbilityLogic_FloweringBloom : AbilityLogic
{
    private bool timeScalePaused;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null)
            yield break;

        FloweringBloomData data = spec.Definition.sourceObject as FloweringBloomData;
        if (data == null)
        {
            Debug.LogError("[FloweringBloom] AbilityDefinition.sourceObject must be FloweringBloomData.");
            yield break;
        }

        FloweringRuntimeData runtimeData = ResolveRuntimeData(system);
        if (runtimeData == null)
        {
            Debug.LogError("[FloweringBloom] Active weapon runtime data must be FloweringRuntimeData.");
            yield break;
        }

        FloweringRuntimeState runtimeState = FloweringRuntimeState.GetOrAdd(system);
        if (runtimeState == null)
            yield break;

        runtimeState.BeginBloomSkillSwapLock();
        try
        {
            runtimeState.AcquireBloomCutInInputBlock();
            PauseCombatTime();
            try
            {
                yield return runtimeState.PlayBloomCutIn(system, spec, data);
            }
            finally
            {
                RestoreCombatTime();
                runtimeState.ReleaseBloomCutInInputBlock();
            }

            if (IsAbilityCancelled(spec))
            {
                runtimeState.EndBloom();
                yield break;
            }

            runtimeState.BeginBloom(system, data, runtimeData);

            bool completedNaturally = false;
            try
            {
                while (runtimeData.IsBloomActive)
                {
                    if (IsAbilityCancelled(spec))
                        break;

                    runtimeData.TickBloom(Time.deltaTime);
                    yield return null;
                }

                completedNaturally = !IsAbilityCancelled(spec) && !runtimeData.IsBloomActive;
            }
            finally
            {
                if (!completedNaturally)
                    runtimeState.EndBloom();
            }

            if (completedNaturally)
            {
                yield return runtimeState.PlayBloomEndTransition(system, spec, data);
                runtimeState.EndBloom();
            }
        }
        finally
        {
            runtimeState.EndBloomSkillSwapLock();
        }
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        RestoreCombatTime();
        FloweringRuntimeState runtimeState = FloweringRuntimeState.ResolveExisting(system);
        if (runtimeState == null)
            return;

        runtimeState.EndBloom();
        runtimeState.EndBloomSkillSwapLock();
    }

    private static FloweringRuntimeData ResolveRuntimeData(AbilitySystem system)
    {
        WeaponInventory2D inventory = system != null ? system.GetComponent<WeaponInventory2D>() : null;
        return inventory != null ? inventory.ActiveRuntimeData as FloweringRuntimeData : null;
    }

    private void PauseCombatTime()
    {
        if (timeScalePaused)
            return;

        TimeScalePauseService.Acquire(this);
        timeScalePaused = true;
    }

    private void RestoreCombatTime()
    {
        if (!timeScalePaused)
            return;

        TimeScalePauseService.Release(this);
        timeScalePaused = false;
    }
}
