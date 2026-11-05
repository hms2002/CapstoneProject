using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 취룡 보스의 시작 미연시에서 어떤 Ink knot으로 진입할지 결정한다.
/// - 조우 횟수, 호감도, 남은 시간, 이전 전투 결과 같은 취룡 전용 조건을 공용 BossDialogueRunner 밖에 격리한다.
/// </summary>
public sealed class DragonDialogueStartKnotSelector : MonoBehaviour, IDialogueStartKnotSelector
{
    [SerializeField] private string defaultStartKnot = "DRAGON_01";
    [SerializeField] private string[] repeatStartKnots =
    {
        "SPIRI_SLIME_SNACK",
        "SPIRI_EMPTY_KEG_ERRAND"
    };

    public string SelectStartKnot(NPCData npcData, TextAsset inkJSON)
    {
        int encounterCount = npcData != null
            ? BossDialogueProgressStore.GetEncounterCount(npcData.id)
            : 0;
        string selectedStartKnot = encounterCount > 0
            ? SelectRepeatStartKnot(npcData)
            : defaultStartKnot;

        return string.IsNullOrWhiteSpace(selectedStartKnot)
            ? null
            : selectedStartKnot.Trim();
    }

    private string SelectRepeatStartKnot(NPCData npcData)
    {
        if (repeatStartKnots == null || repeatStartKnots.Length == 0)
            return defaultStartKnot;

        List<string> candidates = new List<string>(repeatStartKnots.Length);
        foreach (string startKnot in repeatStartKnots)
        {
            if (!string.IsNullOrWhiteSpace(startKnot))
                candidates.Add(startKnot.Trim());
        }

        return BossDialogueRepeatSelection.Select(
            candidates, BossDialogueProgressStore.GetLastPrimaryStartPath(npcData.id)) ?? defaultStartKnot;
    }
}
