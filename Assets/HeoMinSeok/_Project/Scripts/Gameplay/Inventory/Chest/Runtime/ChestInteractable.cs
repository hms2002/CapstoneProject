using UnityEngine;

[RequireComponent(typeof(TreasureChest))]
public class ChestInteractable : MonoBehaviour, IInteractable
{
    [Header("프롬프트")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string openPromptText = "상자 열기";
    [SerializeField] private string lockedPromptFormat = "잠김 ({0})";

    private TreasureChest chest;
    private ChestMonsterKillLock killLock;

    private void Awake()
    {
        chest = GetComponent<TreasureChest>();
        killLock = GetComponent<ChestMonsterKillLock>();
    }

    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }

    public bool CanInteract(IPlayerInteractor player)
    {
        if (player == null || player.CurrentState != InteractState.Idle)
            return false;

        if (killLock != null && !killLock.IsUnlocked)
            return false;

        return true;
    }

    public InteractState GetInteractType() => InteractState.Shopping;

    public string GetInteractDescription()
    {
        if (killLock != null && !killLock.IsUnlocked)
            return string.Format(lockedPromptFormat, killLock.RemainingAliveCount);

        return openPromptText;
    }

    public void GetInteract(string text) { }
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (chest == null || player == null)
            return;

        if (!CanInteract(player))
            return;

        chest.Open();
        player.SetInteractState(InteractState.Shopping);
    }
}
