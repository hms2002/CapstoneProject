using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BossDialogueRunner : MonoBehaviour
{
    [SerializeField] private NPCData npcData;
    [SerializeField] private MonoBehaviour startKnotSelectorBehaviour;
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

        TextAsset dialogueInk = ResolveDialogueInk();
        if (dialogueInk == null)
        {
            Debug.LogError("[BossDialogueRunner] No dialogue ink is assigned on NPCData.", this);
            yield break;
        }

        if (DialogueService.Instance == null)
        {
            Debug.LogError("[BossDialogueRunner] DialogueService instance was not found.", this);
            yield break;
        }

        List<NPCData> participants = new List<NPCData> { npcData };
        string startKnot = ResolveStartKnot(dialogueInk);
        if (!DialogueService.Instance.TryStartDialogue(dialogueInk, participants, startKnot))
            yield break;

        yield return new WaitUntil(() =>
            DialogueService.Instance == null || !DialogueService.Instance.IsPlaying);
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
        if (startKnotSelector == null && startKnotSelectorBehaviour != null)
            startKnotSelector = startKnotSelectorBehaviour as IDialogueStartKnotSelector;

        return startKnotSelector?.SelectStartKnot(npcData, dialogueInk);
    }
}
