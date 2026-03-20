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
        // 파일 관리자(GameData)가 아닌, 런타임 상태 관리자(ItemManager)를 호출합니다.
        if (ItemManager.Instance != null)
        {
            if (weapons != null)
            {
                foreach (var w in weapons)
                {
                    if (w != null) ItemManager.Instance.UnlockWeapon(w.weaponId);
                }
            }

            if (relics != null)
            {
                foreach (var r in relics)
                {
                    if (r != null) ItemManager.Instance.UnlockRelic(r.relicId);
                }
            }
        }
        else
        {
            Debug.LogError("[UnlockItemAffectionEffect] ItemManager 인스턴스를 찾을 수 없습니다!");
        }
    }
}