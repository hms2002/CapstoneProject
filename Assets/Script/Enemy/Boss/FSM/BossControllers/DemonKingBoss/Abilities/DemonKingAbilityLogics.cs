using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public abstract class AbilityLogic_DemonKingBase : AbilityLogic
{
    private const float JumpTravelEndNormalized = 0.28f;
    private const float JumpHoldEndNormalized = 0.86f;
    private const float JumpPreDropEndNormalized = 0.96f;
    private const float JumpPreDropHeightScale = 0.9f;
    private const float JumpTravelEaseOutPower = 2.2f;
    private const float JumpLandingDropSharpness = 0.22f;

    private static readonly RaycastHit2D[] WallHitBuffer = new RaycastHit2D[12];
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
        demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                AttackTelegraphSpec.CreateRectangle(center, size, rotationDeg, duration, demon.DefaultWarningStyle)));
    }

    protected static void ShowCircleWarning(
        DemonKingController demon,
        Vector2 center,
        float diameter,
        float duration)
    {
        demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                AttackTelegraphSpec.CreateCircle(center, diameter, duration, demon.DefaultWarningStyle)));
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
        demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                AttackTelegraphSpec.CreateSector(
                    origin,
                    radius,
                    angleDeg,
                    DemonKingCombatUtil.RotationDeg(safeDirection),
                    duration,
                    demon.DefaultWarningStyle)));
    }

    protected static void PlayPatternSound(
        DemonKingController demon,
        SoundRef sound,
        Vector2 position,
        Object sourceObject,
        GameObject target = null)
    {
        if (demon == null || !sound.IsSet)
            return;

        SoundPlaybackUtility.Play(
            sound,
            instigator: demon.gameObject,
            causer: demon.gameObject,
            target: target != null ? target : demon.CurrentTarget != null ? demon.CurrentTarget.gameObject : null,
            position: position,
            sourceObject: sourceObject != null ? sourceObject : demon);
    }

    protected static void PlayPatternShake(
        DemonKingController demon,
        CameraShakeHook shake,
        Vector2 direction,
        string debugReason)
    {
        if (demon == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        shake.TryPlay(demon.gameObject, safeDirection, debugReason: debugReason);
    }

    protected static bool TryPlayTimedCircleDamage(
        DemonKingAnimationClipVisual visual,
        DemonKingController demon,
        Vector2 center,
        float diameter,
        float damage,
        float knockback,
        SoundRef impactSound,
        CameraShakeHook impactCameraShake,
        Object sourceObject,
        string debugReason)
    {
        if (demon == null)
            return false;

        CombatHitPayload payload = DemonKingCombatUtil.MakePayload(
            demon,
            demon.DefaultDamageEffect,
            damage,
            knockback);
        bool presentationPlayed = false;
        void PlayPresentationOnce()
        {
            if (presentationPlayed)
                return;

            presentationPlayed = true;
            PlayPatternSound(demon, impactSound, center, sourceObject);
            PlayPatternShake(demon, impactCameraShake, Vector2.down, debugReason);
        }

        bool timed = DemonKingPatternVfx.TryPlayCircleTimedHit(
            visual,
            demon,
            diameter,
            payload,
            null,
            PlayPresentationOnce);
        if (timed)
            return true;

        PlayPresentationOnce();
        DemonKingCombatUtil.ApplyCircleDamage(
            demon,
            center,
            diameter * 0.5f,
            demon.DefaultDamageEffect,
            damage,
            knockbackImpulse: knockback);
        return false;
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
        AbilitySpec spec,
        bool showAttackPrimitive = true,
        float lungeEaseOutPower = 2f)
    {
        demon?.BeginBodyAfterimage();

        if (motion != null)
            motion.StartLunge(start, direction, distance, duration, lungeEaseOutPower);
        else
            demon.transform.position = start + direction * distance;

        try
        {
            if (showAttackPrimitive)
            {
                DemonKingPrimitiveVisual.SpawnSquare(
                    start + direction * (distance * 0.5f),
                    new Vector2(Mathf.Max(0.1f, distance), hitWidth),
                    DemonKingCombatUtil.RotationDeg(direction),
                    duration,
                    AttackSquareColor,
                    "DemonKing_LungeSquareAttack");
            }

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
        finally
        {
            demon?.StopBodyAfterimage();
        }
    }

    protected static IEnumerator RunParabolicPatternJump(
        DemonKingController demon,
        Vector2 start,
        Vector2 target,
        float duration,
        float arcHeight,
        float landingFrameSwitchRatio,
        AbilitySpec spec)
    {
        if (demon == null)
            yield break;

        float safeDuration = Mathf.Max(0.01f, duration);
        float safeArcHeight = Mathf.Max(0f, arcHeight);
        _ = landingFrameSwitchRatio;
        float z = demon.transform.position.z;

        demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordHandJumpAttackState);

        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);

            float horizontalProgress = ResolveDemonKingJumpGroundProgress(normalizedTime);
            Vector2 groundPosition = Vector2.Lerp(start, target, horizontalProgress);
            float height = ResolveDemonKingJumpHeight(normalizedTime, safeArcHeight);
            demon.transform.position = new Vector3(groundPosition.x, groundPosition.y + height, z);
            yield return null;
        }

        demon.transform.position = new Vector3(target.x, target.y, z);
    }

    private static float ResolveDemonKingJumpGroundProgress(float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (clampedTime >= JumpTravelEndNormalized)
            return 1f;

        float travelProgress = Mathf.Clamp01(clampedTime / JumpTravelEndNormalized);
        return 1f - Mathf.Pow(1f - travelProgress, JumpTravelEaseOutPower);
    }

    private static float ResolveDemonKingJumpHeight(float normalizedTime, float maxHeight)
    {
        float safeHeight = Mathf.Max(0f, maxHeight);
        if (safeHeight <= 0f)
            return 0f;

        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (clampedTime <= JumpTravelEndNormalized)
        {
            float travelProgress = Mathf.Clamp01(clampedTime / JumpTravelEndNormalized);
            float easedProgress = 1f - Mathf.Pow(1f - travelProgress, JumpTravelEaseOutPower);
            return Mathf.Lerp(0f, safeHeight, easedProgress);
        }

        if (clampedTime <= JumpHoldEndNormalized)
            return safeHeight;

        if (clampedTime <= JumpPreDropEndNormalized)
        {
            float preDropProgress = Mathf.InverseLerp(
                JumpHoldEndNormalized,
                JumpPreDropEndNormalized,
                clampedTime);
            return Mathf.Lerp(safeHeight, safeHeight * JumpPreDropHeightScale, preDropProgress);
        }

        float dropProgress = Mathf.InverseLerp(JumpPreDropEndNormalized, 1f, clampedTime);
        float easedDrop = Mathf.Pow(Mathf.Clamp01(dropProgress), JumpLandingDropSharpness);
        return Mathf.Lerp(safeHeight * JumpPreDropHeightScale, 0f, easedDrop);
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

        return demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(spec));
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
        view.UpdateGeometry(AttackTelegraphSpecUtility.WithThinWarningOutline(
            AttackTelegraphSpec.CreateRectangle(
                center,
                new Vector2(length, width),
                DemonKingCombatUtil.RotationDeg(direction),
                duration,
                demon.DefaultWarningStyle)));
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
        return demon.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                AttackTelegraphSpec.CreateRectangle(
                    line.Center,
                    line.Size,
                    line.RotationDeg,
                    duration,
                    demon.DefaultWarningStyle)));
    }

    protected static void UpdateLineAreaWarning(
        AttackTelegraphView view,
        DemonKingController demon,
        LineArea line,
        float duration)
    {
        view?.UpdateGeometry(AttackTelegraphSpecUtility.WithThinWarningOutline(
            AttackTelegraphSpec.CreateRectangle(
                line.Center,
                line.Size,
                line.RotationDeg,
                duration,
                demon.DefaultWarningStyle)));
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

    protected static List<Vector2> CreateLineAreaExplosionPoints(LineArea line, float spacing)
    {
        List<Vector2> points = new();
        float safeSpacing = Mathf.Max(0.1f, spacing);
        int pointCount = Mathf.Max(1, Mathf.CeilToInt(line.Length / safeSpacing) + 1);
        Vector2 direction = DirectionFromRotation(line.RotationDeg);
        Vector2 start = line.Center - direction * (line.Length * 0.5f);
        Vector2 end = line.Center + direction * (line.Length * 0.5f);

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount == 1 ? 0.5f : i / (float)(pointCount - 1);
            points.Add(Vector2.Lerp(start, end, t));
        }

        return points;
    }

    protected static Vector2 DirectionFromRotation(float rotationDeg)
    {
        float radians = rotationDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }
}

public class AbilityLogic_DemonKingPierceCombo : AbilityLogic_DemonKingBase
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
    [SerializeField, Min(0f)] private float dashEndPoseHoldSeconds = 0.1f;
    [SerializeField] private SoundRef dashCommitSound;

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
            demon.FacePatternDirection(lockedTarget - start);
            demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordSwordDashStabReadyState);
            AttackTelegraphView warningView = ShowLineWarning(demon, start, lockedTarget, hitWidth, currentWarningSeconds);

            float elapsed = 0f;
            while (elapsed < currentWarningSeconds)
            {
                if (IsAbilityCancelled(spec))
                {
                    demon.PopFaceTargetLock();
                    yield break;
                }

                lockedTarget = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : lockedTarget;
                UpdateLineWarning(warningView, demon, start, lockedTarget, hitWidth, currentWarningSeconds);

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
            demon.PlayPatternAnimation(DemonKingController.DarkLordSwordDashStabState);
            PlayPatternSound(demon, dashCommitSound, start, this);
            Vector3 stabLocalOffset = demon.ResolveVfxSocketLocal(DemonKingVfxSocketId.SwordStabOrigin, Vector2.zero);
            DemonKingPatternVfx.SpawnAttachedStab(demon.transform, direction, stabLocalOffset);

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
                spec,
                showAttackPrimitive: false);

            if (!IsAbilityCancelled(spec))
            {
                demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordSwordDashStabReadyState);
                if (i < pierceCount - 1)
                    yield return ReturnToStart(demon, motion, start, returnSeconds, spec);
                else if (dashEndPoseHoldSeconds > 0f)
                    yield return WaitForSecondsUnlessCancelled(dashEndPoseHoldSeconds, spec);
            }

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

public class AbilityLogic_DemonKingHeavySlash : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.1f)] private float moveSpeedMultiplier = 4f;
    [SerializeField, Min(0.01f)] private float fallbackMoveSeconds = 0.35f;
    [SerializeField, Min(0f)] private float stopBeforeTargetDistance = 1.1f;
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
    [SerializeField, Min(0f)] private float slashEndPoseHoldSeconds = 0.12f;
    [SerializeField] private SoundRef approachSound;
    [SerializeField] private SoundRef slashCommitSound;
    [SerializeField] private SoundRef lineExplosionSound;
    [SerializeField] private CameraShakeHook slashImpactCameraShake = CameraShakeHook.Create(0.12f, 1f, 0.28f, 0.04f);
    [SerializeField] private CameraShakeHook lineExplosionCameraShake = CameraShakeHook.Create(0.08f, 1f, 0.18f, 0.08f);

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        demon.PushFaceTargetLock();
        try
        {
            Vector2 moveStart = demon.transform.position;
            Vector2 moveTarget = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : moveStart;
            Vector2 moveDelta = moveTarget - moveStart;
            float stopDistance = Mathf.Max(0f, stopBeforeTargetDistance);
            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                Vector2 approachDirection = moveDelta.normalized;
                moveTarget -= approachDirection * Mathf.Min(stopDistance, Mathf.Max(0f, moveDelta.magnitude - 0.05f));
                moveDelta = moveTarget - moveStart;
            }

            if (moveDelta.magnitude > 0.05f)
            {
                float moveDuration = SecondsForSpeed(
                    moveDelta.magnitude,
                    demon.PlayerMoveSpeedReference * moveSpeedMultiplier,
                    fallbackMoveSeconds);

                demon.FacePatternDirection(moveDelta);
                PlayPatternSound(demon, approachSound, moveStart, this);
                if (motion != null)
                    motion.StartLunge(moveStart, moveDelta.normalized, moveDelta.magnitude, moveDuration, 1.8f);
                else
                    demon.transform.position = moveTarget;

                yield return WaitForSecondsUnlessCancelled(moveDuration, spec);
                if (IsAbilityCancelled(spec))
                    yield break;
            }

            Vector2 origin = demon.ResolveVfxSocketWorld(DemonKingVfxSocketId.SwordSlashOrigin, Vector2.zero);
            Vector2 direction = demon.GetDirectionToTargetOrFacing(origin);
            demon.FacePatternDirection(direction);
            origin = demon.ResolveVfxSocketWorld(DemonKingVfxSocketId.SwordSlashOrigin, Vector2.zero);
            demon.HoldPatternAnimationFrame(DemonKingController.DarkLordSwordSlashState, 1);
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

            DemonKingAnimationClipVisual slashVfx = DemonKingPatternVfx.SpawnSlash(origin, direction, slashRadius);
            void CommitSlashDamage()
            {
                if (IsAbilityCancelled(spec) || demon == null || demon.IsDead)
                    return;

                demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordSwordSlashState);
                PlayPatternSound(demon, slashCommitSound, origin, this);
                PlayPatternShake(demon, slashImpactCameraShake, direction, "DemonKing.HeavySlashImpact");
                DemonKingCombatUtil.ApplySectorDamage(
                    demon,
                    origin,
                    direction,
                    slashRadius,
                    slashAngle,
                    demon.DefaultDamageEffect,
                    damage,
                    knockback);
            }

            if (!DemonKingPatternVfx.TryPlayTimedSignal(slashVfx, CommitSlashDamage))
                CommitSlashDamage();

            yield return SpawnLineExplosions(demon, origin, lineDirections, lineAreas, spec);
            if (!IsAbilityCancelled(spec) && slashEndPoseHoldSeconds > 0f)
            {
                demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordSwordSlashState);
                yield return WaitForSecondsUnlessCancelled(slashEndPoseHoldSeconds, spec);
            }
        }
        finally
        {
            demon.PopFaceTargetLock();
        }
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

            List<Vector2> centers = new();
            for (int i = 0; i < lineDirections.Count; i++)
            {
                if (distance > lineAreas[i].Length + 0.01f)
                    continue;

                Vector2 direction = lineDirections[i].sqrMagnitude > 0.0001f ? lineDirections[i].normalized : Vector2.right;
                centers.Add(origin + direction * distance);
            }

            DemonKingDelayedDamageArea.SpawnCircleCluster(
                demon,
                centers,
                explosionDiameter,
                explosionWarningSeconds,
                explosionDamage,
                explosionVfxKind: DemonKingDelayedExplosionVfxKind.DarkLordExplosion2,
                impactSound: lineExplosionSound,
                impactCameraShake: lineExplosionCameraShake);

            if (explosionStepInterval > 0f)
                yield return WaitForSecondsUnlessCancelled(explosionStepInterval, spec);
        }

        yield return WaitForSecondsUnlessCancelled(explosionWarningSeconds, spec);
    }
}

public class AbilityLogic_DemonKingThrowEgoSword : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0f)] private float warningSeconds = 1.4f;
    [SerializeField, Min(0.1f)] private float throwSpeedMultiplier = 5f;
    [SerializeField, Min(0)] private int wallBounceCount = 5;
    [SerializeField, Min(0f)] private float throwEndPoseHoldSeconds = 0.12f;
    [SerializeField] private SoundRef throwReleaseSound;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        EgoSwordActor sword = demon.EgoSword;
        if (sword == null)
            yield break;

        demon.FacePatternDirection(demon.GetDirectionToTargetOrFacing(sword.ResolveThrowOriginPosition()));
        demon.PushFaceTargetLock();
        try
        {
            float elapsed = 0f;
            while (elapsed < warningSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 animationOrigin = sword.ResolveThrowOriginPosition();
            Vector2 animationDirection = demon.GetDirectionToTargetOrFacing(animationOrigin);
            demon.FacePatternDirection(animationDirection);
            demon.PlayPatternAnimationOncePerPattern(DemonKingController.DarkLordSwordThrowingState);

            float throwDelaySeconds = demon.ResolvePatternAnimationLastFrameStartDelay(
                DemonKingController.DarkLordSwordThrowingState);
            yield return WaitForSecondsUnlessCancelled(throwDelaySeconds, spec);

            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 origin = sword.ResolveThrowOriginPosition();
            Vector2 direction = demon.GetDirectionToTargetOrFacing(origin);

            PlayPatternSound(demon, throwReleaseSound, origin, this);
            sword.Throw(origin, direction, demon.PlayerMoveSpeedReference * throwSpeedMultiplier, wallBounceCount, demon.WallMask);
            demon.SetSwordDropped();
            if (throwEndPoseHoldSeconds > 0f)
            {
                demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordSwordThrowingState);
                yield return WaitForSecondsUnlessCancelled(throwEndPoseHoldSeconds, spec);
            }
        }
        finally
        {
            demon.PopFaceTargetLock();
        }
    }
}

public class AbilityLogic_DemonKingHomingMagic : AbilityLogic_DemonKingBase
{
    private const string StockOrbVisualResourcePath = "DemonKing/Vfx/HomingMagicBaltStockVfx";
    private const string FiredProjectileVisualResourcePath = "DemonKing/Vfx/HomingMagicBaltProjectileVfx";

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
    [SerializeField] private GameObject stockOrbVisualPrefab;
    [SerializeField] private GameObject firedProjectileVisualPrefab;
    [SerializeField] private Vector2 stockOrbBaseLocalOffset = new(0f, 1.6f);
    [SerializeField, Min(0f)] private float stockOrbSpacing = 0.45f;
    [SerializeField, Min(0f)] private float stockOrbArcHeight = 0.35f;
    [SerializeField] private SoundRef castSound;
    [SerializeField] private SoundRef fireSound;

    private GameObject resolvedStockOrbVisualPrefab;
    private GameObject resolvedFiredProjectileVisualPrefab;
    private bool stockOrbVisualMissingLogged;
    private bool firedProjectileVisualMissingLogged;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        demon.PushFaceTargetLock();
        List<GameObject> stockOrbVisuals = null;
        try
        {
            int count = Mathf.Max(1, projectileCount);
            GameObject stockVisualPrefab = ResolveStockOrbVisualPrefab();
            GameObject firedVisualPrefab = ResolveFiredProjectileVisualPrefab();
            stockOrbVisuals = CreateStockOrbVisuals(demon, count, stockVisualPrefab);
            for (int i = 0; i < count; i++)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                Vector2 bossPosition = demon.transform.position;
                Vector2 spawnPosition = ResolveNextProjectileSpawnPosition(demon, stockOrbVisuals, i, count);
                Vector2 fireDirection = demon.CurrentTarget != null
                    ? ((Vector2)demon.CurrentTarget.position - spawnPosition).normalized
                    : demon.GetDirectionToTargetOrFacing(spawnPosition);

                demon.FacePatternDirection(fireDirection);
                PlayPatternSound(demon, castSound, bossPosition, this);
                demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordHandBaltState);
                DemonKingProjectile2D.Spawn(
                    demon,
                    spawnPosition,
                    fireDirection,
                    null,
                    demon.PlayerMoveSpeedReference * projectileSpeedMultiplier,
                    0f,
                    projectileRadius,
                    projectileDamage,
                    lifetimeSeconds,
                    firedVisualPrefab);
                PlayPatternSound(demon, fireSound, spawnPosition, this);
                demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordHandBaltState);

                Vector2 moveDirection = ResolveMagicMoveDirection(demon, bossPosition);
                float moveDistance = demon.PlayerDashDistanceReference;
                float moveWaitSeconds = Mathf.Max(0f, moveSeconds);
                demon.BeginBodyAfterimage();
                try
                {
                    if (motion != null && moveDistance > 0f)
                        motion.StartLunge(bossPosition, moveDirection, moveDistance, moveSeconds, 1.8f);
                    else
                        demon.transform.position = bossPosition + moveDirection * moveDistance;

                    yield return WaitForSecondsUnlessCancelled(moveWaitSeconds, spec);
                }
                finally
                {
                    demon.StopBodyAfterimage();
                }

                if (IsAbilityCancelled(spec))
                    yield break;

                float remainingWaitSeconds = Mathf.Max(0f, shotIntervalSeconds - moveWaitSeconds);
                yield return WaitForSecondsUnlessCancelled(remainingWaitSeconds, spec);
            }
        }
        finally
        {
            CleanupStockOrbVisuals(stockOrbVisuals);
            demon.PopFaceTargetLock();
        }
    }

    private List<GameObject> CreateStockOrbVisuals(
        DemonKingController demon,
        int count,
        GameObject visualPrefab)
    {
        List<GameObject> stockOrbVisuals = new();
        if (demon == null || visualPrefab == null)
            return stockOrbVisuals;

        int safeCount = Mathf.Max(0, count);
        for (int i = 0; i < safeCount; i++)
        {
            GameObject visual = UnityEngine.Object.Instantiate(visualPrefab, demon.transform);
            if (visual == null)
                continue;

            visual.name = "DemonKing_HomingMagicStockOrb";
            ConfigureStockOrbVisual(visual);
            stockOrbVisuals.Add(visual);
        }

        ApplyStockOrbLayout(demon, stockOrbVisuals);
        return stockOrbVisuals;
    }

    private static void ConfigureStockOrbVisual(GameObject visual)
    {
        if (visual == null)
            return;

        SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            DemonKingPrimitiveVisual.ApplyProjectileSorting(renderers[i]);

        Collider2D[] colliders = visual.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private Vector2 ResolveNextProjectileSpawnPosition(
        DemonKingController demon,
        List<GameObject> stockOrbVisuals,
        int shotIndex,
        int totalCount)
    {
        if (TryConsumeTargetSideStockOrb(demon, stockOrbVisuals, out Vector2 stockPosition))
        {
            ApplyStockOrbLayout(demon, stockOrbVisuals);
            return stockPosition;
        }

        Vector2 bossPosition = demon.transform.position;
        Vector2 spawnOffset = ResolveOrbOffset(shotIndex, totalCount, demon.GetDirectionToTargetOrFacing(bossPosition));
        return bossPosition + spawnOffset;
    }

    private bool TryConsumeTargetSideStockOrb(
        DemonKingController demon,
        List<GameObject> stockOrbVisuals,
        out Vector2 position)
    {
        position = Vector2.zero;
        if (demon == null || stockOrbVisuals == null || stockOrbVisuals.Count == 0)
            return false;

        int sideSign = ResolveTargetSideSign(demon);
        int selectedIndex = -1;
        float selectedX = sideSign >= 0 ? float.NegativeInfinity : float.PositiveInfinity;
        for (int i = 0; i < stockOrbVisuals.Count; i++)
        {
            GameObject visual = stockOrbVisuals[i];
            if (visual == null)
                continue;

            float localX = visual.transform.localPosition.x;
            bool better = sideSign >= 0
                ? localX > selectedX
                : localX < selectedX;
            if (!better)
                continue;

            selectedX = localX;
            selectedIndex = i;
        }

        if (selectedIndex < 0)
            return false;

        GameObject selected = stockOrbVisuals[selectedIndex];
        stockOrbVisuals.RemoveAt(selectedIndex);
        if (selected == null)
            return false;

        position = selected.transform.position;
        UnityEngine.Object.Destroy(selected);
        return true;
    }

    private static int ResolveTargetSideSign(DemonKingController demon)
    {
        if (demon == null)
            return 1;

        float deltaX = 0f;
        if (demon.CurrentTarget != null)
            deltaX = demon.CurrentTarget.position.x - demon.transform.position.x;

        if (Mathf.Abs(deltaX) <= 0.01f)
            deltaX = demon.FacingDirection.x;

        return deltaX < 0f ? -1 : 1;
    }

    private void ApplyStockOrbLayout(DemonKingController demon, List<GameObject> stockOrbVisuals)
    {
        if (demon == null || stockOrbVisuals == null)
            return;

        PruneStockOrbVisuals(stockOrbVisuals);
        int count = stockOrbVisuals.Count;
        for (int i = 0; i < count; i++)
        {
            GameObject visual = stockOrbVisuals[i];
            if (visual == null)
                continue;

            Transform visualTransform = visual.transform;
            if (visualTransform.parent != demon.transform)
                visualTransform.SetParent(demon.transform, true);

            Vector2 localOffset = CalculateStockOrbLocalOffset(demon, i, count);
            visualTransform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            visualTransform.localRotation = Quaternion.identity;
        }
    }

    private Vector2 CalculateStockOrbLocalOffset(DemonKingController demon, int index, int count)
    {
        Vector2 baseLocalOffset = demon != null
            ? (Vector2)demon.ResolveVfxSocketLocal(DemonKingVfxSocketId.HomingStockCenter, stockOrbBaseLocalOffset)
            : stockOrbBaseLocalOffset;

        if (count <= 0)
            return baseLocalOffset;

        float centeredIndex = index - (count - 1) * 0.5f;
        float maxCenteredIndex = Mathf.Max(1f, (count - 1) * 0.5f);
        float centerRatio = 1f - Mathf.Clamp01(Mathf.Abs(centeredIndex) / maxCenteredIndex);
        float arcOffsetY = stockOrbArcHeight * centerRatio;
        return baseLocalOffset + new Vector2(centeredIndex * stockOrbSpacing, arcOffsetY);
    }

    private static void PruneStockOrbVisuals(List<GameObject> stockOrbVisuals)
    {
        if (stockOrbVisuals == null)
            return;

        for (int i = stockOrbVisuals.Count - 1; i >= 0; i--)
        {
            if (stockOrbVisuals[i] == null)
                stockOrbVisuals.RemoveAt(i);
        }
    }

    private static void CleanupStockOrbVisuals(List<GameObject> stockOrbVisuals)
    {
        if (stockOrbVisuals == null)
            return;

        for (int i = stockOrbVisuals.Count - 1; i >= 0; i--)
        {
            if (stockOrbVisuals[i] != null)
                UnityEngine.Object.Destroy(stockOrbVisuals[i]);
        }

        stockOrbVisuals.Clear();
    }

    private GameObject ResolveStockOrbVisualPrefab()
    {
        if (stockOrbVisualPrefab != null)
            return stockOrbVisualPrefab;

        if (resolvedStockOrbVisualPrefab == null)
            resolvedStockOrbVisualPrefab = Resources.Load<GameObject>(StockOrbVisualResourcePath);

        if (resolvedStockOrbVisualPrefab == null && !stockOrbVisualMissingLogged)
        {
            stockOrbVisualMissingLogged = true;
            Debug.LogWarning($"DemonKing HomingMagic stock VFX prefab not found at Resources/{StockOrbVisualResourcePath}. Stock display will be skipped.", this);
        }

        return resolvedStockOrbVisualPrefab;
    }

    private GameObject ResolveFiredProjectileVisualPrefab()
    {
        if (firedProjectileVisualPrefab != null)
            return firedProjectileVisualPrefab;

        if (resolvedFiredProjectileVisualPrefab == null)
            resolvedFiredProjectileVisualPrefab = Resources.Load<GameObject>(FiredProjectileVisualResourcePath);

        if (resolvedFiredProjectileVisualPrefab == null && !firedProjectileVisualMissingLogged)
        {
            firedProjectileVisualMissingLogged = true;
            Debug.LogWarning($"DemonKing HomingMagic projectile VFX prefab not found at Resources/{FiredProjectileVisualResourcePath}. Primitive projectile visual will be used.", this);
        }

        return resolvedFiredProjectileVisualPrefab;
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

public class AbilityLogic_DemonKingBombardment : AbilityLogic_DemonKingBase
{
    private const float ReleaseImpactPoseHoldSeconds = 1f;

    [SerializeField, Min(1)] private int strikeCount = 6;
    [SerializeField, Min(0f)] private float moveSeconds = 0.5f;
    [SerializeField, Min(0f)] private float warningSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float warningIntervalSeconds = 0.3f;
    [SerializeField, Min(0.1f)] private float sideOffset = 4.8f;
    [SerializeField, Min(0.1f)] private float laneWidth = 1.6f;
    [SerializeField, Min(0.1f)] private float fallbackMapHeight = 40f;
    [SerializeField, Min(0.1f)] private float explosionDiameter = 1.35f;
    [SerializeField, Min(0.1f)] private float explosionSpacing = 1.35f;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField] private SoundRef chargeSound;
    [SerializeField] private SoundRef releaseSound;
    [SerializeField] private SoundRef explosionSound;
    [SerializeField] private CameraShakeHook bombardmentReleaseImpactCameraShake = CameraShakeHook.Create(0.12f, 1f, 0.28f, 0.04f);
    [SerializeField] private CameraShakeHook explosionCameraShake = CameraShakeHook.Create(0.08f, 1f, 0.18f, 0.08f);

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        demon.PushFaceTargetLock();
        try
        {
            demon.PlayPatternAnimationOncePerPattern(DemonKingController.DarkLordHandChargeState);
            PlayPatternSound(demon, chargeSound, demon.transform.position, this);
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
            yield return PlayBombardmentReleasePose(demon, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            for (int i = 0; i < count; i++)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                float t = count == 1 ? 0f : i / (float)(count - 1);
                Vector2 laneOrigin = new(Mathf.Lerp(startX, endX, t), arenaCenter.y);
                LineArea lane = ResolveFullLineArea(demon, laneOrigin, Vector2.up, laneWidth, fallbackMapHeight);
                DemonKingDelayedDamageArea.SpawnCircleCluster(
                    demon,
                    CreateLineAreaExplosionPoints(lane, explosionSpacing),
                    explosionDiameter,
                    warningSeconds,
                    damage,
                    explosionVfxKind: DemonKingDelayedExplosionVfxKind.DarkLordExplosion2,
                    impactSound: explosionSound,
                    impactCameraShake: explosionCameraShake);

                yield return WaitForSecondsUnlessCancelled(warningIntervalSeconds, spec);
            }

            yield return WaitForSecondsUnlessCancelled(warningSeconds, spec);
        }
        finally
        {
            demon.PopFaceTargetLock();
        }
    }

    private IEnumerator PlayBombardmentReleasePose(DemonKingController demon, AbilitySpec spec)
    {
        string releaseState = DemonKingController.DarkLordHandGroggyCounterState;
        if (demon.PlayPatternAnimationOncePerPattern(releaseState))
        {
            float releaseDelaySeconds = demon.ResolvePatternAnimationLastFrameStartDelay(releaseState);
            yield return WaitForSecondsUnlessCancelled(releaseDelaySeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;
        }

        demon.HoldPatternAnimationLastFrame(releaseState);
        Vector2 releaseCenter = demon.ResolveVfxSocketWorld(DemonKingVfxSocketId.HandCounterImpact, Vector2.zero);
        PlayPatternSound(demon, releaseSound, releaseCenter, this);
        DemonKingPatternVfx.SpawnImpact(releaseCenter, explosionDiameter);
        PlayPatternShake(demon, bombardmentReleaseImpactCameraShake, Vector2.down, "DemonKing.BombardmentReleaseImpact");

        yield return WaitForSecondsUnlessCancelled(ReleaseImpactPoseHoldSeconds, spec);
        if (!IsAbilityCancelled(spec))
            demon.PlayPatternAnimationIfChanged(DemonKingController.DarkLordHandIdleState);
    }
}

public class AbilityLogic_DemonKingExplosionJump : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.1f)] private float travelSeconds = 0.7f;
    [SerializeField, Min(0.1f)] private float impactDiameter = 3.2f;
    [SerializeField, Min(0f)] private float damage = 2f;
    [SerializeField, Min(0f)] private float knockback = 12f;
    [SerializeField, Min(0f)] private float radialWarningSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float radialLineWidth = 1.3f;
    [SerializeField, Min(0.1f)] private float radialFallbackLength = 40f;
    [SerializeField, Min(0.1f)] private float radialExplosionDiameter = 1.35f;
    [SerializeField, Min(0.1f)] private float radialExplosionSpacing = 1.35f;
    [SerializeField, Min(0f)] private float radialExplosionStepInterval = 0.04f;
    [SerializeField, Min(0f)] private float radialDamage = 1f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 1.35f;
    [SerializeField, Range(0f, 1f)] private float landingFrameSwitchRatio = 0.78f;
    [SerializeField, Min(0f)] private float landingPoseHoldSeconds = 0.14f;
    [SerializeField] private SoundRef jumpStartSound;
    [SerializeField] private SoundRef landingImpactSound;
    [SerializeField] private SoundRef radialExplosionSound;
    [SerializeField] private CameraShakeHook landingImpactCameraShake = CameraShakeHook.Create(0.18f, 1f, 0.35f, 0.04f);
    [SerializeField] private CameraShakeHook radialExplosionCameraShake = CameraShakeHook.Create(0.08f, 1f, 0.18f, 0.08f);

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        Vector2 start = demon.transform.position;
        Vector2 target = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : (Vector2)demon.ArenaCenterPosition;
        Vector2 direction = target - start;
        demon.FacePatternDirection(direction);
        Vector2 impactCenter = demon.ResolveVfxSocketWorldAtBasePosition(
            DemonKingVfxSocketId.FootLandingImpact,
            target,
            Vector2.zero);

        demon.PushFaceTargetLock();
        try
        {
            ShowCircleWarning(demon, impactCenter, impactDiameter, travelSeconds);
            PlayPatternSound(demon, jumpStartSound, start, this);

            demon.BeginBodyAfterimage();
            try
            {
                yield return RunParabolicPatternJump(
                    demon,
                    start,
                    target,
                    travelSeconds,
                    jumpArcHeight,
                    landingFrameSwitchRatio,
                    spec);
            }
            finally
            {
                demon.StopBodyAfterimage();
            }

            if (IsAbilityCancelled(spec))
                yield break;

            demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordHandJumpAttackState);
            impactCenter = demon.ResolveVfxSocketWorld(DemonKingVfxSocketId.FootLandingImpact, Vector2.zero);
            DemonKingAnimationClipVisual landingImpact = DemonKingPatternVfx.SpawnImpact(impactCenter, impactDiameter);
            TryPlayTimedCircleDamage(
                landingImpact,
                demon,
                impactCenter,
                impactDiameter,
                damage,
                knockback,
                landingImpactSound,
                landingImpactCameraShake,
                this,
                "DemonKing.ExplosionJumpLanding");
            if (landingPoseHoldSeconds > 0f)
                yield return WaitForSecondsUnlessCancelled(landingPoseHoldSeconds, spec);

            Vector2[] radialDirections = CreateRadialDirections();
            LineArea[] radialLines = CreateRadialLines(demon, impactCenter, radialDirections);
            for (int i = 0; i < radialLines.Length; i++)
                ShowLineAreaWarning(demon, radialLines[i], radialWarningSeconds);

            yield return WaitForSecondsUnlessCancelled(radialWarningSeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            yield return SpawnRadialExplosionWave(demon, impactCenter, radialDirections, radialLines, spec);
        }
        finally
        {
            demon.PopFaceTargetLock();
        }
    }

    private static Vector2[] CreateRadialDirections()
    {
        Vector2[] directions = new Vector2[8];
        for (int i = 0; i < directions.Length; i++)
            directions[i] = (Vector2)(Quaternion.Euler(0f, 0f, i * 45f) * Vector2.right);

        return directions;
    }

    private LineArea[] CreateRadialLines(DemonKingController demon, Vector2 origin, IReadOnlyList<Vector2> directions)
    {
        LineArea[] lines = new LineArea[directions.Count];
        for (int i = 0; i < lines.Length; i++)
        {
            Vector2 direction = directions[i].sqrMagnitude > 0.0001f ? directions[i].normalized : Vector2.right;
            lines[i] = ResolveForwardLineArea(demon, origin, direction, radialLineWidth, radialFallbackLength);
        }

        return lines;
    }

    private IEnumerator SpawnRadialExplosionWave(
        DemonKingController demon,
        Vector2 origin,
        IReadOnlyList<Vector2> directions,
        IReadOnlyList<LineArea> lines,
        AbilitySpec spec)
    {
        float spacing = Mathf.Max(0.1f, radialExplosionSpacing);
        float maxLength = 0f;
        for (int i = 0; i < lines.Count; i++)
            maxLength = Mathf.Max(maxLength, lines[i].Length);

        HashSet<GameObject> fallbackDamagedTargets = new();
        TimedAnimatedHitEffect2D.SharedHitRegistry sharedHitRegistry = new();
        for (float distance = spacing; distance <= maxLength + 0.01f; distance += spacing)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            bool stepPresentationPlayed = false;
            void PlayStepPresentationOnce(Vector2 presentationCenter)
            {
                if (stepPresentationPlayed)
                    return;

                stepPresentationPlayed = true;
                PlayPatternSound(demon, radialExplosionSound, presentationCenter, this);
                PlayPatternShake(demon, radialExplosionCameraShake, Vector2.down, "DemonKing.ExplosionJumpRadialExplosion");
            }

            for (int i = 0; i < directions.Count && i < lines.Count; i++)
            {
                if (distance > lines[i].Length + 0.01f)
                    continue;

                Vector2 direction = directions[i].sqrMagnitude > 0.0001f ? directions[i].normalized : Vector2.right;
                Vector2 center = origin + direction * distance;
                CombatHitPayload payload = DemonKingCombatUtil.MakePayload(
                    demon,
                    demon.DefaultDamageEffect,
                    radialDamage,
                    knockback);
                DemonKingAnimationClipVisual radialExplosion = DemonKingPatternVfx.SpawnExplosion(center, radialExplosionDiameter);
                bool timed = DemonKingPatternVfx.TryPlayCircleTimedHit(
                    radialExplosion,
                    demon,
                    radialExplosionDiameter,
                    payload,
                    sharedHitRegistry,
                    () => PlayStepPresentationOnce(center));

                if (!timed)
                {
                    PlayStepPresentationOnce(center);
                    DemonKingCombatUtil.ApplyCircleDamage(
                        demon,
                        center,
                        radialExplosionDiameter * 0.5f,
                        demon.DefaultDamageEffect,
                        radialDamage,
                        fallbackDamagedTargets,
                        knockback);

                    if (radialExplosion == null)
                    {
                        DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
                            center,
                            radialExplosionDiameter,
                            AttackSquareColor,
                            "DemonKing_RadialExplosionCircleAttack");
                    }
                }
            }

            if (radialExplosionStepInterval > 0f)
                yield return WaitForSecondsUnlessCancelled(radialExplosionStepInterval, spec);
        }
    }
}

public class AbilityLogic_DemonKingRecallEgoSword : AbilityLogic_DemonKingBase
{
    [SerializeField, Min(0.1f)] private float recallSpeedMultiplier = 5f;
    [SerializeField, Min(0.1f)] private float timeoutSeconds = 2.5f;
    [SerializeField, Min(0f)] private float recoverEndPoseHoldSeconds = 0.16f;
    [SerializeField] private SoundRef recallStartSound;
    [SerializeField] private SoundRef recallCompleteSound;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        EgoSwordActor sword = demon != null ? demon.EgoSword : null;
        if (demon == null || sword == null)
            yield break;

        demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordHandSwordRecoverState);
        try
        {
            PlayPatternSound(demon, recallStartSound, sword.transform.position, this);
            sword.Recall(demon.PlayerMoveSpeedReference * recallSpeedMultiplier);

            float elapsed = 0f;
            while (!sword.IsHeld && elapsed < timeoutSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!sword.IsHeld)
                sword.CompleteRecallAtOwner();

            PlayPatternSound(demon, recallCompleteSound, demon.transform.position, this);
            demon.ReleasePatternAnimationHold();
            demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordHandSwordRecoverState);
            float finalFrameSeconds = demon.ResolvePatternAnimationFrameSeconds(
                DemonKingController.DarkLordHandSwordRecoverState);
            yield return WaitForSecondsUnlessCancelled(
                Mathf.Max(finalFrameSeconds, recoverEndPoseHoldSeconds),
                spec);
        }
        finally
        {
            demon.ReleasePatternAnimationHold();
        }
    }
}

public class AbilityLogic_DemonKingEgoSwordVerticalStrike : AbilityLogic_DemonKingBase
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        EgoSwordActor sword = demon != null ? demon.EgoSword : null;
        if (sword == null)
            yield break;

        yield return sword.RunVerticalStrikeAbilityPattern(spec);
    }
}

public class AbilityLogic_DemonKingEgoSwordCrossLaser : AbilityLogic_DemonKingBase
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        EgoSwordActor sword = demon != null ? demon.EgoSword : null;
        if (sword == null)
            yield break;

        yield return sword.RunCrossLaserAbilityPattern(spec);
    }
}

public class AbilityLogic_DemonKingWallBounceRush : AbilityLogic_DemonKingBase
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
    [SerializeField, Min(0f)] private float finalJumpArcHeight = 1.35f;
    [SerializeField, Range(0f, 1f)] private float finalLandingFrameSwitchRatio = 0.78f;
    [SerializeField, Min(0f)] private float rushEndPoseHoldSeconds = 0.1f;
    [SerializeField, Min(0f)] private float finalLandingPoseHoldSeconds = 0.14f;
    [SerializeField] private SoundRef rushStartSound;
    [SerializeField] private SoundRef rushEndpointSound;
    [SerializeField] private SoundRef finalLandingSound;
    [SerializeField] private CameraShakeHook rushEndpointCameraShake = CameraShakeHook.Create(0.08f, 1f, 0.18f, 0.08f);
    [SerializeField] private CameraShakeHook finalLandingCameraShake = CameraShakeHook.Create(0.18f, 1f, 0.35f, 0.04f);

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
        EntityCollisionProfile2D collisionProfile = demon.GetComponent<EntityCollisionProfile2D>();
        collisionProfile?.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);
        demon.PushFaceTargetLock();
        demon.PushThresholdStaggerGuard();

        try
        {
            Vector2 retreatStart = demon.transform.position;
            Vector2 awayFromPlayer = -demon.GetDirectionToTargetOrFacing(retreatStart);
            float retreatDistance = demon.PlayerDashDistanceReference;
            demon.FacePatternDirection(-awayFromPlayer);
            if (demon.RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold)
                demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordSwordDashStabReadyState);
            demon.BeginBodyAfterimage();
            try
            {
                if (motion != null && retreatDistance > 0f)
                    motion.StartLunge(retreatStart, awayFromPlayer, retreatDistance, retreatSeconds, 1f);
                else
                    demon.transform.position = retreatStart + awayFromPlayer * retreatDistance;

                yield return WaitForSecondsUnlessCancelled(retreatSeconds, spec);
            }
            finally
            {
                demon.StopBodyAfterimage();
            }

            if (IsAbilityCancelled(spec))
                yield break;

            Vector2 warningStart = demon.transform.position;
            Vector2 direction = demon.GetDirectionToTargetOrFacing(warningStart);
            if (demon.RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold)
                demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordSwordDashStabReadyState);
            LineArea warningLine = ResolveForwardLineArea(demon, warningStart, direction, hitWidth, fallbackRushDistance);
            AttackTelegraphView warningView = ShowLineAreaWarning(demon, warningLine, warningSeconds);

            float elapsed = 0f;
            while (elapsed < warningSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                direction = demon.GetDirectionToTargetOrFacing(warningStart);
                warningLine = ResolveForwardLineArea(demon, warningStart, direction, hitWidth, fallbackRushDistance);
                UpdateLineAreaWarning(warningView, demon, warningLine, warningSeconds);

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
                string rushState = demon.RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold
                    ? DemonKingController.DarkLordSwordDashStabState
                    : DemonKingController.DarkLordHandJumpAttackState;
                if (demon.RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold)
                    demon.PlayPatternAnimation(rushState);
                else
                    demon.HoldPatternAnimationFirstFrame(rushState);
                float distance = Mathf.Max(0.5f, ResolveWallDistance(demon, start, direction, fallbackRushDistance) - 0.35f);
                float rushSeconds = SecondsForSpeed(distance, rushSpeed, 0.2f);

                Vector3 chargeLoopOffset = demon.ResolveVfxSocketLocal(DemonKingVfxSocketId.ChargeLoop, Vector2.zero);
                DemonKingAnimationClipVisual chargeLoop = DemonKingPatternVfx.SpawnChargeLoop(demon.transform, direction, chargeLoopOffset);
                PlayPatternSound(demon, rushStartSound, start, this);
                try
                {
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
                        spec,
                        lungeEaseOutPower: 1f);
                }
                finally
                {
                    chargeLoop?.StopAndRelease();
                }

                if (IsAbilityCancelled(spec))
                    yield break;

                demon.transform.position = start + direction * distance;
                Vector2 chargeDisappearCenter = demon.ResolveVfxSocketWorld(DemonKingVfxSocketId.ChargeDisappear, Vector2.zero);
                DemonKingPatternVfx.SpawnChargeDisappear(chargeDisappearCenter, direction);
                PlayPatternSound(demon, rushEndpointSound, chargeDisappearCenter, this);
                PlayPatternShake(demon, rushEndpointCameraShake, direction, "DemonKing.WallBounceRushEndpoint");
                if (demon.RuntimeData.SwordMode == DemonKingEgoSwordMode.Hold)
                    demon.HoldPatternAnimationFirstFrame(DemonKingController.DarkLordSwordDashStabReadyState);
                else
                    demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordHandJumpAttackState);

                yield return WaitForSecondsUnlessCancelled(Mathf.Max(0.1f, rushEndPoseHoldSeconds), spec);

                direction = demon.GetDirectionToTargetOrFacing(demon.transform.position);
            }

            yield return RunFinalJump(demon, spec);
        }
        finally
        {
            demon.StopBodyAfterimage();
            demon.PopThresholdStaggerGuard();
            collisionProfile?.RestoreDefaultMode();
            motion?.CancelMotion();
            demon.PopFaceTargetLock();
        }

        demon.MarkHp50PatternUsed();
    }

    private IEnumerator RunFinalJump(
        DemonKingController demon,
        AbilitySpec spec)
    {
        Vector2 start = demon.transform.position;
        Vector2 target = demon.CurrentTarget != null ? (Vector2)demon.CurrentTarget.position : (Vector2)demon.ArenaCenterPosition;
        Vector2 delta = target - start;
        demon.FacePatternDirection(delta);
        Vector2 impactCenter = demon.ResolveVfxSocketWorldAtBasePosition(
            DemonKingVfxSocketId.FootLandingImpact,
            target,
            Vector2.zero);
        ShowCircleWarning(demon, impactCenter, finalImpactDiameter, finalJumpSeconds);

        demon.BeginBodyAfterimage();
        try
        {
            yield return RunParabolicPatternJump(
                demon,
                start,
                target,
                finalJumpSeconds,
                finalJumpArcHeight,
                finalLandingFrameSwitchRatio,
                spec);
        }
        finally
        {
            demon.StopBodyAfterimage();
        }

        if (IsAbilityCancelled(spec))
            yield break;

        demon.HoldPatternAnimationLastFrame(DemonKingController.DarkLordHandJumpAttackState);
        impactCenter = demon.ResolveVfxSocketWorld(DemonKingVfxSocketId.FootLandingImpact, Vector2.zero);
        DemonKingAnimationClipVisual finalImpact = DemonKingPatternVfx.SpawnImpact(impactCenter, finalImpactDiameter);
        TryPlayTimedCircleDamage(
            finalImpact,
            demon,
            impactCenter,
            finalImpactDiameter,
            finalImpactDamage,
            knockback,
            finalLandingSound,
            finalLandingCameraShake,
            this,
            "DemonKing.WallBounceRushFinalLanding");
        DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
            impactCenter,
            finalImpactDiameter,
            AttackSquareColor,
            "DemonKing_FinalJumpCircleAttack");
        if (finalLandingPoseHoldSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(finalLandingPoseHoldSeconds, spec);
    }
}

public class AbilityLogic_DemonKingGroggyRecoverCounter : AbilityLogic_DemonKingBase
{
    private const float DimFadeOutRatio = 0.45f;
    private const float EyeFlashHoldRatio = 0.2f;
    private const float ImpactPoseHoldSeconds = 0.5f;

    [SerializeField, Min(0f)] private float attackDelaySeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float explosionDiameter = 5.4f;
    [SerializeField, Min(0f)] private float damage = 2f;
    [SerializeField, Min(0f)] private float knockback = 14f;
    [SerializeField, Range(0f, 1f)] private float dimTargetAlpha = 0.55f;
    [SerializeField] private Vector2 eyeFlashLocalOffset = new(0f, 0.75f);
    [SerializeField] private Vector2 eyeFlashSize = new(2.4f, 0.9f);
    [SerializeField, Min(0f)] private float counterEndPoseHoldSeconds = 0.12f;
    [SerializeField] private SoundRef warningPingSound;
    [SerializeField] private SoundRef counterImpactSound;
    [SerializeField] private CameraShakeHook counterImpactCameraShake = CameraShakeHook.Create(0.2f, 1f, 0.38f, 0.04f);

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        string counterState = demon.ResolveGroggyCounterAnimationState();
        demon.PushFaceTargetLock();
        DemonKingWorldDimmingOverlay dimming = DemonKingWorldDimmingOverlay.Begin(demon, 0f);
        try
        {
            float fadeOutSeconds = attackDelaySeconds * DimFadeOutRatio;
            float eyeFlashHoldSeconds = attackDelaySeconds * EyeFlashHoldRatio;
            float fadeInSeconds = Mathf.Max(0f, attackDelaySeconds - fadeOutSeconds - eyeFlashHoldSeconds);

            demon.HoldGroggyPoseAnimation(allowDuringGroggy: true);
            yield return FadeDimming(dimming, dimTargetAlpha, fadeOutSeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            demon.HoldGroggyPoseAnimation(allowDuringGroggy: true);
            Vector3 eyeFlashOffset = demon.ResolveVfxSocketLocal(DemonKingVfxSocketId.EyeFlash, eyeFlashLocalOffset);
            DemonKingPatternVfx.SpawnEyeFlash(demon.transform, (Vector2)eyeFlashOffset, eyeFlashSize);
            PlayWarningPing(demon);

            yield return WaitForSecondsUnlessCancelled(eyeFlashHoldSeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            yield return FadeDimming(dimming, 0f, fadeInSeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;

            yield return PlayCounterImpact(demon, counterState, spec);
        }
        finally
        {
            if (dimming != null)
                dimming.Release();
            demon.PopFaceTargetLock();
        }
    }

    private IEnumerator PlayCounterImpact(DemonKingController demon, string counterState, AbilitySpec spec)
    {
        if (demon.PlayPatternAnimationOncePerPattern(counterState, allowDuringGroggy: true))
        {
            float releaseDelaySeconds = demon.ResolvePatternAnimationLastFrameStartDelay(counterState);
            yield return WaitForSecondsUnlessCancelled(releaseDelaySeconds, spec);
            if (IsAbilityCancelled(spec))
                yield break;
        }

        demon.HoldPatternAnimationLastFrame(counterState, allowDuringGroggy: true);
        DemonKingVfxSocketId socketId = counterState == DemonKingController.DarkLordSwordGroggyCounterState
            ? DemonKingVfxSocketId.SwordSlashOrigin
            : DemonKingVfxSocketId.HandCounterImpact;
        Vector2 center = demon.ResolveVfxSocketWorld(socketId, Vector2.zero);
        DemonKingAnimationClipVisual counterImpact = counterState == DemonKingController.DarkLordSwordGroggyCounterState
            ? DemonKingPatternVfx.SpawnGroggyRelease(center, explosionDiameter)
            : DemonKingPatternVfx.SpawnImpact(center, explosionDiameter);
        TryPlayTimedCircleDamage(
            counterImpact,
            demon,
            center,
            explosionDiameter,
            damage,
            knockback,
            counterImpactSound,
            counterImpactCameraShake,
            this,
            "DemonKing.GroggyRecoverCounterImpact");

        float holdSeconds = Mathf.Max(counterEndPoseHoldSeconds, ImpactPoseHoldSeconds);
        if (holdSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(holdSeconds, spec);

        if (!IsAbilityCancelled(spec))
            demon.RestoreCombatPose();
    }

    private static IEnumerator FadeDimming(
        DemonKingWorldDimmingOverlay dimming,
        float targetAlpha,
        float duration,
        AbilitySpec spec)
    {
        if (dimming == null)
        {
            yield return WaitForSecondsUnlessCancelled(duration, spec);
            yield break;
        }

        float startAlpha = dimming.Alpha;
        if (duration <= 0f)
        {
            dimming.SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            dimming.SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        dimming.SetAlpha(targetAlpha);
    }

    private void PlayWarningPing(DemonKingController demon)
    {
        if (demon == null)
            return;

        SoundRef resolvedWarningPingSound = warningPingSound.IsSet
            ? warningPingSound
            : demon.GroggyRecoverCounterWarningPingSound;
        if (!resolvedWarningPingSound.IsSet)
            return;

        SoundPlaybackUtility.Play(
            resolvedWarningPingSound,
            instigator: demon.gameObject,
            causer: demon.gameObject,
            position: demon.transform.position,
            sourceObject: demon);
    }
}

public class AbilityLogic_DemonKingFinalDesperation : AbilityLogic_DemonKingBase
{
    private const string FinalLaserVfxResourcePath = "DemonKing/DemonKingEgoLaserVfx";

    [SerializeField, Min(0.01f)] private float moveToCenterSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float openingKnockbackDiameter = 40f;
    [SerializeField, Min(0f)] private float openingKnockbackDamage = 0f;
    [SerializeField, Min(0f)] private float openingKnockback = 26f;
    [SerializeField, Min(0f)] private float bombIntervalSeconds = 0.2f;
    [SerializeField, Min(0f)] private float bombWarningSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float bombDiameter = 2.1f;
    [SerializeField, Min(0f)] private float bombDamage = 1f;
    [SerializeField, Min(0.1f)] private float bombOffsetRange = 5f;
    [SerializeField, Min(0f)] private float laserWarningSeconds = 1f;
    [SerializeField, Min(0f)] private float laserAttackSeconds = 1f;
    [SerializeField, Min(0.1f)] private float laserWidth = 0.75f;
    [SerializeField, Min(0f)] private float laserVfxRayOriginOffset = 0.35f;
    [SerializeField, Min(0.1f)] private float fallbackLaserLength = 40f;
    [SerializeField, Min(0f)] private float laserDamage = 1f;
    [SerializeField] private SoundRef startSound;
    [SerializeField] private SoundRef bombExplosionSound;
    [SerializeField] private SoundRef laserWarningSound;
    [SerializeField] private SoundRef laserFireSound;
    [SerializeField] private CameraShakeHook openingCameraShake = CameraShakeHook.Create(0.18f, 1f, 0.35f, 0.04f);
    [SerializeField] private CameraShakeHook bombExplosionCameraShake = CameraShakeHook.Create(0.08f, 1f, 0.18f, 0.08f);

    private DemonKingEgoLaserVfx finalLaserVfxPrefab;
    private bool finalLaserVfxMissingLogged;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DemonKingController demon = GetDemonKing(system);
        if (demon == null)
            yield break;

        demon.PushThresholdStaggerGuard();
        try
        {
            demon.MarkFinalDesperationStarted();
            demon.CompleteEgoSwordRecall();

            AbilityMotionController2D motion = demon.GetComponent<AbilityMotionController2D>();
            Vector2 center = demon.ArenaCenterPosition;
            Vector2 start = demon.transform.position;
            Vector2 delta = center - start;
            demon.BeginBodyAfterimage();
            try
            {
                if (delta.sqrMagnitude > 0.0001f && motion != null)
                    motion.StartLunge(start, delta.normalized, delta.magnitude, moveToCenterSeconds, 1.8f);
                else
                    demon.transform.position = center;

                yield return WaitForSecondsUnlessCancelled(moveToCenterSeconds, spec);
            }
            finally
            {
                demon.StopBodyAfterimage();
            }

            if (IsAbilityCancelled(spec) || demon.IsDead)
                yield break;

            demon.transform.position = center;
            demon.HoldPatternAnimationFirstFrame(
                DemonKingController.DarkLord10PercentState,
                allowDuringFinalDesperation: true);
            PlayPatternSound(demon, startSound, center, this);
            PlayPatternShake(demon, openingCameraShake, Vector2.down, "DemonKing.FinalDesperationStart");
            DemonKingCombatUtil.ApplyCircleDamage(
                demon,
                center,
                openingKnockbackDiameter * 0.5f,
                demon.DefaultDamageEffect,
                openingKnockbackDamage,
                knockbackImpulse: openingKnockback);
            demon.ReleaseFinalDesperationHealthClamp();

            float bombTimer = 0f;

            IEnumerator RunLaserStep(Vector2 firstDirection, Vector2 secondDirection)
            {
                center = demon.ArenaCenterPosition;
                LineArea firstLine = ResolveFullLineArea(demon, center, firstDirection, laserWidth, fallbackLaserLength);
                LineArea secondLine = ResolveFullLineArea(demon, center, secondDirection, laserWidth, fallbackLaserLength);
                PlayPatternSound(demon, laserWarningSound, center, this);
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

                DemonKingEgoLaserVfx[] firstLaserVfx = SpawnFinalLaserVfx(demon, center, firstDirection);
                DemonKingEgoLaserVfx[] secondLaserVfx = SpawnFinalLaserVfx(demon, center, secondDirection);
                PlayPatternSound(demon, laserFireSound, center, this);
                bool usingAnimatedVfx = HasAnyFinalLaserVfx(firstLaserVfx) || HasAnyFinalLaserVfx(secondLaserVfx);
                if (!usingAnimatedVfx)
                {
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
                }

                HashSet<GameObject> damagedTargets = new();
                float attackElapsed = 0f;
                while (usingAnimatedVfx ? IsAnyFinalLaserVfxPlaying(firstLaserVfx, secondLaserVfx) : attackElapsed < laserAttackSeconds)
                {
                    if (IsAbilityCancelled(spec) || demon.IsDead)
                        yield break;

                    bombTimer = TickFinalBombardment(demon, bombTimer, Time.fixedDeltaTime);
                    if (!usingAnimatedVfx || IsAnyFinalLaserDamageActive(firstLaserVfx))
                        ApplyLineAreaDamage(demon, firstLine, laserDamage, damagedTargets);
                    if (!usingAnimatedVfx || IsAnyFinalLaserDamageActive(secondLaserVfx))
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
        finally
        {
            demon.StopBodyAfterimage();
            demon.ReleaseFinalDesperationHealthClamp();
            demon.PopThresholdStaggerGuard();
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

            Vector2 offset = Random.insideUnitCircle * bombOffsetRange;

            DemonKingDelayedDamageArea.SpawnCircle(
                demon,
                targetCenter + offset,
                bombDiameter,
                bombWarningSeconds,
                bombDamage,
                ignoreOwnerGroggy: true,
                explosionVfxKind: DemonKingDelayedExplosionVfxKind.DarkLordExplosion2,
                impactSound: bombExplosionSound,
                impactCameraShake: bombExplosionCameraShake);

            timer += Mathf.Max(0.01f, bombIntervalSeconds);
        }

        return timer;
    }

    private DemonKingEgoLaserVfx[] SpawnFinalLaserVfx(DemonKingController demon, Vector2 origin, Vector2 direction)
    {
        DemonKingEgoLaserVfx prefab = ResolveFinalLaserVfxPrefab();
        if (demon == null || prefab == null)
            return null;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float forwardLength = ResolveWallDistance(demon, origin, safeDirection, fallbackLaserLength * 0.5f);
        float backwardLength = ResolveWallDistance(demon, origin, -safeDirection, fallbackLaserLength * 0.5f);
        float forwardOffset = ResolveFinalLaserVfxRayOriginOffset(forwardLength);
        float backwardOffset = ResolveFinalLaserVfxRayOriginOffset(backwardLength);
        Vector2 backwardDirection = -safeDirection;

        return new[]
        {
            SpawnFinalLaserRay(prefab, origin + safeDirection * forwardOffset, safeDirection, forwardLength - forwardOffset),
            SpawnFinalLaserRay(prefab, origin + backwardDirection * backwardOffset, backwardDirection, backwardLength - backwardOffset)
        };
    }

    private float ResolveFinalLaserVfxRayOriginOffset(float rayLength)
    {
        return Mathf.Clamp(laserVfxRayOriginOffset, 0f, Mathf.Max(0f, rayLength - 0.01f));
    }

    private DemonKingEgoLaserVfx SpawnFinalLaserRay(
        DemonKingEgoLaserVfx prefab,
        Vector2 origin,
        Vector2 direction,
        float length)
    {
        if (prefab == null || length <= 0.01f)
            return null;

        DemonKingEgoLaserVfx instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = "DemonKing_FinalLaserAnimatedAttack";
        instance.Play(origin, direction, length, laserWidth, laserAttackSeconds);
        return instance;
    }

    private DemonKingEgoLaserVfx ResolveFinalLaserVfxPrefab()
    {
        if (finalLaserVfxPrefab != null)
            return finalLaserVfxPrefab;

        GameObject prefabObject = Resources.Load<GameObject>(FinalLaserVfxResourcePath);
        if (prefabObject != null)
            finalLaserVfxPrefab = prefabObject.GetComponent<DemonKingEgoLaserVfx>();

        if (finalLaserVfxPrefab == null && !finalLaserVfxMissingLogged)
        {
            finalLaserVfxMissingLogged = true;
            Debug.LogWarning(
                $"DemonKing FinalDesperation could not load animated laser VFX at Resources/{FinalLaserVfxResourcePath}. Falling back to primitive laser visuals.",
                this);
        }

        return finalLaserVfxPrefab;
    }

    private static bool HasAnyFinalLaserVfx(DemonKingEgoLaserVfx[] views)
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

    private static bool IsAnyFinalLaserDamageActive(DemonKingEgoLaserVfx[] views)
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

    private static bool IsAnyFinalLaserVfxPlaying(params DemonKingEgoLaserVfx[][] viewGroups)
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
}
