using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class LightningSpearRecoverShotTrailEffect : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer trailRenderer;
    [SerializeField] private SpriteMask trailMask;
    [SerializeField] private HitboxVisualAnimatorPlayer visualPlayer;
    [SerializeField] private AnimationClip visualClip;

    [Header("Layout")]
    [FormerlySerializedAs("sliceMaxDistance")]
    [SerializeField, Min(0f)] private float maskMaxDistance = 2.5f;
    [SerializeField, Min(0.01f)] private float height = 0.45f;

    [Header("Fallback Lifetime")]
    [Tooltip("Used only when no animation clip duration can be resolved.")]
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.16f;

#if UNITY_EDITOR
    [Header("Authoring Preview")]
    [SerializeField] private bool drawAuthoringGizmo = true;
    [SerializeField, Min(0.01f)] private float gizmoPreviewDistance = 2.5f;
    [SerializeField] private Color gizmoTrailColor = new Color(0f, 0.85f, 1f, 0.85f);
    [SerializeField] private Color gizmoMaskColor = new Color(1f, 0.92f, 0.2f, 0.85f);
#endif

    private Coroutine lifetimeRoutine;

    public event System.Action<LightningSpearRecoverShotTrailEffect> Destroyed;

    public void Configure(float newSliceMaxDistance)
    {
        maskMaxDistance = Mathf.Max(0f, newSliceMaxDistance);
    }

    public void Play(Vector2 start, Vector2 end)
    {
        if (!IsFinite(start) || !IsFinite(end))
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        if (trailRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.0001f ? delta / distance : Vector2.right;

        transform.position = new Vector3(start.x, start.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        float visualDuration = PlayVisual();
        ApplyLayout(Mathf.Max(0.01f, distance));

        if (lifetimeRoutine != null)
            StopCoroutine(lifetimeRoutine);

        lifetimeRoutine = StartCoroutine(CoDestroyAfterAnimation(ResolveLifetimeSeconds(visualDuration)));
    }

    private void ResolveReferences()
    {
        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (visualRoot == null && trailRenderer != null)
            visualRoot = trailRenderer.transform;

        if (trailMask == null)
            trailMask = GetComponentInChildren<SpriteMask>(true);

        if (visualPlayer == null)
            visualPlayer = GetComponentInChildren<HitboxVisualAnimatorPlayer>(true);
    }

    private void ApplyLayout(float distance)
    {
        Sprite trailSprite = trailRenderer.sprite;
        Vector2 trailSize = trailSprite != null ? trailSprite.bounds.size : Vector2.one;
        float trailWidth = Mathf.Max(0.01f, trailSize.x);
        float trailHeight = Mathf.Max(0.01f, trailSize.y);

        if (trailMask != null && maskMaxDistance > 0f && distance <= maskMaxDistance)
        {
            ApplyMaskedCrop(distance, trailWidth, trailHeight);
            return;
        }

        ApplyStretch(distance, trailWidth, trailHeight);
    }

    private void ApplyMaskedCrop(float distance, float trailWidth, float trailHeight)
    {
        trailMask.enabled = true;
        ConfigureMaskSortingRange();
        trailRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        trailRenderer.drawMode = SpriteDrawMode.Simple;
        trailRenderer.size = Vector2.one;

        Transform rendererTransform = visualRoot != null ? visualRoot : trailRenderer.transform;
        rendererTransform.localPosition = new Vector3(trailWidth * 0.5f, 0f, 0f);
        rendererTransform.localRotation = Quaternion.identity;
        rendererTransform.localScale = new Vector3(1f, height / trailHeight, 1f);

        Transform maskTransform = trailMask.transform;
        Sprite maskSprite = trailMask.sprite;
        Vector2 maskSize = maskSprite != null ? maskSprite.bounds.size : Vector2.one;
        float maskWidth = Mathf.Max(0.01f, maskSize.x);
        float maskHeight = Mathf.Max(0.01f, maskSize.y);
        maskTransform.localPosition = new Vector3(distance * 0.5f, 0f, 0f);
        maskTransform.localRotation = Quaternion.identity;
        maskTransform.localScale = new Vector3(distance / maskWidth, height / maskHeight, 1f);
    }

    private void ApplyStretch(float distance, float trailWidth, float trailHeight)
    {
        if (trailMask != null)
            trailMask.enabled = false;

        trailRenderer.maskInteraction = SpriteMaskInteraction.None;
        trailRenderer.drawMode = SpriteDrawMode.Simple;
        trailRenderer.size = Vector2.one;

        Transform rendererTransform = visualRoot != null ? visualRoot : trailRenderer.transform;
        rendererTransform.localPosition = new Vector3(distance * 0.5f, 0f, 0f);
        rendererTransform.localRotation = Quaternion.identity;
        rendererTransform.localScale = new Vector3(distance / trailWidth, height / trailHeight, 1f);
    }

    private void ConfigureMaskSortingRange()
    {
        if (trailMask == null || trailRenderer == null)
            return;

        int sortingOrder = LightningSpearTrailMaskSortingOrder.Allocate(trailRenderer.sortingOrder);
        trailRenderer.sortingOrder = sortingOrder;
        trailMask.isCustomRangeActive = true;
        trailMask.frontSortingLayerID = trailRenderer.sortingLayerID;
        trailMask.backSortingLayerID = trailRenderer.sortingLayerID;
        trailMask.frontSortingOrder = sortingOrder;
        trailMask.backSortingOrder = sortingOrder;
    }

    private float PlayVisual()
    {
        if (visualPlayer != null)
        {
            if (visualClip != null)
                visualPlayer.PlayClip(visualClip);
            else
                visualPlayer.Play();

            if (visualPlayer.CurrentClipDuration > 0f)
                return visualPlayer.CurrentClipDuration;
        }

        return visualClip != null ? visualClip.length : 0f;
    }

    private float ResolveLifetimeSeconds(float visualDuration)
    {
        return visualDuration > 0f
            ? visualDuration
            : Mathf.Max(0.01f, lifetimeSeconds);
    }

    private IEnumerator CoDestroyAfterAnimation(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Destroyed?.Invoke(this);
        Destroyed = null;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawAuthoringGizmo)
            return;

        float previewDistance = Mathf.Max(0.01f, gizmoPreviewDistance);
        float previewHeight = Mathf.Max(0.01f, height);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = gizmoTrailColor;
        DrawLocalRect(previewDistance, previewHeight);
        Gizmos.DrawLine(Vector3.zero, Vector3.right * previewDistance);

        if (maskMaxDistance > 0f)
        {
            float maskPreviewDistance = Mathf.Min(maskMaxDistance, previewDistance);
            Gizmos.color = gizmoMaskColor;
            DrawLocalRect(maskPreviewDistance, previewHeight);
            Gizmos.DrawLine(
                new Vector3(maskPreviewDistance, -previewHeight * 0.6f, 0f),
                new Vector3(maskPreviewDistance, previewHeight * 0.6f, 0f));
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static void DrawLocalRect(float distance, float previewHeight)
    {
        if (distance <= 0f || previewHeight <= 0f)
            return;

        float halfHeight = previewHeight * 0.5f;
        Vector3 leftTop = new Vector3(0f, halfHeight, 0f);
        Vector3 rightTop = new Vector3(distance, halfHeight, 0f);
        Vector3 rightBottom = new Vector3(distance, -halfHeight, 0f);
        Vector3 leftBottom = new Vector3(0f, -halfHeight, 0f);

        Gizmos.DrawLine(leftTop, rightTop);
        Gizmos.DrawLine(rightTop, rightBottom);
        Gizmos.DrawLine(rightBottom, leftBottom);
        Gizmos.DrawLine(leftBottom, leftTop);
    }
#endif
}
