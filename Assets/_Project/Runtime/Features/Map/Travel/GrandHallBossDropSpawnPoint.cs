using UnityEngine;

/// <summary>
/// 책임 : Grand Hall에서 보스 처치 후 낙하 연출이 사용할 보스별 착지 지점 식별자를 정의한다.
/// </summary>
public enum GrandHallBossDropPointId
{
    Slime,
    Dragon,
    Shadow
}

/// <summary>
/// 책임 : Grand Hall 씬 안에 배치된 보스별 낙하 연출 착지 위치를 런타임 코드가 안정적으로 찾을 수 있게 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GrandHallBossDropSpawnPoint : MonoBehaviour
{
    [SerializeField] private GrandHallBossDropPointId bossId;

    public GrandHallBossDropPointId BossId => bossId;
    public Vector3 Position => transform.position;

#if UNITY_EDITOR
    public void EditorConfigure(GrandHallBossDropPointId id)
    {
        bossId = id;
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = ResolveGizmoColor();
        const float radius = 0.35f;
        Vector3 position = transform.position;
        Gizmos.DrawWireSphere(position, radius);
        Gizmos.DrawLine(position + Vector3.left * radius, position + Vector3.right * radius);
        Gizmos.DrawLine(position + Vector3.down * radius, position + Vector3.up * radius);
    }

    private Color ResolveGizmoColor()
    {
        return bossId switch
        {
            GrandHallBossDropPointId.Slime => new Color(0.35f, 1f, 0.45f, 0.9f),
            GrandHallBossDropPointId.Dragon => new Color(1f, 0.35f, 0.18f, 0.9f),
            GrandHallBossDropPointId.Shadow => new Color(0.55f, 0.35f, 1f, 0.9f),
            _ => Color.white
        };
    }
}
