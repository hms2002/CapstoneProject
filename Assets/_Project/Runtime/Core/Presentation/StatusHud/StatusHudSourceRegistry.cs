using System.Collections.Generic;

/// <summary>
/// 책임 :
/// - gameplay owner에 붙은 상태 HUD source의 등록/해제 목록을 Core 계약 계층에서 보관한다.
/// - UI 서비스가 gameplay source 컴포넌트를 직접 소유하지 않고도 현재 상태 HUD 엔트리를 수집하게 한다.
/// </summary>
public static class StatusHudSourceRegistry
{
    private static readonly HashSet<IStatusHudSource> Sources = new();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Sources.Clear();
    }

    public static void RegisterSource(IStatusHudSource source)
    {
        if (source != null)
            Sources.Add(source);
    }

    public static void UnregisterSource(IStatusHudSource source)
    {
        if (source != null)
            Sources.Remove(source);
    }

    public static void CollectEntries(List<StatusHudEntry> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        if (Sources.Count == 0)
            return;

        foreach (IStatusHudSource source in Sources)
            source?.CollectStatusHudEntries(buffer);
    }
}
