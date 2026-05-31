using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class EgoSwordActor : MonoBehaviour
{
    private const string EgoSwordAuraControllerResourcePath = "DemonKing/Vfx/EgoSwordAttackAuraVfx";
    private const string EgoSwordAuraStartStateName = "Start";
    private const string EgoSwordAuraIdleStateName = "Idle";
    private const string EgoSwordAuraEndStateName = "End";

    private static readonly RaycastHit2D[] WallHitBuffer = new RaycastHit2D[8];
    private static readonly Color AttackSquareColor = new(1f, 0.85f, 0.2f, 0.62f);
#if UNITY_EDITOR
    private static readonly Color HeldGizmoColor = new(0.25f, 0.85f, 1f, 1f);
    private static readonly Color ThrowGizmoColor = new(1f, 0.85f, 0.15f, 1f);
    private static readonly Color RecallGizmoColor = new(0.45f, 1f, 0.35f, 1f);
    private static readonly Color LaserGizmoColor = new(1f, 0.25f, 0.25f, 1f);
    private static readonly Color ImpactGizmoColor = new(1f, 0.45f, 0.1f, 1f);
    private const float SocketGizmoRadius = 0.1f;
#endif

    private enum SwordState
    {
        Held,
        Flying,
        Planting,
        Fixed,
        Recalling,
        CinematicPlanted
    }

    [Header("Held")]
    [SerializeField] private Vector3 heldOffset = new(0.85f, 0.1f, 0f);
    [SerializeField] private Vector3 throwOriginLocalOffset = new(0.85f, 0.1f, 0f);
    [SerializeField] private Vector3 recallTargetLocalOffset = new(0.85f, 0.1f, 0f);
    [SerializeField] private float throwInitialRotation = 0f;
    [SerializeField] private float recallInitialRotation = 0f;

    [Header("Recall")]
    [SerializeField, Min(0f)] private float recallLiftHeight = 2.2f;
    [SerializeField, Min(0f)] private float recallLiftSeconds = 0.16f;
    [SerializeField, Min(0f)] private float recallLiftHoldSeconds = 0.18f;
    [SerializeField, Min(0f)] private float recallReturnMinimumSeconds = 0.35f;

    [Header("Throw")]
    [SerializeField, Min(0.01f)] private float contactRadius = 0.45f;
    [SerializeField, Min(0f)] private float contactDamage = 1f;
    [SerializeField, Min(0f)] private float contactKnockback = 6f;
    [SerializeField, Min(0f)] private float flyingRotationDegreesPerSecond = 720f;

    [Header("Throw Landing")]
    [SerializeField, Min(0f)] private float plantingHopSeconds = 0.18f;
    [SerializeField, Min(0f)] private float plantingHopDistance = 0.45f;
    [SerializeField, Min(0f)] private float plantingHopArcHeight = 0.25f;
    [SerializeField, Range(0.05f, 0.95f)] private float buriedMaskHeightRatio = 0.42f;
    [SerializeField, Min(0.1f)] private float buriedMaskWidthMultiplier = 1.6f;
    [SerializeField] private SpriteMask buriedMask;

    [Header("Cinematic Planting")]
    [SerializeField, Min(0f)] private float finalPlantDistance = 1.1f;
    [SerializeField] private Vector3 finalPlantLocalOffset = new(0f, -0.15f, 0f);
    [SerializeField, Min(0f)] private float deathPlantDistance = 1.1f;
    [SerializeField, Min(0f)] private float deathPlantTravelSeconds = 0.28f;
    [SerializeField, Min(0f)] private float deathPlantSpinDegreesPerSecond = 720f;
    [SerializeField, Min(0f)] private float deathPlantArcHeight = 0.25f;

    [Header("Dropped Patterns")]
    [SerializeField, Min(0.1f)] private float patternIntervalSeconds = 1.5f;
    [SerializeField, Min(0f)] private float laserWarningSeconds = 1f;
    [SerializeField, Min(0f)] private float laserAttackDurationSeconds = 1f;
    [SerializeField, Range(0.1f, 1f)] private float laserTempoMultiplier = 0.75f;
    [SerializeField, Min(0.1f)] private float fallbackMapLaserLength = 40f;
    [SerializeField, Min(0.1f)] private float laserWidth = 0.75f;
    [SerializeField, Min(0f)] private float laserVfxRayOriginOffset = 0.35f;
    [SerializeField] private bool useAnimatedLaserVfx = true;
    [SerializeField] private DemonKingEgoLaserVfx laserVfxPrefab;
    [SerializeField] private string laserVfxResourcePath = "DemonKing/DemonKingEgoLaserVfx";
    [SerializeField, Min(0.1f)] private float verticalTrackSeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float verticalHoverHeight = 2.2f;
    [SerializeField, Min(0.1f)] private float verticalStrikeDiameter = 2.3f;
    [SerializeField, Min(0f)] private float verticalStrikeApproachSeconds = 0.12f;
    [SerializeField, Min(0f)] private float verticalStrikeLiftSeconds = 0.1f;
    [SerializeField, Min(0f)] private float verticalStrikeLiftHeight = 0.45f;
    [SerializeField, Min(0.01f)] private float verticalStrikeDropSeconds = 0.16f;
    [SerializeField, Min(0f)] private float patternDamage = 1f;
    [SerializeField, Min(0.1f)] private float recallImpactDiameter = 3.2f;
    [SerializeField, Min(0f)] private float recallImpactDamage = 1.5f;
    [SerializeField, Min(0f)] private float recallImpactKnockback = 8f;

    [Header("Impact Presentation")]
    [SerializeField] private DemonKingVfxCueRef swordSpinVfx =
        DemonKingVfxCueRef.BuiltIn(DemonKingBuiltInVfxKind.SwordSpin, DemonKingVfxSocketId.SwordThrowEffectOrigin, Vector2.zero, Vector2.zero, leaveFragment: false);
    [SerializeField] private DemonKingVfxCueRef plantAttackVfx =
        DemonKingVfxCueRef.BuiltIn(DemonKingBuiltInVfxKind.EgoSwordAttack, DemonKingVfxSocketId.SwordThrowEffectOrigin, Vector2.zero, Vector2.zero, leaveFragment: false);
    [SerializeField] private DemonKingVfxCueRef plantImpactVfx =
        DemonKingVfxCueRef.BuiltIn(DemonKingBuiltInVfxKind.Impact, DemonKingVfxSocketId.SwordThrowEffectOrigin, Vector2.zero, Vector2.zero, leaveFragment: false);
    [SerializeField] private DemonKingVfxCueRef verticalAttackVfx =
        DemonKingVfxCueRef.BuiltIn(DemonKingBuiltInVfxKind.EgoSwordAttack, DemonKingVfxSocketId.SwordThrowEffectOrigin, Vector2.zero, Vector2.zero, leaveFragment: false);
    [SerializeField] private DemonKingVfxCueRef verticalImpactVfx =
        DemonKingVfxCueRef.BuiltIn(DemonKingBuiltInVfxKind.Impact, DemonKingVfxSocketId.SwordThrowEffectOrigin, Vector2.zero, Vector2.zero, leaveFragment: false);
    [SerializeField] private SoundRef verticalStrikeImpactSound;
    [SerializeField] private CameraShakeHook plantImpactCameraShake = CameraShakeHook.Create(0.12f, 1f, 0.28f, 0.04f);
    [SerializeField] private CameraShakeHook verticalStrikeImpactCameraShake = CameraShakeHook.Create(0.18f, 1f, 0.35f, 0.04f);

    [Header("Afterimage")]
    [SerializeField] private bool enableAfterimage = true;
    [SerializeField, Min(0.01f)] private float afterimageIntervalSeconds = 0.035f;
    [SerializeField, Min(0.01f)] private float afterimageLifetimeSeconds = 0.14f;
    [SerializeField] private Color afterimageColor = new(1f, 0.25f, 0.12f, 0.42f);

    private DemonKingController owner;
    private SwordState state = SwordState.Held;
    private Vector2 velocityDirection = Vector2.right;
    private float flyingSpeed;
    private int remainingBounces;
    private LayerMask wallMask;
    private Coroutine droppedPatternRoutine;
    private Coroutine plantingRoutine;
    private bool useCrossPatternNext;
    private bool laserVfxMissingLogged;
    private SpriteRenderer[] baseSwordRenderers;
    private SpriteMask runtimeBuriedMask;
    private DemonKingAnimationClipVisual activeVerticalAttackVfx;
    private DemonKingAnimationClipVisual activeBuriedFragmentVfx;
    private DemonKingAnimationClipVisual activeSwordSpinVfx;
    private SpriteRenderer primarySwordRenderer;
    private Animator swordAnimator;
    private RuntimeAnimatorController defaultSwordAnimatorController;
    private RuntimeAnimatorController auraAnimatorController;
    private Sprite defaultSwordSprite;
    private Coroutine recallLiftRoutine;
    private Coroutine verticalAuraAnimationRoutine;
    private bool verticalAuraAnimationActive;
    private bool swordAnimationDefaultsCaptured;
    private bool auraControllerMissingLogged;
    private bool auraControllerInvalidLogged;
    private bool recallMovementActive;
    private bool recallAuraReady;
    private bool recallLiftReady;
    private bool subPatternAbilityRunning;
    private AbilitySpec activeSubPatternSpec;
    private SpriteAfterimageEmitter2D afterimageEmitter;

    public bool IsHeld => state == SwordState.Held;
    public bool IsDropped => state == SwordState.Flying ||
                             state == SwordState.Planting ||
                             state == SwordState.Fixed ||
                             state == SwordState.Recalling ||
                             state == SwordState.CinematicPlanted;
    public bool IsRecallActive => state == SwordState.Recalling;

    private void Awake()
    {
        CacheBaseSwordRenderers();
        CacheSwordAnimationDefaults();
        ApplyProjectileSortingOnce();
        ClearBuriedMask();

        if (TryGetComponent(out Collider2D collider2d))
            collider2d.isTrigger = true;
    }

    private void OnEnable()
    {
        CacheBaseSwordRenderers();
        CacheSwordAnimationDefaults();
        ApplyProjectileSortingOnce();
    }

    private void OnDisable()
    {
        StopAfterimage(clearGhosts: true);
        StopRecallLiftRoutine();
        StopSwordSpinEffect();
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        ReleaseVerticalStrikeVfx();
        ReleaseAttachedOneShotVfx();
        ResetRecallReadiness();
    }

    private void LateUpdate()
    {
        if (owner == null || state != SwordState.Held)
            return;

        transform.position = owner.ResolveSwordHoldPosition(heldOffset);
    }

    private void FixedUpdate()
    {
        if (owner == null)
            return;

        if (state == SwordState.Flying)
            TickFlying();
        else if (state == SwordState.Recalling)
            TickRecall();
    }

    public void Bind(DemonKingController newOwner)
    {
        owner = newOwner;
        ApplyProjectileSortingOnce();
    }

    public void HideWhileHeld()
    {
        StopAfterimage(clearGhosts: true);
        StopRecallLiftRoutine();
        StopSwordSpinEffect();
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        ReleaseVerticalStrikeVfx();
        ReleaseAttachedOneShotVfx();
        ResetRecallReadiness();
        subPatternAbilityRunning = false;
        activeSubPatternSpec = null;
        state = SwordState.Held;
        flyingSpeed = 0f;
        remainingBounces = 0;
        transform.SetParent(owner != null ? owner.transform : null, true);
        transform.rotation = Quaternion.identity;
        if (owner != null)
            transform.position = owner.ResolveSwordHoldPosition(heldOffset);

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public Vector2 ResolveThrowOriginPosition()
    {
        if (owner != null)
            return owner.ResolveVfxSocketWorld(DemonKingVfxSocketId.SwordThrowOrigin, throwOriginLocalOffset);

        return (Vector2)(transform.position + throwOriginLocalOffset);
    }

    public void CleanupForBossBattleEnd()
    {
        StopAfterimage(clearGhosts: true);
        StopRecallLiftRoutine();
        StopSwordSpinEffect();
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        ReleaseVerticalStrikeVfx();
        ReleaseAttachedOneShotVfx();
        ResetRecallReadiness();
        subPatternAbilityRunning = false;
        activeSubPatternSpec = null;
        state = SwordState.Held;
        flyingSpeed = 0f;
        remainingBounces = 0;
        transform.SetParent(owner != null ? owner.transform : null, true);
        transform.rotation = Quaternion.identity;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void Throw(Vector2 origin, Vector2 direction, float speed, int bounceCount, LayerMask newWallMask)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopRecallLiftRoutine();
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        ResetRecallReadiness();
        transform.SetParent(null, true);
        transform.position = origin;
        transform.rotation = Quaternion.Euler(0f, 0f, throwInitialRotation);
        velocityDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        flyingSpeed = Mathf.Max(0.01f, speed);
        remainingBounces = Mathf.Max(0, bounceCount);
        wallMask = newWallMask;
        useCrossPatternNext = false;
        state = SwordState.Flying;
        PlayAuraStartAnimation(playIdleAfterStart: true);
        StartSwordSpinEffect();
        BeginAfterimage();

        if (remainingBounces <= 0)
            BeginPlantingAtCurrentPosition(velocityDirection);
    }

    public void FixAtCurrentPosition()
    {
        if (state == SwordState.Fixed || state == SwordState.Planting)
            return;

        BeginPlantingAtCurrentPosition(velocityDirection);
    }

    public void ShowFinalDesperationPlanted(Vector2 ownerCenter, Vector2 facingDirection)
    {
        Vector2 safeDirection = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.left;
        Vector2 side = new(-safeDirection.y, safeDirection.x);
        Vector2 target = ownerCenter
            + safeDirection * Mathf.Max(0f, finalPlantDistance)
            + safeDirection * finalPlantLocalOffset.x
            + side * finalPlantLocalOffset.y;
        PlaceCinematicPlanted(target, spawnFragment: true);
    }

    public void StartDeathPlant(Vector2 ownerCenter, Vector2 facingDirection)
    {
        Vector2 safeDirection = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.left;
        Vector2 target = ownerCenter + safeDirection * Mathf.Max(0f, deathPlantDistance);
        PrepareCinematicPlanting();

        Vector2 start = owner != null
            ? owner.ResolveSwordHoldPosition(throwOriginLocalOffset)
            : (Vector2)transform.position;
        transform.position = new Vector3(start.x, start.y, transform.position.z);
        transform.rotation = Quaternion.identity;

        if (!isActiveAndEnabled || deathPlantTravelSeconds <= 0.001f)
        {
            PlaceCinematicPlanted(target, spawnFragment: false);
            return;
        }

        plantingRoutine = StartCoroutine(CoDeathPlant(start, target));
    }

    public void Recall(float speed)
    {
        StopRecallLiftRoutine();
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        transform.SetParent(null, true);
        transform.rotation = Quaternion.Euler(0f, 0f, recallInitialRotation);
        flyingSpeed = Mathf.Max(0.01f, speed);
        ResetRecallReadiness();
        state = SwordState.Recalling;
        PlayRecallAuraStartup();
        StartRecallLiftMotion();
    }

    public float EstimateRecallTimeoutSeconds(float speed)
    {
        float safeSpeed = Mathf.Max(0.01f, speed);
        Vector2 liftedPosition = (Vector2)transform.position + Vector2.up * Mathf.Max(0f, recallLiftHeight);
        Vector2 targetPosition = ResolveRecallTargetPosition();
        float returnSeconds = Vector2.Distance(liftedPosition, targetPosition) / safeSpeed;
        return Mathf.Max(0f, recallLiftSeconds)
            + Mathf.Max(0f, recallLiftHoldSeconds)
            + Mathf.Max(Mathf.Max(0f, recallReturnMinimumSeconds), returnSeconds)
            + 0.75f;
    }

    private void TickFlying()
    {
        RotateClockwise(Time.fixedDeltaTime);

        Vector2 current = transform.position;
        float remainingMoveDistance = flyingSpeed * Time.fixedDeltaTime;

        while (remainingMoveDistance > 0.0001f && state == SwordState.Flying)
        {
            RaycastHit2D wallHit = FindNearestWallHit(current, velocityDirection, remainingMoveDistance);
            if (wallHit.collider != null)
            {
                Vector2 collisionCenter = wallHit.centroid != Vector2.zero ? wallHit.centroid : wallHit.point;
                transform.position = collisionCenter;
                velocityDirection = Vector2.Reflect(velocityDirection, wallHit.normal).normalized;
                remainingBounces--;

                if (remainingBounces <= 0)
                {
                    BeginPlantingAtCurrentPosition(velocityDirection);
                    return;
                }

                float consumedDistance = Mathf.Max(0f, wallHit.distance);
                remainingMoveDistance = Mathf.Max(0f, remainingMoveDistance - consumedDistance);
                current = (Vector2)transform.position + wallHit.normal * Mathf.Max(0.02f, contactRadius * 0.1f);
                transform.position = current;
            }
            else
            {
                current += velocityDirection * remainingMoveDistance;
                transform.position = current;
                remainingMoveDistance = 0f;
            }
        }

        DemonKingCombatUtil.ApplyCircleDamage(
            owner,
            transform.position,
            contactRadius,
            owner.DefaultDamageEffect,
            contactDamage,
            knockbackImpulse: contactKnockback);
    }

    private void TickRecall()
    {
        if (!recallMovementActive)
            return;

        RotateClockwise(Time.fixedDeltaTime);

        Vector2 targetPosition = ResolveRecallTargetPosition();
        Vector2 current = transform.position;
        Vector2 delta = targetPosition - current;
        float step = flyingSpeed * Time.fixedDeltaTime;

        if (delta.magnitude <= Mathf.Max(0.05f, step))
        {
            CompleteRecallAtOwner();
            return;
        }

        transform.position = current + delta.normalized * step;
        DemonKingCombatUtil.ApplyCircleDamage(
            owner,
            transform.position,
            contactRadius,
            owner.DefaultDamageEffect,
            contactDamage,
            knockbackImpulse: contactKnockback);
    }

    public void CompleteRecallAtOwner()
    {
        StopRecallLiftRoutine();
        ResetRecallReadiness();
        StopAfterimage();
        StopSwordSpinEffect();
        if (owner == null)
            return;

        Vector2 impactCenter = ResolveRecallTargetPosition();
        DemonKingCombatUtil.ApplyCircleDamage(
            owner,
            impactCenter,
            recallImpactDiameter * 0.5f,
            owner.DefaultDamageEffect,
            recallImpactDamage,
            knockbackImpulse: recallImpactKnockback);

        owner.CompleteEgoSwordRecall();
    }

    private Vector2 ResolveRecallTargetPosition()
    {
        if (owner != null)
            return owner.ResolveVfxSocketWorld(DemonKingVfxSocketId.SwordThrowReturnOrigin, recallTargetLocalOffset);

        return (Vector2)(transform.position + recallTargetLocalOffset);
    }

    private void StartRecallLiftMotion()
    {
        StopRecallLiftRoutine();

        if (!isActiveAndEnabled)
        {
            MarkRecallLiftReady();
            return;
        }

        recallLiftRoutine = StartCoroutine(CoRecallLiftBeforeReturn());
    }

    private IEnumerator CoRecallLiftBeforeReturn()
    {
        Vector2 start = transform.position;
        Vector2 end = start + Vector2.up * Mathf.Max(0f, recallLiftHeight);
        float duration = Mathf.Max(0f, recallLiftSeconds);

        if (duration <= 0.001f)
        {
            transform.position = new Vector3(end.x, end.y, transform.position.z);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (state != SwordState.Recalling)
                {
                    recallLiftRoutine = null;
                    yield break;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                Vector2 position = Vector2.Lerp(start, end, easedT);
                transform.position = new Vector3(position.x, position.y, transform.position.z);
                transform.rotation = Quaternion.Euler(0f, 0f, recallInitialRotation);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        transform.position = new Vector3(end.x, end.y, transform.position.z);
        float holdSeconds = Mathf.Max(0f, recallLiftHoldSeconds);
        float holdElapsed = 0f;
        while (holdElapsed < holdSeconds)
        {
            if (state != SwordState.Recalling)
            {
                recallLiftRoutine = null;
                yield break;
            }

            transform.position = new Vector3(end.x, end.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, recallInitialRotation);
            holdElapsed += Time.deltaTime;
            yield return null;
        }

        recallLiftRoutine = null;
        MarkRecallLiftReady();
    }

    private void StopRecallLiftRoutine()
    {
        if (recallLiftRoutine == null)
            return;

        StopCoroutine(recallLiftRoutine);
        recallLiftRoutine = null;
    }

    private void ResetRecallReadiness()
    {
        recallMovementActive = false;
        recallAuraReady = false;
        recallLiftReady = false;
    }

    private void MarkRecallAuraReady()
    {
        recallAuraReady = true;
        TryStartRecallMovement();
    }

    private void MarkRecallLiftReady()
    {
        recallLiftReady = true;
        TryStartRecallMovement();
    }

    private void TryStartRecallMovement()
    {
        if (state != SwordState.Recalling || !recallAuraReady || !recallLiftReady || recallMovementActive)
            return;

        ClampRecallSpeedForVisibleReturn();
        StartSwordSpinEffect();
        BeginAfterimage();
        recallMovementActive = true;
    }

    private void ClampRecallSpeedForVisibleReturn()
    {
        float minimumSeconds = Mathf.Max(0f, recallReturnMinimumSeconds);
        if (minimumSeconds <= 0.001f)
            return;

        float distance = Vector2.Distance(transform.position, ResolveRecallTargetPosition());
        if (distance <= 0.001f)
            return;

        float visibleSpeed = distance / minimumSeconds;
        flyingSpeed = Mathf.Min(flyingSpeed, visibleSpeed);
    }

    private void RotateClockwise(float deltaTime)
    {
        if (flyingRotationDegreesPerSecond <= 0f)
            return;

        transform.Rotate(0f, 0f, -flyingRotationDegreesPerSecond * deltaTime);
    }

    private void StartDroppedPatterns()
    {
        StopDroppedPatterns();
        droppedPatternRoutine = StartCoroutine(RunDroppedPatternLoop());
    }

    private void StopPlantingRoutine()
    {
        if (plantingRoutine == null)
            return;

        StopCoroutine(plantingRoutine);
        plantingRoutine = null;
    }

    private void ApplyProjectileSortingOnce()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            DemonKingPrimitiveVisual.ApplyProjectileSorting(renderers[i]);
    }

    private void StopDroppedPatterns()
    {
        if (droppedPatternRoutine != null)
        {
            StopCoroutine(droppedPatternRoutine);
            droppedPatternRoutine = null;
        }

        if (activeSubPatternSpec != null && activeSubPatternSpec.Token != null)
            activeSubPatternSpec.Token.Cancel();

        subPatternAbilityRunning = false;
        activeSubPatternSpec = null;
        ReleaseVerticalStrikeVfx();
    }

    private void PrepareCinematicPlanting()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopAfterimage(clearGhosts: true);
        StopRecallLiftRoutine();
        StopSwordSpinEffect();
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        ReleaseVerticalStrikeVfx();
        ReleaseAttachedOneShotVfx();
        StopVerticalAuraAnimation();
        ResetRecallReadiness();
        subPatternAbilityRunning = false;
        activeSubPatternSpec = null;
        flyingSpeed = 0f;
        remainingBounces = 0;
        state = SwordState.CinematicPlanted;
        transform.SetParent(null, true);
    }

    private void PlaceCinematicPlanted(Vector2 position, bool spawnFragment)
    {
        PrepareCinematicPlanting();
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        ApplyBuriedMask(spawnFragment);
    }

    private IEnumerator CoDeathPlant(Vector2 start, Vector2 target)
    {
        float duration = Mathf.Max(0.01f, deathPlantTravelSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector2 flatPosition = Vector2.Lerp(start, target, t);
            float arcOffset = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, deathPlantArcHeight);
            transform.position = new Vector3(flatPosition.x, flatPosition.y + arcOffset, transform.position.z);
            if (deathPlantSpinDegreesPerSecond > 0f)
                transform.Rotate(0f, 0f, -deathPlantSpinDegreesPerSecond * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = new Vector3(target.x, target.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        ApplyBuriedMask(spawnFragment: false);
        state = SwordState.CinematicPlanted;
        plantingRoutine = null;
    }

    private void BeginPlantingAtCurrentPosition(Vector2 hopDirection)
    {
        StopPlantingRoutine();
        StopDroppedPatterns();
        ClearBuriedMask();
        StopVerticalAuraAnimation();
        StopSwordSpinEffect();
        recallMovementActive = false;

        state = SwordState.Planting;
        flyingSpeed = 0f;
        remainingBounces = 0;
        transform.rotation = Quaternion.identity;

        if (!isActiveAndEnabled)
        {
            CompletePlantingAtCurrentPosition(startPatterns: false);
            return;
        }

        plantingRoutine = StartCoroutine(CoPlantAfterThrow(hopDirection));
    }

    private IEnumerator CoPlantAfterThrow(Vector2 hopDirection)
    {
        Vector2 start = transform.position;
        Vector2 safeDirection = hopDirection.sqrMagnitude > 0.0001f ? hopDirection.normalized : Vector2.right;
        Vector2 end = start + safeDirection * plantingHopDistance;
        float duration = Mathf.Max(0f, plantingHopSeconds);

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 flatPosition = Vector2.Lerp(start, end, t);
                float arcOffset = Mathf.Sin(t * Mathf.PI) * plantingHopArcHeight;
                transform.position = new Vector3(flatPosition.x, flatPosition.y + arcOffset, transform.position.z);
                transform.rotation = Quaternion.identity;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        transform.position = new Vector3(end.x, end.y, transform.position.z);
        CompletePlantingAtCurrentPosition();
        plantingRoutine = null;
    }

    private void CompletePlantingAtCurrentPosition(bool startPatterns = true)
    {
        StopAfterimage();
        state = SwordState.Fixed;
        flyingSpeed = 0f;
        transform.rotation = Quaternion.identity;
        ApplyBuriedMask();
        DemonKingPatternVfx.SpawnCueAttachedOneShot(
            plantAttackVfx,
            transform,
            Vector3.zero,
            verticalStrikeDiameter,
            Vector2.down,
            "EgoSword_PlantAttackVfx");
        DemonKingPatternVfx.SpawnCueOneShot(
            plantImpactVfx,
            transform.position,
            verticalStrikeDiameter,
            Vector2.down,
            "EgoSword_PlantImpactVfx");
        PlayPlantImpactShake();
        useCrossPatternNext = false;
        if (startPatterns && isActiveAndEnabled)
            StartDroppedPatterns();
    }

    private void PlayPlantImpactShake()
    {
        if (owner == null || owner.IsDead || !owner.IsCombatActive)
            return;

        plantImpactCameraShake.TryPlay(gameObject, Vector2.down, debugReason: "DemonKing.EgoSwordPlantImpact");
    }

    private IEnumerator RunDroppedPatternLoop()
    {
        yield return WaitDroppedPatternInterval();

        while (CanRunDroppedPatterns())
        {
            bool activated = useCrossPatternNext
                ? TryActivateCrossLaserAbility()
                : TryActivateVerticalStrikeAbility();
            if (!activated)
            {
                yield return WaitDroppedPatternInterval();
                continue;
            }

            yield return null;
            while (CanRunDroppedPatterns() && subPatternAbilityRunning)
                yield return null;

            if (!CanRunDroppedPatterns())
                yield break;

            useCrossPatternNext = !useCrossPatternNext;
            owner.NotifyEgoSwordPatternCompleted();
            yield return WaitDroppedPatternInterval();
        }
    }

    public IEnumerator RunCrossLaserAbilityPattern(AbilitySpec spec)
    {
        BeginSubPatternAbility(spec);
        try
        {
            yield return RunCrossPattern(spec);
        }
        finally
        {
            EndSubPatternAbility(spec);
        }
    }

    public IEnumerator RunVerticalStrikeAbilityPattern(AbilitySpec spec)
    {
        BeginSubPatternAbility(spec);
        try
        {
            yield return RunVerticalStrikePattern(spec);
        }
        finally
        {
            EndSubPatternAbility(spec);
        }
    }

    private bool TryActivateVerticalStrikeAbility()
    {
        if (!BeginExpectedSubPatternAbility())
            return false;

        bool activated = owner != null && owner.TryStartEgoSwordVerticalStrikeSubPattern();
        if (!activated)
            EndExpectedSubPatternAbility();

        return activated;
    }

    private bool TryActivateCrossLaserAbility()
    {
        if (!BeginExpectedSubPatternAbility())
            return false;

        bool activated = owner != null && owner.TryStartEgoSwordCrossLaserSubPattern();
        if (!activated)
            EndExpectedSubPatternAbility();

        return activated;
    }

    private bool BeginExpectedSubPatternAbility()
    {
        if (!CanRunDroppedPatterns())
            return false;

        if (subPatternAbilityRunning)
            return false;

        subPatternAbilityRunning = true;
        activeSubPatternSpec = null;
        return true;
    }

    private void EndExpectedSubPatternAbility()
    {
        if (activeSubPatternSpec != null)
            return;

        subPatternAbilityRunning = false;
    }

    private void BeginSubPatternAbility(AbilitySpec spec)
    {
        subPatternAbilityRunning = true;
        activeSubPatternSpec = spec;
    }

    private void EndSubPatternAbility(AbilitySpec spec)
    {
        if (activeSubPatternSpec != null && activeSubPatternSpec != spec)
            return;

        activeSubPatternSpec = null;
        subPatternAbilityRunning = false;
    }

    private IEnumerator WaitDroppedPatternInterval()
    {
        float intervalSeconds = ResolveLaserTempoSeconds(patternIntervalSeconds);
        float elapsed = 0f;
        while (elapsed < intervalSeconds)
        {
            if (!CanRunDroppedPatterns())
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool CanRunDroppedPatterns()
    {
        return owner != null &&
               !owner.IsDead &&
               owner.IsCombatActive &&
               state == SwordState.Fixed;
    }

    private bool IsSubPatternCancelled(AbilitySpec spec)
    {
        return owner == null ||
               owner.IsDead ||
               !owner.IsCombatActive ||
               state != SwordState.Fixed ||
               spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private IEnumerator RunCrossPattern(AbilitySpec spec)
    {
        if (IsSubPatternCancelled(spec))
            yield break;

        yield return RunLaserPair(Vector2.right, Vector2.up, spec);
        if (IsSubPatternCancelled(spec))
            yield break;

        yield return RunLaserPair(new Vector2(1f, 1f).normalized, new Vector2(1f, -1f).normalized, spec);
    }

    private IEnumerator RunLaserPair(Vector2 firstDirection, Vector2 secondDirection, AbilitySpec spec)
    {
        if (IsSubPatternCancelled(spec))
            yield break;

        AttackTelegraphService telegraph = owner.GetTelegraphService();
        Vector2 laserOrigin = transform.position;
        LaserLine firstWarningLine = ResolveWallClippedLaserLine(laserOrigin, firstDirection);
        LaserLine secondWarningLine = ResolveWallClippedLaserLine(laserOrigin, secondDirection);
        LaserLine firstAttackLine = ResolvePiercingLaserLine(laserOrigin, firstDirection);
        LaserLine secondAttackLine = ResolvePiercingLaserLine(laserOrigin, secondDirection);
        bool shouldSyncAuraWithLaser = ResolveLaserVfxPrefab() != null;
        float warningSeconds = ResolveLaserTempoSeconds(laserWarningSeconds);
        float attackSeconds = ResolveLaserTempoSeconds(laserAttackDurationSeconds);

        telegraph?.SpawnDetachedView(CreateLaserSpec(firstWarningLine, warningSeconds));
        telegraph?.SpawnDetachedView(CreateLaserSpec(secondWarningLine, warningSeconds));
        if (shouldSyncAuraWithLaser)
            PlayAuraStartAnimation(playIdleAfterStart: true);

        float warningElapsed = 0f;
        while (warningElapsed < warningSeconds)
        {
            if (IsSubPatternCancelled(spec))
                yield break;

            warningElapsed += Time.deltaTime;
            yield return null;
        }

        if (shouldSyncAuraWithLaser)
        {
            StopAuraTransitionRoutine();
            PlayAuraIdleAnimation();
        }

        DemonKingEgoLaserVfx[] firstLaserVfx = SpawnLaserLineVfx(firstAttackLine, attackSeconds);
        DemonKingEgoLaserVfx[] secondLaserVfx = SpawnLaserLineVfx(secondAttackLine, attackSeconds);
        bool usingAnimatedVfx = HasAnyLaserVfx(firstLaserVfx) || HasAnyLaserVfx(secondLaserVfx);

        if (!usingAnimatedVfx)
        {
            DemonKingPrimitiveVisual.SpawnSquare(
                firstAttackLine.Center,
                firstAttackLine.Size,
                firstAttackLine.RotationDeg,
                attackSeconds,
                AttackSquareColor,
                "DemonKing_EgoLaserSquareAttack");
            DemonKingPrimitiveVisual.SpawnSquare(
                secondAttackLine.Center,
                secondAttackLine.Size,
                secondAttackLine.RotationDeg,
                attackSeconds,
                AttackSquareColor,
                "DemonKing_EgoLaserSquareAttack");
            telegraph?.SpawnDetachedView(CreateLaserSpec(firstAttackLine, attackSeconds));
            telegraph?.SpawnDetachedView(CreateLaserSpec(secondAttackLine, attackSeconds));
        }

        HashSet<GameObject> damagedTargets = new();
        float elapsed = 0f;
        bool auraEndStarted = false;
        while (usingAnimatedVfx ? IsAnyLaserVfxPlaying(firstLaserVfx, secondLaserVfx) : elapsed < attackSeconds)
        {
            if (IsSubPatternCancelled(spec))
                yield break;

            if (usingAnimatedVfx)
            {
                if (!auraEndStarted && (IsAnyLaserEndActive(firstLaserVfx) || IsAnyLaserEndActive(secondLaserVfx)))
                {
                    auraEndStarted = true;
                    PlayAuraEndAnimation();
                }
            }

            if (!usingAnimatedVfx || IsAnyLaserDamageActive(firstLaserVfx))
            {
                DemonKingCombatUtil.ApplyRectangleDamage(
                    owner,
                    firstAttackLine.Center,
                    firstAttackLine.Size,
                    firstAttackLine.RotationDeg,
                    owner.DefaultDamageEffect,
                    patternDamage,
                    damagedTargets);
            }

            if (!usingAnimatedVfx || IsAnyLaserDamageActive(secondLaserVfx))
            {
                DemonKingCombatUtil.ApplyRectangleDamage(
                    owner,
                    secondAttackLine.Center,
                    secondAttackLine.Size,
                    secondAttackLine.RotationDeg,
                    owner.DefaultDamageEffect,
                    patternDamage,
                    damagedTargets);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (usingAnimatedVfx && !auraEndStarted)
            PlayAuraEndAnimation();
        else if (!usingAnimatedVfx && shouldSyncAuraWithLaser)
            StopVerticalAuraAnimation();
    }

    private DemonKingEgoLaserVfx[] SpawnLaserLineVfx(LaserLine line, float attackSeconds)
    {
        DemonKingEgoLaserVfx prefab = ResolveLaserVfxPrefab();
        if (prefab == null)
            return null;

        DemonKingEgoLaserVfx[] views = new DemonKingEgoLaserVfx[2];
        float forwardOffset = ResolveLaserVfxRayOriginOffset(line.ForwardDistance);
        float backwardOffset = ResolveLaserVfxRayOriginOffset(line.BackwardDistance);
        Vector2 backwardDirection = -line.Direction;

        views[0] = SpawnLaserRayVfx(
            prefab,
            line.Origin + line.Direction * forwardOffset,
            line.Direction,
            line.ForwardDistance - forwardOffset,
            attackSeconds);
        views[1] = SpawnLaserRayVfx(
            prefab,
            line.Origin + backwardDirection * backwardOffset,
            backwardDirection,
            line.BackwardDistance - backwardOffset,
            attackSeconds);
        return views;
    }

    private float ResolveLaserVfxRayOriginOffset(float rayLength)
    {
        return Mathf.Clamp(laserVfxRayOriginOffset, 0f, Mathf.Max(0f, rayLength - 0.01f));
    }

    private DemonKingEgoLaserVfx SpawnLaserRayVfx(
        DemonKingEgoLaserVfx prefab,
        Vector2 origin,
        Vector2 direction,
        float length,
        float attackSeconds)
    {
        if (prefab == null || length <= 0.01f)
            return null;

        DemonKingEgoLaserVfx instance = Instantiate(prefab);
        instance.name = "DemonKing_EgoLaserAnimatedAttack";
        instance.Play(origin, direction, length, laserWidth, attackSeconds);
        return instance;
    }

    private DemonKingEgoLaserVfx ResolveLaserVfxPrefab()
    {
        if (!useAnimatedLaserVfx)
            return null;

        if (laserVfxPrefab != null)
            return laserVfxPrefab;

        if (!string.IsNullOrWhiteSpace(laserVfxResourcePath))
        {
            GameObject prefabObject = Resources.Load<GameObject>(laserVfxResourcePath);
            if (prefabObject != null)
                laserVfxPrefab = prefabObject.GetComponent<DemonKingEgoLaserVfx>();
        }

        if (laserVfxPrefab == null && !laserVfxMissingLogged)
        {
            laserVfxMissingLogged = true;
            Debug.LogWarning(
                $"EgoSwordActor could not load animated laser VFX at Resources/{laserVfxResourcePath}. Falling back to primitive laser visuals.",
                this);
        }

        return laserVfxPrefab;
    }

    private static bool HasAnyLaserVfx(DemonKingEgoLaserVfx[] views)
    {
        if (views == null)
            return false;

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null)
                return true;
        }

        return false;
    }

    private static bool IsAnyLaserDamageActive(DemonKingEgoLaserVfx[] views)
    {
        if (views == null)
            return false;

        for (int i = 0; i < views.Length; i++)
        {
            DemonKingEgoLaserVfx view = views[i];
            if (view != null && view.DamageActive)
                return true;
        }

        return false;
    }

    private static bool IsAnyLaserEndActive(DemonKingEgoLaserVfx[] views)
    {
        if (views == null)
            return false;

        for (int i = 0; i < views.Length; i++)
        {
            DemonKingEgoLaserVfx view = views[i];
            if (view != null && view.EndActive)
                return true;
        }

        return false;
    }

    private static bool IsAnyLaserVfxPlaying(params DemonKingEgoLaserVfx[][] viewGroups)
    {
        if (viewGroups == null)
            return false;

        for (int groupIndex = 0; groupIndex < viewGroups.Length; groupIndex++)
        {
            DemonKingEgoLaserVfx[] views = viewGroups[groupIndex];
            if (views == null)
                continue;

            for (int i = 0; i < views.Length; i++)
            {
                DemonKingEgoLaserVfx view = views[i];
                if (view != null && view.IsPlaying)
                    return true;
            }
        }

        return false;
    }

    private IEnumerator RunVerticalStrikePattern(AbilitySpec spec)
    {
        if (IsSubPatternCancelled(spec))
            yield break;

        AttackTelegraphService telegraph = owner.GetTelegraphService();
        Vector2 groundTarget = owner.CurrentTarget != null
            ? (Vector2)owner.CurrentTarget.position
            : (Vector2)transform.position;
        Vector2 hoverTarget = groundTarget + Vector2.up * verticalHoverHeight;
        float warningDuration = verticalTrackSeconds + ResolveVerticalStrikeCommitMotionSeconds();
        ClearBuriedMask();
        ReleaseVerticalStrikeVfx();
        PlayVerticalAuraAnimation();
        BeginAfterimage();

        AttackTelegraphView warning = telegraph?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                DemonKingCombatUtil.CreateTopDownCircleWarningSpec(
                    owner,
                    groundTarget,
                    verticalStrikeDiameter,
                    warningDuration)));

        try
        {
            float elapsed = 0f;
            float approachSeconds = Mathf.Min(verticalStrikeApproachSeconds, Mathf.Max(0f, verticalTrackSeconds));
            Vector2 approachStart = transform.position;
            while (elapsed < verticalTrackSeconds)
            {
                if (IsSubPatternCancelled(spec))
                    yield break;

                if (owner.CurrentTarget != null)
                    groundTarget = owner.CurrentTarget.position;

                hoverTarget = groundTarget + Vector2.up * verticalHoverHeight;
                Vector2 swordPosition = hoverTarget;
                if (approachSeconds > 0.001f && elapsed < approachSeconds)
                {
                    float approachT = Mathf.Clamp01(elapsed / approachSeconds);
                    float easedApproachT = 1f - Mathf.Pow(1f - approachT, 3f);
                    swordPosition = Vector2.Lerp(approachStart, hoverTarget, easedApproachT);
                }

                transform.position = new Vector3(swordPosition.x, swordPosition.y, transform.position.z);
                transform.rotation = Quaternion.identity;
                warning?.UpdateGeometry(AttackTelegraphSpecUtility.WithThinWarningOutline(
                    DemonKingCombatUtil.CreateTopDownCircleWarningSpec(
                        owner,
                        groundTarget,
                        verticalStrikeDiameter,
                        warningDuration)));

                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return RunVerticalStrikeCommitMotion(groundTarget, spec);

            if (IsSubPatternCancelled(spec))
                yield break;

            StopVerticalAuraAnimation();
            CommitVerticalStrikeImpact(groundTarget);
        }
        finally
        {
            StopAfterimage();
            StopVerticalAuraAnimation();
        }
    }

    private IEnumerator RunVerticalStrikeCommitMotion(Vector2 groundTarget, AbilitySpec spec)
    {
        Vector2 start = transform.position;
        Vector2 apex = start + Vector2.up * verticalStrikeLiftHeight;
        if (verticalStrikeLiftHeight > 0f && verticalStrikeLiftSeconds > 0f)
            yield return MoveVerticalStrikeSword(start, apex, verticalStrikeLiftSeconds, easeIn: false, spec);
        else
            transform.position = new Vector3(apex.x, apex.y, transform.position.z);

        if (IsSubPatternCancelled(spec))
            yield break;

        Vector2 dropStart = transform.position;
        yield return MoveVerticalStrikeSword(dropStart, groundTarget, verticalStrikeDropSeconds, easeIn: true, spec);
        if (IsSubPatternCancelled(spec))
            yield break;

        transform.position = new Vector3(groundTarget.x, groundTarget.y, transform.position.z);
        transform.rotation = Quaternion.identity;
    }

    private IEnumerator MoveVerticalStrikeSword(Vector2 start, Vector2 end, float duration, bool easeIn, AbilitySpec spec)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            if (IsSubPatternCancelled(spec))
                yield break;

            float t = Mathf.Clamp01(elapsed / safeDuration);
            float easedT = easeIn
                ? t * t * t
                : 1f - Mathf.Pow(1f - t, 3f);

            Vector2 position = Vector2.Lerp(start, end, easedT);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            transform.rotation = Quaternion.identity;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private float ResolveVerticalStrikeCommitMotionSeconds()
    {
        float liftSeconds = verticalStrikeLiftHeight > 0f ? Mathf.Max(0f, verticalStrikeLiftSeconds) : 0f;
        return liftSeconds + Mathf.Max(0.01f, verticalStrikeDropSeconds);
    }

    private void CommitVerticalStrikeImpact(Vector2 groundTarget)
    {
        transform.position = groundTarget;
        transform.rotation = Quaternion.identity;
        ApplyBuriedMask();
        activeVerticalAttackVfx = DemonKingPatternVfx.SpawnCueAttachedOneShot(
            verticalAttackVfx,
            transform,
            Vector3.zero,
            verticalStrikeDiameter,
            Vector2.down,
            "EgoSword_VerticalAttackVfx");
        DemonKingAnimationClipVisual impactVfx = DemonKingPatternVfx.SpawnCueOneShot(
            verticalImpactVfx,
            groundTarget,
            verticalStrikeDiameter,
            Vector2.down,
            "EgoSword_VerticalImpactVfx");
        bool presentationPlayed = false;
        void PlayImpactPresentationOnce()
        {
            if (presentationPlayed)
                return;

            presentationPlayed = true;
            PlayActorSound(verticalStrikeImpactSound, groundTarget);
            verticalStrikeImpactCameraShake.TryPlay(gameObject, Vector2.down, debugReason: "DemonKing.EgoSwordVerticalStrikeImpact");
        }

        CombatHitPayload payload = DemonKingCombatUtil.MakePayload(owner, owner.DefaultDamageEffect, patternDamage);
        bool timed = DemonKingPatternVfx.TryPlayCircleTimedHit(
            activeVerticalAttackVfx,
            owner,
            verticalStrikeDiameter,
            payload,
            null,
            PlayImpactPresentationOnce);
        if (!timed)
        {
            timed = DemonKingPatternVfx.TryPlayCircleTimedHit(
                impactVfx,
                owner,
                verticalStrikeDiameter,
                payload,
                null,
                PlayImpactPresentationOnce);
        }

        if (!timed)
        {
            PlayImpactPresentationOnce();
            DemonKingPrimitiveVisual.SpawnCircle(
                groundTarget,
                verticalStrikeDiameter,
                0.12f,
                AttackSquareColor,
                "DemonKing_EgoVerticalCircleAttack");

            DemonKingCombatUtil.ApplyTopDownEllipseDamage(
                owner,
                groundTarget,
                verticalStrikeDiameter,
                owner.DefaultDamageEffect,
                patternDamage);
        }
    }

    private void PlayActorSound(SoundRef sound, Vector2 position)
    {
        if (owner == null || !sound.IsSet)
            return;

        SoundPlaybackUtility.Play(
            sound,
            instigator: owner.gameObject,
            causer: gameObject,
            target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
            position: position,
            sourceObject: this);
    }

    private void CacheBaseSwordRenderers()
    {
        if (baseSwordRenderers == null || baseSwordRenderers.Length == 0)
            baseSwordRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void CacheSwordAnimationDefaults()
    {
        if (primarySwordRenderer == null)
            primarySwordRenderer = ResolvePrimarySwordRenderer();

        if (swordAnimator == null)
            TryGetComponent(out swordAnimator);

        if (swordAnimationDefaultsCaptured)
            return;

        defaultSwordSprite = primarySwordRenderer != null ? primarySwordRenderer.sprite : null;
        if (swordAnimator != null)
            defaultSwordAnimatorController = swordAnimator.runtimeAnimatorController;
        swordAnimationDefaultsCaptured = true;
    }

    private void ApplyBuriedMask(bool spawnFragment = true)
    {
        CacheBaseSwordRenderers();
        SpriteMask mask = ResolveBuriedMask(createIfMissing: true);
        if (mask == null)
            return;

        ConfigureBuriedMask(mask);
        mask.enabled = true;
        if (spawnFragment)
            SpawnBuriedFragment();
        else
            ReleaseBuriedFragment(fade: false);
        for (int i = 0; i < baseSwordRenderers.Length; i++)
        {
            if (baseSwordRenderers[i] != null)
                baseSwordRenderers[i].maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        }
    }

    private void ClearBuriedMask()
    {
        CacheBaseSwordRenderers();
        for (int i = 0; i < baseSwordRenderers.Length; i++)
        {
            if (baseSwordRenderers[i] != null)
                baseSwordRenderers[i].maskInteraction = SpriteMaskInteraction.None;
        }

        SpriteMask mask = ResolveBuriedMask(createIfMissing: false);
        if (mask != null)
            mask.enabled = false;
        ReleaseBuriedFragment(fade: true);
    }

    private void SpawnBuriedFragment()
    {
        if (activeBuriedFragmentVfx != null)
            return;

        activeBuriedFragmentVfx = DemonKingPatternVfx.SpawnPersistentFragment(transform.position, "EgoSword_BuriedFragmentVfx");
    }

    private void ReleaseBuriedFragment(bool fade)
    {
        if (activeBuriedFragmentVfx == null)
            return;

        if (fade)
            activeBuriedFragmentVfx.FadeAndRelease(0.5f);
        else
            activeBuriedFragmentVfx.StopAndRelease();
        activeBuriedFragmentVfx = null;
    }

    private void StartSwordSpinEffect()
    {
        StopSwordSpinEffect();
        activeSwordSpinVfx = DemonKingPatternVfx.SpawnCueFollowingLoop(
            swordSpinVfx,
            transform,
            Vector3.zero,
            contactRadius * 2f,
            velocityDirection,
            "EgoSword_SpinVfx");
    }

    private void StopSwordSpinEffect()
    {
        if (activeSwordSpinVfx == null)
            return;

        activeSwordSpinVfx.StopAndRelease();
        activeSwordSpinVfx = null;
    }

    private SpriteMask ResolveBuriedMask(bool createIfMissing)
    {
        if (buriedMask != null)
            return buriedMask;

        if (runtimeBuriedMask != null)
            return runtimeBuriedMask;

        buriedMask = GetComponentInChildren<SpriteMask>(includeInactive: true);
        if (buriedMask != null || !createIfMissing)
            return buriedMask;

        SpriteRenderer primaryRenderer = ResolvePrimarySwordRenderer();
        if (primaryRenderer == null)
            return null;

        GameObject maskObject = new("EgoSword_BuriedMask");
        maskObject.transform.SetParent(primaryRenderer.transform, false);
        runtimeBuriedMask = maskObject.AddComponent<SpriteMask>();
        runtimeBuriedMask.sprite = DemonKingPrimitiveVisual.GetSquareSprite();
        buriedMask = runtimeBuriedMask;
        return runtimeBuriedMask;
    }

    private void ConfigureBuriedMask(SpriteMask mask)
    {
        SpriteRenderer primaryRenderer = ResolvePrimarySwordRenderer();
        if (primaryRenderer == null || primaryRenderer.sprite == null)
            return;

        Bounds bounds = primaryRenderer.sprite.bounds;
        float maskHeight = Mathf.Max(0.01f, bounds.size.y * Mathf.Clamp01(buriedMaskHeightRatio));
        float maskWidth = Mathf.Max(0.01f, bounds.size.x * buriedMaskWidthMultiplier);
        mask.transform.SetParent(primaryRenderer.transform, false);
        mask.transform.localPosition = new Vector3(0f, bounds.min.y + maskHeight * 0.5f, 0f);
        mask.transform.localRotation = Quaternion.identity;
        mask.transform.localScale = new Vector3(maskWidth, maskHeight, 1f);
        mask.sprite = mask.sprite != null ? mask.sprite : DemonKingPrimitiveVisual.GetSquareSprite();
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = primaryRenderer.sortingLayerID;
        mask.backSortingLayerID = primaryRenderer.sortingLayerID;
        mask.frontSortingOrder = primaryRenderer.sortingOrder + 1;
        mask.backSortingOrder = primaryRenderer.sortingOrder - 1;
    }

    private SpriteRenderer ResolvePrimarySwordRenderer()
    {
        if (primarySwordRenderer != null)
            return primarySwordRenderer;

        CacheBaseSwordRenderers();
        for (int i = 0; i < baseSwordRenderers.Length; i++)
        {
            if (baseSwordRenderers[i] != null && baseSwordRenderers[i].sprite != null)
            {
                primarySwordRenderer = baseSwordRenderers[i];
                return primarySwordRenderer;
            }
        }

        return null;
    }

    private void BeginAfterimage()
    {
        if (!enableAfterimage || !isActiveAndEnabled)
            return;

        SpriteAfterimageEmitter2D emitter = ResolveAfterimageEmitter();
        if (emitter == null)
            return;

        SpriteRenderer primaryRenderer = ResolvePrimarySwordRenderer();
        emitter.Begin(
            primaryRenderer != null ? primaryRenderer.transform : transform,
            afterimageIntervalSeconds,
            afterimageLifetimeSeconds,
            afterimageColor);
    }

    private void StopAfterimage(bool clearGhosts = false)
    {
        if (afterimageEmitter == null)
            return;

        afterimageEmitter.StopEmission();
        if (clearGhosts)
            afterimageEmitter.ClearSpawnedGhosts();
    }

    private SpriteAfterimageEmitter2D ResolveAfterimageEmitter()
    {
        if (afterimageEmitter != null)
            return afterimageEmitter;

        if (!TryGetComponent(out afterimageEmitter))
            afterimageEmitter = gameObject.AddComponent<SpriteAfterimageEmitter2D>();

        return afterimageEmitter;
    }

    private float ResolveLaserTempoSeconds(float seconds)
    {
        return Mathf.Max(0f, seconds) * Mathf.Clamp(laserTempoMultiplier, 0.1f, 1f);
    }

    private void PlayVerticalAuraAnimation()
    {
        PlayAuraStartAnimation(playIdleAfterStart: true);
    }

    private void PlayAuraStartAnimation(bool playIdleAfterStart)
    {
        StopVerticalAuraAnimation();
        if (!PlayAuraStartState(out float startLength))
            return;

        if (playIdleAfterStart)
            verticalAuraAnimationRoutine = StartCoroutine(CoPlayAuraIdleAfterStart(startLength));
    }

    private void PlayRecallAuraStartup()
    {
        StopVerticalAuraAnimation();
        if (!PlayAuraStartState(out float startLength))
        {
            MarkRecallAuraReady();
            return;
        }

        verticalAuraAnimationRoutine = StartCoroutine(CoStartRecallMovementAfterAuraStart(startLength));
    }

    private bool PlayAuraStartState(out float startLength)
    {
        startLength = 0f;
        if (!PrepareAuraPlayback())
            return false;

        if (!TryPlaySwordAnimatorState(EgoSwordAuraStartStateName, out startLength))
        {
            StopVerticalAuraAnimation();
            return false;
        }

        return true;
    }

    private bool PrepareAuraPlayback()
    {
        CacheSwordAnimationDefaults();

        RuntimeAnimatorController controller = ResolveAuraAnimatorController();
        if (controller == null)
            return false;

        SpriteRenderer renderer = ResolvePrimarySwordRenderer();
        Animator animator = ResolveSwordAnimator(createIfMissing: true);
        if (renderer == null || animator == null)
        {
            WarnAuraAnimationInvalid("missing EgoSword SpriteRenderer or Animator");
            return false;
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;
        verticalAuraAnimationActive = true;
        return true;
    }

    private IEnumerator CoPlayAuraIdleAfterStart(float startLength)
    {
        if (startLength > 0f)
            yield return new WaitForSeconds(startLength);

        verticalAuraAnimationRoutine = null;
        if (!verticalAuraAnimationActive)
            yield break;

        PlayAuraIdleAnimation();
    }

    private IEnumerator CoStartRecallMovementAfterAuraStart(float startLength)
    {
        if (startLength > 0f)
            yield return new WaitForSeconds(startLength);

        verticalAuraAnimationRoutine = null;
        if (!verticalAuraAnimationActive || state != SwordState.Recalling)
            yield break;

        if (!TryPlaySwordAnimatorState(EgoSwordAuraIdleStateName, out _))
        {
            StopVerticalAuraAnimation();
            MarkRecallAuraReady();
            yield break;
        }

        MarkRecallAuraReady();
    }

    private void PlayAuraIdleAnimation()
    {
        if (!verticalAuraAnimationActive && !PrepareAuraPlayback())
            return;

        if (!TryPlaySwordAnimatorState(EgoSwordAuraIdleStateName, out _))
            StopVerticalAuraAnimation();
    }

    private void PlayAuraEndAnimation()
    {
        if (!verticalAuraAnimationActive && !PrepareAuraPlayback())
            return;

        StopAuraTransitionRoutine();

        if (!TryPlaySwordAnimatorState(EgoSwordAuraEndStateName, out float endLength))
        {
            StopVerticalAuraAnimation();
            return;
        }

        verticalAuraAnimationRoutine = StartCoroutine(CoStopAuraAfterEnd(endLength));
    }

    private void StopAuraTransitionRoutine()
    {
        if (verticalAuraAnimationRoutine == null)
            return;

        StopCoroutine(verticalAuraAnimationRoutine);
        verticalAuraAnimationRoutine = null;
    }

    private IEnumerator CoStopAuraAfterEnd(float endLength)
    {
        if (endLength > 0f)
            yield return new WaitForSeconds(endLength);

        verticalAuraAnimationRoutine = null;
        StopVerticalAuraAnimation();
    }

    private void StopVerticalAuraAnimation()
    {
        if (verticalAuraAnimationRoutine != null)
        {
            StopCoroutine(verticalAuraAnimationRoutine);
            verticalAuraAnimationRoutine = null;
        }

        if (!verticalAuraAnimationActive)
            return;

        verticalAuraAnimationActive = false;
        recallMovementActive = false;
        if (swordAnimator != null)
        {
            swordAnimator.runtimeAnimatorController = defaultSwordAnimatorController;
            if (defaultSwordAnimatorController == null)
                swordAnimator.enabled = false;
        }

        if (primarySwordRenderer != null && defaultSwordSprite != null)
            primarySwordRenderer.sprite = defaultSwordSprite;
    }

    private void ReleaseVerticalStrikeVfx()
    {
        StopVerticalAuraAnimation();

        if (activeVerticalAttackVfx == null)
            return;

        activeVerticalAttackVfx.StopAndRelease();
        activeVerticalAttackVfx = null;
    }

    private void ReleaseAttachedOneShotVfx()
    {
        if (!Application.isPlaying)
            return;

        DemonKingAnimationClipVisual[] visuals = GetComponentsInChildren<DemonKingAnimationClipVisual>(true);
        for (int i = 0; i < visuals.Length; i++)
        {
            DemonKingAnimationClipVisual visual = visuals[i];
            if (visual != null)
                visual.StopAndRelease();
        }
    }

    private RuntimeAnimatorController ResolveAuraAnimatorController()
    {
        if (auraAnimatorController != null)
            return auraAnimatorController;

        auraAnimatorController = Resources.Load<RuntimeAnimatorController>(EgoSwordAuraControllerResourcePath);
        if (auraAnimatorController == null && !auraControllerMissingLogged)
        {
            auraControllerMissingLogged = true;
            Debug.LogWarning($"EgoSword aura AnimatorController not found at Resources/{EgoSwordAuraControllerResourcePath}.", this);
        }

        return auraAnimatorController;
    }

    private Animator ResolveSwordAnimator(bool createIfMissing)
    {
        if (swordAnimator != null)
            return swordAnimator;

        if (!TryGetComponent(out swordAnimator) && createIfMissing)
            swordAnimator = gameObject.AddComponent<Animator>();

        return swordAnimator;
    }

    private bool TryPlaySwordAnimatorState(string stateName, out float clipLength)
    {
        clipLength = 0f;
        if (swordAnimator == null)
            return false;

        if (!TryResolveSwordAnimatorStateHash(stateName, out int stateHash))
        {
            WarnAuraAnimationInvalid($"AnimatorController has no state '{stateName}'");
            return false;
        }

        swordAnimator.Play(stateHash, 0, 0f);
        swordAnimator.Update(0f);
        clipLength = ResolveSwordAnimatorClipLength(stateName);
        return true;
    }

    private bool TryResolveSwordAnimatorStateHash(string stateName, out int stateHash)
    {
        stateHash = Animator.StringToHash(stateName);
        if (swordAnimator.HasState(0, stateHash))
            return true;

        if (swordAnimator.layerCount <= 0)
            return false;

        string layerName = swordAnimator.GetLayerName(0);
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        stateHash = Animator.StringToHash($"{layerName}.{stateName}");
        return swordAnimator.HasState(0, stateHash);
    }

    private float ResolveSwordAnimatorClipLength(string nameFragment)
    {
        RuntimeAnimatorController controller = swordAnimator != null ? swordAnimator.runtimeAnimatorController : null;
        AnimationClip[] clips = controller != null ? controller.animationClips : System.Array.Empty<AnimationClip>();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return Mathf.Max(0.01f, clip.length);
        }

        return 0f;
    }

    private void WarnAuraAnimationInvalid(string reason)
    {
        if (auraControllerInvalidLogged)
            return;

        auraControllerInvalidLogged = true;
        Debug.LogWarning($"EgoSword aura animation is invalid: {reason}.", this);
    }

    private RaycastHit2D FindNearestWallHit(Vector2 start, Vector2 direction, float distance)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(wallMask);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int hitCount = Physics2D.CircleCast(start, contactRadius, direction, filter, WallHitBuffer, distance);
        RaycastHit2D nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = WallHitBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        return nearestHit;
    }

    private LaserLine ResolveWallClippedLaserLine(Vector2 origin, Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float forwardDistance = ResolveWallDistance(origin, safeDirection);
        float backwardDistance = ResolveWallDistance(origin, -safeDirection);
        return CreateLaserLine(origin, safeDirection, forwardDistance, backwardDistance);
    }

    private LaserLine ResolvePiercingLaserLine(Vector2 origin, Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float halfLength = Mathf.Max(0.05f, fallbackMapLaserLength * 0.5f);
        return CreateLaserLine(origin, safeDirection, halfLength, halfLength);
    }

    private LaserLine CreateLaserLine(
        Vector2 origin,
        Vector2 safeDirection,
        float forwardDistance,
        float backwardDistance)
    {
        float length = Mathf.Max(0.1f, forwardDistance + backwardDistance);
        Vector2 center = origin + safeDirection * ((forwardDistance - backwardDistance) * 0.5f);

        return new LaserLine(
            origin,
            safeDirection,
            center,
            new Vector2(length, laserWidth),
            DemonKingCombatUtil.RotationDeg(safeDirection),
            forwardDistance,
            backwardDistance);
    }

    private float ResolveWallDistance(Vector2 origin, Vector2 direction)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(owner.WallMask);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int hitCount = Physics2D.Raycast(origin, direction, filter, WallHitBuffer, fallbackMapLaserLength);
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = WallHitBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hit.distance);
        }

        return float.IsInfinity(nearestDistance) ? fallbackMapLaserLength * 0.5f : nearestDistance;
    }

    private AttackTelegraphSpec CreateLaserSpec(LaserLine line, float duration)
    {
        return AttackTelegraphSpecUtility.WithThinWarningOutlineOnly(
            AttackTelegraphSpec.CreateRectangle(
                line.Center,
                line.Size,
                line.RotationDeg,
                duration,
                owner.DefaultWarningStyle));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DemonKingController resolvedOwner = owner != null ? owner : GetComponentInParent<DemonKingController>();
        Vector3 ownerPosition = resolvedOwner != null ? resolvedOwner.transform.position : transform.position;

        DrawOffsetPoint("Held", ResolveEditorOwnerOffsetPosition(resolvedOwner, heldOffset), ownerPosition, HeldGizmoColor);
        DrawOffsetPoint("Throw Origin", ResolveEditorSocketPosition(resolvedOwner, DemonKingVfxSocketId.SwordThrowOrigin, throwOriginLocalOffset), ownerPosition, ThrowGizmoColor);
        DrawOffsetPoint("Recall Target", ResolveEditorSocketPosition(resolvedOwner, DemonKingVfxSocketId.SwordThrowReturnOrigin, recallTargetLocalOffset), ownerPosition, RecallGizmoColor);
        DrawLaserOffsetGizmo();
        DrawImpactGizmo();
    }

    private Vector3 ResolveEditorOwnerOffsetPosition(DemonKingController resolvedOwner, Vector3 localOffset)
    {
        if (resolvedOwner != null)
            return resolvedOwner.ResolveSwordHoldPosition(localOffset);

        return transform.position + localOffset;
    }

    private Vector3 ResolveEditorSocketPosition(
        DemonKingController resolvedOwner,
        DemonKingVfxSocketId socketId,
        Vector3 fallbackLocalOffset)
    {
        if (resolvedOwner != null)
            return resolvedOwner.ResolveVfxSocketWorld(socketId, fallbackLocalOffset);

        return transform.position + fallbackLocalOffset;
    }

    private void DrawOffsetPoint(string label, Vector3 position, Vector3 origin, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawLine(origin, position);
        Gizmos.DrawWireSphere(position, SocketGizmoRadius);
        Handles.color = color;
        Handles.Label(position + Vector3.up * 0.14f, label);
    }

    private void DrawLaserOffsetGizmo()
    {
        float offset = Mathf.Max(0f, laserVfxRayOriginOffset);
        if (offset <= 0f)
            return;

        Vector3 center = transform.position;
        Vector3[] directions =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down
        };

        Gizmos.color = LaserGizmoColor;
        Handles.color = LaserGizmoColor;
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 point = center + directions[i] * offset;
            Gizmos.DrawLine(center, point);
            Gizmos.DrawWireSphere(point, SocketGizmoRadius * 0.75f);
        }

        Handles.Label(center + Vector3.up * (offset + 0.18f), "Laser VFX Origin Offset");
    }

    private void DrawImpactGizmo()
    {
        Vector3 center = transform.position;
        Gizmos.color = ImpactGizmoColor;
        Gizmos.DrawWireSphere(center, verticalStrikeDiameter * 0.5f);
        Handles.color = ImpactGizmoColor;
        Handles.Label(center + Vector3.up * (verticalStrikeDiameter * 0.5f + 0.12f), "Vertical/Plant Impact");
    }
#endif

    private readonly struct LaserLine
    {
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public Vector2 Center { get; }
        public Vector2 Size { get; }
        public float RotationDeg { get; }
        public float ForwardDistance { get; }
        public float BackwardDistance { get; }

        public LaserLine(
            Vector2 origin,
            Vector2 direction,
            Vector2 center,
            Vector2 size,
            float rotationDeg,
            float forwardDistance,
            float backwardDistance)
        {
            Origin = origin;
            Direction = direction;
            Center = center;
            Size = size;
            RotationDeg = rotationDeg;
            ForwardDistance = forwardDistance;
            BackwardDistance = backwardDistance;
        }
    }
}
