// 2. ESC로 닫히는 '팝업(스택) UI' 전용 인터페이스
/// <summary>
/// 책임 :
/// - UIManager가 팝업 UI의 열기/닫기, 동시 오픈 가능 여부, 게임플레이 잠금 정책을 일관되게 다룰 수 있도록 규약을 제공한다.
/// </summary>
public enum UIOpenGroup
{
    None = 0,
    ExclusiveModal = 1 << 0,
    Overlay = 1 << 1,
    PassiveHud = 1 << 2
}

/// <summary>
/// 책임 :
/// - UI가 활성화된 동안 게임 진행과 플레이어 조작에 어떤 영향을 주는지 표현한다.
/// </summary>
public enum UIGameplayLockProfile
{
    None = 0,
    BlockControlOnly = 1,
    FreezeAndBlockControl = 2
}

public interface IStackableUI : IUIView
{
    // true면 ESC로 닫힘, false면 ESC를 무시함 (예: 강화 연출 중, 보상 선택 강제 등)
    bool CanCloseOnEscape { get; }

    // 이 UI가 속한 오픈 그룹
    UIOpenGroup OpenGroup { get; }

    // 이 UI가 열릴 때 동시에 열리면 안 되는 그룹
    UIOpenGroup BlockedOpenGroups { get; }

    // 이 UI가 열려 있는 동안 적용할 게임플레이 잠금 정책
    UIGameplayLockProfile GameplayLockProfile { get; }
}
