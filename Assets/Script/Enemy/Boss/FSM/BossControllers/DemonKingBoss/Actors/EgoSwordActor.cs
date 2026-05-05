using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class EgoSwordActor : MonoBehaviour
{
    private static readonly RaycastHit2D[] WallHitBuffer = new RaycastHit2D[8];
    private static readonly Color WarningSquareColor = new(1f, 0.15f, 0.08f, 0.35f);
    private static readonly Color AttackSquareColor = new(1f, 0.85f, 0.2f, 0.62f);

    private enum SwordState
    {
        Held,
        Flying,
        Fixed,
        Recalling
    }

    [Header("Held")]
    [SerializeField] private Vector3 heldOffset = new(0.85f, 0.1f, 0f);

    [Header("Throw")]
    [SerializeField, Min(0.01f)] private float contactRadius = 0.45f;
    [SerializeField, Min(0f)] private float contactDamage = 1f;
    [SerializeField, Min(0f)] private float contactKnockback = 6f;
    [SerializeField, Min(0f)] private float flyingRotationDegreesPerSecond = 720f;

    [Header("Dropped Patterns")]
    [SerializeField, Min(0.1f)] private float patternIntervalSeconds = 1.5f;
    [SerializeField, Min(0f)] private float laserWarningSeconds = 1f;
    [SerializeField, Min(0f)] private float laserAttackDurationSeconds = 1f;
    [SerializeField, Min(0.1f)] private float fallbackMapLaserLength = 40f;
    [SerializeField, Min(0.1f)] private float laserWidth = 0.75f;
    [SerializeField, Min(0.1f)] private float verticalTrackSeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float verticalHoverHeight = 2.2f;
    [SerializeField, Min(0.1f)] private float verticalStrikeDiameter = 2.3f;
    [SerializeField, Min(0f)] private float patternDamage = 1f;
    [SerializeField, Min(0.1f)] private float recallImpactDiameter = 3.2f;
    [SerializeField, Min(0f)] private float recallImpactDamage = 1.5f;
    [SerializeField, Min(0f)] private float recallImpactKnockback = 8f;

    private DemonKingController owner;
    private SwordState state = SwordState.Held;
    private Vector2 velocityDirection = Vector2.right;
    private float flyingSpeed;
    private int remainingBounces;
    private LayerMask wallMask;
    private Coroutine droppedPatternRoutine;
    private bool useCrossPatternNext = true;

    public bool IsHeld => state == SwordState.Held;
    public bool IsDropped => state == SwordState.Flying || state == SwordState.Fixed || state == SwordState.Recalling;
    public bool IsRecallActive => state == SwordState.Recalling;

    private void Awake()
    {
        ApplyProjectileSortingOnce();

        if (TryGetComponent(out Collider2D collider2d))
            collider2d.isTrigger = true;
    }

    private void OnEnable()
    {
        ApplyProjectileSortingOnce();
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

    public void AttachToOwner()
    {
        StopDroppedPatterns();
        state = SwordState.Held;
        flyingSpeed = 0f;
        remainingBounces = 0;
        transform.SetParent(owner != null ? owner.transform : null, true);
        if (owner != null)
            transform.position = owner.ResolveSwordHoldPosition(heldOffset);
    }

    public void Throw(Vector2 origin, Vector2 direction, float speed, int bounceCount, LayerMask newWallMask)
    {
        StopDroppedPatterns();
        transform.SetParent(null, true);
        transform.position = origin;
        velocityDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        flyingSpeed = Mathf.Max(0.01f, speed);
        remainingBounces = Mathf.Max(0, bounceCount);
        wallMask = newWallMask;
        state = SwordState.Flying;

        if (remainingBounces <= 0)
            FixAtCurrentPosition();
    }

    public void FixAtCurrentPosition()
    {
        if (state == SwordState.Fixed)
            return;

        state = SwordState.Fixed;
        flyingSpeed = 0f;
        StartDroppedPatterns();
    }

    public void Recall(float speed)
    {
        StopDroppedPatterns();
        transform.SetParent(null, true);
        flyingSpeed = Mathf.Max(0.01f, speed);
        state = SwordState.Recalling;
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
                    FixAtCurrentPosition();
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
        RotateClockwise(Time.fixedDeltaTime);

        Vector2 targetPosition = owner.ResolveSwordHoldPosition(heldOffset);
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
        if (owner == null)
            return;

        Vector2 impactCenter = owner.ResolveSwordHoldPosition(heldOffset);
        DemonKingCombatUtil.ApplyCircleDamage(
            owner,
            impactCenter,
            recallImpactDiameter * 0.5f,
            owner.DefaultDamageEffect,
            recallImpactDamage,
            knockbackImpulse: recallImpactKnockback);

        owner.CompleteEgoSwordRecall();
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

    private void ApplyProjectileSortingOnce()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            DemonKingPrimitiveVisual.ApplyProjectileSorting(renderers[i], 1050);
    }

    private void StopDroppedPatterns()
    {
        if (droppedPatternRoutine == null)
            return;

        StopCoroutine(droppedPatternRoutine);
        droppedPatternRoutine = null;
    }

    private IEnumerator RunDroppedPatternLoop()
    {
        yield return new WaitForSeconds(patternIntervalSeconds);

        while (owner != null && state == SwordState.Fixed)
        {
            if (useCrossPatternNext)
                yield return RunCrossPattern();
            else
                yield return RunVerticalStrikePattern();

            useCrossPatternNext = !useCrossPatternNext;
            owner.NotifyEgoSwordPatternCompleted();
            yield return new WaitForSeconds(patternIntervalSeconds);
        }
    }

    private IEnumerator RunCrossPattern()
    {
        yield return RunLaserPair(Vector2.right, Vector2.up);
        yield return RunLaserPair(new Vector2(1f, 1f).normalized, new Vector2(1f, -1f).normalized);
    }

    private IEnumerator RunLaserPair(Vector2 firstDirection, Vector2 secondDirection)
    {
        AttackTelegraphService telegraph = owner.GetTelegraphService();
        LaserLine firstLine = ResolveMapLaserLine(transform.position, firstDirection);
        LaserLine secondLine = ResolveMapLaserLine(transform.position, secondDirection);

        DemonKingPrimitiveVisual.SpawnSquare(
            firstLine.Center,
            firstLine.Size,
            firstLine.RotationDeg,
            laserWarningSeconds,
            WarningSquareColor,
            "DemonKing_EgoLaserSquareWarning");
        DemonKingPrimitiveVisual.SpawnSquare(
            secondLine.Center,
            secondLine.Size,
            secondLine.RotationDeg,
            laserWarningSeconds,
            WarningSquareColor,
            "DemonKing_EgoLaserSquareWarning");
        telegraph?.SpawnDetachedView(CreateLaserSpec(firstLine, laserWarningSeconds));
        telegraph?.SpawnDetachedView(CreateLaserSpec(secondLine, laserWarningSeconds));

        yield return new WaitForSeconds(laserWarningSeconds);

        DemonKingPrimitiveVisual.SpawnSquare(
            firstLine.Center,
            firstLine.Size,
            firstLine.RotationDeg,
            laserAttackDurationSeconds,
            AttackSquareColor,
            "DemonKing_EgoLaserSquareAttack");
        DemonKingPrimitiveVisual.SpawnSquare(
            secondLine.Center,
            secondLine.Size,
            secondLine.RotationDeg,
            laserAttackDurationSeconds,
            AttackSquareColor,
            "DemonKing_EgoLaserSquareAttack");
        telegraph?.SpawnDetachedView(CreateLaserSpec(firstLine, laserAttackDurationSeconds));
        telegraph?.SpawnDetachedView(CreateLaserSpec(secondLine, laserAttackDurationSeconds));

        HashSet<GameObject> damagedTargets = new();
        float elapsed = 0f;
        while (elapsed < laserAttackDurationSeconds)
        {
            DemonKingCombatUtil.ApplyRectangleDamage(
                owner,
                firstLine.Center,
                firstLine.Size,
                firstLine.RotationDeg,
                owner.DefaultDamageEffect,
                patternDamage,
                damagedTargets);
            DemonKingCombatUtil.ApplyRectangleDamage(
                owner,
                secondLine.Center,
                secondLine.Size,
                secondLine.RotationDeg,
                owner.DefaultDamageEffect,
                patternDamage,
                damagedTargets);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator RunVerticalStrikePattern()
    {
        AttackTelegraphService telegraph = owner.GetTelegraphService();
        Vector2 groundTarget = owner.CurrentTarget != null
            ? (Vector2)owner.CurrentTarget.position
            : (Vector2)transform.position;
        Vector2 hoverTarget = groundTarget + Vector2.up * verticalHoverHeight;

        AttackTelegraphView warning = telegraph?.SpawnDetachedView(
            AttackTelegraphSpec.CreateCircle(hoverTarget, verticalStrikeDiameter, verticalTrackSeconds, owner.DefaultWarningStyle));
        DemonKingPrimitiveVisual warningSquare = DemonKingPrimitiveVisual.SpawnSquare(
            hoverTarget,
            new Vector2(verticalStrikeDiameter, verticalStrikeDiameter),
            0f,
            verticalTrackSeconds,
            WarningSquareColor,
            "DemonKing_EgoVerticalSquareWarning");

        float elapsed = 0f;
        while (elapsed < verticalTrackSeconds)
        {
            if (owner.CurrentTarget != null)
                groundTarget = owner.CurrentTarget.position;

            hoverTarget = groundTarget + Vector2.up * verticalHoverHeight;
            transform.position = new Vector3(hoverTarget.x, hoverTarget.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            warning?.UpdateGeometry(AttackTelegraphSpec.CreateCircle(
                hoverTarget,
                verticalStrikeDiameter,
                verticalTrackSeconds,
                owner.DefaultWarningStyle));
            warningSquare?.UpdateGeometry(
                hoverTarget,
                new Vector2(verticalStrikeDiameter, verticalStrikeDiameter),
                0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = groundTarget;
        DemonKingPrimitiveVisual.SpawnSquare(
            groundTarget,
            new Vector2(verticalStrikeDiameter, verticalStrikeDiameter),
            0f,
            0.12f,
            AttackSquareColor,
            "DemonKing_EgoVerticalSquareAttack");

        DemonKingCombatUtil.ApplyCircleDamage(
            owner,
            groundTarget,
            verticalStrikeDiameter * 0.5f,
            owner.DefaultDamageEffect,
            patternDamage);
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

    private LaserLine ResolveMapLaserLine(Vector2 origin, Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float forwardDistance = ResolveWallDistance(origin, safeDirection);
        float backwardDistance = ResolveWallDistance(origin, -safeDirection);
        float length = Mathf.Max(0.1f, forwardDistance + backwardDistance);
        Vector2 center = origin + safeDirection * ((forwardDistance - backwardDistance) * 0.5f);

        return new LaserLine(
            center,
            new Vector2(length, laserWidth),
            DemonKingCombatUtil.RotationDeg(safeDirection));
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
        return AttackTelegraphSpec.CreateRectangle(
            line.Center,
            line.Size,
            line.RotationDeg,
            duration,
            owner.DefaultWarningStyle);
    }

    private readonly struct LaserLine
    {
        public Vector2 Center { get; }
        public Vector2 Size { get; }
        public float RotationDeg { get; }

        public LaserLine(Vector2 center, Vector2 size, float rotationDeg)
        {
            Center = center;
            Size = size;
            RotationDeg = rotationDeg;
        }
    }
}
