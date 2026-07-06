using UnityEngine;

/// <summary>
/// 책임 : 선택 실패 화면 피드백 구현을 구체 UI 타입 없이 준비하게 하는 Core-level backend 계약이다.
/// </summary>
public interface IChoiceFailurePresentationBackend
{
    void PrepareChoiceFailurePresentation();
}

/// <summary>
/// 책임 : Infrastructure/Gameplay 코드가 선택 실패 UI 구현을 직접 참조하지 않고 scene 준비 요청을 전달하게 한다.
/// </summary>
public static class ChoiceFailurePresentationPlayback
{
    private static IChoiceFailurePresentationBackend backend;

    public static void RegisterBackend(IChoiceFailurePresentationBackend presentationBackend)
    {
        backend = presentationBackend;
    }

    public static void UnregisterBackend(IChoiceFailurePresentationBackend presentationBackend)
    {
        if (ReferenceEquals(backend, presentationBackend))
            backend = null;
    }

    public static void PrepareSceneInstance()
    {
        if (backend != null)
        {
            backend.PrepareChoiceFailurePresentation();
            return;
        }

        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IChoiceFailurePresentationBackend candidate)
                continue;

            RegisterBackend(candidate);
            candidate.PrepareChoiceFailurePresentation();
            return;
        }
    }
}
