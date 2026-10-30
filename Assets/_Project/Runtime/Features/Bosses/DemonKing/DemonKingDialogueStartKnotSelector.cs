using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마왕의 첫 조우 본 대사는 Ink 루트에서 유지하고,
/// 재조우부터는 짧은 본 대사 후보 가운데 하나를 균등 무작위로 고른다.
/// </summary>
public sealed class DemonKingDialogueStartKnotSelector : MonoBehaviour, IDialogueStartKnotSelector
{
    [SerializeField] private string[] repeatStartKnots =
    {
        "DEMONKING_LESSON",
        "DEMONKING_REPAIR_COST"
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

        return eligibleStartKnots.Count > 0
            ? eligibleStartKnots[Random.Range(0, eligibleStartKnots.Count)]
            : null;
    }
}
