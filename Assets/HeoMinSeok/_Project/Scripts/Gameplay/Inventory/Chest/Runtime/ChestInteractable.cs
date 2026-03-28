using UnityEngine;

[RequireComponent(typeof(TreasureChest))]
public class ChestInteractable : InteractableBase
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

    public override bool CanInteract(IPlayerInteractor player)
    {
        if (player == null || player.CurrentState != InteractState.Idle)
            return false;

        if (killLock != null && !killLock.IsUnlocked)
            return false;

        return true;
    }

    public override InteractState GetInteractType() => InteractState.Shopping;

    public override string GetInteractDescription()
    {
        if (killLock != null && !killLock.IsUnlocked)
            return string.Format(lockedPromptFormat, killLock.RemainingAliveCount);

        return openPromptText;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (chest == null || player == null)
            return;

        if (!CanInteract(player))
            return;

        chest.Open();
        player.SetInteractState(InteractState.Shopping);
    }
}
