using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerInteractableTracker2D))]
[RequireComponent(typeof(PlayerInteractionTargetResolver2D))]
[RequireComponent(typeof(PlayerInteractionPromptPresenter))]
[RequireComponent(typeof(PlayerSpeechController))]
public class PlayerInteractor2D : MonoBehaviour, IPlayerInteractor
{
    public static PlayerInteractor2D Instance { get; private set; }

    public Transform Transform => transform;
    public InteractState CurrentState { get; private set; } = InteractState.Idle;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private WorldInteractionPromptController interactionPrompt;

    [Header("Speech System")]
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private PlayerSpeechData speechData;

    [Header("Interaction Components")]
    [SerializeField] private PlayerInteractableTracker2D interactableTracker;
    [SerializeField] private PlayerInteractionTargetResolver2D targetResolver;
    [SerializeField] private PlayerInteractionPromptPresenter promptPresenter;
    [SerializeField] private PlayerSpeechController speechController;

    protected virtual void Awake()
    {
        Instance = this;

        ResolveComponents();
        MigrateLegacySerializedReferences();
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

        IInteractable currentTarget = RefreshInteractionTarget();
        promptPresenter?.RefreshPrompt(currentTarget, CurrentState);

        if (Input.GetKeyDown(interactKey) && currentTarget != null)
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
}
