using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
// 책임: 런 시간 제한 종료 이벤트를 허브 복귀 씬 전환으로 연결한다.
public sealed class RunTimeOverReturnToHub : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private string hubSceneName = "ProtoTypeHub";
    [SerializeField] private bool useFadeTransitionService = true;

    private bool isHandlingTransition;
    private RunTimeLimitSystem boundTimeLimitSystem;

    private void OnEnable()
    {
        RunSessionStore.OnRunStarted += HandleRunStarted;

        RunTimeLimitSystem.InstanceChanged += HandleTimeLimitSystemChanged;
        BindTimeLimitSystem(RunTimeLimitSystem.Instance);
    }

    private void OnDisable()
    {
        RunSessionStore.OnRunStarted -= HandleRunStarted;

        RunTimeLimitSystem.InstanceChanged -= HandleTimeLimitSystemChanged;
        BindTimeLimitSystem(null);
    }

    private void HandleRunStarted()
    {
        isHandlingTransition = false;
    }

    private void HandleTimeLimitSystemChanged(RunTimeLimitSystem system)
    {
        BindTimeLimitSystem(system);
    }

    private void BindTimeLimitSystem(RunTimeLimitSystem system)
    {
        if (boundTimeLimitSystem == system)
            return;

        if (boundTimeLimitSystem != null)
            boundTimeLimitSystem.OnTimeExpired -= HandleTimeExpired;

        boundTimeLimitSystem = system;

        if (boundTimeLimitSystem != null)
            boundTimeLimitSystem.OnTimeExpired += HandleTimeExpired;
    }

    private void HandleTimeExpired()
    {
        if (isHandlingTransition)
            return;

        if (string.IsNullOrWhiteSpace(hubSceneName))
        {
            Debug.LogWarning("[RunTimeOverReturnToHub] Hub scene name is empty. TimeOver transition was skipped.", this);
            return;
        }

        isHandlingTransition = true;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        PlayerDeathReturnToHub2D deathReturn = playerTransform != null
            ? playerTransform.GetComponent<PlayerDeathReturnToHub2D>()
            : null;

        if (deathReturn != null)
        {
            if (deathReturn.TryStartTimeOverSequence(hubSceneName, useFadeTransitionService))
                return;

            Debug.LogWarning(
                "[RunTimeOverReturnToHub] Player death return sequence is already running. TimeOver fallback was skipped.",
                this);
            return;
        }

        Debug.LogWarning(
            "[RunTimeOverReturnToHub] Player death return component is missing. Falling back to immediate TimeOver return.",
            this);
        FallbackReturnToHub();
    }

    private void FallbackReturnToHub()
    {
        UiCommandPlayback.CloseAllPopups();
        UiCommandPlayback.HideHoverImmediate();
        UiCommandPlayback.HideWorldPrompt();

        RunSessionStore.EndRun(RunEndReason.TimeOver);

        if (useFadeTransitionService)
        {
            ISceneTransitionHandle transitionCoordinator = SceneTransitionPlayback.Instance;
            if (transitionCoordinator != null && transitionCoordinator.TryLoadScene(hubSceneName))
                return;
        }

        SceneManager.LoadScene(hubSceneName);
    }
}
