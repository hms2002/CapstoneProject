using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BossDialogueRunner : MonoBehaviour
{
    [SerializeField] private NPCData npcData;
    [SerializeField] private bool playEncounterDialogue = true;
    [SerializeField] private bool playPrimaryDialogueAfterEncounter = true;
    [SerializeField] private bool recordEncounterProgress = true;

    [FormerlySerializedAs("inkJSON")]
    [SerializeField, HideInInspector] private TextAsset legacyInkJSON;

    public void ApplyLegacyDialogueData(NPCData data, TextAsset legacyInk)
    {
        npcData = data;
        legacyInkJSON = legacyInk;
    }

    public IEnumerator PlayDialogueRoutine()
    {
        if (npcData == null)
        {
            Debug.LogError("[BossDialogueRunner] npcData is missing.", this);
            yield break;
        }

        if (DialogueService.Instance == null)
        {
            Debug.LogError("[BossDialogueRunner] DialogueService instance was not found.", this);
            yield break;
        }

        List<NPCData> participants = new List<NPCData> { npcData };
        TextAsset primaryInk = ResolveDialogueInk();
        TextAsset encounterInk = ResolveEncounterInk(out BossEncounterDialogueEntry encounterEntry);
        string encounterStartPath = encounterEntry != null ? encounterEntry.StartPath : null;
        List<DialogueStorySegment> storySegments = BuildStorySegments(primaryInk, encounterInk, encounterStartPath);
        if (storySegments.Count == 0)
        {
            Debug.LogError("[BossDialogueRunner] No encounter or primary dialogue ink is assigned on NPCData.", this);
            yield break;
        }

        if (!DialogueService.Instance.TryStartDialogueSequence(storySegments, participants))
            yield break;

        yield return WaitForDialogueToFinish();

        if (recordEncounterProgress)
            BossDialogueProgressStore.RegisterEncounter(npcData);
    }

    private List<DialogueStorySegment> BuildStorySegments(
        TextAsset primaryInk,
        TextAsset encounterInk,
        string encounterStartPath)
    {
        List<DialogueStorySegment> storySegments = new List<DialogueStorySegment>();

        if (encounterInk != null)
            storySegments.Add(new DialogueStorySegment(encounterInk, encounterStartPath));

        if (playPrimaryDialogueAfterEncounter && primaryInk != null)
            storySegments.Add(new DialogueStorySegment(primaryInk));

        return storySegments;
    }

    private IEnumerator WaitForDialogueToFinish()
    {
        yield return new WaitUntil(() =>
            DialogueService.Instance == null || !DialogueService.Instance.IsPlaying);
    }

    private TextAsset ResolveEncounterInk(out BossEncounterDialogueEntry entry)
    {
        entry = null;

        if (!playEncounterDialogue)
            return null;

        if (!BossEncounterDialogueSelector.TrySelect(npcData, out entry))
            return null;

        return npcData.BossEncounterInk != null
            ? npcData.BossEncounterInk
            : entry.InkOverride;
    }

    private TextAsset ResolveDialogueInk()
    {
        if (npcData != null)
        {
            if (npcData.PrimaryInk != null)
                return npcData.PrimaryInk;

            if (legacyInkJSON != null)
            {
                npcData.AssignPrimaryInkIfEmpty(legacyInkJSON);
                if (npcData.PrimaryInk != null)
                    return npcData.PrimaryInk;
            }
        }

        return legacyInkJSON;
    }
}
