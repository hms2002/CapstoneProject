using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임: 유해 상호작용이 어떤 보상 정책을 사용할지 구분한다.
/// </summary>
public enum GraveType { Weapon, Relic, JunkWeapon, JunkRelic }

public class GraveInteractable : InteractableBase
{
    private static readonly SoundRef OpenSound = SoundRef.FromKey("sound_grave_Open");

    [Header("유해 설정")]
    public GraveType graveType;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "조사하기";
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("이펙트")]
    public GameObject destroyEffect;

    private MaterialPropertyBlock propBlock;
    private readonly HashSet<Object> guidanceOwners = new();
    private bool interactionHighlighted;

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private bool isLooted;

    [HideInInspector] public int bonusMinDropCount;
    [HideInInspector] public int bonusMaxDropCount;
    [HideInInspector] public float bonusRareChance;
    [HideInInspector] public float bonusEpicChance;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    public override void OnHighlight()
    {
        interactionHighlighted = true;
        RefreshOutline();
    }

    public override void OnUnHighlight()
    {
        interactionHighlighted = false;
        RefreshOutline();
    }

    public void SetGuidanceHighlight(Object owner, bool enabled)
    {
        if (owner == null) return;
        if (enabled) guidanceOwners.Add(owner);
        else guidanceOwners.Remove(owner);
        RefreshOutline();
    }

    private void RefreshOutline()
    {
        if (spriteRenderer == null) return;
        propBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, isActiveAndEnabled && !isLooted &&
            (interactionHighlighted || guidanceOwners.Count > 0) ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    private void OnDisable()
    {
        guidanceOwners.Clear();
        interactionHighlighted = false;
        RefreshOutline();
    }

    public override bool CanInteract(IPlayerInteractor player) => !isLooted && player != null && player.CurrentState == InteractState.Idle;
    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player)) return;

        isLooted = true;
        OnUnHighlight();

        SoundPlaybackUtility.Play(OpenSound, causer: gameObject, position: transform.position, sourceObject: this);

        if (LootManager.Instance != null)
            LootManager.Instance.SpawnGraveLoot(transform.position, graveType, bonusMinDropCount, bonusMaxDropCount, bonusRareChance, bonusEpicChance);

        if (destroyEffect != null)
            Instantiate(destroyEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
