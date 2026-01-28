using UnityEngine;
using UnityGAS;

public class PlayerDamageProfile : MonoBehaviour
{
    public DamageFormulaStats formulaStats;

    public DamageFormulaStats GetFormulaStats() => formulaStats;
}
