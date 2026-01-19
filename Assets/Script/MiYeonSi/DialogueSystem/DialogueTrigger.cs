using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [Header("데이터 설정")]
    [SerializeField] private NPCData npcData;
    [SerializeField] private TextAsset inkJSON;

    [Header("시각적 설정")]
    [SerializeField] private GameObject visualCue ; // NPC 머리 위 'F' 아이콘 등

    private void Awake()
    {
        visualCue.SetActive(false);
    }

    // 범위 내에 들어오면 시각적 안내 표시
    public void OnPlayerNearby() => visualCue.SetActive(true);
    public void OnPlayerLeave() => visualCue.SetActive(false);

    public void OnPlayerInteract(TempPlayer player)
    {
        if (CanInteract(player))
        {
            DialogueManager.GetInstance().EnterDialogueMode(inkJSON, npcData);
        }
    }

    // 최단 거리 타겟으로 선정되었을 때 (나중에 셰이더 적용 시 사용)
    public void OnHighlight()
    {
        Debug.Log($"{npcData.npcName} 하이라이트 시작");
    }

    public void OnUnHighlight()
    {
        Debug.Log($"{npcData.npcName} 하이라이트 종료");
    }

    public bool CanInteract(TempPlayer player)
    {
        return !DialogueManager.GetInstance().dialogueIsPlaying;
    }

    public void GetInteract(string text) { }
    public InteractState GetInteractType() => InteractState.Talking;
    public string GetInteractDescription() => "대화하기";
}