using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RunTimeOverReturnToHub : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private string hubSceneName = "ProtoTypeHub";
    [SerializeField] private bool useFadeTransitionService = true;

    private bool isHandlingTransition;
    private RunTimeLimitSystem boundTimeLimitSystem;

    private void OnEnable()
    {
        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.OnRunStarted += HandleRunStarted;

        RunTimeLimitSystem.InstanceChanged += HandleTimeLimitSystemChanged;
        BindTimeLimitSystem(RunTimeLimitSystem.Instance);
    }

    private void OnDisable()
    {
        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.OnRunStarted -= HandleRunStarted;

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
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllPopups();
            UIManager.Instance.HideHoverImmediate();
            UIManager.Instance.HideWorldPrompt();
        }

        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.EndRun(RunEndReason.TimeOver);

        if (useFadeTransitionService)
        {
            SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.Instance;
            if (transitionCoordinator != null && transitionCoordinator.TryLoadScene(hubSceneName))
                return;
        }

        SceneManager.LoadScene(hubSceneName);
    }
}
