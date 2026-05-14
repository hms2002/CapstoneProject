using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

/// <summary>
/// 번개 창 표식 돌진 경로와 도착 충격의 짧은 시각 효과를 재생하고 정리할 책임을 가집니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LightningSpearDashStabTrailEffect : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer trailRenderer;
    [SerializeField] private SpriteMask trailMask;
    [SerializeField] private HitboxVisualAnimatorPlayer visualPlayer;
    [SerializeField] private AnimationClip visualClip;

    [Header("Hitboxes")]
    [SerializeField] private MeleeHitboxActor trailHitbox;
    [SerializeField] private MeleeHitboxActor impactHitbox;

    [Header("Layout")]
    [FormerlySerializedAs("sliceMaxDistance")]
    [SerializeField, Min(0f)] private float maskMaxDistance = 2.5f;
    [SerializeField, Min(0.01f)] private float height = 0.45f;
    [Tooltip("Moves only the trail start point. X follows rush direction, Y is local perpendicular.")]
    [SerializeField] private Vector2 startLocalOffset;
    [Tooltip("Moves only the trail end point. X follows rush direction, Y is local perpendicular.")]
    [FormerlySerializedAs("trailLocalOffset")]
    [SerializeField] private Vector2 endLocalOffset;

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
    private Coroutine trailVisualRoutine;
    private Coroutine impactRoutine;
    private float trailLifetimeSeconds;
    private float rootCleanupSeconds;

    public void Play(Vector2 start, Vector2 end)
    {
        if (!IsFinite(start) || !IsFinite(end))
        {
            Destroy(gameObject);
            return;
        }

        Vector2 direction = ResolveDirection(start, end);
        Vector2 trailStart = start + CalculateTrailWorldOffset(direction, startLocalOffset);
        Vector2 trailEnd = end + CalculateTrailWorldOffset(direction, endLocalOffset);
        PlayInternal(trailStart, trailEnd, ResolveDirection(trailStart, trailEnd));
    }

    public bool PlayMarkRush(
        Vector2 start,
        Vector2 end,
        LightningSpearHitConfig hitConfig,
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 direction,
        int facingSideSign,
        float impactDelaySeconds)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : ResolveDirection(start, end);
        Vector2 trailStart = start + CalculateTrailWorldOffset(safeDirection, startLocalOffset);
        Vector2 trailEnd = end + CalculateTrailWorldOffset(safeDirection, endLocalOffset);
        Vector2 trailDirection = ResolveDirection(trailStart, trailEnd);
        PlayInternal(trailStart, trailEnd, trailDirection);

        if (hitConfig == null || system == null || spec == null)
            return false;

        CombatHitPayload payload = hitConfig.BuildPayload(system, spec);
        if (payload == null)
            return false;

        float safeImpactDelay = Mathf.Max(0f, impactDelaySeconds);
        var sharedHitTargetIds = new HashSet<int>();
        bool configuredAnyHitbox = false;

        if (impactHitbox != null)
            EnsureRootCleanupAtLeast(safeImpactDelay + ResolveImpactLifetimeSeconds(hitConfig));

        configuredAnyHitbox |= SetupTrailHitbox(
            hitConfig,
            system,
            spec,
            payload,
            sharedHitTargetIds,
            trailStart,
            trailEnd,
            trailDirection);

        if (impactHitbox != null)
        {
            if (impactRoutine != null)
                StopCoroutine(impactRoutine);

            impactRoutine = StartCoroutine(CoSetupImpactHitbox(
                hitConfig,
                system,
                spec,
                payload,
                sharedHitTargetIds,
                end,
                safeDirection,
                facingSideSign,
                safeImpactDelay));
            configuredAnyHitbox = true;
        }

        return configuredAnyHitbox;
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

    private void PlayInternal(Vector2 start, Vector2 end, Vector2 direction)
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

        if (visualRoot != null && visualRoot != transform)
            visualRoot.gameObject.SetActive(true);

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : ResolveDirection(start, end);

        transform.position = new Vector3(start.x, start.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg);

        float visualDuration = PlayVisual();
        ApplyLayout(Mathf.Max(0.01f, distance));
        trailLifetimeSeconds = ResolveLifetimeSeconds(visualDuration);
        StartTrailVisualLifetime(trailLifetimeSeconds);
        StartRootCleanupAfter(trailLifetimeSeconds);
    }

    private static Vector2 CalculateTrailWorldOffset(Vector2 direction, Vector2 localOffset)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 localUp = new Vector2(-safeDirection.y, safeDirection.x);
        return safeDirection * localOffset.x + localUp * localOffset.y;
    }

    private bool SetupTrailHitbox(
        LightningSpearHitConfig hitConfig,
        AbilitySystem system,
        AbilitySpec spec,
        CombatHitPayload payload,
        HashSet<int> sharedHitTargetIds,
        Vector2 start,
        Vector2 end,
        Vector2 direction)
    {
        if (trailHitbox == null)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
            return false;

        trailHitbox.gameObject.SetActive(true);
        Vector2 center = start + direction * (distance * 0.5f);
        var context = new MeleeHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = Mathf.Max(0.01f, trailLifetimeSeconds),
            wallLayers = hitConfig.WallLayers,
            damageLayers = hitConfig.HitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = new Vector2(distance, Mathf.Max(0.01f, height)),
            hitOncePerTarget = true,
            destroyOnFirstHit = false,
            direction = direction,
            overrideSizingMode = true,
            sizingMode = MeleeHitboxSizingMode.OverrideColliderWorldSizeKeepVisualScale,
            overrideAttachToOwnerOnSetup = true,
            attachToOwnerOnSetup = false,
            sharedHitTargetIds = sharedHitTargetIds
        };

        trailHitbox.Setup(context);
        return true;
    }

    private IEnumerator CoSetupImpactHitbox(
        LightningSpearHitConfig hitConfig,
        AbilitySystem system,
        AbilitySpec spec,
        CombatHitPayload payload,
        HashSet<int> sharedHitTargetIds,
        Vector2 end,
        Vector2 direction,
        int facingSideSign,
        float delaySeconds)
    {
        if (impactHitbox == null)
            yield break;

        impactHitbox.gameObject.SetActive(false);

        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        if (impactHitbox == null || system == null)
            yield break;

        impactHitbox.gameObject.SetActive(true);
        Vector2 center = end + direction * hitConfig.ForwardOffset;
        int visualSideSign = facingSideSign < 0 ? -1 : 1;
        var context = new MeleeHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = hitConfig.ActiveTime,
            wallLayers = hitConfig.WallLayers,
            damageLayers = hitConfig.HitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = hitConfig.HitboxSize,
            hitOncePerTarget = true,
            destroyOnFirstHit = false,
            direction = direction,
            flipVisualX = visualSideSign < 0,
            overrideAttachToOwnerOnSetup = true,
            attachToOwnerOnSetup = false,
            sharedHitTargetIds = sharedHitTargetIds
        };

        impactHitbox.Setup(context);
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

    private void StartTrailVisualLifetime(float seconds)
    {
        if (trailVisualRoutine != null)
            StopCoroutine(trailVisualRoutine);

        trailVisualRoutine = StartCoroutine(CoDisableTrailVisualAfterAnimation(Mathf.Max(0.01f, seconds)));
    }

    private void StartRootCleanupAfter(float seconds)
    {
        rootCleanupSeconds = Mathf.Max(0.01f, seconds);
        if (lifetimeRoutine != null)
            StopCoroutine(lifetimeRoutine);

        lifetimeRoutine = StartCoroutine(CoDestroyAfterAnimation(rootCleanupSeconds));
    }

    private void EnsureRootCleanupAtLeast(float requiredSeconds)
    {
        if (requiredSeconds <= rootCleanupSeconds)
            return;

        StartRootCleanupAfter(requiredSeconds);
    }

    private IEnumerator CoDisableTrailVisualAfterAnimation(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        trailVisualRoutine = null;
        if (visualRoot != null && visualRoot != transform)
            visualRoot.gameObject.SetActive(false);
    }

    private IEnumerator CoDestroyAfterAnimation(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        Destroy(gameObject);
    }

    private float ResolveImpactLifetimeSeconds(LightningSpearHitConfig hitConfig)
    {
        float duration = hitConfig != null ? hitConfig.ActiveTime : 0f;
        if (impactHitbox != null)
        {
            HitboxVisualAnimatorPlayer impactVisualPlayer =
                impactHitbox.GetComponentInChildren<HitboxVisualAnimatorPlayer>(true);
            if (impactVisualPlayer != null)
                duration = Mathf.Max(duration, impactVisualPlayer.CurrentClipDuration);
        }

        return Mathf.Max(0.01f, duration);
    }

    private void OnDestroy()
    {
        if (trailVisualRoutine != null)
        {
            StopCoroutine(trailVisualRoutine);
            trailVisualRoutine = null;
        }

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }

    private static Vector2 ResolveDirection(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        return distance > 0.0001f ? delta / distance : Vector2.right;
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

        Vector3 previewStart = new Vector3(startLocalOffset.x, startLocalOffset.y, 0f);
        Vector3 previewEnd = new Vector3(previewDistance + endLocalOffset.x, endLocalOffset.y, 0f);
        Vector3 previewDelta = previewEnd - previewStart;
        float previewTrailDistance = Mathf.Max(0.01f, previewDelta.magnitude);
        Vector3 previewDirection = previewDelta.sqrMagnitude > 0.0001f
            ? previewDelta.normalized
            : Vector3.right;

        Gizmos.color = gizmoTrailColor;
        DrawLocalRect(previewStart, previewEnd, previewHeight);
        Gizmos.DrawLine(previewStart, previewEnd);

        if (maskMaxDistance > 0f)
        {
            float maskPreviewDistance = Mathf.Min(maskMaxDistance, previewTrailDistance);
            Vector3 maskPreviewEnd = previewStart + previewDirection * maskPreviewDistance;
            Gizmos.color = gizmoMaskColor;
            DrawLocalRect(previewStart, maskPreviewEnd, previewHeight);
            Gizmos.DrawLine(
                maskPreviewEnd + Perpendicular(previewDirection) * (-previewHeight * 0.6f),
                maskPreviewEnd + Perpendicular(previewDirection) * (previewHeight * 0.6f));
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static void DrawLocalRect(Vector3 start, Vector3 end, float previewHeight)
    {
        Vector3 delta = end - start;
        if (delta.sqrMagnitude <= 0.0001f || previewHeight <= 0f)
            return;

        Vector3 direction = delta.normalized;
        Vector3 up = Perpendicular(direction);
        float halfHeight = previewHeight * 0.5f;
        Vector3 leftTop = start + up * halfHeight;
        Vector3 rightTop = end + up * halfHeight;
        Vector3 rightBottom = end - up * halfHeight;
        Vector3 leftBottom = start - up * halfHeight;

        Gizmos.DrawLine(leftTop, rightTop);
        Gizmos.DrawLine(rightTop, rightBottom);
        Gizmos.DrawLine(rightBottom, leftBottom);
        Gizmos.DrawLine(leftBottom, leftTop);
    }

    private static Vector3 Perpendicular(Vector3 direction)
    {
        return new Vector3(-direction.y, direction.x, 0f);
    }
#endif
}

internal static class LightningSpearTrailMaskSortingOrder
{
    private const int Cycle = 200;
    private static int nextOffset;

    public static int Allocate(int baseOrder)
    {
        int offset = 1 + nextOffset;
        nextOffset = (nextOffset + 1) % Cycle;
        return baseOrder + offset;
    }
}
