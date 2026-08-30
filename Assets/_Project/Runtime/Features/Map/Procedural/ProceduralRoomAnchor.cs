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
    public void EditorConfigure(string value, ProceduralRoomAnchorScope anchorScope)
    {
        slotId = value;
        scope = anchorScope;
    }
#endif
}
