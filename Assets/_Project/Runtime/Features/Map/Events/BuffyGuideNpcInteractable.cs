using UnityEngine;

/// <summary>
/// 책임 : 버피 이벤트 방에서 보상 선택 상태를 소유하지 않고 운동기구 이용 방법만 안내한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class BuffyGuideNpcInteractable : InteractableBase
{
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "대화하기";
    [SerializeField, TextArea] private string guideText =
        "운동기구 세 개 중 하나를 사용해! 근력은 공격력, 바퀴는 이동속도, 통나무는 경험치를 올려줘!";

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
