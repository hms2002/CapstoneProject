using UnityEngine;

/// <summary>
/// 슬라임 여왕 독성 투하의 초록 포물선 탄막 비주얼입니다.
/// </summary>
public sealed class SlimeQueenToxicDropProjectileVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer projectileRenderer;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 baseScale;
    private float durationSeconds;
    private float arcHeight;
    private float elapsedSeconds;
    private bool isActive;
    private bool isFinished = true;

    public bool IsFinished => isFinished;

    private void Awake()
    {
        CacheRenderer();
        baseScale = transform.localScale;
        SetVisible(false);
    }

    private void OnValidate()
    {
        CacheRenderer();
    }

    private void Update()
    {
        if (!isActive)
            return;

        elapsedSeconds += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
        ApplyPose(normalizedTime);

        if (normalizedTime >= 1f)
            Finish();
    }

    public void Begin(Vector3 start, Vector3 end, float flightSeconds, float height)
    {
        CacheRenderer();

        startPosition = start;
        endPosition = end;
        durationSeconds = Mathf.Max(0.01f, flightSeconds);
        arcHeight = Mathf.Max(0f, height);
        elapsedSeconds = 0f;
        isActive = true;
        isFinished = false;

        ApplyPose(0f);
        SetVisible(true);
    }

    public void SyncSorting(SpriteRenderer referenceRenderer, int sortingOrderOffset)
    {
        if (projectileRenderer == null || referenceRenderer == null)
            return;

        projectileRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        projectileRenderer.sortingOrder = referenceRenderer.sortingOrder + sortingOrderOffset;
        projectileRenderer.maskInteraction = referenceRenderer.maskInteraction;
    }

    private void ApplyPose(float normalizedTime)
    {
        float arcOffset = Mathf.Sin(normalizedTime * Mathf.PI) * arcHeight;
        Vector3 groundPosition = Vector3.Lerp(startPosition, endPosition, normalizedTime);
        transform.position = groundPosition + Vector3.up * arcOffset;

        float scaleOffset = 1f + Mathf.Sin(normalizedTime * Mathf.PI) * 0.2f;
        transform.localScale = baseScale * scaleOffset;
    }

    private void Finish()
    {
        isActive = false;
        isFinished = true;
        transform.position = endPosition;
    }

    private void SetVisible(bool isVisible)
    {
        if (projectileRenderer != null)
            projectileRenderer.enabled = isVisible;
    }

    private void CacheRenderer()
    {
        if (projectileRenderer == null)
            projectileRenderer = GetComponent<SpriteRenderer>();
    }
}
