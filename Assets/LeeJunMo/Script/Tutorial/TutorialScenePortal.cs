using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TutorialScenePortal : InteractableBase
{
    [Header("Target")]
    [SerializeField] private string targetSceneName = "DarkLord_Tutorial";

    [Header("Interact")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "Move";

    [Header("Runtime State")]
    [SerializeField, HideInInspector] private bool preservePlayerRuntimeState = true;
    [SerializeField, HideInInspector] private bool prepareTransitionContext = true;
    [SerializeField] private bool resetPlayerRuntimeStateOnTravel;
    [SerializeField] private bool skipTransitionContextPreparation;

    [Header("Optional Visual")]
    [SerializeField] private GameObject highlightTarget;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private bool isTransitioning;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

    private void Awake()
    {
        NormalizeLegacyRuntimeStateFlags();
        propertyBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void Reset()
    {
        Collider2D portalCollider = GetComponent<Collider2D>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        NormalizeLegacyRuntimeStateFlags();
        resetPlayerRuntimeStateOnTravel = false;
        skipTransitionContextPreparation = false;
    }

    private void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        NormalizeLegacyRuntimeStateFlags();
    }

    public override void OnPlayerLeave()
    {
        OnUnHighlight();
    }

    public override void OnHighlight()
    {
        SetOutline(true);

        if (highlightTarget != null)
            highlightTarget.SetActive(true);
    }

    public override void OnUnHighlight()
    {
        SetOutline(false);

        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return !isTransitioning &&
               player != null &&
               player.CurrentState == InteractState.Idle &&
               !string.IsNullOrWhiteSpace(targetSceneName);
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        isTransitioning = true;
        OnUnHighlight();
        player.SetInteractState(InteractState.None);

        GamePlayDataManager gameplay = GamePlayDataManager.Instance;
        PlayerRuntimeState previousPlayerState = gameplay != null
            ? gameplay.PeekPendingPlayerState()
            : null;
        SceneTransitionContext previousTransitionContext = gameplay != null
            ? gameplay.PeekPendingTransition()
            : null;
        bool capturedPlayerState = TryCapturePlayerRuntimeState(player, gameplay);
        bool preparedTransitionContext = TryPrepareTransitionContext(gameplay);

        if (!TryLoadTargetScene())
        {
            RestorePendingRuntimeState(
                gameplay,
                previousPlayerState,
                previousTransitionContext,
                capturedPlayerState,
                preparedTransitionContext);
            isTransitioning = false;
            player.SetInteractState(InteractState.Idle);
        }
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private bool TryLoadTargetScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[TutorialScenePortal] Target scene name is empty.", this);
            return false;
        }

        SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.EnsureInstance();
        if (transitionCoordinator != null)
        {
            if (transitionCoordinator.TryLoadScene(targetSceneName))
                return true;

            if (transitionCoordinator.IsTransitionActive)
            {
                Debug.LogWarning(
                    $"[TutorialScenePortal] Scene transition is already active. target={targetSceneName}",
                    this);
                return false;
            }
        }

        try
        {
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[TutorialScenePortal] Failed to load scene '{targetSceneName}': {ex.Message}",
                this);
            return false;
        }
    }

    private bool TryCapturePlayerRuntimeState(IPlayerInteractor player, GamePlayDataManager gameplay)
    {
        if (resetPlayerRuntimeStateOnTravel)
            return false;

        if (gameplay == null)
        {
            Debug.LogWarning("[TutorialScenePortal] GamePlayDataManager is missing. Player runtime state was not preserved.", this);
            return false;
        }

        GameObject playerObject = ResolvePlayerObject(player);
        if (playerObject == null)
        {
            Debug.LogWarning("[TutorialScenePortal] Player object was not found. Player runtime state was not preserved.", this);
            return false;
        }

        CleanupPlayerBeforeCapture(playerObject);

        PlayerRuntimeCaptureBridge captureBridge = playerObject.GetComponent<PlayerRuntimeCaptureBridge>();
        if (captureBridge == null)
        {
            Debug.LogWarning("[TutorialScenePortal] PlayerRuntimeCaptureBridge is missing. Player runtime state was not preserved.", playerObject);
            return false;
        }

        gameplay.PreparePlayerState(captureBridge.CaptureRuntimeState());
        return true;
    }

    private bool TryPrepareTransitionContext(GamePlayDataManager gameplay)
    {
        if (skipTransitionContextPreparation || gameplay == null || string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        var context = new SceneTransitionContext
        {
            fromScene = gameObject.scene.IsValid()
                ? gameObject.scene.name
                : SceneManager.GetActiveScene().name,
            toScene = targetSceneName,
            exitPointId = gameObject.name,
            entryPointId = "Default",
            transitionType = TransitionType.None
        };

        gameplay.PrepareTransition(context);
        return true;
    }

    private static void RestorePendingRuntimeState(
        GamePlayDataManager gameplay,
        PlayerRuntimeState previousPlayerState,
        SceneTransitionContext previousTransitionContext,
        bool capturedPlayerState,
        bool preparedTransitionContext)
    {
        if (gameplay == null)
            return;

        if (capturedPlayerState)
            gameplay.PreparePlayerState(previousPlayerState);

        if (preparedTransitionContext)
            gameplay.PrepareTransition(previousTransitionContext);
    }

    private static GameObject ResolvePlayerObject(IPlayerInteractor player)
    {
        if (player?.Transform != null)
            return player.Transform.gameObject;

        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (registeredPlayer != null)
            return registeredPlayer.gameObject;

        return GameObject.FindGameObjectWithTag("Player");
    }

    private static void CleanupPlayerBeforeCapture(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        AbilitySystem abilitySystem = playerObject.GetComponent<AbilitySystem>();
        abilitySystem?.CancelAllForSceneTransition();
    }

    private void NormalizeLegacyRuntimeStateFlags()
    {
        if (!preservePlayerRuntimeState)
            preservePlayerRuntimeState = true;

        if (!prepareTransitionContext)
            prepareTransitionContext = true;
    }

    private void SetOutline(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
