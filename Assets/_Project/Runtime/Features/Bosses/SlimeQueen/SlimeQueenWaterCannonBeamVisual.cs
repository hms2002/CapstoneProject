using UnityEngine;

/// <summary>
/// 슬라임 여왕 물대포의 임시 파란 막대기 비주얼입니다.
/// </summary>
public sealed class SlimeQueenWaterCannonBeamVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer beamRenderer;
    [SerializeField, Min(0f)] private float minVisibleLength = 0.05f;
    [SerializeField] private int sortingOrderOffset = 20;

    private void Awake()
    {
        if (beamRenderer == null)
            beamRenderer = GetComponent<SpriteRenderer>();

        HideImmediate();
    }

    public void SyncSorting(SpriteRenderer referenceRenderer)
    {
        if (beamRenderer == null || referenceRenderer == null)
            return;

        beamRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        beamRenderer.sortingOrder = referenceRenderer.sortingOrder + sortingOrderOffset;
        beamRenderer.maskInteraction = referenceRenderer.maskInteraction;
    }

    public void Show(Vector2 start, Vector2 direction, float length, float width, float normalizedLength)
    {
        if (beamRenderer == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float visibleLength = Mathf.Max(0f, length) * Mathf.Clamp01(normalizedLength);
        if (visibleLength <= minVisibleLength)
        {
            beamRenderer.enabled = false;
            return;
        }

        transform.position = start + safeDirection * (visibleLength * 0.5f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(visibleLength, Mathf.Max(0.05f, width), 1f);
        beamRenderer.enabled = true;
    }

    public void HideImmediate()
    {
        if (beamRenderer != null)
            beamRenderer.enabled = false;
    }
}
