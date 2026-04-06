using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BossDialogueRunner : MonoBehaviour
{
    [SerializeField] private NPCData npcData;
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
        if (!DialogueService.Instance.TryStartDialogue(dialogueInk, participants))
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
}
