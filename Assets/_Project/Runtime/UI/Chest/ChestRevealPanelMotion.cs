using UnityEngine;

internal sealed class ChestRevealPanelMotion
{
    private readonly ChestRevealLayoutDriverScope layoutDriverScope = new();
    private RectTransform chestPanel;
    private RectTransform inventoryPanel;

    public void Configure(RectTransform chestPanel, RectTransform inventoryPanel)
    {
        this.chestPanel = chestPanel;
        this.inventoryPanel = inventoryPanel;
    }

    public void BeginOwnership()
    {
        layoutDriverScope.CaptureAndDisable(chestPanel, inventoryPanel);
    }

    public void EndOwnership()
    {
        if (layoutDriverScope.IsActive)
            layoutDriverScope.Restore();
    }

    public void ApplyPositions(Vector2 chestPosition, Vector2 inventoryPosition)
    {
        if (chestPanel != null)
            chestPanel.anchoredPosition = chestPosition;

        if (inventoryPanel != null)
            inventoryPanel.anchoredPosition = inventoryPosition;
    }
}
