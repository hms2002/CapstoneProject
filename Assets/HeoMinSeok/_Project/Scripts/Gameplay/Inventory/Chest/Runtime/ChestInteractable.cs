using UnityEngine;

[RequireComponent(typeof(TreasureChest))]
public class ChestInteractable : MonoBehaviour, IInteractable
{
    private TreasureChest chest;

    private void Awake() => chest = GetComponent<TreasureChest>();

    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }

    public void OnHighlight() { }
    public void OnUnHighlight() { }

    public bool CanInteract(IPlayerInteractor player)
        => player != null && player.CurrentState == InteractState.Idle;

    public InteractState GetInteractType() => InteractState.Shopping;

    public string GetInteractDescription() => "상자 열기";

    public void GetInteract(string text) { }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (chest == null || player == null) return;

        ChestUIManager.Instance.OpenChest(chest);
        player.SetInteractState(InteractState.Shopping);
    }
}
