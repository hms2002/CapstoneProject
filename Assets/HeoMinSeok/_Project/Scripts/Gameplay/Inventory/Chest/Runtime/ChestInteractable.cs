using UnityEngine;

/// <summary>
/// 책임 : 플레이어의 상자 상호작용을 처리하고,
/// 상자가 현재 열릴 수 있는 상태인지 검사한 뒤 실제 개방을 요청한다.
/// </summary>
[RequireComponent(typeof(TreasureChest))]
public class ChestInteractable : MonoBehaviour, IInteractable
{
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
            return $"잠김 ({killLock.RemainingAliveCount})";

        return "상자 열기";
    }

    public void GetInteract(string text) { }

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