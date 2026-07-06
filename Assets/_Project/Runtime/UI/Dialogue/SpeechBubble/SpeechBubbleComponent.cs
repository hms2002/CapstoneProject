using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 책임 :
/// - 월드 말풍선 prefab pool, typing, 위치 보정, 병렬 말풍선 layout을 관리한다.
/// - gameplay 호출부에는 ISpeechBubblePlayback 계약으로만 노출되는 UI 구현체다.
/// </summary>
public class SpeechBubbleComponent : MonoBehaviour, ISpeechBubblePlayback
{
    private const int DefaultPoolCapacity = 2;
    private const int MaxPooledBubbles = 8;
    private const float ParallelBubblePadding = 0.08f;
    private const float ParallelSmallHorizontalNudge = 0.45f;
    private const float ParallelWideHorizontalNudge = 0.9f;
    private const float ParallelVerticalFallbackStep = 0.35f;
    private const float ParallelWideVerticalFallback = 0.7f;
    private const float PlacementOverlapScoreWeight = 10000f;
    private const float PlacementTailDistanceScoreWeight = 160f;
    private const float PlacementOffscreenScoreWeight = 40f;
    private const float PlacementHorizontalMoveScoreWeight = 18f;
    private const float PlacementVerticalMoveScoreWeight = 14f;
    private const float ActiveRightTailTieBreakPenalty = 0.25f;

    private static readonly SpeechBubbleTailSide[] PlacementTailSides =
    {
        SpeechBubbleTailSide.Left,
        SpeechBubbleTailSide.Right
    };

    private static readonly Vector3[] ParallelPlacementOffsets =
    {
        Vector3.zero,
        new Vector3(-ParallelSmallHorizontalNudge, 0f, 0f),
        new Vector3(ParallelSmallHorizontalNudge, 0f, 0f),
        new Vector3(0f, ParallelVerticalFallbackStep, 0f),
        new Vector3(-ParallelSmallHorizontalNudge, ParallelVerticalFallbackStep, 0f),
        new Vector3(ParallelSmallHorizontalNudge, ParallelVerticalFallbackStep, 0f),
        new Vector3(-ParallelWideHorizontalNudge, 0f, 0f),
        new Vector3(ParallelWideHorizontalNudge, 0f, 0f),
        new Vector3(0f, ParallelWideVerticalFallback, 0f)
    };

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
    private readonly List<BubblePlacementCandidate> placementObstacles = new List<BubblePlacementCandidate>();

    public Transform BubbleTransform => transform;

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

        if (bubble == null)
            return;

        bubble.SetPlacement(SpeechBubbleTailSide.Left, Vector3.zero);

        parallelBubbles.Remove(bubble);
        if (parallelBubbles.Count == 0)
            ResetConversationPlacement();

        bubblePool.Release(bubble);
    }

    private void LayoutParallelBubbles()
    {
        PruneParallelBubbles();
        if (parallelBubbles.Count == 0)
        {
            ResetConversationPlacement();
            return;
        }

        Canvas.ForceUpdateCanvases();

        if (activeBubble != null &&
            parallelBubbles.Count == 1 &&
            TrySolvePairPlacement(activeBubble, parallelBubbles[0], out PairPlacementResult pairResult))
        {
            ApplyPlacement(pairResult.ActiveCandidate);
            ApplyPlacement(pairResult.ParallelCandidate);
            Canvas.ForceUpdateCanvases();
            return;
        }

        LayoutCandidatePlacements();
        Canvas.ForceUpdateCanvases();
    }

    private void ResetConversationPlacement()
    {
        if (activeBubble != null)
            activeBubble.SetPlacement(SpeechBubbleTailSide.Left, Vector3.zero);

        for (int i = 0; i < parallelBubbles.Count; i++)
        {
            SpeechBubble bubble = parallelBubbles[i];
            if (bubble != null)
                bubble.SetPlacement(SpeechBubbleTailSide.Left, Vector3.zero);
        }
    }

    private void LayoutCandidatePlacements()
    {
        placementObstacles.Clear();

        if (activeBubble != null)
        {
            if (TryResolveBestActiveCandidate(activeBubble, placementObstacles, out BubblePlacementCandidate activeCandidate))
            {
                ApplyPlacement(activeCandidate);
                placementObstacles.Add(activeCandidate);
            }
            else
            {
                activeBubble.SetPlacement(SpeechBubbleTailSide.Left, Vector3.zero);
            }
        }

        for (int i = 0; i < parallelBubbles.Count; i++)
        {
            SpeechBubble bubble = parallelBubbles[i];
            if (bubble != null)
                LayoutParallelBubbleCandidate(bubble, placementObstacles);
        }
    }

    private void LayoutParallelBubbleCandidate(
        SpeechBubble bubble,
        List<BubblePlacementCandidate> obstacles)
    {
        if (TryResolveBestParallelCandidate(bubble, obstacles, out BubblePlacementCandidate candidate))
        {
            ApplyPlacement(candidate);
            obstacles.Add(candidate);
            return;
        }

        if (bubble != null)
            bubble.SetPlacement(SpeechBubbleTailSide.Left, Vector3.zero);
    }

    private bool TrySolvePairPlacement(
        SpeechBubble active,
        SpeechBubble parallel,
        out PairPlacementResult result)
    {
        result = default;
        bool hasResult = false;

        for (int activeSideIndex = 0; activeSideIndex < PlacementTailSides.Length; activeSideIndex++)
        {
            SpeechBubbleTailSide activeSide = PlacementTailSides[activeSideIndex];
            if (!TryCreatePlacementCandidate(active, activeSide, Vector3.zero, true, out BubblePlacementCandidate activeCandidate))
                continue;

            for (int parallelSideIndex = 0; parallelSideIndex < PlacementTailSides.Length; parallelSideIndex++)
            {
                SpeechBubbleTailSide parallelSide = PlacementTailSides[parallelSideIndex];
                for (int offsetIndex = 0; offsetIndex < ParallelPlacementOffsets.Length; offsetIndex++)
                {
                    Vector3 parallelOffset = ParallelPlacementOffsets[offsetIndex];
                    if (!TryCreatePlacementCandidate(parallel, parallelSide, parallelOffset, false, out BubblePlacementCandidate parallelCandidate))
                        continue;

                    float score =
                        activeCandidate.Score +
                        parallelCandidate.Score +
                        CalculatePlacementOverlapScore(activeCandidate.Bounds, parallelCandidate.Bounds);

                    if (hasResult && score >= result.Score)
                        continue;

                    result = new PairPlacementResult(activeCandidate, parallelCandidate, score);
                    hasResult = true;
                }
            }
        }

        return hasResult;
    }

    private void PruneParallelBubbles()
    {
        for (int i = parallelBubbles.Count - 1; i >= 0; i--)
        {
            if (parallelBubbles[i] == null)
                parallelBubbles.RemoveAt(i);
        }
    }

    private bool TryResolveBestActiveCandidate(
        SpeechBubble bubble,
        List<BubblePlacementCandidate> obstacles,
        out BubblePlacementCandidate bestCandidate)
    {
        bestCandidate = default;
        bool hasCandidate = false;

        for (int sideIndex = 0; sideIndex < PlacementTailSides.Length; sideIndex++)
        {
            SpeechBubbleTailSide side = PlacementTailSides[sideIndex];
            if (!TryCreatePlacementCandidate(bubble, side, Vector3.zero, true, out BubblePlacementCandidate candidate))
                continue;

            float score = candidate.Score + CalculateObstacleOverlapScore(candidate.Bounds, obstacles);
            candidate = candidate.WithScore(score);
            if (hasCandidate && candidate.Score >= bestCandidate.Score)
                continue;

            bestCandidate = candidate;
            hasCandidate = true;
        }

        return hasCandidate;
    }

    private bool TryResolveBestParallelCandidate(
        SpeechBubble bubble,
        List<BubblePlacementCandidate> obstacles,
        out BubblePlacementCandidate bestCandidate)
    {
        bestCandidate = default;
        bool hasCandidate = false;

        for (int sideIndex = 0; sideIndex < PlacementTailSides.Length; sideIndex++)
        {
            SpeechBubbleTailSide side = PlacementTailSides[sideIndex];
            for (int offsetIndex = 0; offsetIndex < ParallelPlacementOffsets.Length; offsetIndex++)
            {
                Vector3 offset = ParallelPlacementOffsets[offsetIndex];
                if (!TryCreatePlacementCandidate(bubble, side, offset, false, out BubblePlacementCandidate candidate))
                    continue;

                float score = candidate.Score + CalculateObstacleOverlapScore(candidate.Bounds, obstacles);
                candidate = candidate.WithScore(score);
                if (hasCandidate && candidate.Score >= bestCandidate.Score)
                    continue;

                bestCandidate = candidate;
                hasCandidate = true;
            }
        }

        return hasCandidate;
    }

    private bool TryCreatePlacementCandidate(
        SpeechBubble bubble,
        SpeechBubbleTailSide tailSide,
        Vector3 layoutOffset,
        bool isActive,
        out BubblePlacementCandidate candidate)
    {
        candidate = default;

        if (bubble == null)
            return false;

        if (!bubble.TryGetPlacementBounds(
            tailSide,
            layoutOffset,
            out Bounds bounds,
            out Vector3 desiredRootPosition,
            out Vector3 tailPivotPosition))
            return false;

        float score = CalculatePlacementBaseScore(
            tailSide,
            layoutOffset,
            bounds,
            desiredRootPosition,
            tailPivotPosition,
            isActive);

        candidate = new BubblePlacementCandidate(
            bubble,
            tailSide,
            layoutOffset,
            bounds,
            desiredRootPosition,
            tailPivotPosition,
            score);
        return true;
    }

    private static float CalculatePlacementBaseScore(
        SpeechBubbleTailSide tailSide,
        Vector3 layoutOffset,
        Bounds bounds,
        Vector3 desiredRootPosition,
        Vector3 tailPivotPosition,
        bool isActive)
    {
        float tailDistance = Vector2.Distance(
            new Vector2(tailPivotPosition.x, tailPivotPosition.y),
            new Vector2(desiredRootPosition.x, desiredRootPosition.y));

        float score =
            tailDistance * PlacementTailDistanceScoreWeight +
            Mathf.Abs(layoutOffset.x) * PlacementHorizontalMoveScoreWeight +
            Mathf.Abs(layoutOffset.y) * PlacementVerticalMoveScoreWeight +
            CalculateOffscreenPenalty(bounds) * PlacementOffscreenScoreWeight;

        if (isActive && tailSide == SpeechBubbleTailSide.Right)
            score += ActiveRightTailTieBreakPenalty;

        return score;
    }

    private static float CalculateObstacleOverlapScore(
        Bounds bounds,
        List<BubblePlacementCandidate> obstacles)
    {
        float overlapArea = 0f;
        for (int i = 0; i < obstacles.Count; i++)
        {
            BubblePlacementCandidate obstacle = obstacles[i];
            if (!obstacle.IsValid)
                continue;

            overlapArea += CalculatePlacementOverlapArea(bounds, obstacle.Bounds);
        }

        return overlapArea * PlacementOverlapScoreWeight;
    }

    private static float CalculatePlacementOverlapScore(Bounds first, Bounds second)
    {
        return CalculatePlacementOverlapArea(first, second) * PlacementOverlapScoreWeight;
    }

    private static float CalculatePlacementOverlapArea(Bounds first, Bounds second)
    {
        return CalculateOverlapArea(first, ExpandBounds(second, ParallelBubblePadding));
    }

    private static void ApplyPlacement(BubblePlacementCandidate candidate)
    {
        if (!candidate.IsValid || candidate.Bubble == null)
            return;

        candidate.Bubble.SetPlacement(candidate.TailSide, candidate.LayoutOffset);
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

    // 책임: 말풍선 단일 배치 후보의 꼬리 방향, bounds, 목표 위치, 점수를 보관한다.
    private struct BubblePlacementCandidate
    {
        public SpeechBubble Bubble { get; }
        public SpeechBubbleTailSide TailSide { get; }
        public Vector3 LayoutOffset { get; }
        public Bounds Bounds { get; }
        public Vector3 DesiredRootPosition { get; }
        public Vector3 TailPivotPosition { get; }
        public float Score { get; }
        public bool IsValid { get; }

        public BubblePlacementCandidate(
            SpeechBubble bubble,
            SpeechBubbleTailSide tailSide,
            Vector3 layoutOffset,
            Bounds bounds,
            Vector3 desiredRootPosition,
            Vector3 tailPivotPosition,
            float score)
        {
            Bubble = bubble;
            TailSide = tailSide;
            LayoutOffset = layoutOffset;
            Bounds = bounds;
            DesiredRootPosition = desiredRootPosition;
            TailPivotPosition = tailPivotPosition;
            Score = score;
            IsValid = bubble != null;
        }

        public BubblePlacementCandidate WithScore(float score)
        {
            return new BubblePlacementCandidate(
                Bubble,
                TailSide,
                LayoutOffset,
                Bounds,
                DesiredRootPosition,
                TailPivotPosition,
                score);
        }
    }

    // 책임: 활성/병렬 말풍선 후보 조합과 최종 배치 점수를 보관한다.
    private struct PairPlacementResult
    {
        public BubblePlacementCandidate ActiveCandidate { get; }
        public BubblePlacementCandidate ParallelCandidate { get; }
        public float Score { get; }

        public PairPlacementResult(
            BubblePlacementCandidate activeCandidate,
            BubblePlacementCandidate parallelCandidate,
            float score)
        {
            ActiveCandidate = activeCandidate;
            ParallelCandidate = parallelCandidate;
            Score = score;
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
