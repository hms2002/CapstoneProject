using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossSpecialRewardPreset", menuName = "Loot/Boss Special Reward Preset")]
public sealed class BossSpecialRewardPresetSO : ScriptableObject
{
    [Header("Boss Special Rewards")]
    [SerializeField] private List<BossSpecificLoot> specialLoots = new List<BossSpecificLoot>();

    public IReadOnlyList<BossSpecificLoot> SpecialLoots => specialLoots;
}
