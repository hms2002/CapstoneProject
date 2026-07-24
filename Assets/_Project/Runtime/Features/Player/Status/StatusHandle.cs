/// <summary>
/// 책임 :
/// - 외부 시스템이 자신이 부여한 상태를 다시 해제하거나 갱신할 때 사용하는 런타임 토큰을 표현한다.
/// - 상태 적용 허브의 내부 id를 직접 노출하지 않고, 부여한 쪽이 안전하게 자기 상태만 회수하게 만든다.
/// </summary>
public readonly struct StatusHandle
{
    private readonly PlayerStatusRuntime owner;

    public int RuntimeId { get; }
    public bool IsValid => owner != null && RuntimeId > 0;

    internal StatusHandle(PlayerStatusRuntime owner, int runtimeId)
    {
        this.owner = owner;
        RuntimeId = runtimeId;
    }

    public bool Release()
    {
        return owner != null && owner.Release(this);
    }
}
