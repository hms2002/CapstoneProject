using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class SpeechBubbleComponent : MonoBehaviour
{
    private const int DefaultPoolCapacity = 2;
    private const int MaxPooledBubbles = 8;
    private const float ParallelBubblePadding = 0.08f;
    private const float ParallelMaxHorizontalOffset = 4f;
    private const float ParallelVerticalFallbackStep = 0.35f;
    private const float ParallelMaxVerticalOffset = 1.4f;
    private const float LayoutClearEpsilon = 0.0001f;
    private const float LayoutOverlapScoreWeight = 10000f;
    private const float LayoutOffscreenScoreWeight = 1000f;
    private const float LayoutHorizontalMoveScoreWeight = 10f;
    private const float LayoutVerticalMoveScoreWeight = 12f;

    [Header("Bubble Settings")]
    [SerializeField] private SpeechBubble bubblePrefab;
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0, 2f, 0);
    [SerializeField] private bool showBubbleOffsetGizmo = true;
    [SerializeField] private Color bubbleOffsetGizmoColor = new Color(1f, 0.92f, 0.16f, 0.9f);
    [SerializeField] private float bubbleOffsetGizmoRadius = 0.12f;

    [Header("Typing Settings")]
    [SerializeField] private bool defaultUseTyping = true;
    [SerializeField] private float defaultTypingSpeed = 0.05f;

    private IObjectPool<SpeechBubble> bubblePool;
    private SpeechBubble activeBubble;
    private readonly List<SpeechBubble> parallelBubbles = new List<SpeechBubble>();
    private readonly List<SpeechBubble> layoutObstacles = new List<SpeechBubble>();

    private void Awake()
    {
        bubblePool = new ObjectPool<SpeechBubble>(
            createFunc: () => Instantiate(bubblePrefab),
            actionOnGet: (bubble) => bubble.gameObject.SetActive(true),
            actionOnRelease: (bubble) => bubble.gameObject.SetActive(false),
            actionOnDestroy: (bubble) => Destroy(bubble.gameObject),
            defaultCapacity: DefaultPoolCapacity,
            maxSize: MaxPooledBubbles
        );
    }

    private void LateUpdate()
    {
        LayoutParallelBubbles();
    }

    public void Speak(string text, float duration = 2.5f)
    {
        Speak(text, duration, null);
    }

    public void Speak(string text, float duration, SpeechBubbleThemeSettings theme)
    {
        Speak(text, duration, theme, null);
    }

    public void Speak(string text, float duration, SpeechBubbleThemeSettings theme, Action onHidden)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            Vector3.zero,
            false,
            0f,
            0f,
            0f);
    }

    public void SpeakAnimated(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        DialogueAnimType animType)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            Vector3.zero,
            false,
            0f,
            0f,
            0f,
            true,
            animType);
    }

    public void SpeakParallelAt(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Transform anchor,
        Vector3 offsetDelta)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            offsetDelta,
            false,
            0f,
            0f,
            0f,
            false,
            DialogueAnimType.Normal,
            anchor,
            null,
            true);
    }

    public void SpeakParallelAt(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Func<Vector3> anchorPositionResolver,
        Vector3 offsetDelta)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            offsetDelta,
            false,
            0f,
            0f,
            0f,
            false,
            DialogueAnimType.Normal,
            null,
            anchorPositionResolver,
            true);
    }

    public void SpeakParallelAt(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Func<Vector3> anchorPositionResolver,
        Func<Quaternion> anchorRotationResolver,
        Vector3 offsetDelta)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            offsetDelta,
            false,
            0f,
            0f,
            0f,
            false,
            DialogueAnimType.Normal,
            null,
            anchorPositionResolver,
            true,
            anchorRotationResolver);
    }

    public void SpeakParallelAnimatedAt(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        DialogueAnimType animType,
        Transform anchor,
        Vector3 offsetDelta)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            offsetDelta,
            false,
            0f,
            0f,
            0f,
            true,
            animType,
            anchor,
            null,
            true);
    }

    public void SpeakParallelAnimatedAt(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        DialogueAnimType animType,
        Func<Vector3> anchorPositionResolver,
        Vector3 offsetDelta)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            offsetDelta,
            false,
            0f,
            0f,
            0f,
            true,
            animType,
            null,
            anchorPositionResolver,
            true);
    }

    public void SpeakWithOffsetDelta(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Vector3 offsetDelta)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            offsetDelta,
            false,
            0f,
            0f,
            0f);
    }

    public void SpeakWithPreSizedLayout(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight)
    {
        SpeakInternal(
            text,
            duration,
            theme,
            onHidden,
            Vector3.zero,
            true,
            minTextWidth,
            maxTextWidth,
            minTextHeight);
    }

    private void SpeakInternal(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        Vector3 offsetDelta,
        bool preSizeLayout,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight,
        bool useAnimatedReveal = false,
        DialogueAnimType animType = DialogueAnimType.Normal,
        Transform anchorOverride = null,
        Func<Vector3> anchorPositionResolver = null,
        bool allowParallel = false,
        Func<Quaternion> anchorRotationResolver = null)
    {
        if (bubblePrefab == null ||
            string.IsNullOrWhiteSpace(text) ||
            anchorOverride == null && anchorPositionResolver == null && transform == null)
            return;

        SpeechBubble bubble = GetBubble(allowParallel);
        Transform resolvedAnchor = anchorOverride != null ? anchorOverride : transform;

        if (useAnimatedReveal)
        {
            if (anchorPositionResolver != null)
            {
                bubble.SetupAndShowAnimated(
                    anchorPositionResolver,
                    bubbleOffset + offsetDelta,
                    text,
                    duration,
                    theme,
                    onHidden,
                    HandleBubbleReleased,
                    animType,
                    preSizeLayout,
                    minTextWidth,
                    maxTextWidth,
                    minTextHeight);
            }
            else
            {
                bubble.SetupAndShowAnimated(
                    resolvedAnchor,
                    bubbleOffset + offsetDelta,
                    text,
                    duration,
                    theme,
                    onHidden,
                    HandleBubbleReleased,
                    animType,
                    preSizeLayout,
                    minTextWidth,
                    maxTextWidth,
                    minTextHeight);
            }
        }
        else
        {
            if (anchorPositionResolver != null)
            {
                bubble.SetupAndShow(
                    anchorPositionResolver,
                    anchorRotationResolver,
                    bubbleOffset + offsetDelta,
                    text,
                    duration,
                    defaultUseTyping,
                    defaultTypingSpeed,
                    theme,
                    onHidden,
                    HandleBubbleReleased,
                    preSizeLayout,
                    minTextWidth,
                    maxTextWidth,
                    minTextHeight);
            }
            else
            {
                bubble.SetupAndShow(
                    resolvedAnchor,
                    bubbleOffset + offsetDelta,
                    text,
                    duration,
                    defaultUseTyping,
                    defaultTypingSpeed,
                    theme,
                    onHidden,
                    HandleBubbleReleased,
                    preSizeLayout,
                    minTextWidth,
                    maxTextWidth,
                    minTextHeight);
            }
        }

        if (parallelBubbles.Count > 0)
            LayoutParallelBubbles();
    }

    private SpeechBubble GetBubble(bool allowParallel)
    {
        if (allowParallel)
        {
            SpeechBubble parallelBubble = bubblePool.Get();
            parallelBubbles.Add(parallelBubble);
            return parallelBubble;
        }

        SpeechBubble bubble = activeBubble;
        if (bubble == null)
        {
            bubble = bubblePool.Get();
            activeBubble = bubble;
        }

        return bubble;
    }

    public void HideActive()
    {
        if (!TryGetActiveBubble(out SpeechBubble bubble))
            return;

        bubble.Hide();
    }

    public bool TryAdvanceActive()
    {
        return TryGetActiveBubble(out SpeechBubble bubble) && bubble.TryAdvance();
    }

    private void HandleBubbleReleased(SpeechBubble bubble)
    {
        if (activeBubble == bubble)
            activeBubble = null;

        if (bubble != null)
            bubble.SetLayoutOffset(Vector3.zero);

        parallelBubbles.Remove(bubble);
        bubblePool.Release(bubble);
    }

    private void LayoutParallelBubbles()
    {
        if (parallelBubbles.Count == 0)
            return;

        PruneParallelBubbles();
        if (parallelBubbles.Count == 0)
            return;

        Canvas.ForceUpdateCanvases();
        layoutObstacles.Clear();

        if (activeBubble != null && activeBubble.TryGetWorldBounds(out _))
            layoutObstacles.Add(activeBubble);

        for (int i = 0; i < parallelBubbles.Count; i++)
        {
            SpeechBubble bubble = parallelBubbles[i];
            if (bubble != null)
                bubble.SetLayoutOffset(Vector3.zero);
        }

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < parallelBubbles.Count; i++)
        {
            SpeechBubble bubble = parallelBubbles[i];
            if (bubble == null)
                continue;

            Vector3 layoutOffset = ResolveParallelLayoutOffset(bubble, layoutObstacles);
            bubble.SetLayoutOffset(layoutOffset);
            if (bubble.TryGetWorldBounds(out _))
                layoutObstacles.Add(bubble);
        }
    }

    private void PruneParallelBubbles()
    {
        for (int i = parallelBubbles.Count - 1; i >= 0; i--)
        {
            if (parallelBubbles[i] == null)
                parallelBubbles.RemoveAt(i);
        }
    }

    private Vector3 ResolveParallelLayoutOffset(SpeechBubble bubble, List<SpeechBubble> obstacles)
    {
        if (obstacles.Count == 0 || !bubble.TryGetWorldBounds(out Bounds baseBounds))
            return Vector3.zero;

        if (!TryFindPrimaryOverlap(baseBounds, obstacles, out Bounds primaryOverlap))
            return Vector3.zero;

        float preferredSign = baseBounds.center.x >= primaryOverlap.center.x ? 1f : -1f;
        LayoutCandidateScore preferredHorizontal = EvaluateCandidate(
            baseBounds,
            obstacles,
            new Vector3(ResolveHorizontalOffset(baseBounds, obstacles, preferredSign), 0f, 0f));
        LayoutCandidateScore oppositeHorizontal = EvaluateCandidate(
            baseBounds,
            obstacles,
            new Vector3(ResolveHorizontalOffset(baseBounds, obstacles, -preferredSign), 0f, 0f));
        LayoutCandidateScore bestHorizontal = SelectBetterCandidate(preferredHorizontal, oppositeHorizontal);
        if (bestHorizontal.IsClear)
            return bestHorizontal.Offset;

        float verticalOffset = ResolveVerticalFallbackOffset(baseBounds, obstacles);
        LayoutCandidateScore verticalFallback = EvaluateCandidate(
            baseBounds,
            obstacles,
            new Vector3(0f, verticalOffset, 0f));
        LayoutCandidateScore horizontalVerticalFallback = EvaluateCandidate(
            baseBounds,
            obstacles,
            new Vector3(bestHorizontal.Offset.x, verticalOffset, 0f));

        return SelectBetterCandidate(
            bestHorizontal,
            SelectBetterCandidate(verticalFallback, horizontalVerticalFallback)).Offset;
    }

    private bool TryFindPrimaryOverlap(
        Bounds baseBounds,
        List<SpeechBubble> obstacles,
        out Bounds primaryOverlap)
    {
        primaryOverlap = default;
        float bestOverlapArea = 0f;

        for (int i = 0; i < obstacles.Count; i++)
        {
            SpeechBubble obstacle = obstacles[i];
            if (obstacle == null || !obstacle.TryGetWorldBounds(out Bounds obstacleBounds))
                continue;

            Bounds paddedBounds = ExpandBounds(obstacleBounds, ParallelBubblePadding);
            float overlapArea = CalculateOverlapArea(baseBounds, paddedBounds);
            if (overlapArea <= bestOverlapArea)
                continue;

            bestOverlapArea = overlapArea;
            primaryOverlap = paddedBounds;
        }

        return bestOverlapArea > LayoutClearEpsilon;
    }

    private float ResolveHorizontalOffset(Bounds baseBounds, List<SpeechBubble> obstacles, float sign)
    {
        float requiredOffset = 0f;

        for (int i = 0; i < obstacles.Count; i++)
        {
            SpeechBubble obstacle = obstacles[i];
            if (obstacle == null || !obstacle.TryGetWorldBounds(out Bounds obstacleBounds))
                continue;

            Bounds paddedBounds = ExpandBounds(obstacleBounds, ParallelBubblePadding);
            if (!RangesOverlap(baseBounds.min.y, baseBounds.max.y, paddedBounds.min.y, paddedBounds.max.y))
                continue;

            float required = sign > 0f
                ? paddedBounds.max.x - baseBounds.min.x
                : baseBounds.max.x - paddedBounds.min.x;
            requiredOffset = Mathf.Max(requiredOffset, required);
        }

        return Mathf.Clamp(requiredOffset, 0f, ParallelMaxHorizontalOffset) * Mathf.Sign(sign);
    }

    private float ResolveVerticalFallbackOffset(Bounds baseBounds, List<SpeechBubble> obstacles)
    {
        float requiredOffset = ParallelVerticalFallbackStep;

        for (int i = 0; i < obstacles.Count; i++)
        {
            SpeechBubble obstacle = obstacles[i];
            if (obstacle == null || !obstacle.TryGetWorldBounds(out Bounds obstacleBounds))
                continue;

            Bounds paddedBounds = ExpandBounds(obstacleBounds, ParallelBubblePadding);
            if (!RangesOverlap(baseBounds.min.x, baseBounds.max.x, paddedBounds.min.x, paddedBounds.max.x))
                continue;

            requiredOffset = Mathf.Max(requiredOffset, paddedBounds.max.y - baseBounds.min.y);
        }

        return Mathf.Clamp(requiredOffset, ParallelVerticalFallbackStep, ParallelMaxVerticalOffset);
    }

    private LayoutCandidateScore EvaluateCandidate(
        Bounds baseBounds,
        List<SpeechBubble> obstacles,
        Vector3 offset)
    {
        Bounds movedBounds = baseBounds;
        movedBounds.center += offset;

        float overlapArea = 0f;
        for (int i = 0; i < obstacles.Count; i++)
        {
            SpeechBubble obstacle = obstacles[i];
            if (obstacle == null || !obstacle.TryGetWorldBounds(out Bounds obstacleBounds))
                continue;

            overlapArea += CalculateOverlapArea(
                movedBounds,
                ExpandBounds(obstacleBounds, ParallelBubblePadding));
        }

        float offscreenPenalty = CalculateOffscreenPenalty(movedBounds);
        float score =
            overlapArea * LayoutOverlapScoreWeight +
            offscreenPenalty * LayoutOffscreenScoreWeight +
            Mathf.Abs(offset.x) * LayoutHorizontalMoveScoreWeight +
            Mathf.Abs(offset.y) * LayoutVerticalMoveScoreWeight;

        return new LayoutCandidateScore(offset, score, overlapArea, offscreenPenalty);
    }

    private static LayoutCandidateScore SelectBetterCandidate(
        LayoutCandidateScore first,
        LayoutCandidateScore second)
    {
        return second.Score < first.Score ? second : first;
    }

    private static float CalculateOverlapArea(Bounds first, Bounds second)
    {
        float width = Mathf.Min(first.max.x, second.max.x) - Mathf.Max(first.min.x, second.min.x);
        float height = Mathf.Min(first.max.y, second.max.y) - Mathf.Max(first.min.y, second.min.y);
        if (width <= 0f || height <= 0f)
            return 0f;

        return width * height;
    }

    private static Bounds ExpandBounds(Bounds bounds, float padding)
    {
        bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
        return bounds;
    }

    private static bool RangesOverlap(float firstMin, float firstMax, float secondMin, float secondMax)
    {
        return firstMin < secondMax && firstMax > secondMin;
    }

    private static float CalculateOffscreenPenalty(Bounds bounds)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return 0f;

        float penalty = 0f;
        penalty += CalculateViewportPointPenalty(camera.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z)));
        penalty += CalculateViewportPointPenalty(camera.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)));
        penalty += CalculateViewportPointPenalty(camera.WorldToViewportPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z)));
        penalty += CalculateViewportPointPenalty(camera.WorldToViewportPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)));
        return penalty;
    }

    private static float CalculateViewportPointPenalty(Vector3 viewportPoint)
    {
        if (viewportPoint.z < 0f)
            return 10f;

        float penalty = 0f;
        penalty += Mathf.Max(0f, -viewportPoint.x);
        penalty += Mathf.Max(0f, viewportPoint.x - 1f);
        penalty += Mathf.Max(0f, -viewportPoint.y);
        penalty += Mathf.Max(0f, viewportPoint.y - 1f);
        return penalty;
    }

    private struct LayoutCandidateScore
    {
        public Vector3 Offset { get; }
        public float Score { get; }
        public bool IsClear { get; }

        public LayoutCandidateScore(
            Vector3 offset,
            float score,
            float overlapArea,
            float offscreenPenalty)
        {
            Offset = offset;
            Score = score;
            IsClear = overlapArea <= LayoutClearEpsilon && offscreenPenalty <= LayoutClearEpsilon;
        }
    }

    private bool TryGetActiveBubble(out SpeechBubble bubble)
    {
        bubble = activeBubble;
        if (bubble != null)
            return true;

        activeBubble = null;
        bubble = null;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showBubbleOffsetGizmo)
            return;

        Vector3 origin = transform.position;
        Vector3 targetPosition = origin + bubbleOffset;
        float radius = Mathf.Max(0.01f, bubbleOffsetGizmoRadius);

        Gizmos.color = bubbleOffsetGizmoColor;
        Gizmos.DrawLine(origin, targetPosition);
        Gizmos.DrawWireSphere(targetPosition, radius);
        Gizmos.DrawSphere(targetPosition, radius * 0.35f);
    }
}
