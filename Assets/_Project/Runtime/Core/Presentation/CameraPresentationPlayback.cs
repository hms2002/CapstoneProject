using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 : Gameplay/Core 계층이 구체 카메라 연출 구현 없이 보스/컷씬 카메라 시퀀스를 요청하게 하는 계약이다.
/// </summary>
public interface ICameraPresentationDirector
{
    IEnumerator FocusBossRoutine();
    IEnumerator FocusBossWithDeathLensRoutine();
    IEnumerator FocusTargetWithDeathLensRoutine(Transform target);
    IEnumerator FocusBossWithPhaseLensRoutine();
    void BindBossCameraTarget(Transform target);
    IEnumerator ReturnToPlayerRoutine();
    void RestoreDefaultState();
    void BeginBossFocusWithDeathLens();
    void BeginBossFocusWithPhaseLens();
    IEnumerator PlayBossPhasePresentationRoutine(float holdSeconds);
}

/// <summary>
/// 책임 : 레거시 조우 코드가 Cinemachine 타입에 Core 계약을 묶지 않고 카메라 연출 설정을 전달하게 하는 계약이다.
/// </summary>
public interface ICameraPresentationSettingsReceiver
{
    void ApplyPresentationSettings(
        Component playerCamera,
        Component bossCamera,
        int defaultPriority,
        int highlightedPriority,
        bool disableLegacyFollow,
        float blendFallbackSeconds);
}

/// <summary>
/// 책임 : 구체 카메라 연출 컴포넌트를 생성해야 하는 레거시 경로를 Presentation backend로 위임한다.
/// </summary>
public interface ICameraPresentationFactoryBackend
{
    ICameraPresentationDirector GetOrAdd(GameObject host);
}

/// <summary>
/// 책임 : Gameplay 호출자가 Presentation 구현체 타입 없이 카메라 연출 계약을 조회하고 실행하게 한다.
/// </summary>
public static class CameraPresentationPlayback
{
    private static ICameraPresentationFactoryBackend factoryBackend;

    public static void RegisterFactoryBackend(ICameraPresentationFactoryBackend backend)
    {
        factoryBackend = backend;
    }

    public static ICameraPresentationDirector FromBehaviour(MonoBehaviour behaviour)
    {
        return behaviour as ICameraPresentationDirector;
    }

    public static ICameraPresentationSettingsReceiver AsSettingsReceiver(MonoBehaviour behaviour)
    {
        return behaviour as ICameraPresentationSettingsReceiver;
    }

    public static ICameraPresentationDirector Get(Component owner)
    {
        if (owner == null)
            return null;

        if (owner is ICameraPresentationDirector directDirector)
            return directDirector;

        MonoBehaviour[] behaviours = owner.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICameraPresentationDirector director)
                return director;
        }

        return null;
    }

    public static ICameraPresentationDirector GetOrAdd(GameObject host)
    {
        if (host == null)
            return null;

        ICameraPresentationDirector existing = Get(host.transform);
        if (existing != null)
            return existing;

        return factoryBackend?.GetOrAdd(host);
    }

    public static ICameraPresentationDirector FindAny(bool includeInactive = true)
    {
        FindObjectsInactive inactiveMode = includeInactive
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
            inactiveMode,
            FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICameraPresentationDirector director)
                return director;
        }

        return null;
    }
}
