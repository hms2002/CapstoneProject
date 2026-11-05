using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멜타의 첫 조우 본 대사와 재조우 본 대사를 분리한다.
/// 시민권 후속담은 첫 대화에서 문장제를 선택한 경우에만 재조우 후보가 된다.
/// </summary>
public sealed class SlimeQueenDialogueStartKnotSelector : MonoBehaviour, IDialogueStartKnotSelector
{
    private const string CitizenshipMottoSelectedFlag = "melta.citizenship.motto_selected";
    private const string CitizenshipFollowupSeenFlag = "melta.citizenship.identity_followup_seen";

    [SerializeField] private string firstEncounterStartKnot = "SLIME_QUEEN_01";
    [SerializeField] private string[] repeatStartKnots =
    {
        "SLIME_QUEEN_CITIZENSHIP_CEREMONY",
        "SLIME_QUEEN_SPIRI_ACCESS_BAN",
        "MELTA_CITIZENSHIP_IDENTITY_FOLLOWUP"
    };

    public string SelectStartKnot(NPCData npcData, TextAsset inkJSON)
    {
        int encounterCount = npcData != null
            ? BossDialogueProgressStore.GetEncounterCount(npcData.id)
            : 0;

        string selectedStartKnot = encounterCount > 0
            ? SelectRepeatStartKnot(npcData)
            : firstEncounterStartKnot;

        return string.IsNullOrWhiteSpace(selectedStartKnot)
            ? null
            : selectedStartKnot.Trim();
    }

    private string SelectRepeatStartKnot(NPCData npcData)
    {
        if (repeatStartKnots == null || repeatStartKnots.Length == 0)
            return firstEncounterStartKnot;

        List<string> eligibleStartKnots = new List<string>(repeatStartKnots.Length);
        foreach (string startKnot in repeatStartKnots)
        {
            if (string.IsNullOrWhiteSpace(startKnot))
                continue;

            string normalizedStartKnot = startKnot.Trim();
            if (normalizedStartKnot == "MELTA_CITIZENSHIP_IDENTITY_FOLLOWUP" && !CanPlayCitizenshipFollowup(npcData))
                continue;

            eligibleStartKnots.Add(normalizedStartKnot);
        }

        if (eligibleStartKnots.Count == 0)
            return firstEncounterStartKnot;

        return BossDialogueRepeatSelection.Select(
            eligibleStartKnots, BossDialogueProgressStore.GetLastPrimaryStartPath(npcData.id));
    }

    private static bool CanPlayCitizenshipFollowup(NPCData npcData)
    {
        if (npcData == null)
            return false;

        return BossDialogueProgressStore.HasDialogueFlag(npcData.id, CitizenshipMottoSelectedFlag)
            && !BossDialogueProgressStore.HasDialogueFlag(npcData.id, CitizenshipFollowupSeenFlag);
    }
}
