using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 근처 상호작용 대상을 탐색하고 하이라이트/월드 프롬프트를 갱신한다.
/// - 플레이어의 상호작용 입력을 처리하고 현재 상호작용 상태 및 말풍선 출력을 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerInteractableTracker2D))]
[RequireComponent(typeof(PlayerInteractionTargetResolver2D))]
[RequireComponent(typeof(PlayerInteractionPromptPresenter))]
[RequireComponent(typeof(PlayerSpeechController))]
public class PlayerInteractor2D : MonoBehaviour, IPlayerInteractor
{
    private const string InteractBlockedTagResourcePath = "Tags/State.Interact.Blocked";

    public static PlayerInteractor2D Instance { get; private set; }

    public Transform Transform => transform;
    public InteractState CurrentState { get; private set; } = InteractState.Idle;

    [Header("Interaction")]
    [SerializeField] private WorldInteractionPromptController interactionPrompt;

    [Header("Speech System")]
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private PlayerSpeechData speechData;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag interactBlockedTag;

    [Header("Interaction Components")]
    [SerializeField] private PlayerInteractableTracker2D interactableTracker;
    [SerializeField] private PlayerInteractionSensor2D interactionSensor;
    [SerializeField] private PlayerInteractionTargetResolver2D targetResolver;
    [SerializeField] private PlayerInteractionPromptPresenter promptPresenter;
    [SerializeField] private PlayerSpeechController speechController;

    protected virtual void Awake()
    {
        Instance = this;

        ResolveComponents();
        MigrateLegacySerializedReferences();
        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();
        if (interactBlockedTag == null)
            interactBlockedTag = Resources.Load<GameplayTag>(InteractBlockedTagResourcePath);
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        
        PlayerRuntimeRegistry.Unregister(this);
    }

    public void SetInteractState(InteractState state)
    {
        Debug.Log($"[Player] SetInteractState: {CurrentState} -> {state}");
        CurrentState = state;

        if (state != InteractState.Idle)
        {
            targetResolver?.ClearCurrentTarget();
            promptPresenter?.HidePrompt();
        }
    }

    protected virtual void Update()
    {
        if (CurrentState != InteractState.Idle)
        {
            promptPresenter?.HidePrompt();
            return;
        }

        if (IsInteractionBlocked())
        {
            targetResolver?.ClearCurrentTarget();
            promptPresenter?.HidePrompt();
            return;
        }

        IInteractable currentTarget = RefreshInteractionTarget();
        promptPresenter?.RefreshPrompt(currentTarget, CurrentState);

        InputBindingService input = InputBindingService.EnsureInstance();
        if (input.WasPressedThisFrame(InputActionId.Interact) && currentTarget != null)
        {
            bool canInteract = currentTarget.CanInteract(this);
            Debug.Log($"[Player] currentTarget.CanInteract = {canInteract}");

            if (canInteract)
            {
                currentTarget.OnPlayerInteract(this);
                currentTarget = RefreshInteractionTarget();
                promptPresenter?.RefreshPrompt(currentTarget, CurrentState);
            }
        }
    }

    public void SpeakSituation(PlayerSpeechSituationEnum situation, float duration = 2f)
    {
        speechController?.SpeakSituation(situation, duration);
    }

    private IInteractable RefreshInteractionTarget()
    {
        if (targetResolver == null)
            return null;

        IInteractable previousTarget = targetResolver.CurrentTarget;
        IInteractable currentTarget = targetResolver.RefreshTarget(this, transform.position);

        if (!ReferenceEquals(previousTarget, currentTarget) && currentTarget != null)
            Debug.Log($"[Player] New currentTarget = {(currentTarget as MonoBehaviour)?.name}");

        return currentTarget;
    }

    private void ResolveComponents()
    {
        if (interactableTracker == null)
            interactableTracker = GetComponent<PlayerInteractableTracker2D>();
        if (interactableTracker == null)
            interactableTracker = gameObject.AddComponent<PlayerInteractableTracker2D>();

        Collider2D bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null)
            bodyCollider.isTrigger = false;

        interactionSensor = PlayerInteractionSensor2D.EnsureFor(transform, bodyCollider, interactableTracker);

        if (targetResolver == null)
            targetResolver = GetComponent<PlayerInteractionTargetResolver2D>();
        if (targetResolver == null)
            targetResolver = gameObject.AddComponent<PlayerInteractionTargetResolver2D>();

        if (promptPresenter == null)
            promptPresenter = GetComponent<PlayerInteractionPromptPresenter>();
        if (promptPresenter == null)
            promptPresenter = gameObject.AddComponent<PlayerInteractionPromptPresenter>();

        if (speechController == null)
            speechController = GetComponent<PlayerSpeechController>();
        if (speechController == null)
            speechController = gameObject.AddComponent<PlayerSpeechController>();
    }

    private void MigrateLegacySerializedReferences()
    {
        if (interactionPrompt == null)
            interactionPrompt = WorldInteractionPromptController.Instance ?? FindFirstObjectByType<WorldInteractionPromptController>();

        promptPresenter?.SetPromptController(interactionPrompt);
        speechController?.SetSpeechDependencies(speechBubble, speechData);
    }

    private bool IsInteractionBlocked()
    {
        return tagSystem != null &&
               interactBlockedTag != null &&
               tagSystem.HasTag(interactBlockedTag);
    }
}
