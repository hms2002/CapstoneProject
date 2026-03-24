using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ScenePortal : MonoBehaviour, IInteractable
{
    [Header("Scene")]
    [SerializeField] private string targetSceneName;

    [Header("Spawn")]
    [SerializeField] private string entryPointId = "Default";

    [Header("Transition")]
    [SerializeField] private TransitionType transitionType = TransitionType.None;

    [Header("Policy Hints")]
    [SerializeField] private bool fullyHealPlayer;
    [SerializeField] private bool resetCooldowns;
    [SerializeField] private bool clearAllEffects;
    [SerializeField] private bool clearCombatOnlyEffects;

    [Header("Run Control")]
    [SerializeField] private bool startRunOnTravel;
    [SerializeField] private bool endRunOnTravel;
    [SerializeField] private RunEndReason runEndReason = RunEndReason.None;

    [Header("프롬프트")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "이동하기";

    [Header("Optional Visual")]
    [SerializeField] private GameObject highlightTarget;

    private bool isTransitioning;

    public void OnPlayerNearby()
    {
        Debug.Log($"[ScenePortal:{name}] Player nearby");
    }

    public void OnPlayerLeave()
    {
        Debug.Log($"[ScenePortal:{name}] Player leave");
        OnUnHighlight();
    }

    public void GetInteract(string text) { }

    public void OnHighlight()
    {
        Debug.Log($"[ScenePortal:{name}] Highlight");
        if (highlightTarget != null)
            highlightTarget.SetActive(true);
    }

    public void OnUnHighlight()
    {
        Debug.Log($"[ScenePortal:{name}] UnHighlight");
        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public bool CanInteract(IPlayerInteractor player)
    {
        bool result =
            !isTransitioning &&
            player != null &&
            player.CurrentState == InteractState.Idle &&
            !string.IsNullOrWhiteSpace(targetSceneName);

        Debug.Log($"[ScenePortal:{name}] CanInteract = {result}, isTransitioning={isTransitioning}, playerState={player?.CurrentState.ToString() ?? "null"}, targetScene={targetSceneName}");
        return result;
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        Debug.Log($"[ScenePortal:{name}] OnPlayerInteract called");

        if (!CanInteract(player))
        {
            Debug.LogWarning($"[ScenePortal:{name}] OnPlayerInteract blocked");
            return;
        }

        isTransitioning = true;
        player.SetInteractState(InteractState.None);
        Travel();
    }

    public InteractState GetInteractType() => InteractState.Idle;
    public string GetInteractDescription() => interactPromptText;
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private void Travel()
    {
        Debug.Log($"[ScenePortal:{name}] Travel start -> {targetSceneName}");

        var gameplay = GamePlayDataManager.Instance;
        if (gameplay == null)
        {
            Debug.LogError($"[ScenePortal:{name}] GamePlayDataManager is null");
            isTransitioning = false;
            return;
        }

        if (!endRunOnTravel)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                var facade = playerGo.GetComponent<PlayerSceneTransitionFacade>();
                if (facade != null)
                {
                    var state = facade.CaptureRuntimeState();
                    gameplay.PreparePlayerState(state);
                    Debug.Log($"[ScenePortal:{name}] PlayerRuntimeState captured");
                }
                else
                {
                    Debug.LogWarning("[ScenePortal] PlayerSceneTransitionFacade가 없음. 상태 복원 없이 이동함.");
                }
            }
        }

        if (startRunOnTravel)
            gameplay.StartRun();

        if (endRunOnTravel)
            gameplay.EndRun(runEndReason);

        var ctx = new SceneTransitionContext
        {
            fromScene = SceneManager.GetActiveScene().name,
            toScene = targetSceneName,
            entryPointId = entryPointId,
            transitionType = transitionType,
            fullyHealPlayer = fullyHealPlayer,
            resetCooldowns = resetCooldowns,
            clearAllEffects = clearAllEffects,
            clearCombatOnlyEffects = clearCombatOnlyEffects
        };

        Debug.Log($"[ScenePortal:{name}] PrepareTransition from={ctx.fromScene} to={ctx.toScene}, entryPointId={ctx.entryPointId}, type={ctx.transitionType}");

        gameplay.PrepareTransition(ctx);
        SceneManager.LoadScene(targetSceneName);
    }
}
