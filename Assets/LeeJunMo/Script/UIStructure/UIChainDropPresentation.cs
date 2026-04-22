using UnityEngine;

[System.Serializable]
public sealed class UIChainConstraintBinding
{
    [SerializeField] private RectTransform chainAttachPoint;
    [SerializeField] private SettingsPanelFakeChainPresentation fakeChainPresentation;

    public RectTransform ChainAttachPoint => chainAttachPoint;
    public SettingsPanelFakeChainPresentation FakeChainPresentation => fakeChainPresentation;
    public bool IsValid => chainAttachPoint != null && fakeChainPresentation != null;
}

[DisallowMultipleComponent]
public sealed class UIChainDropPresentation : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup interactionCanvasGroup;
    [SerializeField] private Vector2 closedLocalOffset = new Vector2(0f, 850f);
    [SerializeField] private Vector2 dropStartLocalOffset = new Vector2(0f, 850f);
    [SerializeField] private bool snapClosedOnAwake;
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool disableInteractionWhileAnimating = true;

    [Header("Motion")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Vector2 localGravity = new Vector2(0f, -4600f);
    [SerializeField] private Vector2 initialVelocity = new Vector2(0f, -280f);
    [SerializeField, Min(0f)] private float airDamping = 1.2f;
    [SerializeField, Range(0f, 1f)] private float impactBounce = 0.28f;
    [SerializeField, Range(0f, 1f)] private float impactTangentialDamping = 0.18f;
    [SerializeField, Min(0.001f)] private float maxSimulationStep = 1f / 60f;
    [SerializeField, Min(0f)] private float settlePositionThreshold = 2f;
    [SerializeField, Min(0f)] private float settleVelocityThreshold = 36f;

    [Header("Close Motion")]
    [SerializeField] private Vector2 closePullDownOffset = new Vector2(0f, -56f);
    [SerializeField, Min(0.001f)] private float closePullDownDuration = 0.08f;
    [SerializeField, Min(0.001f)] private float closeLaunchDuration = 0.16f;
    [SerializeField] private bool ignoreChainReachDuringCloseAnimation = true;

    [Header("Support Motion Response")]
    [SerializeField] private bool enableSupportMotionResponse;
    [SerializeField] private RectTransform supportMotionSource;
    [SerializeField, Min(0f)] private float supportMotionPositionInfluence = 0.9f;
    [SerializeField, Range(0f, 1f)] private float supportMotionVelocityInfluence = 0.12f;
    [SerializeField, Min(0f)] private float supportMotionDeadZone = 0.5f;
    [SerializeField, Min(0f)] private float supportMotionSmoothing = 18f;
    [SerializeField, Min(0f)] private float supportMotionMaxOffsetPerFrame = 48f;

    [Header("Rotation")]
    [SerializeField] private bool applyRandomStartZRotation = true;
    [SerializeField] private Vector2 randomStartZRotationRange = new Vector2(-6f, 6f);
    [SerializeField, Min(0f)] private float rotationRecoveryDegreesPerSecond = 42f;

    [Header("Chain (Optional)")]
    [SerializeField] private RectTransform chainAttachPoint;
    [SerializeField] private SettingsPanelFakeChainPresentation fakeChainPresentation;
    [SerializeField] private UIChainConstraintBinding[] chainConstraints;
    [SerializeField] private bool constrainPanelByChainReach = true;

    [Header("Preview")]
    [SerializeField] private bool allowPreviewToggle;
    [SerializeField] private KeyCode previewToggleKey = KeyCode.F6;

    private Vector2 openAnchoredPosition;
    private Vector2 currentVelocity;
    private Vector2 closeAnimationStartPosition;
    private bool hasOpenAnchoredPosition;
    private float openLocalZRotation;
    private float closeAnimationElapsed;
    private bool hasOpenLocalZRotation;
    private bool hasImpactedConstraint;
    private bool isAnimatingOpen;
    private bool isAnimatingClose;
    private bool isOpen;
    private bool hasUnlockedInteractionForCurrentOpen;
    private bool hasPreviousSupportMotionSourcePosition;
    private Vector2 previousSupportMotionSourcePosition;
    private Vector2 smoothedSupportMotionLocalDelta;
    private System.Action onCloseAnimationFinished;
    private bool hasLayoutSignature;
    private Vector2 lastParentRectSize;
    private float lastCanvasScaleFactor;
    private Vector2Int lastScreenSize;
    private bool layoutRefreshPending;

    private void Reset()
    {
        panelRoot = transform as RectTransform;
        interactionCanvasGroup = GetComponent<CanvasGroup>();
        chainAttachPoint = panelRoot;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureOpenAnchoredPosition();

        if (snapClosedOnAwake)
            SnapClosed();
        else
        {
            ApplyChainReachConstraint();
            SnapRotationToOpen();
            SnapAllChainPresentations();
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
            PlayOpen();
        else
        {
            ApplyChainReachConstraint();
            SnapRotationToOpen();
            SnapAllChainPresentations();
        }
    }

    private void OnDisable()
    {
        StopActiveMotion();
        SetInteractionEnabled(true);
        ResetSupportMotionCache();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        ResetSupportMotionCache();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        ResetSupportMotionCache();
    }

    private void Update()
    {
        RefreshLayoutIfNeeded();
        UpdateOpenMotion();
        UpdateCloseMotion();

        if (!allowPreviewToggle || !Input.GetKeyDown(previewToggleKey))
            return;

        TogglePreview();
    }

    private void LateUpdate()
    {
        if (!constrainPanelByChainReach || (!isOpen && !isAnimatingOpen && !isAnimatingClose))
            return;

        if (ShouldIgnoreChainReachDuringCloseAnimation())
            return;

        ApplyChainReachConstraint();
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshLayoutIfNeeded();
    }

    public void PlayOpen()
    {
        ResolveReferences();
        CaptureOpenAnchoredPosition();
        StopActiveMotion();

        panelRoot.anchoredPosition = openAnchoredPosition + dropStartLocalOffset;
        ApplyRandomStartRotation();
        currentVelocity = initialVelocity;
        hasImpactedConstraint = false;
        hasUnlockedInteractionForCurrentOpen = false;
        isAnimatingOpen = true;
        isOpen = true;
        SetInteractionEnabled(false);
        ApplyChainReachConstraint();
        SnapAllChainPresentations();
    }

    public void PlayClose(System.Action onComplete = null)
    {
        ResolveReferences();
        CaptureOpenAnchoredPosition();
        StopActiveMotion();

        if (panelRoot == null)
        {
            onComplete?.Invoke();
            return;
        }

        closeAnimationStartPosition = panelRoot.anchoredPosition;
        closeAnimationElapsed = 0f;
        isAnimatingClose = true;
        isOpen = false;
        onCloseAnimationFinished = onComplete;
        SetInteractionEnabled(false);
        if (!ShouldIgnoreChainReachDuringCloseAnimation())
            ApplyChainReachConstraint();
        SnapAllChainPresentations();
    }

    public void SnapOpen()
    {
        ResolveReferences();
        CaptureOpenAnchoredPosition();
        StopActiveMotion();
        panelRoot.anchoredPosition = openAnchoredPosition;
        SnapRotationToOpen();
        currentVelocity = Vector2.zero;
        hasImpactedConstraint = false;
        isOpen = true;
        ApplyChainReachConstraint();
        SetInteractionEnabled(true);
        SnapAllChainPresentations();
    }

    public void SnapClosed()
    {
        ResolveReferences();
        CaptureOpenAnchoredPosition();
        StopActiveMotion();
        panelRoot.anchoredPosition = openAnchoredPosition + closedLocalOffset;
        SnapRotationToOpen();
        currentVelocity = Vector2.zero;
        hasImpactedConstraint = false;
        isOpen = false;
        ApplyChainReachConstraint();
        SetInteractionEnabled(true);
        SnapAllChainPresentations();
    }

    public void TogglePreview()
    {
        if (isAnimatingClose || (!isOpen && !isAnimatingOpen))
        {
            PlayOpen();
            return;
        }

        PlayClose();
    }

    public void StopPresentation()
    {
        StopActiveMotion();
        SetInteractionEnabled(true);
    }

    private void ResolveReferences()
    {
        if (panelRoot == null)
            panelRoot = transform as RectTransform;

        if (chainAttachPoint == null)
            chainAttachPoint = panelRoot;

        if (interactionCanvasGroup == null)
            interactionCanvasGroup = panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
    }

    private void CaptureOpenAnchoredPosition()
    {
        if (panelRoot == null)
            return;

        if (!hasOpenAnchoredPosition)
        {
            openAnchoredPosition = panelRoot.anchoredPosition;
            hasOpenAnchoredPosition = true;
        }

        if (!hasOpenLocalZRotation)
        {
            openLocalZRotation = NormalizeSignedAngle(panelRoot.localEulerAngles.z);
            hasOpenLocalZRotation = true;
        }
    }

    private void RefreshLayoutIfNeeded(bool force = false)
    {
        ResolveReferences();
        if (panelRoot == null)
            return;

        if (!hasLayoutSignature)
        {
            UpdateLayoutSignature();
            return;
        }

        if (layoutRefreshPending && !isAnimatingOpen && !isAnimatingClose)
        {
            ApplyLayoutRefresh();
            return;
        }

        if (!force && !HasLayoutSignatureChanged())
            return;

        UpdateLayoutSignature();

        if (isAnimatingOpen || isAnimatingClose)
        {
            layoutRefreshPending = true;
            return;
        }

        ApplyLayoutRefresh();
    }

    private bool HasLayoutSignatureChanged()
    {
        Vector2 parentRectSize = GetParentRectSize();
        float canvasScaleFactor = GetCanvasScaleFactor();
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        return lastParentRectSize != parentRectSize
            || !Mathf.Approximately(lastCanvasScaleFactor, canvasScaleFactor)
            || lastScreenSize != screenSize;
    }

    private void UpdateLayoutSignature()
    {
        lastParentRectSize = GetParentRectSize();
        lastCanvasScaleFactor = GetCanvasScaleFactor();
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        hasLayoutSignature = true;
    }

    private void ApplyLayoutRefresh()
    {
        layoutRefreshPending = false;
        ResetSupportMotionCache();
        RestorePresentationStateAfterLayoutRefresh();
        SnapAllChainPresentations();

        if (constrainPanelByChainReach && isOpen && !isAnimatingOpen && !isAnimatingClose)
            ApplyChainReachConstraint();
    }

    private void RestorePresentationStateAfterLayoutRefresh()
    {
        if (panelRoot == null)
            return;

        CaptureOpenAnchoredPosition();

        if (isOpen)
        {
            panelRoot.anchoredPosition = openAnchoredPosition;
            hasUnlockedInteractionForCurrentOpen = true;
        }
        else
        {
            panelRoot.anchoredPosition = openAnchoredPosition + closedLocalOffset;
            hasUnlockedInteractionForCurrentOpen = false;
        }

        currentVelocity = Vector2.zero;
        hasImpactedConstraint = false;
        SnapRotationToOpen();
        SetInteractionEnabled(true);
    }

    private Vector2 GetParentRectSize()
    {
        if (panelRoot == null)
            return Vector2.zero;

        RectTransform parentRect = panelRoot.parent as RectTransform;
        return parentRect != null ? parentRect.rect.size : Vector2.zero;
    }

    private float GetCanvasScaleFactor()
    {
        Canvas canvas = panelRoot != null ? panelRoot.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.scaleFactor : 1f;
    }

    private void StopActiveMotion()
    {
        isAnimatingOpen = false;
        isAnimatingClose = false;
        currentVelocity = Vector2.zero;
        closeAnimationElapsed = 0f;
        hasImpactedConstraint = false;
        hasUnlockedInteractionForCurrentOpen = false;
        onCloseAnimationFinished = null;
        ResetSupportMotionCache();
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (!disableInteractionWhileAnimating || interactionCanvasGroup == null)
            return;

        interactionCanvasGroup.interactable = enabled;
        interactionCanvasGroup.blocksRaycasts = enabled;
    }

    private void UpdateOpenMotion()
    {
        if (panelRoot == null)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        bool canWakeFromSupportMotion = CanWakeOpenSimulationFromSupportMotion();
        bool hasSupportMotionResponse = false;
        Vector2 supportMotionLocalDelta = Vector2.zero;
        if (enableSupportMotionResponse && (isAnimatingOpen || canWakeFromSupportMotion))
            hasSupportMotionResponse = TryGetSupportMotionDelta(deltaTime, out supportMotionLocalDelta);

        if (!isAnimatingOpen)
        {
            if (!canWakeFromSupportMotion || !hasSupportMotionResponse)
                return;

            isAnimatingOpen = true;
            isOpen = true;
            hasUnlockedInteractionForCurrentOpen = true;
        }

        float remainingTime = Mathf.Min(deltaTime, 0.1f);
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(remainingTime / maxSimulationStep));
        Vector2 stepSupportMotionLocalDelta = stepCount > 0
            ? supportMotionLocalDelta / stepCount
            : supportMotionLocalDelta;
        while (remainingTime > 0f)
        {
            float step = Mathf.Min(maxSimulationStep, remainingTime);
            remainingTime -= step;
            SimulateOpenStep(step, stepSupportMotionLocalDelta);
        }

        UpdateOpenRotation(deltaTime);
        TryUnlockInteractionDuringOpen();

        bool closeEnoughToRest =
            Vector2.Distance(panelRoot.anchoredPosition, openAnchoredPosition) <= settlePositionThreshold ||
            (hasImpactedConstraint && IsChainConstraintActive());

        if (closeEnoughToRest &&
            currentVelocity.magnitude <= settleVelocityThreshold)
        {
            if (!IsChainConstraintActive())
                panelRoot.anchoredPosition = openAnchoredPosition;

            ApplyChainReachConstraint();
            currentVelocity = Vector2.zero;
            isAnimatingOpen = false;
            SnapRotationToOpen();
            hasUnlockedInteractionForCurrentOpen = true;
            SetInteractionEnabled(true);
        }
    }

    private void TryUnlockInteractionDuringOpen()
    {
        if (hasUnlockedInteractionForCurrentOpen || !disableInteractionWhileAnimating || interactionCanvasGroup == null)
            return;

        bool reachedOpenPosition =
            Vector2.Distance(panelRoot.anchoredPosition, openAnchoredPosition) <= settlePositionThreshold;
        bool reachedChainConstraint = hasImpactedConstraint && IsChainConstraintActive();
        if (!reachedOpenPosition && !reachedChainConstraint)
            return;

        hasUnlockedInteractionForCurrentOpen = true;
        SetInteractionEnabled(true);
    }

    private void UpdateCloseMotion()
    {
        if (!isAnimatingClose || panelRoot == null)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float safePullDownDuration = Mathf.Max(0.001f, closePullDownDuration);
        float safeLaunchDuration = Mathf.Max(0.001f, closeLaunchDuration);
        float totalDuration = safePullDownDuration + safeLaunchDuration;

        closeAnimationElapsed = Mathf.Min(totalDuration, closeAnimationElapsed + deltaTime);

        Vector2 pullDownTarget = closeAnimationStartPosition + closePullDownOffset;
        Vector2 closedTarget = openAnchoredPosition + closedLocalOffset;
        if (closeAnimationElapsed <= safePullDownDuration)
        {
            float normalized = Mathf.Clamp01(closeAnimationElapsed / safePullDownDuration);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            panelRoot.anchoredPosition = Vector2.LerpUnclamped(closeAnimationStartPosition, pullDownTarget, eased);
        }
        else
        {
            float normalized = Mathf.Clamp01((closeAnimationElapsed - safePullDownDuration) / safeLaunchDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            panelRoot.anchoredPosition = Vector2.LerpUnclamped(pullDownTarget, closedTarget, eased);
        }

        if (!ShouldIgnoreChainReachDuringCloseAnimation())
            ApplyChainReachConstraint();
        UpdateOpenRotation(deltaTime);

        if (closeAnimationElapsed < totalDuration)
            return;

        panelRoot.anchoredPosition = closedTarget;
        ApplyChainReachConstraint();
        SnapRotationToOpen();
        currentVelocity = Vector2.zero;
        hasImpactedConstraint = false;
        isAnimatingClose = false;
        closeAnimationElapsed = 0f;

        System.Action closeFinished = onCloseAnimationFinished;
        onCloseAnimationFinished = null;
        closeFinished?.Invoke();
    }

    private bool ShouldIgnoreChainReachDuringCloseAnimation()
    {
        return isAnimatingClose && ignoreChainReachDuringCloseAnimation;
    }

    private void SimulateOpenStep(float deltaTime, Vector2 supportMotionLocalDelta)
    {
        ApplySupportMotionResponse(supportMotionLocalDelta, deltaTime);
        currentVelocity += localGravity * deltaTime;
        currentVelocity *= Mathf.Exp(-airDamping * deltaTime);
        panelRoot.anchoredPosition += currentVelocity * deltaTime;

        bool usedChainConstraint = false;
        Vector2 constraintCorrection = Vector2.zero;
        if (constrainPanelByChainReach && fakeChainPresentation != null)
        {
            constraintCorrection = ApplyChainReachConstraint();
            usedChainConstraint = true;
        }
        else if (constrainPanelByChainReach && HasAnyChainConstraint())
        {
            constraintCorrection = ApplyChainReachConstraint();
            usedChainConstraint = true;
        }
        else
        {
            constraintCorrection = ApplyOpenPositionFloorConstraint();
        }

        if (constraintCorrection.sqrMagnitude > 0.0001f)
        {
            Vector2 inwardNormal = constraintCorrection.normalized;
            float outwardSpeed = Vector2.Dot(currentVelocity, -inwardNormal);
            if (outwardSpeed > 0f)
            {
                Vector2 normalComponent = Vector2.Dot(currentVelocity, inwardNormal) * inwardNormal;
                Vector2 tangentialComponent = currentVelocity - normalComponent;
                currentVelocity = (-normalComponent * impactBounce) + (tangentialComponent * (1f - impactTangentialDamping));
            }

            if (usedChainConstraint)
                hasImpactedConstraint = true;
        }
    }

    private bool CanWakeOpenSimulationFromSupportMotion()
    {
        return isOpen
            && enableSupportMotionResponse
            && !isAnimatingClose
            && constrainPanelByChainReach
            && HasAnyChainConstraint();
    }

    private void ApplySupportMotionResponse(Vector2 supportMotionLocalDelta, float deltaTime)
    {
        if (panelRoot == null)
            return;

        if (supportMotionLocalDelta.sqrMagnitude <= 0.000001f)
            return;

        if (supportMotionPositionInfluence > 0f)
            panelRoot.anchoredPosition -= supportMotionLocalDelta * supportMotionPositionInfluence;

        if (supportMotionVelocityInfluence > 0f && deltaTime > 0f)
            currentVelocity -= (supportMotionLocalDelta / deltaTime) * supportMotionVelocityInfluence;
    }

    private bool TryGetSupportMotionDelta(float deltaTime, out Vector2 supportMotionLocalDelta)
    {
        supportMotionLocalDelta = Vector2.zero;

        RectTransform resolvedSupportMotionSource = ResolveSupportMotionSource();
        if (resolvedSupportMotionSource == null || supportMotionPositionInfluence <= 0f && supportMotionVelocityInfluence <= 0f)
        {
            ResetSupportMotionCache();
            return false;
        }

        if (!TryGetSupportMotionSourcePosition(resolvedSupportMotionSource, out Vector2 currentSupportMotionSourcePosition))
        {
            ResetSupportMotionCache();
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

        Vector2 rawSupportMotionLocalDelta = ConvertSupportMotionSourceDeltaToPanelLocal(resolvedSupportMotionSource, supportMotionSourceDelta);
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

        return panelRoot != null ? panelRoot.parent as RectTransform : null;
    }

    private static bool TryGetSupportMotionSourcePosition(RectTransform source, out Vector2 position)
    {
        position = default;
        if (source == null)
            return false;

        position = source.anchoredPosition;
        return true;
    }

    private Vector2 ConvertSupportMotionSourceDeltaToPanelLocal(RectTransform source, Vector2 supportMotionSourceDelta)
    {
        if (panelRoot == null)
            return supportMotionSourceDelta;

        RectTransform panelParentRect = panelRoot.parent as RectTransform;
        Transform sourceParent = source != null ? source.parent : null;
        if (panelParentRect != null && sourceParent is RectTransform sourceParentRect)
        {
            Vector3 worldDelta = sourceParentRect.TransformVector(new Vector3(supportMotionSourceDelta.x, supportMotionSourceDelta.y, 0f));
            Vector3 localDelta = panelParentRect.InverseTransformVector(worldDelta);
            return new Vector2(localDelta.x, localDelta.y);
        }

        if (panelParentRect != null && source != null)
        {
            Vector3 worldDelta = source.TransformVector(new Vector3(supportMotionSourceDelta.x, supportMotionSourceDelta.y, 0f));
            Vector3 localDelta = panelParentRect.InverseTransformVector(worldDelta);
            return new Vector2(localDelta.x, localDelta.y);
        }

        return supportMotionSourceDelta;
    }

    private void ResetSupportMotionCache()
    {
        hasPreviousSupportMotionSourcePosition = false;
        smoothedSupportMotionLocalDelta = Vector2.zero;
    }

    private Vector2 ApplyOpenPositionFloorConstraint()
    {
        if (panelRoot == null)
            return Vector2.zero;

        Vector2 currentPosition = panelRoot.anchoredPosition;
        if (currentPosition.y >= openAnchoredPosition.y)
            return Vector2.zero;

        Vector2 correction = new Vector2(0f, openAnchoredPosition.y - currentPosition.y);
        panelRoot.anchoredPosition = currentPosition + correction;
        return correction;
    }

    private bool IsChainConstraintActive()
    {
        if (!constrainPanelByChainReach)
            return false;

        bool active = false;
        ForEachChainConstraint((attachPoint, chainPresentation) =>
        {
            if (active)
                return;

            Vector2 currentAttachWorldPosition = attachPoint.position;
            Vector2 clampedAttachWorldPosition = chainPresentation.ClampWorldPositionToReach(currentAttachWorldPosition);
            active = (clampedAttachWorldPosition - currentAttachWorldPosition).sqrMagnitude > 0.0001f;
        });

        return active;
    }

    private Vector2 ApplyChainReachConstraint()
    {
        if (!constrainPanelByChainReach || panelRoot == null || !HasAnyChainConstraint())
            return Vector2.zero;

        Vector2 accumulatedLocalDelta = Vector2.zero;
        const int maxConstraintPasses = 4;
        for (int pass = 0; pass < maxConstraintPasses; pass++)
        {
            Vector2 strongestWorldDelta = Vector2.zero;
            float strongestSqrMagnitude = 0f;

            ForEachChainConstraint((attachPoint, chainPresentation) =>
            {
                Vector2 currentAttachWorldPosition = attachPoint.position;
                Vector2 clampedAttachWorldPosition = chainPresentation.ClampWorldPositionToReach(currentAttachWorldPosition);
                Vector2 worldDelta = clampedAttachWorldPosition - currentAttachWorldPosition;
                float sqrMagnitude = worldDelta.sqrMagnitude;
                if (sqrMagnitude <= strongestSqrMagnitude)
                    return;

                strongestSqrMagnitude = sqrMagnitude;
                strongestWorldDelta = worldDelta;
            });

            if (strongestSqrMagnitude <= 0.0001f)
                break;

            accumulatedLocalDelta += ApplyPanelWorldDelta(strongestWorldDelta);
        }

        return accumulatedLocalDelta;
    }

    private Vector2 ApplyPanelWorldDelta(Vector2 worldDelta)
    {
        RectTransform parentRect = panelRoot != null ? panelRoot.parent as RectTransform : null;
        if (parentRect != null)
        {
            Vector3 localDelta = parentRect.InverseTransformVector(worldDelta);
            panelRoot.anchoredPosition += new Vector2(localDelta.x, localDelta.y);
            return new Vector2(localDelta.x, localDelta.y);
        }

        panelRoot.position += (Vector3)worldDelta;
        return worldDelta;
    }

    private bool HasAnyChainConstraint()
    {
        bool hasAny = false;
        ForEachChainConstraint((_, _) => hasAny = true);
        return hasAny;
    }

    private void ForEachChainConstraint(System.Action<RectTransform, SettingsPanelFakeChainPresentation> action)
    {
        if (action == null)
            return;

        if (chainConstraints != null)
        {
            for (int i = 0; i < chainConstraints.Length; i++)
            {
                UIChainConstraintBinding binding = chainConstraints[i];
                if (binding == null || !binding.IsValid)
                    continue;

                action(binding.ChainAttachPoint, binding.FakeChainPresentation);
            }
        }

        if (chainAttachPoint != null && fakeChainPresentation != null)
            action(chainAttachPoint, fakeChainPresentation);
    }

    private void SnapAllChainPresentations()
    {
        ForEachUniqueChainPresentation(presentation => presentation.SnapToCurrentPose());
    }

    private void ApplyRandomStartRotation()
    {
        if (panelRoot == null)
            return;

        float startZRotation = openLocalZRotation;
        if (applyRandomStartZRotation)
        {
            float min = Mathf.Min(randomStartZRotationRange.x, randomStartZRotationRange.y);
            float max = Mathf.Max(randomStartZRotationRange.x, randomStartZRotationRange.y);
            startZRotation += Random.Range(min, max);
        }

        panelRoot.localRotation = Quaternion.Euler(0f, 0f, startZRotation);
    }

    private void UpdateOpenRotation(float deltaTime)
    {
        if (panelRoot == null || rotationRecoveryDegreesPerSecond <= 0f)
            return;

        float currentZRotation = NormalizeSignedAngle(panelRoot.localEulerAngles.z);
        float nextZRotation = Mathf.MoveTowardsAngle(
            currentZRotation,
            openLocalZRotation,
            rotationRecoveryDegreesPerSecond * deltaTime);

        panelRoot.localRotation = Quaternion.Euler(0f, 0f, nextZRotation);
    }

    private void SnapRotationToOpen()
    {
        if (panelRoot == null)
            return;

        panelRoot.localRotation = Quaternion.Euler(0f, 0f, openLocalZRotation);
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        else if (angle < -180f)
            angle += 360f;

        return angle;
    }

    private void ForEachUniqueChainPresentation(System.Action<SettingsPanelFakeChainPresentation> action)
    {
        if (action == null)
            return;

        SettingsPanelFakeChainPresentation[] visited = new SettingsPanelFakeChainPresentation[8];
        int visitedCount = 0;

        ForEachChainConstraint((_, presentation) =>
        {
            if (presentation == null)
                return;

            for (int i = 0; i < visitedCount; i++)
            {
                if (visited[i] == presentation)
                    return;
            }

            if (visitedCount < visited.Length)
                visited[visitedCount++] = presentation;

            action(presentation);
        });
    }
}
