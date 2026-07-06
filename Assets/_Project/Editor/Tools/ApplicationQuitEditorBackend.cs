using UnityEditor;

/// <summary>
/// 책임 : Editor Play Mode에서 런타임 종료 요청을 Unity Editor 재생 종료로 변환한다.
/// </summary>
[InitializeOnLoad]
public sealed class ApplicationQuitEditorBackend : IApplicationQuitBackend
{
    private static readonly ApplicationQuitEditorBackend Instance = new ApplicationQuitEditorBackend();

    static ApplicationQuitEditorBackend()
    {
        ApplicationQuitPlayback.RegisterBackend(Instance);
    }

    public bool TryQuitApplication()
    {
        EditorApplication.isPlaying = false;
        return true;
    }
}
