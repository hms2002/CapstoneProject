using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Effect_Upgrade_Unlock", menuName = "Game/Upgrade Effects/Unlock Item")]
public class UnlockItemUpgradeEffect : UpgradeEffectSO
{
    [Header("해금할 아이템 (직접 드래그)")]
    public List<WeaponDefinition> weapons;
    public List<RelicDefinition> relics;

    // [수정] SampleTopDownPlayer를 인자로 받지만, 사용하진 않음 (매니저 사용)
    public override void ApplyEffect(SampleTopDownPlayer player)
    {
        if (GameDataManager.Instance != null)
        {
            // GameDataManager를 통해 아이템 해금 및 UI 표시
            GameDataManager.Instance.UnlockItems(weapons, relics);
        }
    }
}