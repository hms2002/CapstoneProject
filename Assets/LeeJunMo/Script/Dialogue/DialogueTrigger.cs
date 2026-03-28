using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DialogueTrigger : InteractableBase
{
    [Header("Dialogue Data")]
    [SerializeField] private NPCData npcData;
    [FormerlySerializedAs("inkJSON")]
    [SerializeField, HideInInspector] private TextAsset legacyInkJSON;

    [Header("Prompt")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "대화하기";

    [Header("Highlight")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private MaterialPropertyBlock propBlock;
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    private NPCFeatureController featureController;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        featureController = GetComponent<NPCFeatureController>();
        TryMigrateLegacyInk();
        OnUnHighlight();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (TryMigrateLegacyInk())
            EditorUtility.SetDirty(npcData);
    }
#endif

    public override void OnHighlight()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 1f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public override void OnUnHighlight()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        DialogueService dialogueService = DialogueService.Instance;
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               npcData != null &&
               ResolveInk() != null &&
               dialogueService != null &&
               !dialogueService.IsPlaying;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (npcData == null)
        {
            Debug.LogError($"[DialogueTrigger] '{name}' has no NPCData assigned.", this);
            return;
        }

        TextAsset dialogueInk = ResolveInk();
        if (dialogueInk == null)
        {
            Debug.LogError($"[DialogueTrigger] '{name}' has no primaryInk assigned on NPCData.", this);
            return;
        }

        if (!CanInteract(player))
            return;

        List<NPCData> participants = new() { npcData };
        DialogueService.Instance?.TryStartDialogue(dialogueInk, participants, featureController);
    }

    public override InteractState GetInteractType() => InteractState.Talking;

    public override string GetInteractDescription() => interactPromptText;

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private TextAsset ResolveInk()
    {
        if (npcData == null)
            return null;

        if (npcData.PrimaryInk != null)
            return npcData.PrimaryInk;

        if (legacyInkJSON == null)
            return null;

        npcData.AssignPrimaryInkIfEmpty(legacyInkJSON);
        return npcData.PrimaryInk != null ? npcData.PrimaryInk : legacyInkJSON;
    }

    private bool TryMigrateLegacyInk()
    {
        if (npcData == null || legacyInkJSON == null || npcData.PrimaryInk != null)
            return false;

        npcData.AssignPrimaryInkIfEmpty(legacyInkJSON);
        return npcData.PrimaryInk != null;
    }
}
