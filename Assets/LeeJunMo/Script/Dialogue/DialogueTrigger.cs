using System.Collections.Generic;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [Header("데이터 설정")]
    [SerializeField] private NPCData npcData;
    [SerializeField] private TextAsset inkJSON;

    [Header("프롬프트")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "대화하기";

    [Header("하이라이트")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private MaterialPropertyBlock propBlock;
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    private NPCFeatureController featureController;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        featureController = GetComponent<NPCFeatureController>();
        OnUnHighlight();
    }

    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }

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

    public bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               DialogueController.Instance != null &&
               !DialogueController.Instance.isPlaying;
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (DialogueController.Instance != null)
        {
            List<NPCData> participants = new() { npcData };
            DialogueController.Instance.EnterDialogueMode(inkJSON, participants, featureController);
        }
    }

    public void GetInteract(string text) { }
    public InteractState GetInteractType() => InteractState.Talking;
    public string GetInteractDescription() => interactPromptText;
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
}
