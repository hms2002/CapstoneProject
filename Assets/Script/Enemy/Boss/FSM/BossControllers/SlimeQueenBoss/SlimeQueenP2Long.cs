using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 2페이즈 원거리 퀸 컨트롤러입니다.
/// </summary>
public sealed class SlimeQueenP2Long : SlimeQueenPhaseTwoBase, ISlimeQueenRandomJumpHost
{
    [Header("Phase 2 Long - Random Movement")]
    [Tooltip("랜덤 착지 위치를 뽑을 바운더리입니다. 비워두면 씬에서 자동 탐색합니다.")]
    [SerializeField] private SlimeQueenRandomMoveBounds randomMoveBounds;

    [Tooltip("점프 착지 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle jumpWarningStyle;

    [Tooltip("점프 착지 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float jumpWarningDiameter = 2.4f;

    [Tooltip("랜덤 위치까지 점프 이동하는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float jumpDurationSeconds = 1.6f;

    [Tooltip("점프 중간 지점에서 올라갈 포물선 높이입니다.")]
    [SerializeField, Min(0f)] private float jumpArcHeight = 2.5f;

    [Tooltip("착지 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float jumpLandingDamageDiameter = 2.4f;

    [Tooltip("착지 시 플레이어에게 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float jumpLandingDamage = 1f;

    [Tooltip("착지 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec jumpLandingDamageEffect;

    [Space(8)]

    [Header("Phase 2 Long - Cross Water Pillar")]
    [Tooltip("팔방 물기둥 경고선 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle crossWaterPillarWarningStyle;

    [Tooltip("물기둥 발생 지점 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle crossWaterPillarBlastStyle;

    [Tooltip("경고선이 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float crossWaterPillarWarningSeconds = 1.4f;

    [Tooltip("경고선 직사각형의 폭입니다.")]
    [SerializeField, Min(0.05f)] private float crossWaterPillarWarningWidth = 0.35f;

    [Tooltip("경고선이 충돌해 멈출 벽 레이어입니다.")]
    [SerializeField] private LayerMask crossWaterPillarWallLayers = 1 << 30;

    [Tooltip("벽을 감지하지 못했을 때 사용할 한쪽 방향 최대 거리입니다.")]
    [SerializeField, Min(0.1f)] private float crossWaterPillarFallbackDistance = 8f;

    [Tooltip("벽에 경고선이 겹치지 않도록 안쪽으로 줄일 거리입니다.")]
    [SerializeField, Min(0f)] private float crossWaterPillarWallStopPadding = 0.05f;

    [Tooltip("경고선 위에 물기둥 지점을 배치하는 간격입니다.")]
    [SerializeField, Min(0.1f)] private float crossWaterPillarBlastInterval = 1.2f;

    [Tooltip("각 물기둥 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float crossWaterPillarBlastDiameter = 1.25f;

    [Tooltip("물기둥 지점 표시가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float crossWaterPillarBlastViewSeconds = 0.2f;

    [Tooltip("물기둥이 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float crossWaterPillarDamage = 1.5f;

    [Tooltip("물기둥 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec crossWaterPillarDamageEffect;

    private readonly List<AttackTelegraphView> crossWaterPillarWarningViews = new List<AttackTelegraphView>();
    private readonly List<AttackTelegraphView> crossWaterPillarBlastViews = new List<AttackTelegraphView>();

    public float JumpDurationSeconds => jumpDurationSeconds;
    public float CrossWaterPillarWarningSeconds => crossWaterPillarWarningSeconds;
    public float CrossWaterPillarBlastViewSeconds => crossWaterPillarBlastViewSeconds;

    public readonly struct CrossWaterPillarSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly Vector2 Center;
        public readonly Vector2 Direction;
        public readonly float Length;
        public readonly float RotationDegrees;

        public bool IsValid => Length > 0f;

        public CrossWaterPillarSegment(Vector2 start, Vector2 end, Vector2 direction)
        {
            Start = start;
            End = end;
            Center = (start + end) * 0.5f;
            Direction = direction.normalized;
            Length = Vector2.Distance(start, end);
            RotationDegrees = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        }
    }

    protected override void OnDestroy()
    {
        CleanupCrossWaterPillarPresentation();
        base.OnDestroy();
    }

    /// <summary>랜덤 착지 위치를 바운더리에서 가져옵니다.</summary>
    public bool TryGetRandomJumpLandingPosition(out Vector3 landingPosition)
    {
        SlimeQueenRandomMoveBounds bounds = ResolveRandomMoveBounds();
        if (bounds == null)
        {
            landingPosition = transform.position;
            return false;
        }

        return bounds.TryGetRandomPoint(transform.position.z, out landingPosition);
    }

    /// <summary>점프 착지 경고를 표시합니다.</summary>
    public void ShowJumpWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            landingPosition,
            jumpWarningDiameter,
            jumpDurationSeconds,
            jumpWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>점프 포물선 진행도에 맞춰 보스 위치를 이동시킵니다.</summary>
    public void SetJumpPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        Vector3 groundPosition = Vector3.Lerp(startPosition, landingPosition, clampedTime);
        float arcOffset = Mathf.Sin(clampedTime * Mathf.PI) * jumpArcHeight;

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = groundPosition + Vector3.up * arcOffset;
    }

    /// <summary>점프 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToJumpLanding(Vector3 landingPosition)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = landingPosition;
    }

    /// <summary>착지 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyJumpLandingDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
        if (jumpLandingDamage <= 0f || CurrentTarget == null || jumpLandingDamageEffect == null)
            return;

        float damageRadius = Mathf.Max(0.1f, jumpLandingDamageDiameter * 0.5f);
        float sqrDistance = ((Vector2)(CurrentTarget.position - landingPosition)).sqrMagnitude;
        if (sqrDistance > damageRadius * damageRadius)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            sourceSpec,
            jumpLandingDamageEffect,
            null,
            CurrentTarget.gameObject,
            jumpLandingDamage,
            0f,
            0f,
            null,
            landingPosition,
            gameObject);
    }

    /// <summary>팔방 물기둥 패턴의 네 개 양방향 경고선 정보를 만듭니다.</summary>
    public void BuildCrossWaterPillarSegments(List<CrossWaterPillarSegment> segments)
    {
        if (segments == null)
            return;

        segments.Clear();
        Vector2 center = transform.position;

        CrossWaterPillarSegment vertical = BuildCrossWaterPillarSegment(center, Vector2.up);
        if (vertical.IsValid)
            segments.Add(vertical);

        CrossWaterPillarSegment horizontal = BuildCrossWaterPillarSegment(center, Vector2.right);
        if (horizontal.IsValid)
            segments.Add(horizontal);

        CrossWaterPillarSegment diagonalUp = BuildCrossWaterPillarSegment(center, new Vector2(1f, 1f).normalized);
        if (diagonalUp.IsValid)
            segments.Add(diagonalUp);

        CrossWaterPillarSegment diagonalDown = BuildCrossWaterPillarSegment(center, new Vector2(1f, -1f).normalized);
        if (diagonalDown.IsValid)
            segments.Add(diagonalDown);
    }

    /// <summary>팔방 물기둥의 네 개 경고선을 표시합니다.</summary>
    public void ShowCrossWaterPillarWarnings(IReadOnlyList<CrossWaterPillarSegment> segments)
    {
        CleanupCrossWaterPillarPresentation();

        AttackTelegraphService service = GetTelegraphService();
        if (service == null || segments == null)
            return;

        for (int i = 0; i < segments.Count; i++)
        {
            CrossWaterPillarSegment segment = segments[i];
            if (!segment.IsValid)
                continue;

            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
                segment.Center,
                new Vector2(segment.Length, crossWaterPillarWarningWidth),
                segment.RotationDegrees,
                crossWaterPillarWarningSeconds,
                crossWaterPillarWarningStyle);

            AttackTelegraphView view = service.SpawnDetachedView(spec);
            if (view != null)
                crossWaterPillarWarningViews.Add(view);
        }
    }

    /// <summary>경고선 위 물기둥 지점을 표시하고 범위 안의 플레이어에게 피해를 적용합니다.</summary>
    public void FireCrossWaterPillars(AbilitySystem sourceSystem, AbilitySpec sourceSpec, IReadOnlyList<CrossWaterPillarSegment> segments)
    {
        ClearViews(crossWaterPillarWarningViews);
        ClearViews(crossWaterPillarBlastViews);

        if (segments == null || crossWaterPillarDamage <= 0f || crossWaterPillarDamageEffect == null)
            return;

        bool hasDamagedTarget = false;
        float interval = Mathf.Max(0.1f, crossWaterPillarBlastInterval);

        for (int i = 0; i < segments.Count; i++)
        {
            CrossWaterPillarSegment segment = segments[i];
            if (!segment.IsValid)
                continue;

            float lastOffset = 0f;
            for (float offset = 0f; offset <= segment.Length + 0.001f; offset += interval)
            {
                Vector3 blastPosition = segment.Start + segment.Direction * offset;
                SpawnWaterPillarBlastView(blastPosition);

                if (!hasDamagedTarget && TryDamagePlayerAtBlast(sourceSystem, sourceSpec, blastPosition))
                    hasDamagedTarget = true;

                lastOffset = offset;
            }

            if (segment.Length - lastOffset > crossWaterPillarBlastDiameter * 0.25f)
            {
                Vector3 blastPosition = segment.End;
                SpawnWaterPillarBlastView(blastPosition);

                if (!hasDamagedTarget && TryDamagePlayerAtBlast(sourceSystem, sourceSpec, blastPosition))
                    hasDamagedTarget = true;
            }
        }
    }

    /// <summary>팔방 물기둥 패턴이 남긴 경고와 물기둥 표시를 정리합니다.</summary>
    public void CleanupCrossWaterPillarPresentation()
    {
        ClearViews(crossWaterPillarWarningViews);
        ClearViews(crossWaterPillarBlastViews);
    }

    /// <summary>지정 방향의 양 끝을 벽 레이캐스트로 잘라 하나의 경고선 세그먼트를 만듭니다.</summary>
    private CrossWaterPillarSegment BuildCrossWaterPillarSegment(Vector2 center, Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        float forwardDistance = GetWallClampedDistance(center, safeDirection);
        float backwardDistance = GetWallClampedDistance(center, -safeDirection);
        Vector2 start = center - safeDirection * backwardDistance;
        Vector2 end = center + safeDirection * forwardDistance;
        return new CrossWaterPillarSegment(start, end, safeDirection);
    }

    /// <summary>지정 방향으로 벽까지의 경고선 허용 거리를 계산합니다.</summary>
    private float GetWallClampedDistance(Vector2 center, Vector2 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            center,
            direction,
            crossWaterPillarFallbackDistance,
            crossWaterPillarWallLayers.value);

        if (hit.collider != null)
            return Mathf.Max(0.1f, hit.distance - crossWaterPillarWallStopPadding);

        return crossWaterPillarFallbackDistance;
    }

    /// <summary>물기둥 발생 지점 표시를 생성합니다.</summary>
    private void SpawnWaterPillarBlastView(Vector3 blastPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            blastPosition,
            crossWaterPillarBlastDiameter,
            crossWaterPillarBlastViewSeconds,
            crossWaterPillarBlastStyle);

        AttackTelegraphView view = service.SpawnDetachedView(spec);
        if (view != null)
            crossWaterPillarBlastViews.Add(view);
    }

    /// <summary>물기둥 판정 원 안에 있는 플레이어에게 한 번 피해를 적용합니다.</summary>
    private bool TryDamagePlayerAtBlast(AbilitySystem sourceSystem, AbilitySpec sourceSpec, Vector3 blastPosition)
    {
        float radius = crossWaterPillarBlastDiameter * 0.5f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(blastPosition, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!HasPlayerTagInHierarchy(hits[i].transform))
                continue;

            GameObject damageTarget = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (damageTarget == null || !damageTarget.CompareTag("Player"))
                continue;

            CombatDamageAction.ApplyDamageAndEmitHit(
                sourceSystem != null ? sourceSystem : AbilitySystem,
                sourceSpec,
                crossWaterPillarDamageEffect,
                null,
                damageTarget,
                crossWaterPillarDamage,
                0f,
                0f,
                null,
                blastPosition,
                gameObject);
            return true;
        }

        return false;
    }

    /// <summary>랜덤 이동 바운더리 참조를 인스펙터 또는 씬 자동 탐색으로 해결합니다.</summary>
    private SlimeQueenRandomMoveBounds ResolveRandomMoveBounds()
    {
        if (randomMoveBounds == null)
            randomMoveBounds = FindAnyObjectByType<SlimeQueenRandomMoveBounds>();

        return randomMoveBounds;
    }

    /// <summary>생성된 텔레그래프 뷰 목록을 제거합니다.</summary>
    private static void ClearViews(List<AttackTelegraphView> views)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Count; i++)
        {
            AttackTelegraphView view = views[i];
            if (view != null)
                Destroy(view.gameObject);
        }

        views.Clear();
    }
}
