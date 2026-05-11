/// <summary>
/// 이 인터페이스의 책임:
/// HUD 버튼이나 단축키 안내 UI가 인벤토리 구현체를 직접 알지 않고 "열기 요청"만 보낼 수 있게 한다.
/// </summary>
public interface IInventoryOpenRequestHandler
{
    bool CanOpenInventory { get; }
    bool IsInventoryOpen { get; }
    bool TryOpenInventory();
}
