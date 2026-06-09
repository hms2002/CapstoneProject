using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractionPromptPresenter : MonoBehaviour
{
    [SerializeField] private WorldInteractionPromptController interactionPrompt;

    private void Awake()
    {
        ResolvePromptController();
    }

    public void SetPromptController(WorldInteractionPromptController promptController)
    {
        if (promptController != null)
            interactionPrompt = promptController;
    }

    public void RefreshPrompt(IInteractable currentTarget, InteractState currentState)
    {
        ResolvePromptController();

        if (currentState != InteractState.Idle || currentTarget == null)
        {
            HidePrompt();
            return;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshWorldPrompt(currentTarget);
        else
            interactionPrompt?.Refresh(currentTarget);
    }

    public void HidePrompt()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.HideWorldPrompt();
        else
            interactionPrompt?.Hide();
    }

    private void ResolvePromptController()
    {
        if (interactionPrompt == null)
            interactionPrompt = WorldInteractionPromptController.Instance ?? FindFirstObjectByType<WorldInteractionPromptController>();
    }
}
