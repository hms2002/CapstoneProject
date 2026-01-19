public interface IInteractable
{
    void OnPlayerNearby();
    void OnPlayerLeave();
    void OnPlayerInteract(TempPlayer player);
    void GetInteract(string text);
    void OnHighlight();
    void OnUnHighlight();
    bool CanInteract(TempPlayer player);
    InteractState GetInteractType();
    string GetInteractDescription();
}

public enum InteractState
{
    Idle,      // 자유 이동 상태
    Talking,   // 대화 중 (이동 불가)
    Shopping,  // 상점 이용 중
    None
}