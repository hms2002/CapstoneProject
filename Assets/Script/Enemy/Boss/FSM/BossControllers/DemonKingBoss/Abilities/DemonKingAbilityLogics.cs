using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public abstract class AbilityLogic_DemonKingBase : AbilityLogic
{
    private static readonly RaycastHit2D[] WallHitBuffer = new RaycastHit2D[12];
    protected static readonly Color WarningSquareColor = new(1f, 0.15f, 0.08f, 0.35f);
    protected static readonly Color AttackSquareColor = new(1f, 0.75f, 0.15f, 0.55f);

    protected readonly struct LineArea
    {
        public Vector2 Center { get; }
        public Vector2 Size { get; }
        public float RotationDeg { get; }
        public float Length => Size.x;

        public LineArea(Vector2 center, Vector2 size, float rotationDeg)
        {
            Center = center;
            Size = size;
            RotationDeg = rotationDeg;
        }
    }

    protected static DemonKingController GetDemonKing(AbilitySystem system)
    {
        return system != null ? system.GetComponent<DemonKingController>() : null;
    }

    protected static void ShowRectangleWarning(
        DemonKingController demon,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float duration)
    {
        DemonKingPrimitiveVisual.SpawnSquare(center, size, rotationDeg, duration, WarningSquareColor);
        demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpec.CreateRectangle(center, size, rotationDeg, duration, demon.DefaultWarningStyle));
    }

    protected static void ShowCircleWarning(
        DemonKingController demon,
        Vector2 center,
        float diameter,
        float duration)
    {
        DemonKingPrimitiveVisual.SpawnCircle(center, diameter, duration, WarningSquareColor);
        demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpec.CreateCircle(center, diameter, duration, demon.DefaultWarningStyle));
    }

    protected static void ShowSectorWarning(
        DemonKingController demon,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float angleDeg,
        float duration)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        DemonKingPrimitiveVisual.SpawnSquare(
            origin + safeDirection * (radius * 0.5f),
            new Vector2(radius, radius * 2f),
            DemonKingCombatUtil.RotationDeg(safeDirection),
            duration,
            WarningSquareColor,
            "DemonKing_SectorSquareWarning");
        demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpec.CreateSector(
                origin,
                radius,
                angleDeg,
                DemonKingCombatUtil.RotationDeg(direction),
                duration,
                demon.DefaultWarningStyle));
    }

    protected static IEnumerator RunLungeContactDamage(
        DemonKingController demon,
        AbilityMotionController2D motion,
        Vector2 start,
        Vector2 direction,
        float distance,
        float duration,
        float hitWidth,
        float damage,
        float knockback,
        AbilitySpec spec)
    {
        if (motion != null)
            motion.StartLunge(start, direction, distance, duration);
        else
            demon.transform.position = start + direction * distance;

        DemonKingPrimitiveVisual.SpawnSquare(
            start + direction * (distance * 0.5f),
            new Vector2(Mathf.Max(0.1f, distance), hitWidth),
            DemonKingCombatUtil.RotationDeg(direction),
            duration,
            AttackSquareColor,
            "DemonKing_LungeSquareAttack");

        HashSet<GameObject> damagedTargets = new();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 center = (Vector2)demon.transform.position + direction * 0.6f;
            DemonKingCombatUtil.ApplyRectangleDamage(
                demon,
                center,
                new Vector2(1.25f, hitWidth),
                DemonKingCombatUtil.RotationDeg(direction),
                demon.DefaultDamageEffect,
                damage,
                damagedTargets,
                knockback);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    protected static AttackTelegraphView ShowLineWarning(
        DemonKingController demon,
        Vector2 start,
        Vector2 end,
        float width,
        float duration)
    {
        Vector2 delta = end - start;
        float length = Mathf.Max(0.1f, delta.magnitude);
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        Vector2 center = start + direction * (length * 0.5f);
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, width),
            DemonKingCombatUtil.RotationDeg(direction),
            duration,
            demon.DefaultWarningStyle);

        return demon.GetTelegraphService()?.SpawnDetachedView(spec);
    }

    protected static DemonKingPrimitiveVisual ShowLinePrimitiveWarning(
        Vector2 start,
        Vector2 end,
        float width,
        float duration)
    {
        Vector2 delta = end - start;
        float length = Mathf.Max(0.1f, delta.magnitude);
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        Vector2 center = start + direction * (length * 0.5f);
        return DemonKingPrimitiveVisual.SpawnSquare(
            center,
            new Vector2(length, width),
            DemonKingCombatUtil.RotationDeg(direction),
            duration,
            WarningSquareColor,
            "DemonKing_LineSquareWarning");
    }

    protected static void UpdateLineWarning(
        AttackTelegraphView view,
        DemonKingController demon,
        Vector2 start,
        Vector2 end,
        float width,
        float duration)
    {
        if (view == null)
            return;

        Vector2 delta = end - start;
        float length = Mathf.Max(0.1f, delta.magnitude);
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        Vector2 center = start + direction * (length * 0.5f);
        view.UpdateGeometry(AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, width),
            DemonKingCombatUtil.RotationDeg(direction),
            duration,
            demon.DefaultWarningStyle));
    }

    protected static void UpdateLinePrimitiveWarning(
        DemonKingPrimitiveVisual view,
        Vector2 start,
        Vector2 end,
        float width)
    {
        if (view == null)
            return;

        Vector2 delta = end - start;
        float length = Mathf.Max(0.1f, delta.magnitude);
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        Vector2 center = start + direction * (length * 0.5f);
        view.UpdateGeometry(center, new Vector2(length, width), DemonKingCombatUtil.RotationDeg(direction));
    }

    protected static LineArea ResolveForwardLineArea(
        DemonKingController demon,
        Vector2 origin,
        Vector2 direction,
        float width,
        float fallbackLength = 40f)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float length = ResolveWallDistance(demon, origin, safeDirection, fallbackLength);
        Vector2 center = origin + safeDirection * (length * 0.5f);
        return new LineArea(center, new Vector2(Mathf.Max(0.1f, length), width), DemonKingCombatUtil.RotationDeg(safeDirection));
    }

    protected static LineArea ResolveFullLineArea(
        DemonKingController demon,
        Vector2 origin,
        Vector2 direction,
        float width,
        float fallbackLength = 40f)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float forward = ResolveWallDistance(demon, origin, safeDirection, fallbackLength * 0.5f);
        float backward = ResolveWallDistance(demon, origin, -safeDirection, fallbackLength * 0.5f);
        float length = Mathf.Max(0.1f, forward + backward);
        Vector2 center = origin + safeDirection * ((forward - backward) * 0.5f);
        return new LineArea(center, new Vector2(length, width), DemonKingCombatUtil.RotationDeg(safeDirection));
    }

    protected static float ResolveWallDistance(
        DemonKingController demon,
        Vector2 origin,
        Vector2 direction,
        float fallbackDistance)
    {
        if (demon == null)
            return fallbackDistance;

        ContactFilter2D filter = new();
        filter.SetLayerMask(demon.WallMask);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        int hitCount = Physics2D.Raycast(origin, safeDirection, filter, WallHitBuffer, fallbackDistance);
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = WallHitBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hit.distance);
        }

        return float.IsInfinity(nearestDistance) ? fallbackDistance : Mathf.Max(0.1f, nearestDistance);
    }

    protected static float SecondsForSpeed(float distance, float speed, float fallbackSeconds)
    {
        if (distance <= 0f || speed <= 0f)
            return fallbackSeconds;

        return Mathf.Max(0.01f, distance / speed);
    }

    protected static AttackTelegraphView ShowLineAreaWarning(DemonKingController demon, LineArea line, float duration)
    {
        DemonKingPrimitiveVisual.SpawnSquare(
            line.Center,
            line.Size,
            line.RotationDeg,
            duration,
            WarningSquareColor,
            "DemonKing_LineAreaSquareWarning");
        return demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpec.CreateRectangle(
                line.Center,
                line.Size,
                line.RotationDeg,
                duration,
                demon.DefaultWarningStyle));
    }

    protected static void UpdateLineAreaWarning(
        AttackTelegraphView view,
        DemonKingController demon,
        LineArea line,
        float duration)
    {
        view?.UpdateGeometry(AttackTelegraphSpec.CreateRectangle(
            line.Center,
            line.Size,
            line.RotationDeg,
            duration,
            demon.DefaultWarningStyle));
    }

    protected static DemonKingPrimitiveVisual ShowLineAreaPrimitiveWarning(LineArea line, float duration)
    {
        return DemonKingPrimitiveVisual.SpawnSquare(
            line.Center,
            line.Size,
            line.RotationDeg,
            duration,
            WarningSquareColor,
            "DemonKing_LineAreaSquareWarning");
    }

    protected static void UpdateLineAreaPrimitiveWarning(DemonKingPrimitiveVisual view, LineArea line)
    {
        view?.UpdateGeometry(line.Center, line.Size, line.RotationDeg);
    }

    protected static void ApplyLineAreaDamage(
        DemonKingController demon,
        LineArea line,
        float damage,
        HashSet<GameObject> damagedTargets = null,
        float knockback = 0f)
    {
        DemonKingCombatUtil.ApplyRectangleDamage(
            demon,
            line.Center,
            line.Size,
            line.RotationDeg,
            demon.DefaultDamageEffect,
            damage,
            damagedTargets,
            knockback);
    }
}

public sealed class AbilityLogic_DemonKingPierceCombo : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(1)] private int pierceCount = 3;
    [SerializeField, Min(0f)] private float firstWarningSeconds = 1f;
    [SerializeField, Min(0f)] private float warningStepDecrease = 0.2f;
    [SerializeField, Min(0.01f)] private float lungeSeconds = 0.16f;
    [SerializeField, Min(0.01f)] private float returnSeconds = 0.12f;
    [SerializeField, Min(0.1f)] private float hitWidth = 1.05f;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0f)] private float knockback = 5f;
    [SerializeField, Min(0f)] private float intervalSeconds = 0.12f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();

        for (int i = 0; i < pierceCount; i++)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 start = demon.transform.position;
            demon.PushFaceTargetLock();

            Vector2 lockedTarget = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : start + demon.FacingDirection;
            float currentWarningSeconds = Mathf.Max(0f, firstWarningSeconds - warningStepDecrease * i);
            AttackTelegraphView warningView = ShowLineWarning(demon, start, lockedTarget, hitWidth, currentWarningSeconds);
            DemonKingPrimitiveVisual warningPrimitive = ShowLinePrimitiveWarning(start, lockedTarget, hitWidth, currentWarningSeconds);

            float elapsed = 0f;
            while (elapsed < currentWarningSeconds)
            {
                if (IsAbilityCancelled(spec))
                {
                    demon.PopFaceTargetLock();
                    yield break;
                }

                lockedTarget = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : lockedTarget;
                Vector2 aimDirection = lockedTarget - start;
                demon.FacePatternDirection(aimDirection);
                UpdateLineWarning(warningView, demon, start, lockedTarget, hitWidth, currentWarningSeconds);
                UpdateLinePrimitiveWarning(warningPrimitive, start, lockedTarget, hitWidth);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (IsAbilityCancelled(spec))
            {
                demon.PopFaceTargetLock();
                yield break;
            }

            Vector2 direction = lockedTarget - start;
            float distance = direction.magnitude;
            if (distance <= 0.05f)
            {
                demon.PopFaceTargetLock();
                continue;
            }

            direction /= distance;
            demon.FacePatternDirection(direction);
            demon.PlayPatternTrigger();
            DemonKingPatternVfx.SpawnStab(start, direction, distance, hitWidth);

            yield return RunLungeContactDamage(
                demon,
                motion,
                start,
                direction,
                distance,
                lungeSeconds,
                hitWidth,
                damage,
                knockback,
                spec);

            if (!IsAbilityCancelled(spec) && i < pierceCount - 1)
                yield return ReturnToStart(demon, motion, start, returnSeconds, spec);

            demon.PopFaceTargetLock();

            if (i < pierceCount - 1)
                yield return WaitForSecondsUnlessCancelled(intervalSeconds, spec);
        }
    }

    private IEnumerator ReturnToStart(
        DemonKingController demon,
        AbilityMotionController2D motion,
        Vector2 start,
        float duration,
        AbilitySpec spec)
    {
        Vector2 current = demon.transform.position;
        Vector2 delta = start - current;
        if (delta.sqrMagnitude <= 0.0001f)
            yield break;

        if (motion != null)
            motion.StartLunge(current, delta.normalized, delta.magnitude, duration, 1.4f);
        else
            demon.transform.position = start;

        yield return WaitForSecondsUnlessCancelled(duration, spec);

        if (!IsAbilityCancelled(spec))
            demon.transform.position = start;
    }
}

public sealed class AbilityLogic_DemonKingHeavySlash : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.1f)] private float moveSpeedMultiplier = 4f;
    [SerializeField, Min(0.01f)] private float fallbackMoveSeconds = 0.35f;
    [SerializeField, Min(0f)] private float warningSeconds = 0.75f;
    [SerializeField, Min(0.1f)] private float slashRadius = 3.9f;
    [SerializeField, Range(1f, 360f)] private float slashAngle = 110f;
    [SerializeField, Min(0f)] private float damage = 2f;
    [SerializeField, Min(0f)] private float knockback = 10f;
    [SerializeField, Min(0.1f)] private float fallbackLineLength = 40f;
    [SerializeField, Min(0.1f)] private float lineWidth = 0.7f;
    [SerializeField, Min(0.1f)] private float explosionSpacing = 1.35f;
    [SerializeField, Min(0.1f)] private float explosionDiameter = 1.35f;
    [SerializeField, Min(0f)] private float explosionWarningSeconds = 0.15f;
    [SerializeField, Min(0f)] private float explosionStepInterval = 0.04f;
    [SerializeField, Min(0f)] private float explosionDamage = 1f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        Vector2 moveStart = demon.transform.position;
        Vector2 moveTarget = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : moveStart;
        Vector2 moveDelta = moveTarget - moveStart;
        if (moveDelta.sqrMagnitude > 0.0001f)
        {
            float moveDuration = SecondsForSpeed(
                moveDelta.magnitude,
                demon.PlayerMoveSpeedReference * moveSpeedMultiplier,
                fallbackMoveSeconds);

            demon.FacePatternDirection(moveDelta);
            if (motion != null)
                motion.StartLunge(moveStart, moveDelta.normalized, moveDelta.magnitude, moveDuration, 1.8f);
            else
                demon.transform.position = moveTarget;

            yield return WaitForSecondsUnlessCancelled(moveDuration, spec);
            if (IsAbilityCancelled(spec))
                yield break;
        }

        Vector2 origin = demon.transform.position;
        Vector2 direction = demon.GetDirectionToTargetOrFacing(origin);
        demon.FacePatternDirection(direction);
        demon.PlayPatternTrigger();
        ShowSectorWarning(demon, origin, direction, slashRadius, slashAngle, warningSeconds);
        Vector2[] lineDirections = CreateHeavySlashLineDirections(direction);
        LineArea[] lineAreas = new LineArea[lineDirections.Length];
        for (int i = 0; i < lineDirections.Length; i++)
        {
            lineAreas[i] = ResolveForwardLineArea(demon, origin, lineDirections[i], lineWidth, fallbackLineLength);
            ShowLineAreaWarning(demon, lineAreas[i], warningSeconds);
        }

        yield return WaitForSecondsUnlessCancelled(warningSeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        DemonKingPatternVfx.SpawnSlash(origin, direction, slashRadius);
        DemonKingCombatUtil.ApplySectorDamage(
            demon,
            origin,
            direction,
            slashRadius,
            slashAngle,
            demon.DefaultDamageEffect,
            damage,
            knockback);

        yield return SpawnLineExplosions(demon, origin, lineDirections, lineAreas, spec);
    }

    private static Vector2[] CreateHeavySlashLineDirections(Vector2 centerDirection)
    {
        Vector2 forward = centerDirection.sqrMagnitude > 0.0001f ? centerDirection.normalized : Vector2.right;
        return new[]
        {
            (Vector2)(Quaternion.Euler(0f, 0f, -30f) * (Vector3)forward),
            forward,
            (Vector2)(Quaternion.Euler(0f, 0f, 30f) * (Vector3)forward)
        };
    }

    private IEnumerator SpawnLineExplosions(
        DemonKingController demon,
        Vector2 origin,
        IReadOnlyList<Vector2> lineDirections,
        IReadOnlyList<LineArea> lineAreas,
        AbilitySpec spec)
    {
        float firstDistance = Mathf.Max(0.5f, explosionSpacing);
        float maxLength = 0f;
        for (int i = 0; i < lineAreas.Count; i++)
            maxLength = Mathf.Max(maxLength, lineAreas[i].Length);

        for (float distance = firstDistance; distance <= maxLength + 0.01f; distance += explosionSpacing)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            for (int i = 0; i < lineDirections.Count; i++)
            {
                if (distance > lineAreas[i].Length + 0.01f)
                    continue;

                Vector2 direction = lineDirections[i].sqrMagnitude > 0.0001f ? lineDirections[i].normalized : Vector2.right;
                DemonKingDelayedDamageArea.SpawnCircle(
                    demon,
                    origin + direction * distance,
                    explosionDiameter,
                    explosionWarningSeconds,
                    explosionDamage);
            }

            if (explosionStepInterval > 0f)
                yield return WaitForSecondsUnlessCancelled(explosionStepInterval, spec);
        }

        yield return WaitForSecondsUnlessCancelled(explosionWarningSeconds, spec);
    }
}

public sealed class AbilityLogic_DemonKingThrowEgoSword : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0f)] private float warningSeconds = 1.4f;
    [SerializeField, Min(0.1f)] private float throwSpeedMultiplier = 5f;
    [SerializeField, Min(0)] private int wallBounceCount = 5;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        demon.PlayPatternTrigger();

        float elapsed = 0f;
        while (elapsed < warningSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 holdPosition = demon.ResolveSwordHoldPosition(new Vector3(0.85f, 0.1f, 0f));
            demon.FacePatternDirection(demon.GetDirectionToTargetOrFacing(holdPosition));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (IsAbilityCancelled(spec))
            yield break;

        Vector2 origin = demon.ResolveSwordHoldPosition(new Vector3(0.85f, 0.1f, 0f));
        Vector2 direction = demon.GetDirectionToTargetOrFacing(origin);
        EgoSwordActor sword = demon.EgoSword;
        if (sword != null)
        {
            sword.Throw(origin, direction, demon.PlayerMoveSpeedReference * throwSpeedMultiplier, wallBounceCount, demon.WallMask);
            demon.SetSwordDropped();
        }
    }
}

public sealed class AbilityLogic_DemonKingHomingMagic : AbilityLogic_DemonKingBase
{
    private static readonly Vector2[] CardinalDirections =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    [SerializeField, Min(1)] private int projectileCount = 5;
    [SerializeField, Min(0.01f)] private float moveSeconds = 0.18f;
    [SerializeField, Min(0f)] private float shotIntervalSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float projectileSpeedMultiplier = 5f;
    [SerializeField, Min(0.05f)] private float projectileRadius = 0.35f;
    [SerializeField, Min(0f)] private float projectileDamage = 1f;
    [SerializeField, Min(0.1f)] private float lifetimeSeconds = 4f;
    [SerializeField, Min(0.1f)] private float orbSpawnRadius = 1.15f;
    [SerializeField, Min(0.1f)] private float wallProbeRadius = 0.45f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        demon.PlayPatternTrigger();

        int count = Mathf.Max(1, projectileCount);
        for (int i = 0; i < count; i++)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 bossPosition = demon.transform.position;
            Vector2 spawnOffset = ResolveOrbOffset(i, count, demon.GetDirectionToTargetOrFacing(bossPosition));
            Vector2 spawnPosition = bossPosition + spawnOffset;
            Vector2 fireDirection = demon.CurrentTarget != null
                ? ((Vector2)demon.CurrentTarget.position - spawnPosition).normalized
                : demon.GetDirectionToTargetOrFacing(spawnPosition);

            demon.FacePatternDirection(fireDirection);
            DemonKingProjectile2D.Spawn(
                demon,
                spawnPosition,
                fireDirection,
                null,
                demon.PlayerMoveSpeedReference * projectileSpeedMultiplier,
                0f,
                projectileRadius,
                projectileDamage,
                lifetimeSeconds);

            Vector2 moveDirection = ResolveMagicMoveDirection(demon, bossPosition);
            float moveDistance = demon.PlayerDashDistanceReference;
            if (motion != null && moveDistance > 0f)
                motion.StartLunge(bossPosition, moveDirection, moveDistance, moveSeconds, 1.8f);
            else
                demon.transform.position = bossPosition + moveDirection * moveDistance;

            float waitSeconds = Mathf.Max(shotIntervalSeconds, moveSeconds);
            yield return WaitForSecondsUnlessCancelled(waitSeconds, spec);
        }
    }

    private Vector2 ResolveOrbOffset(int index, int count, Vector2 forward)
    {
        float centerAngle = DemonKingCombatUtil.RotationDeg(forward);
        float angleStep = 360f / Mathf.Max(1, count);
        float angle = centerAngle + angleStep * index;
        return (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * orbSpawnRadius;
    }

    private Vector2 ResolveMagicMoveDirection(DemonKingController demon, Vector2 origin)
    {
        int startIndex = Random.Range(0, CardinalDirections.Length);
        float distance = demon.PlayerDashDistanceReference;
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2 direction = CardinalDirections[(startIndex + i) % CardinalDirections.Length];
            if (!WouldHitWall(demon, origin, direction, distance))
                return direction;
        }

        return CardinalDirections[startIndex];
    }

    private bool WouldHitWall(DemonKingController demon, Vector2 origin, Vector2 direction, float distance)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(demon.WallMask);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        RaycastHit2D[] hits = new RaycastHit2D[4];
        return Physics2D.CircleCast(origin, wallProbeRadius, direction, filter, hits, distance) > 0;
    }
}

public sealed class AbilityLogic_DemonKingBombardment : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(1)] private int strikeCount = 6;
    [SerializeField, Min(0f)] private float moveSeconds = 0.5f;
    [SerializeField, Min(0f)] private float warningSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float warningIntervalSeconds = 0.3f;
    [SerializeField, Min(0.1f)] private float sideOffset = 4.8f;
    [SerializeField, Min(0.1f)] private float laneWidth = 1.6f;
    [SerializeField, Min(0.1f)] private float fallbackMapHeight = 40f;
    [SerializeField, Min(0f)] private float damage = 1f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        demon.PlayPatternTrigger();
        Vector2 arenaCenter = demon.ArenaCenterPosition;
        Vector2 playerPosition = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : arenaCenter + Vector2.left;
        float bossSide = playerPosition.x < arenaCenter.x ? 1f : -1f;
        Vector2 moveTarget = arenaCenter + Vector2.right * (bossSide * sideOffset);
        Vector2 moveStart = demon.transform.position;
        Vector2 moveDelta = moveTarget - moveStart;
        demon.FacePatternDirection(moveDelta);
        if (moveDelta.sqrMagnitude > 0.0001f && motion != null)
            motion.StartLunge(moveStart, moveDelta.normalized, moveDelta.magnitude, moveSeconds, 1.8f);
        else
            demon.transform.position = moveTarget;

        yield return WaitForSecondsUnlessCancelled(moveSeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        float startX = arenaCenter.x - bossSide * sideOffset;
        float endX = moveTarget.x - bossSide * laneWidth;
        int count = Mathf.Max(1, strikeCount);
        for (int i = 0; i < count; i++)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            float t = count == 1 ? 0f : i / (float)(count - 1);
            Vector2 laneOrigin = new(Mathf.Lerp(startX, endX, t), arenaCenter.y);
            LineArea lane = ResolveFullLineArea(demon, laneOrigin, Vector2.up, laneWidth, fallbackMapHeight);
            DemonKingDelayedDamageArea.SpawnRectangle(
                demon,
                lane.Center,
                lane.Size,
                lane.RotationDeg,
                warningSeconds,
                damage);

            yield return WaitForSecondsUnlessCancelled(warningIntervalSeconds, spec);
        }

        yield return WaitForSecondsUnlessCancelled(warningSeconds, spec);
    }
}

public sealed class AbilityLogic_DemonKingExplosionJump : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.1f)] private float travelSeconds = 0.7f;
    [SerializeField, Min(0.1f)] private float impactDiameter = 3.2f;
    [SerializeField, Min(0f)] private float damage = 2f;
    [SerializeField, Min(0f)] private float knockback = 12f;
    [SerializeField, Min(0f)] private float radialWarningSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float radialLineWidth = 1.3f;
    [SerializeField, Min(0.1f)] private float radialFallbackLength = 40f;
    [SerializeField, Min(0f)] private float radialDamage = 1f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        Vector2 start = demon.transform.position;
        Vector2 target = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : (Vector2)demon.ArenaCenterPosition;
        Vector2 direction = target - start;
        demon.FacePatternDirection(direction);
        demon.PlayPatternTrigger();

        ShowCircleWarning(demon, target, impactDiameter, travelSeconds);

        if (direction.sqrMagnitude > 0.0001f && motion != null)
            motion.StartLunge(start, direction.normalized, direction.magnitude, travelSeconds, 2.7f);
        else
            demon.transform.position = target;

        yield return WaitForSecondsUnlessCancelled(travelSeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        DemonKingCombatUtil.ApplyCircleDamage(
            demon,
            target,
            impactDiameter * 0.5f,
            demon.DefaultDamageEffect,
            damage,
            knockbackImpulse: knockback);
        DemonKingPatternVfx.SpawnImpact(target, impactDiameter);
        DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
            target,
            impactDiameter,
            AttackSquareColor,
            "DemonKing_ImpactCircleAttack");

        LineArea[] radialLines = CreateRadialLines(demon, target);
        for (int i = 0; i < radialLines.Length; i++)
            ShowLineAreaWarning(demon, radialLines[i], radialWarningSeconds);

        yield return WaitForSecondsUnlessCancelled(radialWarningSeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        HashSet<GameObject> damagedTargets = new();
        for (int i = 0; i < radialLines.Length; i++)
            ApplyLineAreaDamage(demon, radialLines[i], radialDamage, damagedTargets, knockback);
    }

    private LineArea[] CreateRadialLines(DemonKingController demon, Vector2 origin)
    {
        LineArea[] lines = new LineArea[8];
        for (int i = 0; i < lines.Length; i++)
        {
            Vector2 direction = (Vector2)(Quaternion.Euler(0f, 0f, i * 45f) * Vector2.right);
            lines[i] = ResolveForwardLineArea(demon, origin, direction, radialLineWidth, radialFallbackLength);
        }

        return lines;
    }
}

public sealed class AbilityLogic_DemonKingRecallEgoSword : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.1f)] private float recallSpeedMultiplier = 5f;
    [SerializeField, Min(0.1f)] private float timeoutSeconds = 2.5f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null || demon.EgoSword == null)
            yield break;

        demon.PlayPatternTrigger();
        demon.EgoSword.Recall(demon.PlayerMoveSpeedReference * recallSpeedMultiplier);

        float elapsed = 0f;
        while (!demon.EgoSword.IsHeld && elapsed < timeoutSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!demon.EgoSword.IsHeld)
            demon.EgoSword.CompleteRecallAtOwner();
    }
}

public sealed class AbilityLogic_DemonKingWallBounceRush : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(1)] private int wallBounceCount = 4;
    [SerializeField, Min(0f)] private float warningSeconds = 0.6f;
    [SerializeField, Min(0.01f)] private float retreatSeconds = 0.16f;
    [SerializeField, Min(0.1f)] private float fallbackRushDistance = 40f;
    [SerializeField, Min(0.1f)] private float rushSpeedMultiplier = 5f;
    [SerializeField, Min(0.1f)] private float hitWidth = 1.6f;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0f)] private float knockback = 12f;
    [SerializeField, Min(0.01f)] private float finalJumpSeconds = 0.5f;
    [SerializeField, Min(0.1f)] private float finalImpactDiameter = 3.4f;
    [SerializeField, Min(0f)] private float finalImpactDamage = 2f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        EntityCollisionProfile2D collisionProfile = demon.GetComponent<EntityCollisionProfile2D>();
        collisionProfile?.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);
        demon.PushFaceTargetLock();

        try
        {
            Vector2 retreatStart = demon.transform.position;
            Vector2 awayFromPlayer = -demon.GetDirectionToTargetOrFacing(retreatStart);
            float retreatDistance = demon.PlayerDashDistanceReference;
            demon.FacePatternDirection(-awayFromPlayer);
            if (motion != null && retreatDistance > 0f)
                motion.StartLunge(retreatStart, awayFromPlayer, retreatDistance, retreatSeconds, 1.8f);
            else
                demon.transform.position = retreatStart + awayFromPlayer * retreatDistance;

            yield return WaitForSecondsUnlessCancelled(retreatSeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 warningStart = demon.transform.position;
            Vector2 direction = demon.GetDirectionToTargetOrFacing(warningStart);
            LineArea warningLine = ResolveForwardLineArea(demon, warningStart, direction, hitWidth, fallbackRushDistance);
            AttackTelegraphView warningView = ShowLineAreaWarning(demon, warningLine, warningSeconds);
            DemonKingPrimitiveVisual warningPrimitive = ShowLineAreaPrimitiveWarning(warningLine, warningSeconds);

            float elapsed = 0f;
            while (elapsed < warningSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                direction = demon.GetDirectionToTargetOrFacing(warningStart);
                demon.FacePatternDirection(direction);
                warningLine = ResolveForwardLineArea(demon, warningStart, direction, hitWidth, fallbackRushDistance);
                UpdateLineAreaWarning(warningView, demon, warningLine, warningSeconds);
                UpdateLineAreaPrimitiveWarning(warningPrimitive, warningLine);

                elapsed += Time.deltaTime;
                yield return null;
            }

            float rushSpeed = demon.PlayerMoveSpeedReference * rushSpeedMultiplier;
            for (int i = 0; i < wallBounceCount; i++)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                Vector2 start = demon.transform.position;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : demon.GetDirectionToTargetOrFacing(start);
                demon.FacePatternDirection(direction);
                demon.PlayPatternTrigger();
                float distance = Mathf.Max(0.5f, ResolveWallDistance(demon, start, direction, fallbackRushDistance) - 0.35f);
                float rushSeconds = SecondsForSpeed(distance, rushSpeed, 0.2f);

                yield return RunLungeContactDamage(
                    demon,
                    motion,
                    start,
                    direction,
                    distance,
                    rushSeconds,
                    hitWidth,
                    damage,
                    knockback,
                    spec);

                if (IsAbilityCancelled(spec))
                    yield break;

                demon.transform.position = start + direction * distance;
                direction = demon.GetDirectionToTargetOrFacing(demon.transform.position);
            }

            yield return RunFinalJump(demon, motion, spec);
        }
        finally
        {
            collisionProfile?.RestoreDefaultMode();
            motion?.CancelMotion();
            demon.PopFaceTargetLock();
        }

        demon.MarkHp50PatternUsed();
    }

    private IEnumerator RunFinalJump(
        DemonKingController demon,
        AbilityMotionController2D motion,
        AbilitySpec spec)
    {
        Vector2 start = demon.transform.position;
        Vector2 target = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : (Vector2)demon.ArenaCenterPosition;
        Vector2 delta = target - start;
        ShowCircleWarning(demon, target, finalImpactDiameter, finalJumpSeconds);

        if (delta.sqrMagnitude > 0.0001f && motion != null)
            motion.StartLunge(start, delta.normalized, delta.magnitude, finalJumpSeconds, 2.7f);
        else
            demon.transform.position = target;

        yield return WaitForSecondsUnlessCancelled(finalJumpSeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        DemonKingCombatUtil.ApplyCircleDamage(
            demon,
            target,
            finalImpactDiameter * 0.5f,
            demon.DefaultDamageEffect,
            finalImpactDamage,
            knockbackImpulse: knockback);
        DemonKingPatternVfx.SpawnImpact(target, finalImpactDiameter);
        DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
            target,
            finalImpactDiameter,
            AttackSquareColor,
            "DemonKing_FinalJumpCircleAttack");
    }
}

public sealed class AbilityLogic_DemonKingGroggyRecoverCounter : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0f)] private float attackDelaySeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float explosionDiameter = 5.4f;
    [SerializeField, Min(0f)] private float damage = 2f;
    [SerializeField, Min(0f)] private float knockback = 14f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        demon.PlayPatternTrigger();
        Vector2 center = demon.transform.position;
        DemonKingPatternVfx.SpawnGroggyRelease(center, explosionDiameter);
        yield return WaitForSecondsUnlessCancelled(attackDelaySeconds, spec);
        if (IsAbilityCancelled(spec))
            yield break;

        DemonKingCombatUtil.ApplyCircleDamage(
            demon,
            center,
            explosionDiameter * 0.5f,
            demon.DefaultDamageEffect,
            damage,
            knockbackImpulse: knockback);
        DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
            center,
            explosionDiameter,
            AttackSquareColor,
            "DemonKing_GroggyCounterCircleAttack");
    }
}

public sealed class AbilityLogic_DemonKingFinalDesperation : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.01f)] private float moveToCenterSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float openingKnockbackDiameter = 40f;
    [SerializeField, Min(0f)] private float openingKnockbackDamage = 0.01f;
    [SerializeField, Min(0f)] private float openingKnockback = 26f;
    [SerializeField, Min(0f)] private float bombIntervalSeconds = 0.2f;
    [SerializeField, Min(0f)] private float bombWarningSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float bombDiameter = 2.1f;
    [SerializeField, Min(0f)] private float bombDamage = 1f;
    [SerializeField, Min(0.1f)] private float bombOffsetRange = 5f;
    [SerializeField, Min(0f)] private float laserWarningSeconds = 1f;
    [SerializeField, Min(0f)] private float laserAttackSeconds = 1f;
    [SerializeField, Min(0.1f)] private float laserWidth = 0.75f;
    [SerializeField, Min(0.1f)] private float fallbackLaserLength = 40f;
    [SerializeField, Min(0f)] private float laserDamage = 1f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        demon.MarkFinalDesperationStarted();
        demon.CompleteEgoSwordRecall();
        demon.PlayPatternTrigger();

        DemonKingCombatUtil.ApplyCircleDamage(
            demon,
            demon.transform.position,
            openingKnockbackDiameter * 0.5f,
            demon.DefaultDamageEffect,
            openingKnockbackDamage,
            knockbackImpulse: openingKnockback);

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        Vector2 center = demon.ArenaCenterPosition;
        Vector2 start = demon.transform.position;
        Vector2 delta = center - start;
        if (delta.sqrMagnitude > 0.0001f && motion != null)
            motion.StartLunge(start, delta.normalized, delta.magnitude, moveToCenterSeconds, 1.8f);
        else
            demon.transform.position = center;

        yield return WaitForSecondsUnlessCancelled(moveToCenterSeconds, spec);
        if (!IsAbilityCancelled(spec))
            demon.transform.position = center;

        float bombTimer = 0f;

        IEnumerator RunLaserStep(Vector2 firstDirection, Vector2 secondDirection)
        {
            center = demon.ArenaCenterPosition;
            LineArea firstLine = ResolveFullLineArea(demon, center, firstDirection, laserWidth, fallbackLaserLength);
            LineArea secondLine = ResolveFullLineArea(demon, center, secondDirection, laserWidth, fallbackLaserLength);
            ShowLineAreaWarning(demon, firstLine, laserWarningSeconds);
            ShowLineAreaWarning(demon, secondLine, laserWarningSeconds);

            float warningElapsed = 0f;
            while (warningElapsed < laserWarningSeconds)
            {
                if (IsAbilityCancelled(spec) || demon.IsDead)
                    yield break;

                bombTimer = TickFinalBombardment(demon, bombTimer, Time.deltaTime);
                warningElapsed += Time.deltaTime;
                yield return null;
            }

            ShowLineAreaWarning(demon, firstLine, laserAttackSeconds);
            ShowLineAreaWarning(demon, secondLine, laserAttackSeconds);
            DemonKingPrimitiveVisual.SpawnSquare(
                firstLine.Center,
                firstLine.Size,
                firstLine.RotationDeg,
                laserAttackSeconds,
                AttackSquareColor,
                "DemonKing_FinalLaserSquareAttack");
            DemonKingPrimitiveVisual.SpawnSquare(
                secondLine.Center,
                secondLine.Size,
                secondLine.RotationDeg,
                laserAttackSeconds,
                AttackSquareColor,
                "DemonKing_FinalLaserSquareAttack");
            HashSet<GameObject> damagedTargets = new();
            float attackElapsed = 0f;
            while (attackElapsed < laserAttackSeconds)
            {
                if (IsAbilityCancelled(spec) || demon.IsDead)
                    yield break;

                bombTimer = TickFinalBombardment(demon, bombTimer, Time.fixedDeltaTime);
                ApplyLineAreaDamage(demon, firstLine, laserDamage, damagedTargets);
                ApplyLineAreaDamage(demon, secondLine, laserDamage, damagedTargets);
                attackElapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        while (!IsAbilityCancelled(spec) && !demon.IsDead)
        {
            yield return RunLaserStep(Vector2.right, Vector2.up);
            yield return RunLaserStep(new Vector2(1f, 1f).normalized, new Vector2(1f, -1f).normalized);
        }
    }

    private float TickFinalBombardment(DemonKingController demon, float timer, float deltaTime)
    {
        timer -= deltaTime;
        while (timer <= 0f)
        {
            Vector2 targetCenter = demon.CurrentTarget != null
                ? (Vector2)demon.CurrentTarget.position
                : (Vector2)demon.ArenaCenterPosition;

            Vector2 offset = new(
                Random.Range(-bombOffsetRange, bombOffsetRange),
                Random.Range(-bombOffsetRange, bombOffsetRange));

            DemonKingDelayedDamageArea.SpawnCircle(
                demon,
                targetCenter + offset,
                bombDiameter,
                bombWarningSeconds,
                bombDamage,
                ignoreOwnerGroggy: true);

            timer += Mathf.Max(0.01f, bombIntervalSeconds);
        }

        return timer;
    }
}
