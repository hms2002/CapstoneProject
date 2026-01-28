using UnityEngine;

public abstract class ShortcutBase : MonoBehaviour, IInteractable
{
    [Header("타겟 문")]
    public DoorObject targetDoor;
    public Transform uiPopupPoint;

    // [수정] 문 찾기는 Awake에서 (자식의 Start보다 먼저)
    protected virtual void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();
    }

    // IInteractable 구현
    public void OnPlayerInteract(TempPlayer player)
    {
        if (targetDoor != null && targetDoor.IsOpen) return;

        if (CheckCondition(player)) OnSuccess();
        else OnFail();
    }

    public virtual bool CanInteract(TempPlayer player) => targetDoor != null && !targetDoor.IsOpen;

    // UI 관련 (사용자 구현 필요 시 채우기)
    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }
    public InteractState GetInteractType() => InteractState.Idle;
    public void GetInteract(string text) { }

    public abstract string GetInteractDescription();

    // 추상 메서드
    protected abstract bool CheckCondition(TempPlayer player);
    protected abstract void OnSuccess(); // 영구/일시적 분기점

    protected virtual void OnFail()
    {
        if (targetDoor != null) targetDoor.PlayShakeAnimation();
    }
}