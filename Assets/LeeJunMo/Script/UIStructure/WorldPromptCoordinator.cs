using UnityEngine;

public sealed class WorldPromptCoordinator
{
    private WorldInteractionPromptController controller;

    public void Initialize(WorldInteractionPromptController initialController)
    {
        controller = initialController;
    }

    public void OnSceneLoaded()
    {
        Hide();
        ResolveController();
    }

    public void Show(IInteractable target, bool isBlocked)
    {
        if (isBlocked || target == null)
        {
            Hide();
            return;
        }

        ResolveController();
        controller?.Show(target);
    }

    public void Refresh(IInteractable target, bool isBlocked)
    {
        if (isBlocked || target == null)
        {
            Hide();
            return;
        }

        ResolveController();
        controller?.Refresh(target);
    }

    public void Hide()
    {
        ResolveController();
        controller?.Hide();
    }

    private void ResolveController()
    {
        if (controller != null)
            return;

        controller = WorldInteractionPromptController.Instance;

        if (controller == null)
            controller = Object.FindFirstObjectByType<WorldInteractionPromptController>();
    }
}
