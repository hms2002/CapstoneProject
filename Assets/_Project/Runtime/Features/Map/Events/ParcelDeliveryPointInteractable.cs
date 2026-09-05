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
    }

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
        WarningPopupPlayback.ShowMessage($"소포 {deliveredCount}개를 배송하고 Epic 유물 {rewardedCount}개를 받았습니다.");
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => parcelDefinition != null ? interactPromptText : string.Empty;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
    public override void OnHighlight() => SetOutline(true);
    public override void OnUnHighlight() => SetOutline(false);
    public override void OnPlayerLeave() => OnUnHighlight();

    private void SetOutline(bool enabled)
    {
        if (outlinePropertyBlock == null || highlightedRenderers == null)
            return;

        for (int i = 0; i < highlightedRenderers.Length; i++)
        {
            SpriteRenderer renderer = highlightedRenderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
}
