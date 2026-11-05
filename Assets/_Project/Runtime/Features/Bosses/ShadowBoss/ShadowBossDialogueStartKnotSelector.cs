using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 클로에의 첫 조우는 Primary Ink 루트에서 재생하고,
/// 재조우 본대사는 명시된 후보 중 하나를 균등 무작위로 선택한다.
/// </summary>
public sealed class ShadowBossDialogueStartKnotSelector : MonoBehaviour, IDialogueStartKnotSelector
{
    [SerializeField] private string[] repeatStartKnots =
    {
        "CHLOE_SILENT_WARDEN_PRACTICE",
        "CHLOE_MONTHLY_PRISONER_REPORT"
    };

    public string SelectStartKnot(NPCData npcData, TextAsset inkJSON)
    {
        int encounterCount = npcData != null
            ? BossDialogueProgressStore.GetEncounterCount(npcData.id)
            : 0;

        if (encounterCount <= 0)
            return null;

        List<string> eligibleStartKnots = new List<string>(repeatStartKnots != null ? repeatStartKnots.Length : 0);
        if (repeatStartKnots != null)
        {
            foreach (string startKnot in repeatStartKnots)
            {
                if (!string.IsNullOrWhiteSpace(startKnot))
                    eligibleStartKnots.Add(startKnot.Trim());
            }
        }

        return BossDialogueRepeatSelection.Select(
            eligibleStartKnots, BossDialogueProgressStore.GetLastPrimaryStartPath(npcData.id));
    }
}
