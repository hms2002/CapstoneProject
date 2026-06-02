using System.Collections;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public sealed class RunSameSceneTeleportNpcFeature : RunSpecialNpcFeatureBase
{
    private const float HoleCheckRadius = 0.2f;
    private static readonly Collider2D[] HoleOverlapBuffer = new Collider2D[16];

    [Header("Affection Gate")]
    [SerializeField] private bool requireAffection;
    [SerializeField] private int requiredAffectionNpcId;
    [SerializeField, Min(0)] private int requiredAffectionAmount = 3;

    [Header("Arrival Points")]
    [SerializeField] private Transform appearancePoint;
    [SerializeField, FormerlySerializedAs("destination")] private Transform landingPoint;
    [SerializeField] private bool clearExternalMovement = true;
    [SerializeField] private bool clearAbilityMotion = true;

    [Header("Arrival Movement")]
    [SerializeField, Min(0f)] private float appearanceHoldDuration;
    [SerializeField, Min(0f)] private float moveToLandingDuration = 0.35f;
    [SerializeField] private AnimationCurve moveToLandingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float moveToLandingArcHeight = 0.75f;

    [Header("Landing Start Particle")]
    [Tooltip("Optional one-shot particle prefab spawned at the appearance point after fade-in, before movement to the landing point starts.")]
    [SerializeField] private SpawnedPresentationHook landingStartParticle = new SpawnedPresentationHook
    {
        scaleMultiplier = Vector3.one,
        targetBoundsReferenceSize = 1f,
        targetBoundsScaleMultiplier = 1f
    };

    [Header("Fade")]
    [SerializeField] private bool useFade = true;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField] private bool allowRuntimeFadeFallback;

    public bool HasDestination => landingPoint != null;

    public override RunSpecialNpcFeatureKind DialogueFeatureKind => RunSpecialNpcFeatureKind.SameSceneTeleport;

    public override bool ExecuteAfterRunSpecialPresentationClose => true;

    public override RunSpecialNpcDialogueBranchKey GetDialogueBranchKey(RunSpecialNpcFeatureContext context)
    {
        if (!HasRequiredAffection())
            return RunSpecialNpcDialogueBranchKey.TeleportLocked;

        Transform playerTransform = ResolvePlayerTransform(context);
        if (!HasDestination || playerTransform == null || IsTeleportPointBlockedByHoleTrap(landingPoint, playerTransform))
            return RunSpecialNpcDialogueBranchKey.TeleportUnavailable;

        return RunSpecialNpcDialogueBranchKey.TeleportAvailable;
    }

    public override bool CanExecute(RunSpecialNpcFeatureContext context)
    {
        if (!base.CanExecute(context) || !HasRequiredAffection() || landingPoint == null)
            return false;

        Transform playerTransform = ResolvePlayerTransform(context);
        return playerTransform != null &&
               !IsTeleportPointBlockedByHoleTrap(landingPoint, playerTransform);
    }

    public override string GetUnavailableReason(RunSpecialNpcFeatureContext context)
    {
        if (!HasRequiredAffection())
            return $"Affection {requiredAffectionNpcId} is below {requiredAffectionAmount}.";

        if (landingPoint == null)
            return "Teleport landing point is missing.";

        Transform playerTransform = ResolvePlayerTransform(context);
        if (playerTransform == null)
            return "Current player transform is missing.";

        if (IsTeleportPointBlockedByHoleTrap(landingPoint, playerTransform))
            return "Teleport landing point overlaps HoleTrap.";

        return string.Empty;
    }

    public override IEnumerator Execute(RunSpecialNpcFeatureContext context)
    {
        if (!CanExecute(context))
        {
            string unavailableReason = GetUnavailableReason(context);
            if (!string.IsNullOrEmpty(unavailableReason))
                LogTeleportWarning(unavailableReason);

            yield break;
        }

        Transform playerTransform = ResolvePlayerTransform(context);
        PlayerCinematicProtection playerProtection = AcquirePlayerCinematicProtection(playerTransform);
        PlayerTargetabilityBlocker targetabilityBlocker = AcquirePlayerTargetabilityBlocker(playerTransform);
        BodyColliderSuppression arrivalColliderSuppression = null;

        SceneFadeTransitionService transitionService = useFade
            ? SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: allowRuntimeFadeFallback)
            : null;

        bool fadeSessionStarted = false;
        if (transitionService != null)
            fadeSessionStarted = transitionService.TryBeginOverlayFadeSession(initialAlpha: 0f);

        try
        {
            if (fadeSessionStarted)
                yield return transitionService.FadeOutAsync(fadeOutDuration);

            Transform arrivalAppearancePoint = ResolveAppearancePoint();
            Transform arrivalLandingPoint = landingPoint;
            if (IsTeleportPointBlockedByHoleTrap(arrivalLandingPoint, playerTransform))
            {
                LogTeleportWarning("Teleport landing point overlaps HoleTrap.");
                yield break;
            }

            bool useArrivalMovement = ShouldUseArrivalMovement(playerTransform, arrivalAppearancePoint, arrivalLandingPoint);
            if (!useArrivalMovement)
            {
                if (WarpPlayer(context, arrivalLandingPoint))
                    yield return new WaitForFixedUpdate();

                if (fadeSessionStarted)
                    yield return transitionService.FadeInAsync(fadeInDuration);

                yield break;
            }

            arrivalColliderSuppression = SuppressPlayerBodyColliderForArrival(playerTransform);

            if (WarpPlayer(context, arrivalAppearancePoint, allowHoleTrapTarget: true))
                yield return new WaitForFixedUpdate();
            else
                yield break;

            if (fadeSessionStarted)
                yield return transitionService.FadeInAsync(fadeInDuration);

            if (arrivalAppearancePoint != arrivalLandingPoint)
                yield return MovePlayerToLanding(context, arrivalAppearancePoint, arrivalLandingPoint);
        }
        finally
        {
            arrivalColliderSuppression?.Restore();

            if (fadeSessionStarted)
                transitionService.EndOverlayFadeSession();

            if (targetabilityBlocker != null)
                targetabilityBlocker.Release(this);

            if (playerProtection != null)
                playerProtection.Release(this);
        }
    }

    public bool HasRequiredAffection()
    {
        if (!requireAffection)
            return true;

        if (AffectionManager.Instance == null)
            return false;

        return AffectionManager.Instance.GetAffection(requiredAffectionNpcId) >= requiredAffectionAmount;
    }

    private Transform ResolveAppearancePoint()
    {
        return appearancePoint != null ? appearancePoint : landingPoint;
    }

    private IEnumerator MovePlayerToLanding(
        RunSpecialNpcFeatureContext context,
        Transform arrivalAppearancePoint,
        Transform arrivalLandingPoint)
    {
        if (arrivalLandingPoint == null)
            yield break;

        Transform playerTransform = ResolvePlayerTransform(context);
        if (playerTransform == null)
            yield break;

        Vector3 startPosition = ResolvePointPosition(arrivalAppearancePoint, playerTransform);
        Vector3 targetPosition = ResolvePointPosition(arrivalLandingPoint, playerTransform);
        if ((targetPosition - startPosition).sqrMagnitude <= 0.0001f)
            yield break;

        if (IsPositionBlockedByHoleTrap(targetPosition))
        {
            LogTeleportWarning("Teleport landing point overlaps HoleTrap.");
            yield break;
        }

        PlayLandingStartParticle(playerTransform, arrivalAppearancePoint, startPosition, targetPosition);

        if (appearanceHoldDuration > 0f)
            yield return new WaitForSeconds(appearanceHoldDuration);

        if (moveToLandingDuration <= 0f)
        {
            if (WarpPlayer(context, arrivalLandingPoint))
                yield return new WaitForFixedUpdate();

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < moveToLandingDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / moveToLandingDuration);
            float moveT = moveToLandingCurve != null
                ? Mathf.Clamp01(moveToLandingCurve.Evaluate(normalizedTime))
                : normalizedTime;

            Vector3 nextPosition = EvaluateParabolicArrivalPosition(
                startPosition,
                targetPosition,
                moveToLandingArcHeight,
                moveT);
            SetPlayerPositionImmediate(playerTransform, nextPosition);
            yield return null;
        }

        if (WarpPlayer(context, arrivalLandingPoint))
            yield return new WaitForFixedUpdate();
    }

    private static Vector3 EvaluateParabolicArrivalPosition(
        Vector3 startPosition,
        Vector3 targetPosition,
        float arcHeight,
        float t)
    {
        t = Mathf.Clamp01(t);
        Vector3 position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
        position.y += 4f * Mathf.Max(0f, arcHeight) * t * (1f - t);
        return position;
    }

    private void PlayLandingStartParticle(
        Transform playerTransform,
        Transform landingStartPoint,
        Vector3 startPosition,
        Vector3 targetPosition)
    {
        if (!landingStartParticle.HasContent || playerTransform == null)
            return;

        Vector3 fallbackDirection = targetPosition - startPosition;
        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            fallbackDirection = Vector3.up;

        Quaternion rotation = landingStartPoint != null
            ? landingStartPoint.rotation
            : playerTransform.rotation;

        WorldPresentationContext presentationContext = WorldPresentationContext.AtWorld(
            instigator: playerTransform.gameObject,
            position: startPosition,
            fallbackDirection: fallbackDirection.normalized,
            target: null,
            sourceObject: this,
            rotation: rotation,
            causer: gameObject);

        WorldPresentationRuntime.SpawnVisual(landingStartParticle, presentationContext);
    }

    private bool WarpPlayer(
        RunSpecialNpcFeatureContext context,
        Transform targetPoint,
        bool allowHoleTrapTarget = false)
    {
        Transform playerTransform = ResolvePlayerTransform(context);
        if (playerTransform == null || targetPoint == null)
            return false;

        Vector3 targetPosition = ResolvePointPosition(targetPoint, playerTransform);
        if (!allowHoleTrapTarget && IsPositionBlockedByHoleTrap(targetPosition))
        {
            LogTeleportWarning("Teleport target overlaps HoleTrap.");
            return false;
        }

        MovementMotor2D movementMotor = playerTransform.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement, clearAbilityMotion);
            return true;
        }

        SetPlayerPositionImmediate(playerTransform, targetPosition);
        return true;
    }

    private static bool ShouldUseArrivalMovement(
        Transform playerTransform,
        Transform arrivalAppearancePoint,
        Transform arrivalLandingPoint)
    {
        if (playerTransform == null || arrivalAppearancePoint == null || arrivalLandingPoint == null)
            return false;

        if (arrivalAppearancePoint == arrivalLandingPoint)
            return false;

        Vector3 appearancePosition = ResolvePointPosition(arrivalAppearancePoint, playerTransform);
        Vector3 landingPosition = ResolvePointPosition(arrivalLandingPoint, playerTransform);
        return (landingPosition - appearancePosition).sqrMagnitude > 0.0001f;
    }

    private static bool IsTeleportPointBlockedByHoleTrap(Transform point, Transform playerTransform)
    {
        if (point == null || playerTransform == null)
            return false;

        return IsPositionBlockedByHoleTrap(ResolvePointPosition(point, playerTransform));
    }

    private static bool IsPositionBlockedByHoleTrap(Vector3 position)
    {
        int hitCount = Physics2D.OverlapCircle(
            position,
            HoleCheckRadius,
            CreateHoleTrapFilter(),
            HoleOverlapBuffer);
        bool hasHoleTrap = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = HoleOverlapBuffer[i];
            if (collider != null && HasHoleTrap(collider))
                hasHoleTrap = true;

            HoleOverlapBuffer[i] = null;
        }

        return hasHoleTrap;
    }

    private static ContactFilter2D CreateHoleTrapFilter()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;
        return filter;
    }

    private static bool HasHoleTrap(Collider2D collider)
    {
        if (collider == null)
            return false;

        return collider.GetComponent<HoleTrap>() != null ||
               collider.GetComponentInParent<HoleTrap>() != null;
    }

    private static Vector3 ResolvePointPosition(Transform point, Transform playerTransform)
    {
        Vector3 targetPosition = point != null ? point.position : playerTransform.position;
        targetPosition.z = playerTransform.position.z;
        return targetPosition;
    }

    private static void SetPlayerPositionImmediate(Transform playerTransform, Vector3 targetPosition)
    {
        if (playerTransform == null)
            return;

        Rigidbody2D body = playerTransform.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = new Vector2(targetPosition.x, targetPosition.y);
            body.linearVelocity = Vector2.zero;
        }

        playerTransform.position = targetPosition;
    }

    private static BodyColliderSuppression SuppressPlayerBodyColliderForArrival(Transform playerTransform)
    {
        Collider2D bodyCollider = ResolvePlayerBodyCollider(playerTransform);
        return bodyCollider != null ? new BodyColliderSuppression(bodyCollider) : null;
    }

    private static Collider2D ResolvePlayerBodyCollider(Transform playerTransform)
    {
        if (playerTransform == null)
            return null;

        PlayerInteractor2D interactor = playerTransform.GetComponent<PlayerInteractor2D>();
        if (interactor != null && interactor.BodyCollider != null)
            return interactor.BodyCollider;

        Collider2D rootCollider = playerTransform.GetComponent<Collider2D>();
        if (rootCollider != null && !rootCollider.isTrigger)
            return rootCollider;

        Collider2D[] colliders = playerTransform.GetComponentsInChildren<Collider2D>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate != null && !candidate.isTrigger)
                return candidate;
        }

        return rootCollider;
    }

    private PlayerCinematicProtection AcquirePlayerCinematicProtection(Transform playerTransform)
    {
        if (playerTransform == null)
            return null;

        PlayerCinematicProtection protection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (protection == null)
            protection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        protection.Acquire(this);
        return protection;
    }

    private PlayerTargetabilityBlocker AcquirePlayerTargetabilityBlocker(Transform playerTransform)
    {
        PlayerTargetabilityBlocker blocker = PlayerTargetabilityBlocker.GetOrAdd(playerTransform);
        if (blocker != null)
            blocker.Acquire(this);

        return blocker;
    }

    private void LogTeleportWarning(string message)
    {
        Debug.LogWarning($"[RunSameSceneTeleportNpcFeature] {message}", this);
    }

    private static Transform ResolvePlayerTransform(RunSpecialNpcFeatureContext context)
    {
        if (context?.Player?.Transform != null)
            return context.Player.Transform;

        return PlayerRuntimeRegistry.GetPlayerTransform();
    }

    private sealed class BodyColliderSuppression
    {
        private readonly Collider2D bodyCollider;
        private readonly bool previousEnabled;
        private bool restored;

        public BodyColliderSuppression(Collider2D bodyCollider)
        {
            this.bodyCollider = bodyCollider;
            if (bodyCollider == null)
                return;

            previousEnabled = bodyCollider.enabled;
            if (previousEnabled)
                bodyCollider.enabled = false;
        }

        public void Restore()
        {
            if (restored)
                return;

            restored = true;
            if (bodyCollider != null)
                bodyCollider.enabled = previousEnabled;
        }
    }
}
