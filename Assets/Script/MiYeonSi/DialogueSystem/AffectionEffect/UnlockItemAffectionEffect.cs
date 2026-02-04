using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Effect_Affection_Unlock", menuName = "NPC/Affection Effects/Unlock Item")]
public class UnlockItemAffectionEffect : AffectionEffect
{
    [Header("해금할 아이템 (직접 드래그)")]
    public List<WeaponDefinition> weapons;
    public List<RelicDefinition> relics;

    public override void Execute()
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.UnlockItems(weapons, relics);
        }
    }
}