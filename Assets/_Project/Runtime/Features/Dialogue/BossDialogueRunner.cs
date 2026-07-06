using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 책임 :
/// - 보스 조우/기본 대사 데이터를 선택하고 DialoguePlayback에 순차 재생 요청을 전달한다.
/// - 조우 대사 진행 기록을 남겨 같은 보스 대사가 반복 재생되지 않도록 한다.
/// </summary>
public class BossDialogueRunner : MonoBehaviour
{
    [SerializeField] private NPCData npcData;
    [SerializeField] private MonoBehaviour startKnotSelectorBehaviour;
    [SerializeField] private bool playEncounterDialogue = true;
    [SerializeField] private bool playPrimaryDialogueAfterEncounter = true;
    [SerializeField] private bool recordEncounterProgress = true;

    [FormerlySerializedAs("inkJSON")]
    [SerializeField, HideInInspector] private TextAsset legacyInkJSON;

    private IDialogueStartKnotSelector startKnotSelector;

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

        if (!DialoguePlayback.IsAvailable)
        {
            Debug.LogError("[BossDialogueRunner] Dialogue playback backend was not found.", this);
            yield break;
        }

        List<NPCData> participants = new List<NPCData> { npcData };
        TextAsset primaryInk = ResolveDialogueInk();
        TextAsset encounterInk = ResolveEncounterInk(out BossEncounterDialogueEntry encounterEntry);
        string encounterStartPath = encounterEntry != null ? encounterEntry.StartPath : null;
        string primaryStartPath = ResolveStartKnot(primaryInk);
        List<DialogueStorySegment> storySegments = BuildStorySegments(
            primaryInk,
            primaryStartPath,
            encounterInk,
            encounterStartPath);
        if (storySegments.Count == 0)
        {
            Debug.LogError("[BossDialogueRunner] No encounter or primary dialogue ink is assigned on NPCData.", this);
            yield break;
        }

        if (!DialoguePlayback.TryStartDialogueSequence(storySegments, participants))
            yield break;

        yield return WaitForDialogueToFinish();

        if (recordEncounterProgress)
            BossDialogueProgressStore.RegisterEncounter(npcData);
    }

    private List<DialogueStorySegment> BuildStorySegments(
        TextAsset primaryInk,
        string primaryStartPath,
        TextAsset encounterInk,
        string encounterStartPath)
    {
        List<DialogueStorySegment> storySegments = new List<DialogueStorySegment>();

        if (encounterInk != null)
            storySegments.Add(new DialogueStorySegment(encounterInk, encounterStartPath));

        if (playPrimaryDialogueAfterEncounter && primaryInk != null)
            storySegments.Add(new DialogueStorySegment(primaryInk, primaryStartPath));

        return storySegments;
    }

    private IEnumerator WaitForDialogueToFinish()
    {
        yield return new WaitUntil(() =>
            !DialoguePlayback.IsPlaying);
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

    /// <summary>
    /// 책임:
    /// - 보스별 대화 선택자가 있으면 이번 대화의 Ink 시작 knot을 위임받는다.
    /// - 선택자가 없거나 비어 있으면 기존처럼 Ink 루트에서 시작한다.
    /// </summary>
    private string ResolveStartKnot(TextAsset dialogueInk)
    {
        if (dialogueInk == null)
            return null;

        if (startKnotSelector == null && startKnotSelectorBehaviour != null)
            startKnotSelector = startKnotSelectorBehaviour as IDialogueStartKnotSelector;

        return startKnotSelector?.SelectStartKnot(npcData, dialogueInk);
    }
}
