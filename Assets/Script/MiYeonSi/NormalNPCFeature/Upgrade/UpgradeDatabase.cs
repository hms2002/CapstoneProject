using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Database")]
public class UpgradeDatabase : ScriptableObject
{
    public List<UpgradeNodeSO> allUpgrades;
}