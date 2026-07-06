/// <summary>
/// 책임 : gameplay scene 진입 시 호감도 화면 피드백 구현을 준비하도록 요청하는 Core-level backend 계약을 제공한다.
/// </summary>
public interface IAffectionPresentationBackend
{
    void PrepareSceneInstance();
}

/// <summary>
/// 책임 : Infrastructure/Gameplay 코드가 concrete 호감도 UI 구현을 직접 참조하지 않고 준비 요청을 전달한다.
/// </summary>
public static class AffectionPresentationPlayback
{
    private static IAffectionPresentationBackend backend;

    public static void RegisterBackend(IAffectionPresentationBackend presentationBackend)
    {
        backend = presentationBackend;
    }

    public static void PrepareSceneInstance()
    {
        backend?.PrepareSceneInstance();
    }
}
