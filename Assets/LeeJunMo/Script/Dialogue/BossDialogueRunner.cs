using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossDialogueRunner : MonoBehaviour
{
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private NPCData npcData;

    public IEnumerator PlayDialogueRoutine()
    {
        if (inkJSON == null)
        {
            Debug.LogError("[BossDialogueRunner] inkJSON이 비어 있다.");
            yield break;
        }

        if (npcData == null)
        {
            Debug.LogError("[BossDialogueRunner] npcData가 비어 있다.");
            yield break;
        }

        if (DialogueController.Instance == null)
        {
            Debug.LogError("[BossDialogueRunner] DialogueController 인스턴스를 찾을 수 없다.");
            yield break;
        }

        List<NPCData> participants = new List<NPCData> { npcData };
        DialogueController.Instance.EnterDialogueMode(inkJSON, participants);

        yield return new WaitUntil(() =>
            DialogueController.Instance == null || !DialogueController.Instance.isPlaying);
    }
}