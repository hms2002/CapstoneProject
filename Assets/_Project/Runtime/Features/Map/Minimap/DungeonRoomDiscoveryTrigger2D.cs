using UnityEngine;

/// <summary>
/// 책임 : 플레이어 본체의 방 진입을 감지해 지도 발견 런타임과 방 진입 구독자에게 안정 배치 Id를 전달한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DungeonRoomDiscoveryTrigger2D : MonoBehaviour
{
    [SerializeField] private DungeonMapRuntimeController targetRuntime;
    [SerializeField] private int roomPlacementId = -1;

    public int RoomPlacementId => roomPlacementId;
    public event System.Action<int> PlayerEnteredRoom;

    public void Configure(DungeonMapRuntimeController runtime, int placementId)
    {
        targetRuntime = runtime;
        roomPlacementId = placementId;
        Collider2D areaCollider = GetComponent<Collider2D>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
    }

    private void Reset()
    {
        Collider2D areaCollider = GetComponent<Collider2D>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (roomPlacementId < 0 || other == null)
            return;

        PlayerInteractor2D player = other.GetComponentInParent<PlayerInteractor2D>();
        if (player == null || !player.CompareTag("Player"))
            return;

        Collider2D bodyCollider = player.BodyCollider;
        if (bodyCollider == null || bodyCollider != other)
            return;

        targetRuntime?.NotifyPlayerEnteredRoom(roomPlacementId);
        PlayerEnteredRoom?.Invoke(roomPlacementId);
    }
}
