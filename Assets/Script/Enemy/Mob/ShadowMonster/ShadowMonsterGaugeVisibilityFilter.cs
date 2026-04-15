using UnityEngine;

[DisallowMultipleComponent]
public class ShadowMonsterGaugeVisibilityFilter : MonoBehaviour, IMonsterGaugeVisibilityFilter
{
    // 이 클래스의 책임:
    // ShadowMonster가 씬의 활성 SpriteMask 영역 안에 있을 때만 속성 게이지를 보이게 판정한다.

    [SerializeField] private SpriteRenderer maskedRenderer;
    [SerializeField] private Transform samplePointOverride;
    [SerializeField] private Vector3 sampleOffset;
    [SerializeField, Min(0.05f)] private float spriteMaskRefreshInterval = 0.25f;

    private readonly System.Collections.Generic.List<SpriteMask> cachedSpriteMasks = new();
    private float nextRefreshTime;

    public bool ShouldShowGauge()
    {
        if (maskedRenderer != null && maskedRenderer.maskInteraction != SpriteMaskInteraction.VisibleInsideMask)
            return true;

        RefreshSpriteMaskCacheIfNeeded();
        if (cachedSpriteMasks.Count == 0)
            return true;

        Vector3 samplePoint = GetSamplePoint();
        for (int i = 0; i < cachedSpriteMasks.Count; i++)
        {
            SpriteMask spriteMask = cachedSpriteMasks[i];
            if (spriteMask == null || !spriteMask.enabled || !spriteMask.gameObject.activeInHierarchy || spriteMask.sprite == null)
                continue;

            if (ContainsSamplePoint(spriteMask, samplePoint))
                return true;
        }

        return false;
    }

    private void Awake()
    {
        if (maskedRenderer == null)
            maskedRenderer = GetComponent<SpriteRenderer>();

        nextRefreshTime = -1f;
    }

    private Vector3 GetSamplePoint()
    {
        if (samplePointOverride != null)
            return samplePointOverride.position + sampleOffset;

        if (maskedRenderer != null)
            return maskedRenderer.bounds.center + sampleOffset;

        return transform.position + sampleOffset;
    }

    private void RefreshSpriteMaskCacheIfNeeded()
    {
        if (Time.time < nextRefreshTime && cachedSpriteMasks.Count > 0)
            return;

        nextRefreshTime = Time.time + spriteMaskRefreshInterval;
        cachedSpriteMasks.Clear();

        SpriteMask[] spriteMasks = FindObjectsByType<SpriteMask>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < spriteMasks.Length; i++)
        {
            SpriteMask spriteMask = spriteMasks[i];
            if (spriteMask == null || !spriteMask.enabled || !spriteMask.gameObject.activeInHierarchy || spriteMask.sprite == null)
                continue;

            cachedSpriteMasks.Add(spriteMask);
        }
    }

    private static bool ContainsSamplePoint(SpriteMask spriteMask, Vector3 samplePoint)
    {
        Vector3 localPoint = spriteMask.transform.InverseTransformPoint(samplePoint);
        Bounds spriteBounds = spriteMask.sprite.bounds;

        float halfWidth = Mathf.Max(spriteBounds.extents.x, 0.0001f);
        float halfHeight = Mathf.Max(spriteBounds.extents.y, 0.0001f);
        float normalizedX = localPoint.x / halfWidth;
        float normalizedY = localPoint.y / halfHeight;

        return (normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1f;
    }
}
