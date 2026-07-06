/// <summary>
/// 책임 : 타이틀 씬 복귀 시 DontDestroyOnLoad 영역에서 제거할 persistent UI 객체를 Infrastructure에 타입명 없이 표시한다.
/// </summary>
public interface ITitleScenePersistentCleanupTarget
{
}

/// <summary>
/// 책임 : 전역 UI 루트 자신을 구체 GlobalUIRoot 타입 없이 식별하게 하는 marker 계약이다.
/// </summary>
public interface IGlobalCanvasRootMarker
{
}
