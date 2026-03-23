using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 : 플레이어 상호작용을 통해 씬 전이 컨텍스트를 준비하고,
/// 필요 시 현재 플레이어 런타임 상태를 캡처한 뒤 대상 씬으로 이동시키는 포털 역할을 담당한다.
/// </summary>
public sealed class ScenePortal : MonoBehaviour, IInteractable
{
    [Header("Scene")]
    [SerializeField] private string targetSceneName;

    [Header("Spawn")]
    [SerializeField] private string exitPointId;
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

    [Header("Optional Visual")]
    [SerializeField] private GameObject highlightTarget;
    [Header("Cleanup Before Capture")]
    [SerializeField] private List<GameplayTagSet> sceneTravelCleanupTagSets = new();

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

    public void GetInteract(string text)
    {
    }

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

    public InteractState GetInteractType()
    {
        return InteractState.Idle;
    }

    public string GetInteractDescription()
    {
        return "이동하기";
    }

    /// <summary>
    /// 책임 : 씬 이동 직전 전이 컨텍스트와 플레이어 런타임 상태를 준비하고,
    /// 대상 씬 로드를 수행한다.
    /// </summary>
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
            CaptureAndStorePlayerRuntimeState(gameplay);
        }

        if (startRunOnTravel)
            gameplay.StartRun();

        if (endRunOnTravel)
            gameplay.EndRun(runEndReason);

        var ctx = new SceneTransitionContext
        {
            fromScene = SceneManager.GetActiveScene().name,
            toScene = targetSceneName,
            exitPointId = exitPointId,
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

    /// <summary>
    /// 책임 : 씬 이동 직전 현재 플레이어의 전투/행동 중간 상태를 먼저 정리한 뒤,
    /// 최신 캡처 파이프라인으로 런타임 상태를 저장한다.
    /// </summary>
    private void CaptureAndStorePlayerRuntimeState(GamePlayDataManager gameplay)
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null)
        {
            Debug.LogWarning($"[ScenePortal:{name}] Player 태그 오브젝트를 찾지 못해 런타임 상태를 저장하지 못했습니다.");
            return;
        }

        CleanupBeforeCapture(playerGo);

        var captureBridge = playerGo.GetComponent<PlayerRuntimeCaptureBridge>();
        if (captureBridge == null)
        {
            Debug.LogWarning($"[ScenePortal:{name}] PlayerRuntimeCaptureBridge가 없어 런타임 상태를 저장하지 못했습니다.", playerGo);
            return;
        }

        var state = captureBridge.CaptureRuntimeState();
        gameplay.PreparePlayerState(state);

        Debug.Log($"[ScenePortal:{name}] PlayerRuntimeState captured by PlayerRuntimeCaptureBridge");
    }
    /// <summary>
    /// 책임 : 런타임 상태 캡처 전에 현재 플레이어의 ability 실행 상태와
    /// 씬 이동 시 유지되면 안 되는 일시 태그를 정리한다.
    /// </summary>
    private void CleanupBeforeCapture(GameObject playerGo)
    {
        if (playerGo == null)
            return;

        var abilitySystem = playerGo.GetComponent<UnityGAS.AbilitySystem>();
        if (abilitySystem != null)
        {
            abilitySystem.CancelAllForSceneTransition(sceneTravelCleanupTagSets);
        }
    }
}