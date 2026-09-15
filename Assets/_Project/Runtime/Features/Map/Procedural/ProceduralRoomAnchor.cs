using UnityEngine;

/// <summary>
/// 책임 : 방 기능 프리팹에서 런타임에 검색할 수 있는 안정적인 Transform slot을 선언한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProceduralRoomAnchor : MonoBehaviour
{
    [SerializeField] private string slotId;
    [SerializeField] private ProceduralRoomAnchorScope scope = ProceduralRoomAnchorScope.LocalRoom;

    public string SlotId => slotId;
    public ProceduralRoomAnchorScope Scope => scope;
    public Transform Target => transform;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (string.IsNullOrEmpty(slotId) || !slotId.StartsWith("ReturnPortal_", System.StringComparison.Ordinal)) return;
        if (!System.Enum.TryParse(slotId.Substring("ReturnPortal_".Length), out RoomSocketDirection direction) ||
            (int)direction < 0 || (int)direction > 3) return;
        Vector3 axis = (Vector3)(Vector2)DungeonReturnPortalPlacement.Direction(direction);
        Vector3 side = new Vector3(-axis.y, axis.x) * 0.15f;
        Vector3 tip = transform.position + axis * 0.6f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
        Gizmos.DrawLine(transform.position, tip);
        Gizmos.DrawLine(tip, tip - axis * 0.2f + side);
        Gizmos.DrawLine(tip, tip - axis * 0.2f - side);
    }

    public void EditorConfigure(string value, ProceduralRoomAnchorScope anchorScope)
    {
        slotId = value;
        scope = anchorScope;
    }
#endif
}
