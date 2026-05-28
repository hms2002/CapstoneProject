using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 2페이즈 원거리 퀸 컨트롤러입니다.
/// </summary>
public sealed class SlimeQueenP2Long : SlimeQueenPhaseTwoBase, ISlimeQueenRandomJumpHost
{
    private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
    private static readonly int IsSinkingHash = Animator.StringToHash("isSinking");
    private static readonly int IdleStateHash = Animator.StringToHash("SlimeQueenC_Idle");

    private const int ToxicDropPositionCount = 3;
    private const float ToxicDropLowerTriangleY = -0.5f;
    private const float ToxicDropTriangleX = 0.8660254f;

    [Header("Phase 2 Long - Random Movement")]
    [Tooltip("랜덤 착지 위치를 뽑을 바운더리입니다. 비워두면 씬에서 자동 탐색합니다.")]
    [SerializeField] private SlimeQueenRandomMoveBounds randomMoveBounds;

    [Tooltip("점프 착지 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle jumpWarningStyle;

    [Tooltip("점프 착지 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float jumpWarningDiameter = 2.4f;

    [Tooltip("랜덤 위치까지 점프 이동하는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float jumpDurationSeconds = 1.6f;

    [Tooltip("착지 위치 위로 올라가 체공할 높이입니다.")]
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

    [Space(8)]

    [Header("Phase 2 Long - Water Cannon")]
    [Tooltip("물대포 조준 경고선 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle waterCannonWarningStyle;

    [Tooltip("물대포 발사 중 표시할 선택적 AttackTelegraph 스타일입니다. 비워두면 물대포 비주얼 프리팹만 표시합니다.")]
    [SerializeField] private AttackTelegraphStyle waterCannonBeamStyle;

    [Tooltip("물대포 발사 중 길어지는 파란 막대기 비주얼 프리팹입니다.")]
    [SerializeField] private GameObject waterCannonBeamVisualPrefab;

    [Tooltip("물대포 조준 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float waterCannonWarningSeconds = 1.4f;

    [Tooltip("물대포가 고정 방향으로 발사되는 시간입니다.")]
    [SerializeField, Min(0f)] private float waterCannonActiveSeconds = 2f;

    [Tooltip("물대포 막대기가 최대 길이까지 뻗는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0f)] private float waterCannonBeamGrowSeconds = 0.18f;

    [Tooltip("보이는 물대포 물줄기 폭입니다.")]
    [SerializeField, Min(0.05f)] private float waterCannonVisualWidth = 1f;

    [Tooltip("실제 물대포 피해 판정 폭입니다. 시각 폭보다 작게 사용합니다.")]
    [SerializeField, Min(0.05f)] private float waterCannonHitWidth = 0.45f;

    [Tooltip("물대포가 충돌해 멈출 벽 레이어입니다.")]
    [SerializeField] private LayerMask waterCannonWallLayers = 1 << 30;

    [Tooltip("벽을 감지하지 못했을 때 사용할 최대 거리입니다.")]
    [SerializeField, Min(0.1f)] private float waterCannonFallbackDistance = 8f;

    [Tooltip("벽에 물대포가 겹치지 않도록 안쪽으로 줄일 거리입니다.")]
    [SerializeField, Min(0f)] private float waterCannonWallStopPadding = 0.05f;

    [Tooltip("물대포 접촉 시 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float waterCannonDamage = 1f;

    [Tooltip("물대포가 같은 패턴 중 피해를 다시 줄 수 있는 최소 간격입니다.")]
    [SerializeField, Min(0.05f)] private float waterCannonDamageIntervalSeconds = 1f;

    [Tooltip("물대포 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec waterCannonDamageEffect;

    [Space(8)]

    [Header("Phase 2 Long - Toxic Drop")]
    [Tooltip("독성 투하 착탄 지점 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle toxicDropWarningStyle;

    [Tooltip("독성 투하 착탄 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float toxicDropWarningSeconds = 1.4f;

    [Tooltip("플레이어를 중심으로 삼각형 착탄점을 만들 때 사용할 반지름입니다.")]
    [SerializeField, Min(0.1f)] private float toxicDropTriangleRadius = 1.2f;

    [Tooltip("독성 투하 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float toxicDropWarningDiameter = 1.5f;

    [Tooltip("착탄 지점까지 포물선으로 날아가는 초록 탄막 비주얼 프리팹입니다.")]
    [SerializeField] private SlimeQueenToxicDropProjectileVisual toxicDropProjectileVisualPrefab;

    [Tooltip("독성 탄막이 착탄 지점까지 날아가는 시간입니다.")]
    [SerializeField, Min(0.05f)] private float toxicDropProjectileFlightSeconds = 0.55f;

    [Tooltip("독성 탄막 포물선의 최고 높이입니다.")]
    [SerializeField, Min(0f)] private float toxicDropProjectileArcHeight = 1.8f;

    [Tooltip("독성 탄막 시작 위치의 보스 기준 Y 오프셋입니다.")]
    [SerializeField, Min(0f)] private float toxicDropProjectileStartHeight = 1f;

    [Tooltip("보스 스프라이트 기준 독성 탄막 렌더링 순서 보정값입니다.")]
    [SerializeField] private int toxicDropProjectileSortingOrderOffset = 20;

    [Tooltip("독성 투하가 착탄 지점에 남길 독구름 프리팹입니다.")]
    [SerializeField] private PoisonCloudArea toxicDropPoisonCloudPrefab;

    [Tooltip("생성된 독구름 피해 판정 반지름입니다.")]
    [SerializeField, Min(0.05f)] private float toxicDropPoisonCloudRadius = 0.75f;

    [Tooltip("생성된 독구름이 피해를 줄 수 있는 활성 시간입니다.")]
    [SerializeField, Min(0f)] private float toxicDropPoisonCloudActiveSeconds = 4f;

    [Tooltip("활성 시간이 끝난 뒤 피해 없이 투명해지는 시간입니다.")]
    [SerializeField, Min(0f)] private float toxicDropPoisonCloudFadeSeconds = 1f;

    [Tooltip("독구름이 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float toxicDropPoisonCloudDamage = 1f;

    [Tooltip("독구름 반복 피해 간격입니다.")]
    [SerializeField, Min(0.05f)] private float toxicDropPoisonCloudDamageIntervalSeconds = 1f;

    [Tooltip("독구름 피해에 사용할 GAS Damage Effect입니다. 비우면 프리팹 기본값을 사용합니다.")]
    [SerializeField] private GE_Damage_Spec toxicDropPoisonCloudDamageEffect;

    private readonly List<AttackTelegraphView> crossWaterPillarWarningViews = new List<AttackTelegraphView>();
    private readonly List<AttackTelegraphView> crossWaterPillarBlastViews = new List<AttackTelegraphView>();
    private readonly List<AttackTelegraphView> toxicDropWarningViews = new List<AttackTelegraphView>();
    private readonly List<SlimeQueenToxicDropProjectileVisual> toxicDropProjectileVisuals = new List<SlimeQueenToxicDropProjectileVisual>();
    private AttackTelegraphView waterCannonWarningView;
    private AttackTelegraphView waterCannonBeamView;
    private SlimeQueenWaterCannonBeamVisual waterCannonBeamVisual;
    private float waterCannonBeamStartTime;
    private Vector2 waterCannonLockedBeamDirection;
    private bool waterCannonHasLockedBeamDirection;
    private float nextWaterCannonDamageTime;
    private bool? hasSinkingParameter;

    public float JumpDurationSeconds => jumpDurationSeconds;
    public float CrossWaterPillarWarningSeconds => crossWaterPillarWarningSeconds;
    public float CrossWaterPillarBlastViewSeconds => crossWaterPillarBlastViewSeconds;
    public float WaterCannonWarningSeconds => waterCannonWarningSeconds;
    public float WaterCannonActiveSeconds => waterCannonActiveSeconds;
    public float ToxicDropWarningSeconds => Mathf.Max(0f, toxicDropWarningSeconds);
    public float ToxicDropProjectileFlightSeconds => Mathf.Max(0.01f, toxicDropProjectileFlightSeconds);

    public void BeginRandomJumpAnimation()
    {
        SetAnimatorBool(IsJumpingHash, true);
    }

    public void EndRandomJumpAnimation()
    {
        SetAnimatorBool(IsJumpingHash, false);
    }

    public override void BeginDrainSinkAnimation()
    {
        SetAnimatorBoolIfExists(IsSinkingHash, ref hasSinkingParameter, true);
    }

    public override void EndDrainSinkAnimation()
    {
        SetAnimatorBoolIfExists(IsSinkingHash, ref hasSinkingParameter, false);
    }

    protected override void ResetPatternAnimatorStateForInterrupt()
    {
        SetAnimatorBoolIfExists(IsJumpingHash, false);

        if (!HasGroggyTag())
            SetAnimatorBoolIfExists(IsSinkingHash, false);

        PlayAnimatorStateIfExists(IdleStateHash);
    }

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

    public readonly struct WaterCannonLine
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly Vector2 Center;
        public readonly Vector2 Direction;
        public readonly float Length;
        public readonly float RotationDegrees;

        public bool IsValid => Length > 0.05f;

        public WaterCannonLine(Vector2 start, Vector2 end, Vector2 direction)
        {
            Start = start;
            End = end;
            Center = (start + end) * 0.5f;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            Length = Vector2.Distance(start, end);
            RotationDegrees = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        }
    }

    protected override void OnDestroy()
    {
        CleanupToxicDropPresentation();
        CleanupWaterCannonPresentation();
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

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
            landingPosition,
            jumpWarningDiameter,
            jumpDurationSeconds,
            jumpWarningStyle));

        service.SpawnDetachedView(spec);
    }

    /// <summary>착지 위치 위로 빠르게 올라가 체공한 뒤 급강하하는 자세를 적용합니다.</summary>
    public void SetJumpPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        ApplyKnightStyleSlamPose(startPosition, landingPosition, normalizedTime, jumpArcHeight);
    }

    /// <summary>점프 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToJumpLanding(Vector3 landingPosition)
    {
        SnapToGroundedMotionLanding(landingPosition);
        EndRandomJumpAnimation();
    }

    private void SetAnimatorBool(int parameterHash, bool value)
    {
        if (animator == null)
            return;

        animator.SetBool(parameterHash, value);
    }

    private void SetAnimatorBoolIfExists(int parameterHash, ref bool? cachedExists, bool value)
    {
        if (animator == null)
            return;

        if (!cachedExists.HasValue)
            cachedExists = HasAnimatorBoolParameter(parameterHash);

        if (cachedExists.Value)
            animator.SetBool(parameterHash, value);
    }

    private bool HasAnimatorBoolParameter(int parameterHash)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    /// <summary>착지 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyJumpLandingDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
        PlayLightSlamLandingCameraShake("SlimeQueenP2Long.JumpLanding");

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

            AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateRectangle(
                segment.Center,
                new Vector2(segment.Length, crossWaterPillarWarningWidth),
                segment.RotationDegrees,
                crossWaterPillarWarningSeconds,
                crossWaterPillarWarningStyle));

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

    /// <summary>현재 플레이어 방향으로 물대포 조준 경고선을 표시합니다.</summary>
    public bool ShowWaterCannonWarning(GameObject explicitTarget)
    {
        CleanupWaterCannonPresentation();
        return UpdateWaterCannonWarning(explicitTarget);
    }

    /// <summary>발사 전 경고선을 현재 플레이어 방향으로 갱신합니다.</summary>
    public bool UpdateWaterCannonWarning(GameObject explicitTarget)
    {
        if (!TryBuildWaterCannonLine(explicitTarget, out WaterCannonLine line))
            return false;

        AttackTelegraphService service = GetTelegraphService();
        if (service != null)
        {
            AttackTelegraphSpec spec = WithThinWarningOutline(CreateWaterCannonSpec(
                line,
                waterCannonVisualWidth,
                waterCannonWarningSeconds,
                waterCannonWarningStyle));

            if (waterCannonWarningView == null)
                waterCannonWarningView = service.SpawnDetachedView(spec);
            else
                waterCannonWarningView.UpdateGeometry(spec);
        }

        return true;
    }

    /// <summary>물대포 발사 연출을 시작하고 첫 피해 판정을 실행합니다.</summary>
    public bool StartWaterCannonBeam(AbilitySystem sourceSystem, AbilitySpec sourceSpec, GameObject explicitTarget)
    {
        ClearView(ref waterCannonWarningView);
        ClearView(ref waterCannonBeamView);
        ClearWaterCannonBeamVisual();

        if (!TryLockWaterCannonBeamDirection(explicitTarget))
            return false;

        waterCannonBeamStartTime = Time.time;
        nextWaterCannonDamageTime = 0f;
        return UpdateWaterCannonBeam(sourceSystem, sourceSpec, explicitTarget);
    }

    /// <summary>발사 시작 시 고정한 방향으로 물대포 물줄기를 갱신하고 접촉 피해를 처리합니다.</summary>
    public bool UpdateWaterCannonBeam(AbilitySystem sourceSystem, AbilitySpec sourceSpec, GameObject explicitTarget)
    {
        if (!waterCannonHasLockedBeamDirection && !TryLockWaterCannonBeamDirection(explicitTarget))
            return false;

        if (!TryBuildWaterCannonLine(waterCannonLockedBeamDirection, out WaterCannonLine line))
            return false;

        AttackTelegraphService service = GetTelegraphService();
        if (service != null && waterCannonBeamStyle != null)
        {
            AttackTelegraphSpec spec = CreateWaterCannonSpec(
                line,
                waterCannonVisualWidth,
                waterCannonActiveSeconds,
                waterCannonBeamStyle);

            if (waterCannonBeamView == null)
                waterCannonBeamView = service.SpawnDetachedView(spec);
            else
                waterCannonBeamView.UpdateGeometry(spec);
        }

        UpdateWaterCannonBeamVisual(line);
        TryDamagePlayerInWaterCannon(sourceSystem, sourceSpec, line);
        return true;
    }

    /// <summary>물대포 경고와 발사 표시를 정리합니다.</summary>
    public void CleanupWaterCannonPresentation()
    {
        ClearView(ref waterCannonWarningView);
        ClearView(ref waterCannonBeamView);
        ClearWaterCannonBeamVisual();
    }

    /// <summary>현재 플레이어 위치를 중심으로 독성 투하 삼각형 착탄 지점 세 개를 계산합니다.</summary>
    public bool BuildToxicDropPositions(GameObject explicitTarget, List<Vector3> dropPositions)
    {
        if (dropPositions == null)
            return false;

        dropPositions.Clear();
        Transform targetTransform = explicitTarget != null ? explicitTarget.transform : CurrentTarget;
        if (targetTransform == null)
            return false;

        Vector3 center = targetTransform.position;
        center.z = transform.position.z;

        float radius = Mathf.Max(0.1f, toxicDropTriangleRadius);
        AddToxicDropPosition(dropPositions, center, new Vector2(0f, radius));
        AddToxicDropPosition(dropPositions, center, new Vector2(-ToxicDropTriangleX * radius, ToxicDropLowerTriangleY * radius));
        AddToxicDropPosition(dropPositions, center, new Vector2(ToxicDropTriangleX * radius, ToxicDropLowerTriangleY * radius));

        return dropPositions.Count == ToxicDropPositionCount;
    }

    /// <summary>독성 투하 착탄 경고 원 세 개를 동시에 표시합니다.</summary>
    public void ShowToxicDropWarnings(IReadOnlyList<Vector3> dropPositions)
    {
        ClearToxicDropWarnings();

        AttackTelegraphService service = GetTelegraphService();
        if (service == null || dropPositions == null)
            return;

        for (int i = 0; i < dropPositions.Count; i++)
        {
            AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
                dropPositions[i],
                toxicDropWarningDiameter,
                ToxicDropWarningSeconds,
                toxicDropWarningStyle));

            AttackTelegraphView view = service.SpawnDetachedView(spec);
            if (view != null)
                toxicDropWarningViews.Add(view);
        }
    }

    /// <summary>독성 투하 경고 표시를 즉시 제거합니다.</summary>
    public void ClearToxicDropWarnings()
    {
        ClearViews(toxicDropWarningViews);
    }

    /// <summary>독성 탄막 세 개를 각 착탄 지점까지 포물선으로 날립니다.</summary>
    public bool LaunchToxicDropProjectiles(
        IReadOnlyList<Vector3> dropPositions,
        WorldPresentationHook impactPresentation = default,
        Object presentationSourceObject = null)
    {
        ClearToxicDropProjectiles();

        if (toxicDropProjectileVisualPrefab == null || dropPositions == null)
            return false;

        Vector3 startPosition = GetToxicDropProjectileStartPosition();
        for (int i = 0; i < dropPositions.Count; i++)
        {
            SlimeQueenToxicDropProjectileVisual projectile = Instantiate(
                toxicDropProjectileVisualPrefab,
                startPosition,
                Quaternion.identity);

            if (projectile == null)
                continue;

            projectile.SyncSorting(sprite, toxicDropProjectileSortingOrderOffset);
            projectile.Begin(
                startPosition,
                dropPositions[i],
                ToxicDropProjectileFlightSeconds,
                toxicDropProjectileArcHeight,
                impactPresentation,
                presentationSourceObject);
            toxicDropProjectileVisuals.Add(projectile);
        }

        return toxicDropProjectileVisuals.Count > 0;
    }

    /// <summary>독성 탄막 비주얼이 모두 착탄했는지 확인합니다.</summary>
    public bool AreToxicDropProjectilesFinished()
    {
        for (int i = 0; i < toxicDropProjectileVisuals.Count; i++)
        {
            SlimeQueenToxicDropProjectileVisual projectile = toxicDropProjectileVisuals[i];
            if (projectile != null && !projectile.IsFinished)
                return false;
        }

        return true;
    }

    /// <summary>남아 있는 독성 탄막 비주얼을 제거합니다.</summary>
    public void ClearToxicDropProjectiles()
    {
        for (int i = 0; i < toxicDropProjectileVisuals.Count; i++)
        {
            SlimeQueenToxicDropProjectileVisual projectile = toxicDropProjectileVisuals[i];
            if (projectile != null)
                Destroy(projectile.gameObject);
        }

        toxicDropProjectileVisuals.Clear();
    }

    /// <summary>독성 투하 착탄 지점들에 독구름 장판을 생성합니다.</summary>
    public void SpawnToxicDropPoisonClouds(IReadOnlyList<Vector3> dropPositions, SoundRef poisonCloudLoopSound = default)
    {
        if (dropPositions == null)
            return;

        for (int i = 0; i < dropPositions.Count; i++)
            SpawnToxicDropPoisonCloud(dropPositions[i], poisonCloudLoopSound);
    }

    /// <summary>독성 투하 착탄 지점에 독구름 장판을 생성합니다.</summary>
    public void SpawnToxicDropPoisonCloud(Vector3 dropPosition, SoundRef poisonCloudLoopSound = default)
    {
        if (toxicDropPoisonCloudPrefab == null)
            return;

        PoisonCloudArea poisonCloud = Instantiate(toxicDropPoisonCloudPrefab, dropPosition, Quaternion.identity);
        poisonCloud.Initialize(
            toxicDropPoisonCloudRadius,
            toxicDropPoisonCloudActiveSeconds,
            toxicDropPoisonCloudFadeSeconds,
            toxicDropPoisonCloudDamage,
            toxicDropPoisonCloudDamageIntervalSeconds,
            toxicDropPoisonCloudDamageEffect,
            poisonCloudLoopSound);
    }

    /// <summary>독성 투하 경고와 탄막 표시를 정리합니다. 생성된 독구름은 자기 수명으로 소멸합니다.</summary>
    public void CleanupToxicDropPresentation()
    {
        ClearToxicDropWarnings();
        ClearToxicDropProjectiles();
    }

    /// <summary>독성 투하 탄막의 시작 위치를 계산합니다.</summary>
    private Vector3 GetToxicDropProjectileStartPosition()
    {
        Vector3 startPosition = transform.position;
        startPosition.y += toxicDropProjectileStartHeight;
        return startPosition;
    }

    /// <summary>독성 투하 삼각형 착탄 지점을 목록에 추가합니다.</summary>
    private static void AddToxicDropPosition(List<Vector3> positions, Vector3 center, Vector2 offset)
    {
        Vector3 position = center;
        position.x += offset.x;
        position.y += offset.y;
        positions.Add(position);
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

    /// <summary>현재 타겟 방향과 벽 충돌을 기준으로 물대포 선분을 만듭니다.</summary>
    private bool TryBuildWaterCannonLine(GameObject explicitTarget, out WaterCannonLine line)
    {
        Vector2 direction = ResolveWaterCannonDirection(explicitTarget);
        return TryBuildWaterCannonLine(direction, out line);
    }

    /// <summary>지정된 방향과 벽 충돌을 기준으로 물대포 선분을 만듭니다.</summary>
    private bool TryBuildWaterCannonLine(Vector2 direction, out WaterCannonLine line)
    {
        Vector2 start = transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            line = new WaterCannonLine();
            return false;
        }

        Vector2 safeDirection = direction.normalized;
        float distance = GetWaterCannonClampedDistance(start, safeDirection);
        Vector2 end = start + safeDirection * distance;
        line = new WaterCannonLine(start, end, safeDirection);
        return line.IsValid;
    }

    /// <summary>물대포 발사 순간의 방향을 고정합니다.</summary>
    private bool TryLockWaterCannonBeamDirection(GameObject explicitTarget)
    {
        Vector2 direction = ResolveWaterCannonDirection(explicitTarget);
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        waterCannonLockedBeamDirection = direction.normalized;
        waterCannonHasLockedBeamDirection = true;
        return true;
    }

    /// <summary>물대포가 향할 현재 플레이어 방향을 계산합니다.</summary>
    private Vector2 ResolveWaterCannonDirection(GameObject explicitTarget)
    {
        Transform targetTransform = explicitTarget != null ? explicitTarget.transform : CurrentTarget;
        if (targetTransform != null)
        {
            Vector2 toTarget = targetTransform.position - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    /// <summary>물대포가 벽 앞에서 멈추도록 허용 거리를 계산합니다.</summary>
    private float GetWaterCannonClampedDistance(Vector2 start, Vector2 direction)
    {
        float fallbackDistance = Mathf.Max(0.1f, waterCannonFallbackDistance);
        if (waterCannonWallLayers.value == 0)
            return fallbackDistance;

        RaycastHit2D hit = Physics2D.Raycast(
            start,
            direction,
            fallbackDistance,
            waterCannonWallLayers.value);

        if (hit.collider != null)
            return Mathf.Max(0.1f, hit.distance - waterCannonWallStopPadding);

        return fallbackDistance;
    }

    /// <summary>물대포 선분을 직사각형 텔레그래프 사양으로 변환합니다.</summary>
    private static AttackTelegraphSpec CreateWaterCannonSpec(
        WaterCannonLine line,
        float width,
        float duration,
        AttackTelegraphStyle style)
    {
        return AttackTelegraphSpec.CreateRectangle(
            line.Center,
            new Vector2(line.Length, Mathf.Max(0.05f, width)),
            line.RotationDegrees,
            duration,
            style);
    }

    /// <summary>물대포 발사 막대기 비주얼을 현재 선분에 맞춰 갱신합니다.</summary>
    private void UpdateWaterCannonBeamVisual(WaterCannonLine line)
    {
        if (waterCannonBeamVisualPrefab == null)
            return;

        if (waterCannonBeamVisual == null)
        {
            GameObject visualObject = Instantiate(waterCannonBeamVisualPrefab);
            waterCannonBeamVisual = visualObject != null
                ? visualObject.GetComponent<SlimeQueenWaterCannonBeamVisual>()
                : null;

            if (waterCannonBeamVisual == null)
            {
                if (visualObject != null)
                    Destroy(visualObject);

                return;
            }

            waterCannonBeamVisual.SyncSorting(sprite);
        }

        float normalizedGrow = waterCannonBeamGrowSeconds <= 0f
            ? 1f
            : Mathf.Clamp01((Time.time - waterCannonBeamStartTime) / waterCannonBeamGrowSeconds);

        waterCannonBeamVisual.Show(
            line.Start,
            line.Direction,
            line.Length,
            waterCannonVisualWidth,
            normalizedGrow);
    }

    /// <summary>물대포 실제 판정 박스 안에 있는 플레이어에게 피해를 적용합니다.</summary>
    private bool TryDamagePlayerInWaterCannon(AbilitySystem sourceSystem, AbilitySpec sourceSpec, WaterCannonLine line)
    {
        if (waterCannonDamage <= 0f || waterCannonDamageEffect == null)
            return false;

        if (Time.time < nextWaterCannonDamageTime)
            return false;

        float visualWidth = Mathf.Max(0.05f, waterCannonVisualWidth);
        float hitWidth = Mathf.Min(Mathf.Max(0.05f, waterCannonHitWidth), visualWidth);
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            line.Center,
            new Vector2(line.Length, hitWidth),
            line.RotationDegrees);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i];
            if (hitCollider == null || !HasPlayerTagInHierarchy(hitCollider.transform))
                continue;

            GameObject damageTarget = CombatTargetResolver2D.ResolveDamageTarget(hitCollider);
            if (damageTarget == null || !damageTarget.CompareTag("Player"))
                continue;

            CombatDamageAction.ApplyDamageAndEmitHit(
                sourceSystem != null ? sourceSystem : AbilitySystem,
                sourceSpec,
                waterCannonDamageEffect,
                null,
                damageTarget,
                waterCannonDamage,
                0f,
                0f,
                null,
                hitCollider.ClosestPoint(line.Center),
                gameObject);

            nextWaterCannonDamageTime = Time.time + Mathf.Max(0.05f, waterCannonDamageIntervalSeconds);
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

    /// <summary>생성된 물대포 막대기 비주얼을 제거합니다.</summary>
    private void ClearWaterCannonBeamVisual()
    {
        if (waterCannonBeamVisual != null)
            Destroy(waterCannonBeamVisual.gameObject);

        waterCannonBeamVisual = null;
        waterCannonLockedBeamDirection = Vector2.zero;
        waterCannonHasLockedBeamDirection = false;
    }

    /// <summary>생성된 단일 텔레그래프 뷰를 제거합니다.</summary>
    private static void ClearView(ref AttackTelegraphView view)
    {
        if (view != null)
            Destroy(view.gameObject);

        view = null;
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
