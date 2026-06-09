using System.Collections.Generic;
using UnityEngine;

public enum SettingsPanelChainBottomEndpointMode
{
    Anchored = 0,
    FreeHanging = 1
}

[DisallowMultipleComponent]
public sealed class SettingsPanelFakeChainPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform chainContainer;
    [SerializeField] private RectTransform topAnchor;
    [SerializeField] private SettingsPanelChainBottomEndpointMode bottomEndpointMode;
    [SerializeField] private RectTransform bottomAnchor;
    [SerializeField] private Vector2 bottomAnchorLocalOffset;
    [SerializeField] private Vector2 freeEndLocalOffset = new Vector2(0f, -220f);
    [SerializeField] private RectTransform[] chainLinks;

    [Header("Simulation")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(1)] private int simulationSubsteps = 2;
    [SerializeField, Min(1)] private int constraintIterations = 12;
    [SerializeField, Min(0.001f)] private float maxSimulationStep = 1f / 60f;
    [SerializeField, Min(0f)] private float velocityDamping = 10f;
    [SerializeField] private Vector2 localGravity = new Vector2(0f, -1800f);
    [SerializeField] private bool snapOnEnable = true;

    [Header("Mouse Interaction")]
    [SerializeField] private bool enableMouseBrushInteraction;
    [SerializeField, Min(0f)] private float mouseBrushRadius = 72f;
    [SerializeField, Min(0f)] private float mouseBrushPushStrength = 520f;
    [SerializeField, Min(0f)] private float mouseBrushDragInfluence = 0.2f;
    [SerializeField, Min(0f)] private float mouseBrushMinDeltaPerFrame = 6f;
    [SerializeField] private bool requirePointerOverVisibleLinks = true;
    [SerializeField] private bool suppressBrushWhileAltHeld = true;
    [SerializeField, Min(0f)] private float maxMouseDeltaPerFrame = 160f;

    [Header("Support Motion Response")]
    [SerializeField] private bool enableSupportMotionResponse = true;
    [SerializeField] private RectTransform supportMotionSource;
    [SerializeField, Min(0f)] private float supportMotionInfluence = 0.85f;
    [SerializeField, Min(0f)] private float supportMotionDeadZone = 0.75f;
    [SerializeField, Min(0f)] private float supportMotionSmoothing = 20f;
    [SerializeField, Min(0f)] private float supportMotionMaxOffsetPerFrame = 64f;

    [Header("Link Layout")]
    [SerializeField, Min(0.01f)] private float segmentLengthMultiplier = 1f;
    [SerializeField, Min(0f)] private float segmentLengthPadding;
    [SerializeField] private float linkAngleOffset = -90f;

    [Header("Reach Constraint")]
    [SerializeField, Min(0.01f)] private float reachMultiplier = 1f;
    [SerializeField] private float reachPadding;

    [Header("Visual Overrides")]
    [SerializeField] private Vector2 lastLinkLocalOffset;
    [SerializeField, HideInInspector] private float[] authoredSegmentLengths;
    [SerializeField, HideInInspector] private float authoredEndpointDistance;

    private Vector2[] jointPositions;
    private Vector2[] previousJointPositions;
    private float[] segmentLengths;
    private float totalChainLength;
    private bool initialized;
    private bool hasPreviousMouseLocalPosition;
    private Vector2 previousMouseLocalPosition;
    private bool hasPreviousSupportMotionSourcePosition;
    private Vector2 previousSupportMotionSourcePosition;
    private Vector2 smoothedSupportMotionLocalDelta;
    private bool hasLayoutSignature;
    private Vector2 lastContainerRectSize;
    private float lastCanvasScaleFactor;
    private Vector2Int lastScreenSize;
    private bool layoutRefreshPending;

    public float TotalChainLength
    {
        get
        {
            RefreshSegmentLengths();
            return Mathf.Max(0f, totalChainLength * reachMultiplier + reachPadding);
        }
    }

    public void CaptureAuthoredSegmentLengths()
    {
        ResolveReferences();
        if (chainLinks == null || chainLinks.Length == 0)
        {
            authoredSegmentLengths = null;
            authoredEndpointDistance = 0f;
            return;
        }

        if (authoredSegmentLengths == null || authoredSegmentLengths.Length != chainLinks.Length)
            authoredSegmentLengths = new float[chainLinks.Length];

        if (TryGetEndpointLocalPositions(out Vector2 topLocal, out Vector2 bottomLocal))
        {
            float authoredReach = Vector2.Distance(topLocal, bottomLocal);
            if (authoredReach > 0.001f)
            {
                authoredEndpointDistance = authoredReach;
                float uniformSegmentLength = Mathf.Max(1f, authoredReach / chainLinks.Length);
                for (int i = 0; i < authoredSegmentLengths.Length; i++)
                    authoredSegmentLengths[i] = uniformSegmentLength;

                return;
            }
        }

        authoredEndpointDistance = 0f;

        float fallbackLength = 32f;
        for (int i = 0; i < chainLinks.Length; i++)
        {
            RectTransform link = chainLinks[i];
            float length = link != null ? MeasureLinkLength(link) : fallbackLength;
            length = Mathf.Max(1f, length);
            authoredSegmentLengths[i] = length;
            fallbackLength = length;
        }
    }

    private void Reset()
    {
        chainContainer = transform as RectTransform;
        CaptureAuthoredSegmentLengths();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ResolveReferences();
        if (chainLinks == null || chainLinks.Length == 0)
        {
            authoredSegmentLengths = null;
            authoredEndpointDistance = 0f;
            return;
        }

        if (!HasValidAuthoredSegmentLengths() || authoredEndpointDistance <= 0f)
            CaptureAuthoredSegmentLengths();
    }

    private void OnEnable()
    {
        if (snapOnEnable)
            SnapToCurrentPose();

        SyncCachedInputState();
    }

    private void OnDisable()
    {
        hasPreviousMouseLocalPosition = false;
        hasPreviousSupportMotionSourcePosition = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SyncCachedInputState();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        SyncCachedInputState();
    }

    private void LateUpdate()
    {
        RefreshLayoutIfNeeded();

        if (!TryGetEndpointLocalPositions(out Vector2 topLocal, out Vector2 bottomLocal))
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        EnsureBuffers();
        if (jointPositions == null || jointPositions.Length == 0)
            return;

        if (!initialized)
            ResetSimulation(topLocal, bottomLocal);

        bool hasMouseBrushInteraction = TryGetMouseBrushState(
            out Vector2 mouseLocalPosition,
            out Vector2 mouseLocalDelta);
        bool hasSupportMotionResponse = TryGetSupportMotionDelta(deltaTime, out Vector2 supportMotionLocalDelta);

        int frameStepCount = 1;
        if (maxSimulationStep > 0f)
            frameStepCount = Mathf.Max(1, Mathf.CeilToInt(deltaTime / maxSimulationStep));

        float stepDeltaTime = deltaTime / frameStepCount;
        Vector2 stepMouseDelta = frameStepCount > 0 ? mouseLocalDelta / frameStepCount : mouseLocalDelta;
        Vector2 stepSupportMotionLocalDelta = frameStepCount > 0
            ? supportMotionLocalDelta / frameStepCount
            : supportMotionLocalDelta;
        Vector2 stepMousePosition = mouseLocalPosition - mouseLocalDelta;

        for (int i = 0; i < frameStepCount; i++)
        {
            if (hasMouseBrushInteraction)
                stepMousePosition += stepMouseDelta;

            Simulate(
                topLocal,
                bottomLocal,
                stepDeltaTime,
                hasMouseBrushInteraction,
                stepMousePosition,
                stepMouseDelta,
                hasSupportMotionResponse,
                stepSupportMotionLocalDelta);
        }

        ApplyLinkTransforms();
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshLayoutIfNeeded();
    }

    public void SnapToCurrentPose()
    {
        if (!TryGetEndpointLocalPositions(out Vector2 topLocal, out Vector2 bottomLocal))
            return;

        EnsureBuffers();
        if (jointPositions == null || jointPositions.Length == 0)
            return;

        ResetSimulation(topLocal, bottomLocal);
        ApplyLinkTransforms();
        SyncCachedInputState();
    }

    public bool TryGetTopAnchorWorldPosition(out Vector2 topWorldPosition)
    {
        ResolveReferences();
        if (topAnchor == null)
        {
            topWorldPosition = default;
            return false;
        }

        topWorldPosition = topAnchor.position;
        return true;
    }

    public bool TryGetBottomAnchorWorldPosition(out Vector2 bottomWorldPosition)
    {
        return TryGetBottomEndpointWorldPosition(out bottomWorldPosition);
    }

    public bool TryGetBottomEndpointWorldPosition(out Vector2 bottomWorldPosition)
    {
        ResolveReferences();
        if (!TryGetEndpointLocalPositions(out _, out Vector2 bottomLocal))
        {
            bottomWorldPosition = default;
            return false;
        }

        bottomWorldPosition = chainContainer.TransformPoint(bottomLocal);
        return true;
    }

    public Vector2 ClampWorldPositionToReach(Vector2 targetWorldPosition)
    {
        if (!TryGetTopAnchorWorldPosition(out Vector2 topWorldPosition))
            return targetWorldPosition;

        float maxReach = GetWorldReachLength();
        if (maxReach <= 0f)
            return targetWorldPosition;

        Vector2 toTarget = targetWorldPosition - topWorldPosition;
        float distance = toTarget.magnitude;
        if (distance <= maxReach || distance <= 0.0001f)
            return targetWorldPosition;

        return topWorldPosition + toTarget / distance * maxReach;
    }

    private float GetWorldReachLength()
    {
        float localReach = TotalChainLength;
        if (localReach <= 0f)
            return 0f;

        if (chainContainer == null)
            return localReach;

        float xScale = chainContainer.TransformVector(Vector3.right).magnitude;
        float yScale = chainContainer.TransformVector(Vector3.up).magnitude;
        float worldUnitsPerLocalUnit = Mathf.Max(xScale, yScale, 0.0001f);
        return localReach * worldUnitsPerLocalUnit;
    }

    public bool TryGetLastLinkHandleWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = default;
        if (!TryGetLastLinkBaseLocalPosition(out Vector2 baseLocalPosition))
            return false;

        worldPosition = chainContainer.TransformPoint(baseLocalPosition + lastLinkLocalOffset);
        return true;
    }

    public bool TryGetLastLinkBaseLocalPosition(out Vector2 localPosition)
    {
        localPosition = default;

        if (!TryGetEndpointLocalPositions(out Vector2 topLocal, out Vector2 bottomLocal))
            return false;

        EnsureBuffers();
        if (jointPositions == null || jointPositions.Length < 2)
            return false;

        if (!initialized)
            ResetSimulation(topLocal, bottomLocal);

        int lastLinkIndex = jointPositions.Length - 2;
        localPosition = (jointPositions[lastLinkIndex] + jointPositions[lastLinkIndex + 1]) * 0.5f;
        return true;
    }

    private void ResolveReferences()
    {
        if (chainContainer == null)
            chainContainer = transform as RectTransform;

        RectTransform resolvedTopAnchor = ResolveTopAnchorReference();
        if (resolvedTopAnchor != null)
            topAnchor = resolvedTopAnchor;
    }

    private RectTransform ResolveTopAnchorReference()
    {
        if (chainContainer == null)
            return topAnchor;

        if (topAnchor != null && !IsDescendantOf(topAnchor, chainContainer))
            return topAnchor;

        Vector3 referenceWorldPosition = bottomAnchor != null ? bottomAnchor.position : chainContainer.position;
        string preferredRootName = BuildPreferredTopAnchorRootName(chainContainer.name);

        RectTransform resolvedTopAnchor = FindBestTopAnchorInScope(
            chainContainer.parent as RectTransform,
            preferredRootName,
            referenceWorldPosition);
        if (resolvedTopAnchor != null)
            return resolvedTopAnchor;

        Transform parent = chainContainer.parent;
        if (parent != null)
        {
            resolvedTopAnchor = FindBestTopAnchorInScope(
                parent.parent as RectTransform,
                "TopAnchorRoot",
                referenceWorldPosition);
            if (resolvedTopAnchor != null)
                return resolvedTopAnchor;
        }

        return topAnchor;
    }

    private RectTransform FindBestTopAnchorInScope(
        RectTransform scope,
        string preferredRootName,
        Vector3 referenceWorldPosition)
    {
        if (scope == null)
            return null;

        RectTransform preferredRoot = null;
        List<RectTransform> fallbackRoots = null;
        int childCount = scope.childCount;
        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = scope.GetChild(i) as RectTransform;
            if (child == null || child == chainContainer)
                continue;

            if (!string.IsNullOrEmpty(preferredRootName) && child.name == preferredRootName)
                preferredRoot = child;

            if (child.name == "TopAnchorRoot" || child.name.EndsWith("TopAnchorRoot"))
            {
                fallbackRoots ??= new List<RectTransform>();
                fallbackRoots.Add(child);
            }
        }

        RectTransform resolvedTopAnchor = FindBestTopAnchorChild(preferredRoot, referenceWorldPosition);
        if (resolvedTopAnchor != null)
            return resolvedTopAnchor;

        if (fallbackRoots == null)
            return null;

        for (int i = 0; i < fallbackRoots.Count; i++)
        {
            RectTransform candidateRoot = fallbackRoots[i];
            if (candidateRoot == preferredRoot)
                continue;

            resolvedTopAnchor = FindBestTopAnchorChild(candidateRoot, referenceWorldPosition);
            if (resolvedTopAnchor != null)
                return resolvedTopAnchor;
        }

        return null;
    }

    private static RectTransform FindBestTopAnchorChild(RectTransform root, Vector3 referenceWorldPosition)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        RectTransform best = null;
        float bestDistanceSquared = float.MaxValue;
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect == root || !rect.name.StartsWith("TopAnchor"))
                continue;

            float distanceSquared = ((Vector2)rect.position - (Vector2)referenceWorldPosition).sqrMagnitude;
            if (distanceSquared >= bestDistanceSquared)
                continue;

            best = rect;
            bestDistanceSquared = distanceSquared;
        }

        return best;
    }

    private static string BuildPreferredTopAnchorRootName(string chainContainerName)
    {
        if (string.IsNullOrEmpty(chainContainerName))
            return "TopAnchorRoot";

        const string chainRootSuffix = "ChainRoot";
        if (chainContainerName.EndsWith(chainRootSuffix))
            return chainContainerName.Substring(0, chainContainerName.Length - chainRootSuffix.Length) + "TopAnchorRoot";

        return "TopAnchorRoot";
    }

    private static bool IsDescendantOf(Transform candidate, Transform potentialAncestor)
    {
        if (candidate == null || potentialAncestor == null)
            return false;

        Transform current = candidate;
        while (current != null)
        {
            if (current == potentialAncestor)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void RefreshLayoutIfNeeded(bool force = false)
    {
        ResolveReferences();
        if (chainContainer == null)
            return;

        if (!hasLayoutSignature)
        {
            UpdateLayoutSignature();
            return;
        }

        if (layoutRefreshPending)
        {
            ApplyLayoutRefresh();
            return;
        }

        if (!force && !HasLayoutSignatureChanged())
            return;

        UpdateLayoutSignature();
        ApplyLayoutRefresh();
    }

    private bool HasLayoutSignatureChanged()
    {
        Vector2 containerRectSize = chainContainer != null ? chainContainer.rect.size : Vector2.zero;
        float canvasScaleFactor = GetCanvasScaleFactor();
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        return lastContainerRectSize != containerRectSize
            || !Mathf.Approximately(lastCanvasScaleFactor, canvasScaleFactor)
            || lastScreenSize != screenSize;
    }

    private void UpdateLayoutSignature()
    {
        lastContainerRectSize = chainContainer != null ? chainContainer.rect.size : Vector2.zero;
        lastCanvasScaleFactor = GetCanvasScaleFactor();
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        hasLayoutSignature = true;
    }

    private void ApplyLayoutRefresh()
    {
        layoutRefreshPending = false;
        initialized = false;
        SyncCachedInputState();

        if (isActiveAndEnabled)
            SnapToCurrentPose();
    }

    private float GetCanvasScaleFactor()
    {
        Canvas canvas = chainContainer != null ? chainContainer.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.scaleFactor : 1f;
    }

    private bool TryGetEndpointLocalPositions(out Vector2 topLocal, out Vector2 bottomLocal)
    {
        topLocal = default;
        bottomLocal = default;

        ResolveReferences();
        if (chainContainer == null || topAnchor == null || chainLinks == null || chainLinks.Length == 0)
            return false;

        topLocal = chainContainer.InverseTransformPoint(topAnchor.position);
        if (UsesAnchoredBottomEndpoint())
        {
            if (bottomAnchor == null)
                return false;

            bottomLocal = (Vector2)chainContainer.InverseTransformPoint(bottomAnchor.position) + bottomAnchorLocalOffset;
        }
        else
        {
            bottomLocal = topLocal + freeEndLocalOffset;
        }

        return true;
    }

    private bool TryGetMouseBrushState(out Vector2 mouseLocalPosition, out Vector2 mouseLocalDelta)
    {
        mouseLocalPosition = default;
        mouseLocalDelta = default;

        if (!enableMouseBrushInteraction || mouseBrushRadius <= 0f || mouseBrushPushStrength <= 0f || chainContainer == null)
        {
            hasPreviousMouseLocalPosition = false;
            return false;
        }

        if (!Application.isFocused)
        {
            hasPreviousMouseLocalPosition = false;
            return false;
        }

        Canvas parentCanvas = chainContainer.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main;

        if (!IsMouseBrushInputAllowed(eventCamera))
        {
            hasPreviousMouseLocalPosition = false;
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                chainContainer,
                Input.mousePosition,
                eventCamera,
                out mouseLocalPosition))
        {
            hasPreviousMouseLocalPosition = false;
            return false;
        }

        if (!hasPreviousMouseLocalPosition)
        {
            previousMouseLocalPosition = mouseLocalPosition;
            hasPreviousMouseLocalPosition = true;
            return false;
        }

        mouseLocalDelta = mouseLocalPosition - previousMouseLocalPosition;
        previousMouseLocalPosition = mouseLocalPosition;
        hasPreviousMouseLocalPosition = true;

        if (maxMouseDeltaPerFrame > 0f && mouseLocalDelta.sqrMagnitude > maxMouseDeltaPerFrame * maxMouseDeltaPerFrame)
            return false;

        if (mouseBrushMinDeltaPerFrame > 0f
            && mouseLocalDelta.sqrMagnitude < mouseBrushMinDeltaPerFrame * mouseBrushMinDeltaPerFrame)
            return false;

        return true;
    }

    private bool IsMouseBrushInputAllowed(Camera eventCamera)
    {
        if (suppressBrushWhileAltHeld && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            return false;

        if (!requirePointerOverVisibleLinks || chainLinks == null || chainLinks.Length == 0)
            return true;

        for (int i = 0; i < chainLinks.Length; i++)
        {
            RectTransform link = chainLinks[i];
            if (link == null || !link.gameObject.activeInHierarchy)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(link, Input.mousePosition, eventCamera))
                return true;
        }

        return false;
    }

    private bool TryGetSupportMotionDelta(float deltaTime, out Vector2 supportMotionLocalDelta)
    {
        supportMotionLocalDelta = default;

        RectTransform resolvedSupportMotionSource = ResolveSupportMotionSource();
        if (!enableSupportMotionResponse
            || supportMotionInfluence <= 0f
            || UsesAnchoredBottomEndpoint()
            || resolvedSupportMotionSource == null
            || !Application.isFocused)
        {
            hasPreviousSupportMotionSourcePosition = false;
            smoothedSupportMotionLocalDelta = Vector2.zero;
            return false;
        }

        if (!TryGetSupportMotionSourcePosition(resolvedSupportMotionSource, out Vector2 currentSupportMotionSourcePosition))
        {
            hasPreviousSupportMotionSourcePosition = false;
            smoothedSupportMotionLocalDelta = Vector2.zero;
            return false;
        }

        if (!hasPreviousSupportMotionSourcePosition)
        {
            previousSupportMotionSourcePosition = currentSupportMotionSourcePosition;
            hasPreviousSupportMotionSourcePosition = true;
            return false;
        }

        Vector2 supportMotionSourceDelta = currentSupportMotionSourcePosition - previousSupportMotionSourcePosition;
        previousSupportMotionSourcePosition = currentSupportMotionSourcePosition;
        if (supportMotionSourceDelta.sqrMagnitude <= 0.000001f)
        {
            smoothedSupportMotionLocalDelta = Vector2.zero;
            return false;
        }

        Vector2 rawSupportMotionLocalDelta = ConvertSupportMotionSourceDeltaToChainLocal(resolvedSupportMotionSource, supportMotionSourceDelta);
        if (supportMotionMaxOffsetPerFrame > 0f)
            rawSupportMotionLocalDelta = Vector2.ClampMagnitude(rawSupportMotionLocalDelta, supportMotionMaxOffsetPerFrame);

        if (supportMotionDeadZone > 0f
            && rawSupportMotionLocalDelta.sqrMagnitude < supportMotionDeadZone * supportMotionDeadZone)
        {
            smoothedSupportMotionLocalDelta = Vector2.zero;
            return false;
        }

        if (supportMotionSmoothing > 0f)
        {
            float smoothingFactor = 1f - Mathf.Exp(-supportMotionSmoothing * deltaTime);
            smoothedSupportMotionLocalDelta = Vector2.Lerp(
                smoothedSupportMotionLocalDelta,
                rawSupportMotionLocalDelta,
                smoothingFactor);
        }
        else
        {
            smoothedSupportMotionLocalDelta = rawSupportMotionLocalDelta;
        }

        if (supportMotionDeadZone > 0f
            && smoothedSupportMotionLocalDelta.sqrMagnitude < supportMotionDeadZone * supportMotionDeadZone)
        {
            smoothedSupportMotionLocalDelta = Vector2.zero;
            return false;
        }

        supportMotionLocalDelta = smoothedSupportMotionLocalDelta;

        return true;
    }

    private RectTransform ResolveSupportMotionSource()
    {
        if (supportMotionSource != null)
            return supportMotionSource;

        ResolveReferences();
        return chainContainer != null ? chainContainer.parent as RectTransform : null;
    }

    private static bool TryGetSupportMotionSourcePosition(RectTransform source, out Vector2 position)
    {
        position = default;
        if (source == null)
            return false;

        position = source.anchoredPosition;
        return true;
    }

    private Vector2 ConvertSupportMotionSourceDeltaToChainLocal(RectTransform source, Vector2 supportMotionSourceDelta)
    {
        if (chainContainer == null)
            return supportMotionSourceDelta;

        Transform sourceParent = source != null ? source.parent : null;
        if (sourceParent is RectTransform sourceParentRect)
        {
            Vector3 worldDelta = sourceParentRect.TransformVector(new Vector3(supportMotionSourceDelta.x, supportMotionSourceDelta.y, 0f));
            Vector3 localDelta = chainContainer.InverseTransformVector(worldDelta);
            return new Vector2(localDelta.x, localDelta.y);
        }

        if (source != null)
        {
            Vector3 worldDelta = source.TransformVector(new Vector3(supportMotionSourceDelta.x, supportMotionSourceDelta.y, 0f));
            Vector3 localDelta = chainContainer.InverseTransformVector(worldDelta);
            return new Vector2(localDelta.x, localDelta.y);
        }

        return supportMotionSourceDelta;
    }

    private void EnsureBuffers()
    {
        ResolveReferences();
        RefreshSegmentLengths();

        int jointCount = segmentLengths != null ? segmentLengths.Length + 1 : 0;
        if (jointCount <= 1)
        {
            jointPositions = null;
            previousJointPositions = null;
            initialized = false;
            return;
        }

        if (jointPositions == null || jointPositions.Length != jointCount)
        {
            jointPositions = new Vector2[jointCount];
            previousJointPositions = new Vector2[jointCount];
            initialized = false;
        }
    }

    private void RefreshSegmentLengths()
    {
        if (chainLinks == null || chainLinks.Length == 0)
        {
            segmentLengths = null;
            totalChainLength = 0f;
            return;
        }

        if (!HasValidAuthoredSegmentLengths() || authoredEndpointDistance <= 0f)
            CaptureAuthoredSegmentLengths();

        if (segmentLengths == null || segmentLengths.Length != chainLinks.Length)
            segmentLengths = new float[chainLinks.Length];

        bool useAuthoredLengths = HasValidAuthoredSegmentLengths();
        if (UsesAnchoredBottomEndpoint()
            && TryGetEndpointLocalPositions(out Vector2 anchoredTopLocal, out Vector2 anchoredBottomLocal))
        {
            totalChainLength = 0f;
            float currentEndpointDistance = Mathf.Max(1f, Vector2.Distance(anchoredTopLocal, anchoredBottomLocal));
            float authoredTotalLength = GetAuthoredTotalLength();
            float anchoredFallbackLength = Mathf.Max(1f, currentEndpointDistance / Mathf.Max(1, chainLinks.Length));

            for (int i = 0; i < chainLinks.Length; i++)
            {
                float authoredLength = GetAuthoredSegmentLength(i, anchoredFallbackLength);
                float length = useAuthoredLengths && authoredTotalLength > 0.001f
                    ? Mathf.Max(1f, authoredLength)
                    : anchoredFallbackLength;
                segmentLengths[i] = length;
                totalChainLength += length;
                anchoredFallbackLength = authoredLength;
            }

            return;
        }

        totalChainLength = 0f;
        float fallbackLength = 32f;
        for (int i = 0; i < chainLinks.Length; i++)
        {
            float authoredLength = GetAuthoredSegmentLength(i, fallbackLength);
            float length = useAuthoredLengths
                ? Mathf.Max(1f, authoredLength)
                : Mathf.Max(1f, authoredLength * segmentLengthMultiplier + segmentLengthPadding);
            segmentLengths[i] = length;
            totalChainLength += length;
            fallbackLength = authoredLength;
        }
    }

    private float GetAuthoredTotalLength()
    {
        if (!HasValidAuthoredSegmentLengths())
            return 0f;

        float totalLength = 0f;
        for (int i = 0; i < authoredSegmentLengths.Length; i++)
            totalLength += authoredSegmentLengths[i];

        return totalLength;
    }

    private bool HasValidAuthoredSegmentLengths()
    {
        if (authoredSegmentLengths == null || chainLinks == null)
            return false;

        if (authoredSegmentLengths.Length != chainLinks.Length)
            return false;

        for (int i = 0; i < authoredSegmentLengths.Length; i++)
        {
            if (authoredSegmentLengths[i] <= 0f)
                return false;
        }

        return true;
    }

    private float GetAuthoredSegmentLength(int index, float fallbackLength)
    {
        if (authoredSegmentLengths != null
            && index >= 0
            && index < authoredSegmentLengths.Length
            && authoredSegmentLengths[index] > 0f)
        {
            return authoredSegmentLengths[index];
        }

        RectTransform link = chainLinks != null && index >= 0 && index < chainLinks.Length
            ? chainLinks[index]
            : null;
        if (link == null)
            return fallbackLength;

        return Mathf.Max(1f, MeasureLinkLength(link));
    }

    private float MeasureLinkLength(RectTransform link)
    {
        if (chainContainer == null || link == null)
            return 0f;

        // Use the longer rect side as the segment length so chain art orientation
        // does not silently change the simulated reach/spacing.
        Vector3 worldHeightVector = link.TransformVector(Vector3.up * link.rect.height);
        Vector3 worldWidthVector = link.TransformVector(Vector3.right * link.rect.width);
        float localHeight = chainContainer.InverseTransformVector(worldHeightVector).magnitude;
        float localWidth = chainContainer.InverseTransformVector(worldWidthVector).magnitude;
        return Mathf.Max(localWidth, localHeight);
    }

    private void ResetSimulation(Vector2 topLocal, Vector2 bottomLocal)
    {
        if (jointPositions == null || jointPositions.Length == 0)
            return;

        float directDistance = Vector2.Distance(topLocal, bottomLocal);
        float slack = Mathf.Max(0f, totalChainLength - directDistance);
        int lastIndex = jointPositions.Length - 1;

        for (int i = 0; i <= lastIndex; i++)
        {
            float t = lastIndex > 0 ? i / (float)lastIndex : 0f;
            Vector2 point = Vector2.Lerp(topLocal, bottomLocal, t);
            if (i != 0 && i != lastIndex)
                point += Vector2.down * Mathf.Sin(t * Mathf.PI) * slack * 0.5f;

            jointPositions[i] = point;
            previousJointPositions[i] = point;
        }

        int settleIterations = Mathf.Max(4, constraintIterations);
        for (int i = 0; i < settleIterations; i++)
            SolveDistanceConstraints(topLocal, bottomLocal, UsesAnchoredBottomEndpoint());

        previousJointPositions[0] = topLocal;
        previousJointPositions[lastIndex] = jointPositions[lastIndex];
        initialized = true;
    }

    private void Simulate(
        Vector2 topLocal,
        Vector2 bottomLocal,
        float deltaTime,
        bool hasMouseBrushInteraction,
        Vector2 mouseLocalPosition,
        Vector2 mouseLocalDelta,
        bool hasSupportMotionResponse,
        Vector2 supportMotionLocalDelta)
    {
        if (jointPositions == null || jointPositions.Length <= 1)
            return;

        int lastIndex = jointPositions.Length - 1;
        bool anchoredBottomEndpoint = UsesAnchoredBottomEndpoint();
        float stepDeltaTime = deltaTime / Mathf.Max(1, simulationSubsteps);
        float dampingFactor = Mathf.Exp(-velocityDamping * stepDeltaTime);
        Vector2 gravityStep = localGravity * (stepDeltaTime * stepDeltaTime);
        Vector2 mouseStepDelta = mouseLocalDelta / Mathf.Max(1, simulationSubsteps);
        Vector2 supportMotionStepDelta = supportMotionLocalDelta / Mathf.Max(1, simulationSubsteps);

        for (int substep = 0; substep < simulationSubsteps; substep++)
        {
            int lastDynamicIndex = anchoredBottomEndpoint ? lastIndex - 1 : lastIndex;
            for (int i = 1; i <= lastDynamicIndex; i++)
            {
                Vector2 current = jointPositions[i];
                Vector2 velocity = (current - previousJointPositions[i]) * dampingFactor;
                previousJointPositions[i] = current;
                jointPositions[i] = current + velocity + gravityStep;
            }

            if (hasSupportMotionResponse)
                ApplySupportMotionResponse(supportMotionStepDelta);

            if (hasMouseBrushInteraction)
                ApplyMouseBrushInteraction(mouseLocalPosition, mouseStepDelta, stepDeltaTime);

            jointPositions[0] = topLocal;
            if (anchoredBottomEndpoint)
                jointPositions[lastIndex] = bottomLocal;

            for (int iteration = 0; iteration < constraintIterations; iteration++)
                SolveDistanceConstraints(topLocal, bottomLocal, anchoredBottomEndpoint);
        }

        previousJointPositions[0] = topLocal;
        if (anchoredBottomEndpoint)
            previousJointPositions[lastIndex] = bottomLocal;
    }

    private void ApplySupportMotionResponse(Vector2 supportMotionStepDelta)
    {
        if (jointPositions == null || jointPositions.Length <= 2 || supportMotionInfluence <= 0f)
            return;

        Vector2 inertialOffset = -supportMotionStepDelta * supportMotionInfluence;
        if (inertialOffset.sqrMagnitude <= 0.000001f)
            return;

        int lastIndex = jointPositions.Length - 1;
        for (int i = 1; i <= lastIndex; i++)
        {
            float t = lastIndex > 0 ? i / (float)lastIndex : 1f;
            float weight = t * t;
            jointPositions[i] += inertialOffset * weight;
        }
    }

    private void ApplyMouseBrushInteraction(Vector2 mouseLocalPosition, Vector2 mouseLocalDelta, float stepDeltaTime)
    {
        if (jointPositions == null || jointPositions.Length <= 2)
            return;

        float mouseSweepLengthSquared = mouseLocalDelta.sqrMagnitude;
        if (mouseSweepLengthSquared <= 0.000001f)
            return;

        float radius = mouseBrushRadius;
        if (radius <= 0f)
            return;

        float radiusSquared = radius * radius;
        Vector2 mouseSweepStart = mouseLocalPosition - mouseLocalDelta;
        Vector2 mouseSweep = mouseLocalDelta;
        float mouseSweepLength = Mathf.Sqrt(mouseSweepLengthSquared);
        float pushDistance = mouseBrushPushStrength * stepDeltaTime;
        int lastIndex = jointPositions.Length - 1;

        for (int i = 1; i < lastIndex; i++)
        {
            Vector2 jointPosition = jointPositions[i];
            float sweepT = Mathf.Clamp01(
                Vector2.Dot(jointPosition - mouseSweepStart, mouseSweep) / mouseSweepLengthSquared);
            Vector2 closestPointOnSweep = mouseSweepStart + mouseSweep * sweepT;
            Vector2 offset = jointPosition - closestPointOnSweep;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > radiusSquared)
                continue;

            float distance = Mathf.Sqrt(distanceSquared);
            float normalizedDistance = radius > 0.0001f ? distance / radius : 1f;
            float influence = 1f - Mathf.Clamp01(normalizedDistance);
            influence *= influence;

            Vector2 pushDirection;
            if (distance > 0.0001f)
                pushDirection = offset / distance;
            else if (mouseSweepLength > 0.0001f)
                pushDirection = new Vector2(-mouseSweep.y, mouseSweep.x) / mouseSweepLength;
            else
                pushDirection = Vector2.up;

            jointPositions[i] += pushDirection * (pushDistance * influence);

            if (mouseBrushDragInfluence > 0f && mouseLocalDelta.sqrMagnitude > 0.0001f)
                jointPositions[i] += mouseLocalDelta * (mouseBrushDragInfluence * influence);
        }
    }

    private void SolveDistanceConstraints(Vector2 topLocal, Vector2 bottomLocal, bool anchoredBottomEndpoint)
    {
        if (jointPositions == null || segmentLengths == null || segmentLengths.Length == 0)
            return;

        int lastIndex = jointPositions.Length - 1;
        jointPositions[0] = topLocal;
        if (anchoredBottomEndpoint)
            jointPositions[lastIndex] = bottomLocal;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            Vector2 start = jointPositions[i];
            Vector2 end = jointPositions[i + 1];
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            Vector2 direction = distance > 0.0001f ? delta / distance : Vector2.down;
            float targetLength = segmentLengths[i];

            if (i == 0)
            {
                jointPositions[i + 1] = start + direction * targetLength;
            }
            else if (anchoredBottomEndpoint && i + 1 == lastIndex)
            {
                jointPositions[i] = end - direction * targetLength;
            }
            else
            {
                float error = distance - targetLength;
                Vector2 correction = direction * (error * 0.5f);
                jointPositions[i] += correction;
                jointPositions[i + 1] -= correction;
            }
        }

        jointPositions[0] = topLocal;
        if (anchoredBottomEndpoint)
            jointPositions[lastIndex] = bottomLocal;
    }

    private bool UsesAnchoredBottomEndpoint()
    {
        return bottomEndpointMode == SettingsPanelChainBottomEndpointMode.Anchored;
    }

    private void SyncCachedInputState()
    {
        hasPreviousMouseLocalPosition = false;
        hasPreviousSupportMotionSourcePosition = false;
        smoothedSupportMotionLocalDelta = Vector2.zero;
    }

    private void ApplyLinkTransforms()
    {
        if (chainLinks == null || jointPositions == null)
            return;

        for (int i = 0; i < chainLinks.Length; i++)
        {
            RectTransform link = chainLinks[i];
            if (link == null)
                continue;

            Vector2 start = jointPositions[i];
            Vector2 end = jointPositions[i + 1];
            Vector2 delta = end - start;
            if (delta.sqrMagnitude <= 0.0001f)
                delta = Vector2.down;

            Vector2 center = (start + end) * 0.5f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + linkAngleOffset;

            if (i == chainLinks.Length - 1)
                center += lastLinkLocalOffset;

            if (link.parent == chainContainer)
                link.anchoredPosition = center;
            else
                link.localPosition = new Vector3(center.x, center.y, link.localPosition.z);
            link.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
