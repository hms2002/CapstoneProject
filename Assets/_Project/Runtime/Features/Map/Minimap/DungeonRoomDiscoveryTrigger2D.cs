using UnityEngine;

/// <summary>
/// 책임 : 모든 절차 생성 방의 내부 진입을 플레이어 본체 콜라이더로 감지해 지도 발견 런타임에 안정 배치 Id를 전달한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DungeonRoomDiscoveryTrigger2D : MonoBehaviour
{
    [SerializeField] private DungeonMapRuntimeController targetRuntime;
    [SerializeField] private int roomPlacementId = -1;

    public int RoomPlacementId => roomPlacementId;

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
        if (targetRuntime == null || roomPlacementId < 0 || other == null)
            return;

        PlayerInteractor2D player = other.GetComponentInParent<PlayerInteractor2D>();
        if (player == null || !player.CompareTag("Player"))
            return;

        Collider2D bodyCollider = player.BodyCollider;
        if (bodyCollider == null || bodyCollider != other)
            return;

        targetRuntime.NotifyPlayerEnteredRoom(roomPlacementId);
    }
}
