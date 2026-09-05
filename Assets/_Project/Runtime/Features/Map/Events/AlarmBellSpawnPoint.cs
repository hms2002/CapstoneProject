using UnityEngine;

/// <summary>
/// 책임 : 경보 종 웨이브 몬스터가 방 내부에서 등장할 수 있는 authoring 위치를 표시한다.
/// </summary>
public sealed class AlarmBellSpawnPoint : MonoBehaviour
{
    [SerializeField] private bool allowSpawn = true;

    public bool IsAvailable => allowSpawn && isActiveAndEnabled;
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = allowSpawn ? new Color(1f, 0.35f, 0.1f, 0.9f) : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.28f);
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.45f);
    }
#endif
}
