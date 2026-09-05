using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임 : 소모되지 않는 상자 더미에서 상호작용할 때마다 소포 하나를 유물 인벤토리에 지급한다.
/// 상자 더미의 시각 상태는 바꾸지 않으며 보유 한도와 빈 슬롯만 검사한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ParcelPickupInteractable : InteractableBase
{
    private const string EventId = "parcel_delivery";
    private const string DeliveryFollowUpId = "parcel_delivery_destination";
    private static readonly SoundRef PickupSound = SoundRef.FromKey("sound_worldDropItem_GetItem");
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

    [SerializeField] private ParcelRelicDefinition parcelDefinition;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "소포 가져가기";
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

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               parcelDefinition != null;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (parcelDefinition == null || player is not Component playerComponent)
            return;

        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        if (inventory == null)
            return;

        RelicInventory.AcquireResult result = inventory.TryAcquireParcelDetailed(parcelDefinition);
        if (result == RelicInventory.AcquireResult.Success)
        {
            int parcelCount = inventory.CountRelicsOfType<ParcelRelicDefinition>();
            if (!RunMapEventProgress.QueueNextUnvisitedBossRouteFollowUp(
                    EventId,
                    DeliveryFollowUpId,
                    parcelCount))
            {
                inventory.RemoveOne(parcelDefinition);
                WarningPopupPlayback.ShowMessage("다음 배송 지점을 예약할 수 없습니다.");
                Debug.LogWarning("[ParcelDelivery] Failed to queue the next-route delivery room.", this);
                return;
            }

            SoundPlaybackUtility.Play(PickupSound, causer: gameObject, position: transform.position, sourceObject: this);
            WarningPopupPlayback.ShowMessage($"소포를 획득했습니다. ({parcelCount}/{ParcelRelicDefinition.MaximumCarryCount})");
            return;
        }

        if (result == RelicInventory.AcquireResult.ParcelCarryLimitReached)
        {
            WarningPopupPlayback.ShowMessage($"소포는 최대 {ParcelRelicDefinition.MaximumCarryCount}개까지 운반할 수 있습니다.");
            return;
        }

        WarningPopupCode warningCode = InventoryDeliveryWarningResolver.FromRelicAcquireResult(result);
        if (warningCode != WarningPopupCode.None)
            WarningPopupPlayback.Show(warningCode);
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription() =>
        parcelDefinition != null ? interactPromptText : string.Empty;

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
