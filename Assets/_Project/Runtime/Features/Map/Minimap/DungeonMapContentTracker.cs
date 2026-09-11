using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Responsibility: adapt chest/pickup lifecycle notifications to one dungeon's content model.
/// Resolve world-space sources against the same tile grid used by the room builder.
/// The map controller owns disposal. Sources never reference UI or the map controller.
/// </summary>
public sealed class DungeonMapContentTracker : IDisposable
{
    private readonly Scene scene;
    private readonly DungeonMapGraphSnapshot graph;
    private readonly GridLayout layoutGrid;
    private readonly Action<int> onChanged;
    private bool disposed;
    public DungeonMapContentModel Model { get; } = new();

    public DungeonMapContentTracker(Scene scene, DungeonMapGraphSnapshot graph, Action<int> onChanged,
        GridLayout layoutGrid = null)
    {
        this.scene = scene;
        this.graph = graph;
        this.layoutGrid = layoutGrid;
        this.onChanged = onChanged;
        TreasureChest.WorldStateChanged += HandleChest;
        FieldHealPickup2D.WorldStateChanged += HandleHeart;
        // Bootstrap once: generation/restoration may precede map configuration.
        foreach (var chest in UnityEngine.Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None))
            HandleChest(chest);
        foreach (var heart in UnityEngine.Object.FindObjectsByType<FieldHealPickup2D>(FindObjectsSortMode.None))
            HandleHeart(heart);
        Model.Changed += onChanged;
    }

    private void HandleChest(TreasureChest chest)
    {
        if (disposed || chest == null)
            return;
        if (!chest.isActiveAndEnabled || chest.gameObject.scene != scene)
        {
            Model.Remove(chest.GetInstanceID());
            return;
        }
        Model.Set(chest.GetInstanceID(), DungeonMapContentRoomResolver.Resolve(graph, chest.transform.position, layoutGrid),
            chest.IsOpened ? DungeonMapContentKind.OpenedChest : DungeonMapContentKind.ClosedChest);
    }

    private void HandleHeart(FieldHealPickup2D heart)
    {
        if (disposed || heart == null)
            return;
        if (!heart.isActiveAndEnabled || heart.IsCollected || heart.gameObject.scene != scene)
        {
            Model.Remove(heart.GetInstanceID());
            return;
        }
        Model.Set(heart.GetInstanceID(), DungeonMapContentRoomResolver.Resolve(graph, heart.GroundPosition, layoutGrid),
            DungeonMapContentKind.Heart);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        TreasureChest.WorldStateChanged -= HandleChest;
        FieldHealPickup2D.WorldStateChanged -= HandleHeart;
        Model.Changed -= onChanged;
    }
}
