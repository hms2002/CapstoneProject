using UnityEngine;

/// <summary>
/// 책임 : 임시 소포 이벤트 방에서 NPC가 선택 상태를 소유하지 않고 배달 규칙만 안내하게 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ParcelGuideNpcInteractable : InteractableBase
{
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "대화하기";
    [SerializeField, TextArea] private string guideText =
        "옆의 상자 더미에서 소포를 가져가 주세요. 소포는 유물 슬롯을 차지하며 버릴 수 없습니다.";

    private void Awake()
    {
        Collider2D interactionCollider = GetComponent<Collider2D>();
        if (interactionCollider != null)
            interactionCollider.isTrigger = true;
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null && player.CurrentState == InteractState.Idle;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        WarningPopupPlayback.ShowMessage(guideText, 4f);
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
}
