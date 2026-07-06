/// <summary>
/// 책임 : gameplay/UI가 공유하는 재바인딩 가능한 입력 동작 식별자를 안정적인 serialized enum 값으로 정의한다.
/// </summary>
public enum InputActionId
{
    MoveUp = 0,
    MoveDown = 1,
    MoveLeft = 2,
    MoveRight = 3,
    PrimaryAttack = 4,
    Interact = 5,
    Dash = 6,
    Skill1 = 7,
    Skill2 = 8,
    SwapWeapon = 9,
    ConsumableSlot1 = 10,
    ConsumableSlot2 = 11,
    ConsumableSlot3 = 12,
    ConsumableSlot4 = 13,
    InventoryToggle = 14,
    DialogueAdvance = 15,
}
