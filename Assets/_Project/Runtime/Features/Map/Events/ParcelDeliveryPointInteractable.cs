using UnityEngine;

/// <summary>
/// 책임 : 다음 일반 복도에 배치된 배송 지점에서 보유 중인 소포를 Epic 유물 바닥 드롭과 교환하고 이벤트를 완료한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ParcelDeliveryPointInteractable : InteractableBase
{
    private const string EventId = "parcel_delivery";
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

    [SerializeField] private ParcelRelicDefinition parcelDefinition;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "소포 배송하기";
    [SerializeField] private SpriteRenderer[] highlightedRenderers;
    [SerializeField] private SpriteRenderer guidanceArrow;

    private bool interactionHighlighted;
    private bool guidanceHighlighted;
    private PlayerInteractor2D guidancePlayer;
    private RelicInventory guidanceInventory;

    private MaterialPropertyBlock outlinePropertyBlock;

    private void Awake()
    {
        Collider2D interactionCollider = GetComponent<Collider2D>();
        if (interactionCollider != null)
            interactionCollider.isTrigger = true;

        if (highlightedRenderers == null || highlightedRenderers.Length == 0)
            highlightedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        outlinePropertyBlock = new MaterialPropertyBlock();
        OnUnHighlight();
        if (guidanceArrow != null) guidanceArrow.enabled = false;
    }

    private void OnDisable() => ClearGuidance();

    public override bool CanInteract(IPlayerInteractor player) =>
        player != null && player.CurrentState == InteractState.Idle && parcelDefinition != null;

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player) || player is not Component playerComponent)
            return;

        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        if (inventory == null)
            return;

        int parcelCount = inventory.CountRelicsOfType<ParcelRelicDefinition>();
        if (parcelCount <= 0)
        {
            WarningPopupPlayback.ShowMessage("배송할 소포가 없습니다.");
            return;
        }

        if (LootManager.Instance == null)
        {
            WarningPopupPlayback.ShowMessage("보상을 생성할 수 없어 소포를 배송하지 않았습니다.");
            return;
        }

        int rewardedCount = LootManager.Instance.SpawnRelicDropsByRarity(
            transform.position,
            ItemRarity.Epic,
            parcelCount);
        if (rewardedCount <= 0)
        {
            WarningPopupPlayback.ShowMessage("Epic 유물 보상을 생성할 수 없어 소포를 배송하지 않았습니다.");
            return;
        }

        int deliveredCount = 0;
        while (deliveredCount < rewardedCount && inventory.RemoveOne(parcelDefinition))
            deliveredCount++;

        if (deliveredCount <= 0)
            return;

        RunMapEventProgress.MarkEventCompleted(RunSessionStore.Data, EventId);
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => parcelDefinition != null ? interactPromptText : string.Empty;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
    public override void OnHighlight()
    {
        interactionHighlighted = true;
        SetOutline(true);
    }
    public override void OnUnHighlight()
    {
        interactionHighlighted = false;
        SetOutline(guidanceHighlighted);
    }
    public override void OnPlayerLeave() => OnUnHighlight();

    public static bool IsDeliveryRoom(RoomTemplateSO template)
    {
        if (template == null || template.LayoutData.roomType != RoomType.Event)
            return false;

        var placements = template.BuildData.objectPlacements;
        if (placements == null)
            return false;

        foreach (var placement in placements)
        {
            if (placement.prefab != null &&
                placement.prefab.GetComponentInChildren<ParcelDeliveryPointInteractable>(true) != null)
                return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        PlayerInteractor2D current = PlayerRuntimeRegistry.CurrentPlayer;
        if (guidancePlayer != current)
        {
            guidancePlayer = current;
            guidanceInventory = current != null ? current.GetComponent<RelicInventory>() : null;
        }

        bool visible = parcelDefinition != null && guidanceArrow != null &&
            guidancePlayer != null && guidancePlayer.isActiveAndEnabled &&
            guidancePlayer.gameObject.scene == gameObject.scene &&
            guidancePlayer.CurrentState == InteractState.Idle && guidanceInventory != null &&
            !DialoguePlayback.IsPlaying && !SceneTransitionPlayback.IsTransitionActive &&
            guidanceInventory.CountRelicsOfType<ParcelRelicDefinition>() > 0;
        if (guidanceHighlighted != visible)
        {
            guidanceHighlighted = visible;
            SetOutline(interactionHighlighted || guidanceHighlighted);
        }
        if (guidanceArrow == null) return;
        guidanceArrow.enabled = visible;
        if (!visible) return;

        float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime / 0.6f * Mathf.PI)) * 0.2f;
        guidanceArrow.transform.position = GetPromptAnchor().position + Vector3.up * (1.2f + bounce);
        guidanceArrow.transform.rotation = Quaternion.identity;
    }

    private void ClearGuidance()
    {
        guidanceHighlighted = false;
        guidancePlayer = null;
        guidanceInventory = null;
        OnUnHighlight();
        if (guidanceArrow != null) guidanceArrow.enabled = false;
    }

    private void SetOutline(bool enabled)
    {
        if (outlinePropertyBlock == null || highlightedRenderers == null)
            return;

        for (int i = 0; i < highlightedRenderers.Length; i++)
        {
            SpriteRenderer renderer = highlightedRenderers[i];
            if (renderer == null || renderer == guidanceArrow)
                continue;

            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
}
