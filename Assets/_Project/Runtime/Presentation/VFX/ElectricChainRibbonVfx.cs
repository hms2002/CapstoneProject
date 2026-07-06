using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임: 전기 연쇄 발현 지점들을 sprite segment/snap 조합으로 시각화하고 수명이 끝나면 자기 자신을 정리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElectricChainRibbonVfx : MonoBehaviour, IChainPointPresentation
    {
        [SerializeField] private SpriteRenderer segmentTemplate;
        [SerializeField] private SpriteRenderer snapTemplate;
        [SerializeField] private Sprite[] snapFrames;
        [SerializeField, Min(0.01f)] private float segmentHeight = 0.45f;
        [SerializeField, Min(0f)] private float segmentEndInset = 0.08f;
        [SerializeField, Min(0f)] private float segmentScaleInSeconds = 0.05f;
        [SerializeField, Min(0f)] private float segmentScaleOutSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float snapScale = 1f;
        [SerializeField, Min(1f)] private float snapFrameRate = 24f;
        [SerializeField, Min(0f)] private float visibleSeconds = 0.22f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.1f;

        private readonly List<SpriteRenderer> activeSegments = new();
        private readonly List<Color> segmentStartColors = new();
        private readonly List<Vector2> segmentBaseSizes = new();
        private readonly List<Vector3> segmentMidpoints = new();
        private readonly List<Quaternion> segmentRotations = new();
        private readonly List<Vector3> segmentPivotCenterRatios = new();
        private readonly List<SpriteRenderer> activeSnaps = new();
        private readonly List<Color> snapStartColors = new();
        private Coroutine lifetimeRoutine;

        private void Awake()
        {
            ResolveTemplate();
        }

        public void Play(IReadOnlyList<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count == 0)
            {
                Destroy(gameObject);
                return;
            }

            ResolveTemplate();
            bool hasChainSegments = worldPoints.Count >= 2;
            if (hasChainSegments && (segmentTemplate == null || segmentTemplate.sprite == null))
            {
                Destroy(gameObject);
                return;
            }

            StopLifetimeRoutine();
            ClearActiveSegments();

            if (hasChainSegments)
            {
                for (int i = 0; i < worldPoints.Count - 1; i++)
                    TryCreateSegment(worldPoints[i], worldPoints[i + 1], i);

                if (activeSegments.Count == 0)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else if (snapTemplate == null)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < worldPoints.Count; i++)
                TryCreateSnap(worldPoints[i], i);

            if (activeSnaps.Count == 0 && activeSegments.Count == 0)
            {
                Destroy(gameObject);
                return;
            }

            lifetimeRoutine = StartCoroutine(CoLifetime());
        }

        private void TryCreateSegment(Vector3 start, Vector3 end, int index)
        {
            if (!IsFinite(start) || !IsFinite(end))
                return;

            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
                return;

            Sprite sprite = segmentTemplate.sprite;
            Vector3 spriteSize = sprite.bounds.size;
            float spriteWidth = Mathf.Max(0.001f, spriteSize.x);
            float spriteHeight = Mathf.Max(0.001f, spriteSize.y);
            float inset = Mathf.Min(segmentEndInset, distance * 0.45f);
            float visualDistance = Mathf.Max(0.001f, distance - inset * 2f);
            float visualHeight = Mathf.Max(0.001f, segmentHeight);

            SpriteRenderer segment = Instantiate(segmentTemplate, transform, worldPositionStays: false);
            segment.name = $"Segment_{index}";
            segment.gameObject.SetActive(true);
            segment.enabled = true;
            segment.drawMode = SpriteDrawMode.Tiled;
            segment.size = new Vector2(visualDistance, visualHeight);

            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Vector3 pivotCenterRatio = new(sprite.bounds.center.x / spriteWidth, sprite.bounds.center.y / spriteHeight, 0f);
            Vector3 visualCenterOffset = rotation * new Vector3(
                pivotCenterRatio.x * visualDistance,
                pivotCenterRatio.y * visualHeight,
                0f);
            Vector3 midpoint = (start + end) * 0.5f;

            Transform segmentTransform = segment.transform;
            segmentTransform.SetPositionAndRotation(midpoint - visualCenterOffset, rotation);
            segmentTransform.localScale = Vector3.one;

            activeSegments.Add(segment);
            segmentStartColors.Add(segment.color);
            segmentBaseSizes.Add(new Vector2(visualDistance, visualHeight));
            segmentMidpoints.Add(midpoint);
            segmentRotations.Add(rotation);
            segmentPivotCenterRatios.Add(pivotCenterRatio);
        }

        private void TryCreateSnap(Vector3 worldPoint, int index)
        {
            if (snapTemplate == null || !IsFinite(worldPoint))
                return;

            SpriteRenderer snap = Instantiate(snapTemplate, transform, worldPositionStays: false);
            snap.name = $"Snap_{index}";
            snap.gameObject.SetActive(true);
            snap.enabled = true;

            if (snapFrames != null && snapFrames.Length > 0 && snapFrames[0] != null)
                snap.sprite = snapFrames[0];

            Transform snapTransform = snap.transform;
            snapTransform.SetPositionAndRotation(worldPoint, Quaternion.identity);
            snapTransform.localScale = Vector3.one * Mathf.Max(0.01f, snapScale);

            activeSnaps.Add(snap);
            snapStartColors.Add(snap.color);
        }

        private IEnumerator CoLifetime()
        {
            float visible = Mathf.Max(0f, visibleSeconds);
            float duration = Mathf.Max(0f, fadeSeconds);
            float totalDuration = visible + duration;

            if (totalDuration <= 0f)
            {
                ApplyAlphaFactor(0f);
                lifetimeRoutine = null;
                Destroy(gameObject);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                UpdateSnapFrames(elapsed);
                ApplySegmentYScale(elapsed, totalDuration);

                float alphaFactor = 1f;
                if (duration > 0f && elapsed > visible)
                    alphaFactor = 1f - Mathf.Clamp01((elapsed - visible) / duration);

                ApplyAlphaFactor(alphaFactor);

                elapsed += Time.deltaTime;
                yield return null;
            }

            UpdateSnapFrames(totalDuration);
            ApplySegmentYScale(totalDuration, totalDuration);
            ApplyAlphaFactor(0f);
            lifetimeRoutine = null;
            Destroy(gameObject);
        }

        private void ApplySegmentYScale(float elapsed, float totalDuration)
        {
            float yScale = EvaluateSegmentYScale(elapsed, totalDuration);

            for (int i = 0; i < activeSegments.Count; i++)
            {
                SpriteRenderer segment = activeSegments[i];
                if (segment == null || i >= segmentBaseSizes.Count)
                    continue;

                Vector2 baseSize = segmentBaseSizes[i];
                float visualHeight = Mathf.Max(0.001f, baseSize.y * yScale);
                segment.size = new Vector2(baseSize.x, visualHeight);

                Quaternion rotation = i < segmentRotations.Count ? segmentRotations[i] : segment.transform.rotation;
                Vector3 midpoint = i < segmentMidpoints.Count ? segmentMidpoints[i] : segment.transform.position;
                Vector3 pivotCenterRatio = i < segmentPivotCenterRatios.Count ? segmentPivotCenterRatios[i] : Vector3.zero;
                Vector3 visualCenterOffset = rotation * new Vector3(
                    pivotCenterRatio.x * baseSize.x,
                    pivotCenterRatio.y * visualHeight,
                    0f);

                Transform segmentTransform = segment.transform;
                segmentTransform.SetPositionAndRotation(midpoint - visualCenterOffset, rotation);
                segmentTransform.localScale = Vector3.one;
            }
        }

        private float EvaluateSegmentYScale(float elapsed, float totalDuration)
        {
            if (totalDuration <= 0f)
                return 1f;

            float yScale = 1f;
            float scaleIn = Mathf.Max(0f, segmentScaleInSeconds);
            if (scaleIn > 0f)
                yScale = Mathf.Min(yScale, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / scaleIn)));

            float scaleOut = Mathf.Max(0f, segmentScaleOutSeconds);
            if (scaleOut > 0f)
                yScale = Mathf.Min(yScale, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((totalDuration - elapsed) / scaleOut)));

            return Mathf.Clamp01(yScale);
        }

        private void UpdateSnapFrames(float elapsed)
        {
            if (snapFrames == null || snapFrames.Length == 0)
                return;

            int frameIndex = Mathf.Clamp(
                Mathf.FloorToInt(elapsed * Mathf.Max(1f, snapFrameRate)),
                0,
                snapFrames.Length - 1);
            Sprite frame = snapFrames[frameIndex];
            if (frame == null)
                return;

            for (int i = 0; i < activeSnaps.Count; i++)
            {
                SpriteRenderer snap = activeSnaps[i];
                if (snap != null)
                    snap.sprite = frame;
            }
        }

        private void ApplyAlphaFactor(float alphaFactor)
        {
            float clampedAlphaFactor = Mathf.Clamp01(alphaFactor);

            for (int i = 0; i < activeSegments.Count; i++)
            {
                SpriteRenderer segment = activeSegments[i];
                if (segment == null)
                    continue;

                Color color = i < segmentStartColors.Count ? segmentStartColors[i] : segment.color;
                color.a *= clampedAlphaFactor;
                segment.color = color;
            }

            for (int i = 0; i < activeSnaps.Count; i++)
            {
                SpriteRenderer snap = activeSnaps[i];
                if (snap == null)
                    continue;

                Color color = i < snapStartColors.Count ? snapStartColors[i] : snap.color;
                color.a *= clampedAlphaFactor;
                snap.color = color;
            }
        }

        private void ResolveTemplate()
        {
            if (segmentTemplate == null)
                segmentTemplate = FindTemplate("SegmentTemplate");

            if (snapTemplate == null)
                snapTemplate = FindTemplate("SnapTemplate");

            if (segmentTemplate != null)
                segmentTemplate.gameObject.SetActive(false);

            if (snapTemplate != null)
                snapTemplate.gameObject.SetActive(false);
        }

        private void ClearActiveSegments()
        {
            for (int i = 0; i < activeSegments.Count; i++)
            {
                SpriteRenderer segment = activeSegments[i];
                if (segment != null)
                    Destroy(segment.gameObject);
            }

            activeSegments.Clear();
            segmentStartColors.Clear();
            segmentBaseSizes.Clear();
            segmentMidpoints.Clear();
            segmentRotations.Clear();
            segmentPivotCenterRatios.Clear();

            for (int i = 0; i < activeSnaps.Count; i++)
            {
                SpriteRenderer snap = activeSnaps[i];
                if (snap != null)
                    Destroy(snap.gameObject);
            }

            activeSnaps.Clear();
            snapStartColors.Clear();
        }

        private void StopLifetimeRoutine()
        {
            if (lifetimeRoutine == null)
                return;

            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        private void OnDestroy()
        {
            StopLifetimeRoutine();
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private SpriteRenderer FindTemplate(string templateName)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null && renderer.gameObject.name == templateName)
                    return renderer;
            }

            return null;
        }
    }
}
