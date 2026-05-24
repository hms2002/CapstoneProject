using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public sealed class RunSameSceneTeleportNpcFeature : RunSpecialNpcFeatureBase
{
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

        if (!HasDestination || ResolvePlayerTransform(context) == null)
            return RunSpecialNpcDialogueBranchKey.TeleportUnavailable;

        return RunSpecialNpcDialogueBranchKey.TeleportAvailable;
    }

    public override bool CanExecute(RunSpecialNpcFeatureContext context)
    {
        return base.CanExecute(context) &&
               HasRequiredAffection() &&
               ResolvePlayerTransform(context) != null &&
               landingPoint != null;
    }

    public override string GetUnavailableReason(RunSpecialNpcFeatureContext context)
    {
        if (!HasRequiredAffection())
            return $"Affection {requiredAffectionNpcId} is below {requiredAffectionAmount}.";

        if (landingPoint == null)
            return "Teleport landing point is missing.";

        if (ResolvePlayerTransform(context) == null)
            return "Current player transform is missing.";

        return string.Empty;
    }

    public override IEnumerator Execute(RunSpecialNpcFeatureContext context)
    {
        if (!CanExecute(context))
            yield break;

        Transform playerTransform = ResolvePlayerTransform(context);
        PlayerCinematicProtection playerProtection = AcquirePlayerCinematicProtection(playerTransform);
        PlayerTargetabilityBlocker targetabilityBlocker = AcquirePlayerTargetabilityBlocker(playerTransform);

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

            WarpPlayer(context, arrivalAppearancePoint);
            yield return new WaitForFixedUpdate();

            if (fadeSessionStarted)
                yield return transitionService.FadeInAsync(fadeInDuration);

            if (arrivalAppearancePoint != arrivalLandingPoint)
                yield return MovePlayerToLanding(context, arrivalAppearancePoint, arrivalLandingPoint);
        }
        finally
        {
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

        if (appearanceHoldDuration > 0f)
            yield return new WaitForSeconds(appearanceHoldDuration);

        Vector3 startPosition = ResolvePointPosition(arrivalAppearancePoint, playerTransform);
        Vector3 targetPosition = ResolvePointPosition(arrivalLandingPoint, playerTransform);
        if ((targetPosition - startPosition).sqrMagnitude <= 0.0001f)
            yield break;

        if (moveToLandingDuration <= 0f)
        {
            WarpPlayer(context, arrivalLandingPoint);
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

            SetPlayerPositionImmediate(playerTransform, Vector3.LerpUnclamped(startPosition, targetPosition, moveT));
            yield return null;
        }

        WarpPlayer(context, arrivalLandingPoint);
        yield return new WaitForFixedUpdate();
    }

    private void WarpPlayer(RunSpecialNpcFeatureContext context, Transform targetPoint)
    {
        Transform playerTransform = ResolvePlayerTransform(context);
        if (playerTransform == null || targetPoint == null)
            return;

        Vector3 targetPosition = ResolvePointPosition(targetPoint, playerTransform);
        MovementMotor2D movementMotor = playerTransform.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement, clearAbilityMotion);
            return;
        }

        SetPlayerPositionImmediate(playerTransform, targetPosition);
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

    private static Transform ResolvePlayerTransform(RunSpecialNpcFeatureContext context)
    {
        if (context?.Player?.Transform != null)
            return context.Player.Transform;

        return PlayerRuntimeRegistry.GetPlayerTransform();
    }
}
