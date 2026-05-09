using UnityEngine;

/// <summary>
/// 책임:
/// 취룡 전투 공간의 유효 낙하지점/스폰 지점을 PolygonCollider2D 기준으로 제공한다.
/// 보스 패턴 AL이 맵 폴리곤 샘플링 세부를 직접 알지 않도록 arena authoring 경계를 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DrunkenDragonArenaBounds2D : MonoBehaviour
{
    [SerializeField] private PolygonCollider2D playableArea;
    [SerializeField, Min(1)] private int defaultMaxAttempts = 80;

    private void Reset()
    {
        playableArea = GetComponent<PolygonCollider2D>();
    }

    private void OnValidate()
    {
        if (playableArea == null)
            playableArea = GetComponent<PolygonCollider2D>();
    }

    public bool TryGetRandomPoint(out Vector2 point)
    {
        return TryGetRandomPoint(defaultMaxAttempts, out point);
    }

    public bool TryGetRandomPoint(int maxAttempts, out Vector2 point)
    {
        point = default;

        if (playableArea == null)
            return false;

        Bounds bounds = playableArea.bounds;
        int attempts = Mathf.Max(1, maxAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate = new(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y));

            if (!playableArea.OverlapPoint(candidate))
                continue;

            point = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetRandomPointAwayFrom(
        Vector2 avoidPoint,
        float minDistance,
        int maxAttempts,
        out Vector2 point)
    {
        point = default;

        float minDistanceSqr = Mathf.Max(0f, minDistance);
        minDistanceSqr *= minDistanceSqr;
        int attempts = Mathf.Max(1, maxAttempts);
        for (int i = 0; i < attempts; i++)
        {
            if (!TryGetRandomPoint(1, out Vector2 candidate))
                continue;

            if ((candidate - avoidPoint).sqrMagnitude < minDistanceSqr)
                continue;

            point = candidate;
            return true;
        }

        return false;
    }
}
