using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SampleTopDownPlayer : MonoBehaviour, IPlayerInteractor
{
    public static SampleTopDownPlayer Instance { get; private set; }

    public Transform Transform => transform;
    public InteractState CurrentState { get; private set; } = InteractState.Idle;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private WorldInteractionPromptController interactionPrompt;

    [Header("Speech System")]
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private PlayerSpeechData speechData;

    private readonly List<IInteractable> nearbyObjects = new();
    private readonly Dictionary<IInteractable, int> nearbyOverlapCounts = new();
    private IInteractable currentTarget;

    private void Awake()
    {
        Instance = this;

        if (interactionPrompt == null)
            interactionPrompt = WorldInteractionPromptController.Instance ?? FindFirstObjectByType<WorldInteractionPromptController>();
    }

    private void OnDestroy()
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
            ClearCurrentTarget();
            if (UIManager.Instance != null)
                UIManager.Instance.HideWorldPrompt();
            else
                interactionPrompt?.Hide();
        }
    }

    private void Update()
    {
        if (CurrentState != InteractState.Idle)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.HideWorldPrompt();
            else
                interactionPrompt?.Hide();
            return;
        }

        HandleInteractSearch();
        RefreshInteractionPrompt();

        if (Input.GetKeyDown(interactKey) && currentTarget != null)
        {
            bool canInteract = currentTarget.CanInteract(this);
            Debug.Log($"[Player] currentTarget.CanInteract = {canInteract}");

            if (canInteract)
            {
                currentTarget.OnPlayerInteract(this);
                HandleInteractSearch();
                RefreshInteractionPrompt();
            }
        }
    }

    public void SpeakSituation(PlayerSpeechSituationEnum situation, float duration = 2f)
    {
        if (speechData == null || speechBubble == null)
        {
            Debug.LogWarning("[Player] SpeechData 또는 SpeechBubbleComponent가 연결되지 않았습니다!");
            return;
        }

        string line = speechData.GetLine(situation);
        if (!string.IsNullOrEmpty(line))
            speechBubble.Speak(line, duration);
    }

    private void HandleInteractSearch()
    {
        IInteractable nearest = GetClosestInteractable();

        if (!ReferenceEquals(nearest, currentTarget))
        {
            if (currentTarget != null)
                currentTarget.OnUnHighlight();

            currentTarget = nearest;

            if (currentTarget != null)
            {
                Debug.Log($"[Player] New currentTarget = {(currentTarget as MonoBehaviour)?.name}");
                currentTarget.OnHighlight();
            }
        }
    }

    private void RefreshInteractionPrompt()
    {
        if (interactionPrompt == null)
            interactionPrompt = WorldInteractionPromptController.Instance ?? FindFirstObjectByType<WorldInteractionPromptController>();

        if (CurrentState != InteractState.Idle || currentTarget == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.HideWorldPrompt();
            else
                interactionPrompt?.Hide();
            return;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshWorldPrompt(currentTarget);
        else
            interactionPrompt?.Refresh(currentTarget);
    }

    private IInteractable GetClosestInteractable()
    {
        if (nearbyObjects.Count == 0)
            return null;

        float closestDist = float.MaxValue;
        IInteractable closestObj = null;

        for (int i = nearbyObjects.Count - 1; i >= 0; i--)
        {
            var obj = nearbyObjects[i];
            if (obj == null || (obj is MonoBehaviour mb && mb == null))
            {
                if (obj != null)
                    nearbyOverlapCounts.Remove(obj);
                nearbyObjects.RemoveAt(i);
                continue;
            }

            var mbObj = (MonoBehaviour)obj;
            float dist = Vector2.Distance(transform.position, mbObj.transform.position);

            if (dist < closestDist && obj.CanInteract(this))
            {
                closestDist = dist;
                closestObj = obj;
            }
        }

        return closestObj;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable == null)
            interactable = other.GetComponentInParent<IInteractable>();

        Debug.Log($"[Player] OnTriggerEnter2D: other={other.name}, foundInteractable={(interactable as MonoBehaviour)?.name ?? "null"}");

        if (interactable == null)
            return;

        if (nearbyOverlapCounts.TryGetValue(interactable, out int overlapCount))
        {
            nearbyOverlapCounts[interactable] = overlapCount + 1;
        }
        else
        {
            nearbyOverlapCounts.Add(interactable, 1);
            nearbyObjects.Add(interactable);
            interactable.OnPlayerNearby();
            Debug.Log($"[Player] Added nearby interactable: {(interactable as MonoBehaviour)?.name}");
        }

        if (CurrentState == InteractState.Idle)
        {
            HandleInteractSearch();
            RefreshInteractionPrompt();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable == null)
            interactable = other.GetComponentInParent<IInteractable>();

        Debug.Log($"[Player] OnTriggerExit2D: other={other.name}, foundInteractable={(interactable as MonoBehaviour)?.name ?? "null"}");

        if (interactable == null || !nearbyOverlapCounts.TryGetValue(interactable, out int overlapCount))
            return;

        overlapCount--;

        if (overlapCount > 0)
        {
            nearbyOverlapCounts[interactable] = overlapCount;
            return;
        }

        nearbyOverlapCounts.Remove(interactable);
        interactable.OnPlayerLeave();

        if (ReferenceEquals(currentTarget, interactable))
            ClearCurrentTarget();

        nearbyObjects.Remove(interactable);
        if (CurrentState == InteractState.Idle)
            HandleInteractSearch();
        RefreshInteractionPrompt();
        Debug.Log($"[Player] Removed nearby interactable: {(interactable as MonoBehaviour)?.name}");
    }

    private void ClearCurrentTarget()
    {
        if (currentTarget != null)
            currentTarget.OnUnHighlight();

        currentTarget = null;
    }
}
