using UnityEngine;

public interface IPlayerInteractor
{
    Transform Transform { get; }
    InteractState CurrentState { get; }
    void SetInteractState(InteractState state);
}
public interface IInteractable
{
    void OnPlayerNearby();
    void OnPlayerLeave();
    void GetInteract(string text);
    void OnHighlight();
    void OnUnHighlight();
    bool CanInteract(IPlayerInteractor player);
    void OnPlayerInteract(IPlayerInteractor player);
    InteractState GetInteractType();
    string GetInteractDescription();
}

public enum InteractState
{
    Idle,      // ���� �̵� ����
    Talking,   // ��ȭ �� (�̵� �Ұ�)
    Shopping,  // ���� �̿� ��
    None
}