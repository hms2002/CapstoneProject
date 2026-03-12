using UnityEngine;
using UnityGAS;

public class PlayerDamageProfile : MonoBehaviour
{
    [Tooltip("Stat bindings used by ScaledStatFormula when using StatId mode.")]
    public StatTypeBindings statBindings;

    [Tooltip("Default post-process applied after formulas when a hit's DamagePayloadConfig has no postProcess.")]
    public DamagePostProcessStats defaultPostProcess;

    public StatTypeBindings GetStatBindings() => statBindings;
    public DamagePostProcessStats GetDefaultPostProcess() => defaultPostProcess;
}
