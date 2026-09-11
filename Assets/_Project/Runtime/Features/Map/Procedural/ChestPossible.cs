using UnityEngine;

/// <summary>
/// Authoring-only candidate source for a chest selected after dungeon room placement.
/// The builder creates the referenced chest, never this marker or its preview visuals.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChestPossible : MonoBehaviour
{
    [SerializeField] private TreasureChest chestPrefab;

    public TreasureChest ChestPrefab => chestPrefab;

    public static bool TryGet(RoomObjectPlacementData placement, out ChestPossible candidate)
    {
        candidate = null;
        return placement.kind == RoomObjectKind.Prop && placement.prefab != null &&
            placement.prefab.TryGetComponent(out candidate);
    }

#if UNITY_EDITOR
    public void EditorConfigure(TreasureChest prefab) => chestPrefab = prefab;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.7f);
    }
#endif
}
