namespace UnityGAS
{
    /// <summary>
    /// AbilitySystem이 "이동 중인지"를 판단하기 위한 선택적 인터페이스.
    /// 이동 컴포넌트(PlayerMovement2D 등)가 구현하면 canCastWhileMoving 규칙이 활성화된다.
    /// </summary>
    public interface IMovementStateProvider
    {
        bool IsMoving { get; }
    }
}
