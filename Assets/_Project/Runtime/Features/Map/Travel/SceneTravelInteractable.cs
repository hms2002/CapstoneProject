using UnityEngine;

/// <summary>
/// 책임 : 플레이어 상호작용을 같은 GameObject의 SceneTravelEndpoint 이동 요청으로 변환하고 로컬 강조 표시를 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SceneTravelEndpoint))]
public sealed class SceneTravelInteractable : InteractableBase
{
    [SerializeField] private SceneTravelEndpoint endpoint;
    [SerializeField] private string interactPromptText = "이동하기";
    [SerializeField] private GameObject highlightTarget;

    private void Awake()
    {
        if (endpoint == null)
            endpoint = GetComponent<SceneTravelEndpoint>();

        OnUnHighlight();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return endpoint != null &&
               !endpoint.IsTravelReserved &&
               player != null &&
               player.CurrentState == InteractState.Idle &&
               !(SceneFadeTransitionPlayback.Instance?.IsTransitionActive ?? false);
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        SceneTravelPlayback.TryTravel(endpoint, player, SceneTravelActivationKind.Interaction);
    }

    public override void OnHighlight()
    {
        if (highlightTarget != null)
            highlightTarget.SetActive(true);
    }

    public override void OnUnHighlight()
    {
        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public override void OnPlayerLeave()
    {
        OnUnHighlight();
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => endpoint != null ? endpoint.DepartureAnchor : transform;
}
