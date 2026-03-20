using UnityEngine;
using System.Collections.Generic; // [추가] List를 사용하기 위해 필요합니다.

public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [Header("데이터 설정")]
    [SerializeField] private NPCData npcData;
    [SerializeField] private TextAsset inkJSON;

    [Header("시각적 가이드")]
    [SerializeField] private GameObject visualCue;

    [SerializeField] private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propBlock;

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");

    private NPCFeatureController featureController;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (visualCue != null) visualCue.SetActive(false);

        featureController = GetComponent<NPCFeatureController>();
        OnUnHighlight();
    }

    public void OnPlayerNearby()
    {
        if (visualCue != null) visualCue.SetActive(true);
    }

    public void OnPlayerLeave()
    {
        if (visualCue != null) visualCue.SetActive(false);
    }

    public void OnHighlight()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 1f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public void OnUnHighlight()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (CanInteract(player))
        {
            if (DialogueController.Instance != null)
            {
                // [에러 해결!] npcData 1명을 List라는 봉투에 예쁘게 담아서(new List) 사장님께 제출합니다!
                List<NPCData> participants = new List<NPCData>() { npcData };
                DialogueController.Instance.EnterDialogueMode(inkJSON, participants, featureController);
            }
        }
    }

    public bool CanInteract(IPlayerInteractor player)
    {
        return DialogueController.Instance != null && !DialogueController.Instance.isPlaying;
    }

    public void GetInteract(string text) { }
    public InteractState GetInteractType() => InteractState.Talking;
    public string GetInteractDescription() => "대화하기";
}