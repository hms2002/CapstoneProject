using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class DialogueTrigger : InteractableBase, INpcRoomIntroductionSource
{
    // 이 클래스의 책임:
    // 월드 NPC/오브젝트의 상호작용 진입점으로서 NPCData와 Ink 대사를 DialoguePlayback에 전달한다.

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
    private int dialogueGeneration;

    public NPCData NpcData => npcData;
    public bool CanOfferAffectionDialogue => NpcAffectionDialogue.CanOffer(npcData);
    // Run affection conversations begin through player interaction, not the profile-once room intro.
    public string IntroductionKey => npcData != null && !npcData.isBoss && !npcData.UsesRunAffectionDialogue ? $"npc:{npcData.id}" : null;
    public bool HandlesIntroductionCamera => false;

    public bool TryStartIntroduction(IPlayerInteractor player, System.Action<bool> onEnded)
    {
        if ((npcData != null && npcData.UsesRunAffectionDialogue) || !CanInteract(player))
            return false;

        return DialoguePlayback.TryStartDialogueSequence(
            new[] { new DialogueStorySegment(ResolveInk()) },
            new List<NPCData> { npcData }, featureController,
            new DialoguePresentationOptions(onEnded: onEnded));
    }

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        featureController = GetComponent<NPCFeatureController>();
        TryMigrateLegacyInk();
        OnUnHighlight();
    }

    private void OnEnable()
    {
        RunSessionStore.OnRunStarted += InvalidateDialogue;
        RunSessionStore.OnRunEnded += HandleRunEnded;
    }

    private void OnDisable()
    {
        RunSessionStore.OnRunStarted -= InvalidateDialogue;
        RunSessionStore.OnRunEnded -= HandleRunEnded;
        InvalidateDialogue();
    }

    private void InvalidateDialogue() => dialogueGeneration++;
    private void HandleRunEnded(RunEndReason reason) => InvalidateDialogue();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (TryMigrateLegacyInk())
            EditorAuthoringPlayback.MarkDirty(npcData);
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
        TextAsset dialogueInk = ResolveInk();
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               npcData != null &&
               dialogueInk != null &&
               DialoguePlayback.IsAvailable &&
               !DialoguePlayback.IsPlaying;
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
        if (npcData.UsesRunAffectionDialogue)
        {
            var backpack = player.Transform.GetComponent<PlayerBackpackInventory>();
            if (!NpcAffectionDialogue.TrySelect(npcData, NpcAffectionDialogue.IsBackpackFull(backpack), out string path, out bool isAffection))
                return;
            int generation = ++dialogueGeneration;
            GamePlayData run = RunSessionStore.Data;
            DialoguePlayback.TryStartDialogueSequence(
                new[] { new DialogueStorySegment(dialogueInk, path) }, participants, featureController,
                new DialoguePresentationOptions(onEnded: completed =>
                {
                    if (completed && isAffection && generation == dialogueGeneration && RunSessionStore.IsRunActive &&
                        ReferenceEquals(run, RunSessionStore.Data))
                        RunSessionStore.TryConsumeAffectionGain(npcData.id);
                }));
            return;
        }
        DialoguePlayback.TryStartDialogue(dialogueInk, participants, featureController);
    }

    public override InteractState GetInteractType() => InteractState.Talking;

    public override string GetInteractDescription() => GameText.Get("interaction.dialoguetrigger.interactprompttext", interactPromptText);

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
