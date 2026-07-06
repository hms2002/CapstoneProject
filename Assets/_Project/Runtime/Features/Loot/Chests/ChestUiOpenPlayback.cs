/// <summary>
/// 책임 : Loot/Chest 계층이 구체 상자 UI 구현 없이 상자 UI 열기를 요청하게 하는 backend 계약이다.
/// </summary>
public interface IChestUiOpenBackend
{
    bool OpenChest(
        TreasureChest chest,
        bool playSlideFadePresentation,
        GameFlowInputBlocker inputBlocker);
}

/// <summary>
/// 책임 : TreasureChest가 UI 구현 타입을 참조하지 않고 현재 등록된 상자 UI backend에 열기 요청을 전달한다.
/// </summary>
public static class ChestUiOpenPlayback
{
    private static IChestUiOpenBackend backend;

    public static void RegisterBackend(IChestUiOpenBackend openBackend)
    {
        backend = openBackend;
    }

    public static bool OpenChest(
        TreasureChest chest,
        bool playSlideFadePresentation = true,
        GameFlowInputBlocker inputBlocker = null)
    {
        return backend != null &&
               backend.OpenChest(chest, playSlideFadePresentation, inputBlocker);
    }
}
