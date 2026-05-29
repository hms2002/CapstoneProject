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
    private const float MinCrossWaterPillarCastDistance = 40f;
    private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
    private static readonly int IsWaterCannonHash = Animator.StringToHash("isWaterCannon");
    private static readonly int IdleStateHash = Animator.StringToHash("SlimeQueenC_Idle");

    private const int ToxicDropPositionCount = 3;
    private const float ToxicDropLowerTriangleY = -0.5f;
    private const float ToxicDropTriangleX = 0.8660254f;
    private const string DefaultWaterCannonLaserVfxResourcePath = "DemonKing/WaterZetLaserVfx";
    private const string DefaultWaterCannonHitEffectResourcePath = "DemonKing/Effect_WaterjetHitparticle";

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

    [Tooltip("물대포 발사 중 사용할 레이저 비주얼 프리팹입니다. WaterZetLaserVfx가 있으면 우선 사용하고, 없으면 기존 막대기 비주얼로 처리합니다.")]
    [SerializeField] private GameObject waterCannonBeamVisualPrefab;

    [Tooltip("기존 물대포 레이저 시작 위치입니다. Center Socket이 비어 있을 때 호환용 기준점으로 사용합니다.")]
    [SerializeField] private Transform waterCannonMuzzleSocket;

    [Tooltip("물대포 조준 중심점입니다. 이 지점에서 플레이어 방향으로 Start Forward Offset만큼 민 위치가 실제 발사 시작점이 됩니다.")]
    [SerializeField] private Transform waterCannonCenterSocket;

    [Tooltip("물대포 중심점에서 플레이어 방향으로 밀어낼 발사 시작 거리입니다.")]
    [SerializeField, Min(0f)] private float waterCannonStartForwardOffset = 0f;

    [Tooltip("Slime Queen 물총 전용 레이저 VFX 프리팹입니다. 비워두면 Resources/DemonKing/WaterZetLaserVfx를 시도합니다.")]
    [SerializeField] private WaterZetLaserVfx waterCannonLaserVfxPrefab;

    [Tooltip("물총 전용 레이저 VFX fallback Resources 경로입니다.")]
    [SerializeField] private string waterCannonLaserVfxResourcePath = DefaultWaterCannonLaserVfxResourcePath;

    [Tooltip("물대포 연발 패턴의 총 지속 제한 시간입니다.")]
    [SerializeField, Min(0f)] private float waterCannonActiveSeconds = 2f;

    [Tooltip("각 물대포 샷 직전 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float waterCannonShotWarningSeconds = 0.25f;

    [Tooltip("각 물대포 레이저 샷이 유지되는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float waterCannonShotActiveSeconds = 0.12f;

    [Tooltip("물대포 샷 사이의 대기 시간입니다.")]
    [SerializeField, Min(0.01f)] private float waterCannonShotIntervalSeconds = 0.1f;

    [Tooltip("샷 1회마다 플레이어 방향으로 회전할 수 있는 최대 각도입니다.")]
    [SerializeField, Min(0f)] private float waterCannonMaxTurnAnglePerShot = 13f;

    [Tooltip("목표 방향과 아직 어긋나 있을 때 최소한 이 각도만큼 회전해 추적 감속을 늦춥니다.")]
    [SerializeField, Min(0f)] private float waterCannonMinTurnAnglePerShot = 8f;

    [Tooltip("목표 방향과 이 각도 이내로 가까워지면 최소 회전량을 적용하지 않고 정확히 목표 방향에 맞춥니다.")]
    [SerializeField, Min(0f)] private float waterCannonAimSnapAngle = 1.5f;

    [Tooltip("물대포 막대기가 최대 길이까지 뻗는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0f)] private float waterCannonBeamGrowSeconds = 0.18f;

    [Tooltip("실제 물대포 피해 판정 폭에 곱해 경고선 폭을 계산하는 배율입니다.")]
    [SerializeField, Min(0.1f)] private float waterCannonWarningWidthMultiplier = 4.125f;

    [Tooltip("물대포 레이저 VFX의 로컬 Y scale 축 폭입니다. 공격 판정 폭과 별도로 조정합니다.")]
    [SerializeField, Min(0.01f)] private float waterCannonVfxWidth = 0.165f;

    [Tooltip("Demon King 레이저 VFX 몸통 길이 보정값입니다. 1이면 wall-clipped 선분 길이를 넘지 않습니다.")]
    [SerializeField, Min(0.01f)] private float waterCannonLaserBodyLengthMultiplier = 1f;

    [Tooltip("실제 물대포 피해 판정 폭입니다. 시각 폭보다 작게 사용합니다.")]
    [SerializeField, Min(0.02f)] private float waterCannonHitWidth = 0.08f;

    [Tooltip("물대포 경고/레이저/피해 판정이 벽 검사 없이 고정으로 뻗는 거리입니다.")]
    [SerializeField, Min(0.1f)] private float waterCannonFixedDistance = 20f;

    [Tooltip("물대포가 고정 길이 안에서 벽에 닿을 때 출력할 물튀김 이펙트 프리팹입니다.")]
    [SerializeField] private GameObject waterCannonWallHitEffectPrefab;

    [Tooltip("물대포 벽 히트 이펙트 fallback Resources 경로입니다.")]
    [SerializeField] private string waterCannonWallHitEffectResourcePath = DefaultWaterCannonHitEffectResourcePath;

    [Tooltip("물대포 히트 이펙트를 출력할 벽 감지 레이어입니다. 레이저 길이는 줄이지 않고 이펙트 위치 계산에만 사용합니다.")]
    [SerializeField] private LayerMask waterCannonHitEffectWallLayers = 1 << 30;

    [Tooltip("벽 표면과 겹치지 않도록 히트 이펙트를 법선 방향으로 살짝 밀어내는 거리입니다.")]
    [SerializeField, Min(0f)] private float waterCannonHitEffectSurfaceOffset = 0.04f;

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
    private readonly List<GameObject> crossWaterPillarBlastEffects = new List<GameObject>();
    private readonly List<AttackTelegraphView> toxicDropWarningViews = new List<AttackTelegraphView>();
    private readonly List<SlimeQueenToxicDropProjectileVisual> toxicDropProjectileVisuals = new List<SlimeQueenToxicDropProjectileVisual>();
    private readonly List<AttackTelegraphView> waterCannonShotWarningViews = new List<AttackTelegraphView>();
    private readonly List<AttackTelegraphView> waterCannonShotBeamViews = new List<AttackTelegraphView>();
    private readonly List<WaterZetLaserVfx> waterCannonLaserVfxViews = new List<WaterZetLaserVfx>();
    private readonly List<GameObject> waterCannonWallHitEffectViews = new List<GameObject>();
    private AttackTelegraphView waterCannonWarningView;
    private AttackTelegraphView waterCannonBeamView;
    private SlimeQueenWaterCannonBeamVisual waterCannonBeamVisual;
    private Vector2 waterCannonLockedBeamDirection;
    private bool waterCannonHasLockedBeamDirection;
    public float JumpDurationSeconds => jumpDurationSeconds;
    public float CrossWaterPillarWarningSeconds => crossWaterPillarWarningSeconds;
    public float CrossWaterPillarBlastViewSeconds => crossWaterPillarBlastViewSeconds;
    public float WaterCannonLimitSeconds => Mathf.Max(0f, waterCannonActiveSeconds);
    public float WaterCannonShotWarningSeconds => Mathf.Max(0f, waterCannonShotWarningSeconds);
    public float WaterCannonShotActiveSeconds => Mathf.Max(0.01f, waterCannonShotActiveSeconds);
    public float WaterCannonShotIntervalSeconds => Mathf.Max(0.01f, waterCannonShotIntervalSeconds);
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

    protected override void ResetPatternAnimatorStateForInterrupt()
    {
        SetAnimatorBoolIfExists(IsJumpingHash, false);
        SetAnimatorBoolIfExists(IsWaterCannonHash, false);

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

    public void BeginWaterCannonAnimation()
    {
        SetAnimatorBoolIfExists(IsWaterCannonHash, true);
    }

    public void EndWaterCannonAnimation()
    {
        SetAnimatorBoolIfExists(IsWaterCannonHash, false);
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
    public void FireCrossWaterPillars(
        AbilitySystem sourceSystem,
        AbilitySpec sourceSpec,
        IReadOnlyList<CrossWaterPillarSegment> segments,
        GameObject blastEffectPrefab = null)
    {
        ClearViews(crossWaterPillarWarningViews);
        ClearViews(crossWaterPillarBlastViews);
        ClearCrossWaterPillarBlastEffects();

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
                SpawnWaterPillarBlastEffect(blastPosition, blastEffectPrefab);

                if (!hasDamagedTarget && TryDamagePlayerAtBlast(sourceSystem, sourceSpec, blastPosition))
                    hasDamagedTarget = true;

                lastOffset = offset;
            }

            if (segment.Length - lastOffset > crossWaterPillarBlastDiameter * 0.25f)
            {
                Vector3 blastPosition = segment.End;
                SpawnWaterPillarBlastEffect(blastPosition, blastEffectPrefab);

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
        ClearCrossWaterPillarBlastEffects();
    }

    /// <summary>물대포 연발 패턴의 첫 조준 방향을 현재 플레이어 방향으로 초기화합니다.</summary>
    public bool BeginWaterCannonBurstAim(GameObject explicitTarget)
    {
        CleanupWaterCannonPresentation();
        return TryLockWaterCannonBeamDirection(explicitTarget);
    }

    /// <summary>현재 조준 방향을 플레이어 쪽으로 제한 회전하고 다음 물대포 샷 선분을 만듭니다.</summary>
    public bool TryBuildNextWaterCannonShot(GameObject explicitTarget, out WaterCannonLine line)
    {
        if (!waterCannonHasLockedBeamDirection && !TryLockWaterCannonBeamDirection(explicitTarget))
        {
            line = new WaterCannonLine();
            return false;
        }

        Vector2 targetDirection = ResolveWaterCannonDirection(explicitTarget);
        if (targetDirection.sqrMagnitude > 0.0001f)
            waterCannonLockedBeamDirection = RotateWaterCannonAimToward(
                waterCannonLockedBeamDirection,
                targetDirection.normalized,
                waterCannonMaxTurnAnglePerShot,
                waterCannonMinTurnAnglePerShot,
                waterCannonAimSnapAngle);

        return TryBuildWaterCannonLine(waterCannonLockedBeamDirection, out line);
    }

    /// <summary>물대포 샷 직전의 짧은 경고선을 표시합니다.</summary>
    public AttackTelegraphView ShowWaterCannonShotWarning(WaterCannonLine line)
    {
        if (!line.IsValid)
            return null;

        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return null;

        AttackTelegraphSpec spec = CreateWaterCannonLineSpec(
            line,
            GetWaterCannonWarningWidth(),
            WaterCannonShotWarningSeconds,
            waterCannonWarningStyle);

        AttackTelegraphView view = service.SpawnDetachedView(spec);
        if (view != null)
            waterCannonShotWarningViews.Add(view);

        return view;
    }

    /// <summary>물대포 샷 경고선을 즉시 제거합니다.</summary>
    public void ClearWaterCannonShotWarning(AttackTelegraphView view)
    {
        if (view == null)
            return;

        waterCannonShotWarningViews.Remove(view);
        Destroy(view.gameObject);
    }

    /// <summary>현재 물대포 샷의 경고/레이저 표시만 제거하고 다음 샷 조준 상태는 유지합니다.</summary>
    public void ClearWaterCannonShotPresentation()
    {
        ClearView(ref waterCannonWarningView);
        ClearView(ref waterCannonBeamView);
        ClearViews(waterCannonShotWarningViews);
        ClearViews(waterCannonShotBeamViews);
        if (waterCannonBeamVisual != null)
            Destroy(waterCannonBeamVisual.gameObject);

        waterCannonBeamVisual = null;
    }

    /// <summary>짧은 물대포 레이저 샷 표시를 시작하고, 실제 피해 타이밍을 맞출 수 있도록 VFX 참조를 반환합니다.</summary>
    public bool StartWaterCannonShotVisual(WaterCannonLine line, out WaterZetLaserVfx laserVfx)
    {
        laserVfx = null;
        if (!line.IsValid)
            return false;

        AttackTelegraphService service = GetTelegraphService();
        if (service != null && waterCannonBeamStyle != null)
        {
            AttackTelegraphSpec spec = CreateWaterCannonLineSpec(
                line,
                GetWaterCannonWarningWidth(),
                WaterCannonShotActiveSeconds,
                waterCannonBeamStyle);

            AttackTelegraphView view = service.SpawnDetachedView(spec);
            if (view != null)
                waterCannonShotBeamViews.Add(view);
        }

        laserVfx = SpawnWaterCannonShotVisual(line);
        return true;
    }

    /// <summary>물대포 레이저 VFX가 실제 발사/Idle 상태에 들어간 타이밍에 벽 히트 물튀김 이펙트를 출력합니다.</summary>
    public void PlayWaterCannonWallHitEffect(WaterCannonLine line)
    {
        SpawnWaterCannonWallHitEffect(line);
    }

    /// <summary>
    /// 책임:
    /// - 물대포 패턴이 2초 발사 제한 이후에도 현재 재생 중인 레이저 종료 연출을 기다릴 수 있게 한다.
    /// - 이미 자연 소멸한 VFX 참조를 함께 정리해 다음 패턴 cleanup이 불필요하게 Destroy를 반복하지 않게 한다.
    /// </summary>
    public bool HasActiveWaterCannonLaserVfx()
    {
        for (int i = waterCannonLaserVfxViews.Count - 1; i >= 0; i--)
        {
            WaterZetLaserVfx laserVfx = waterCannonLaserVfxViews[i];
            if (laserVfx == null)
            {
                waterCannonLaserVfxViews.RemoveAt(i);
                continue;
            }

            if (laserVfx.IsPlaying)
                return true;
        }

        return false;
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
            ResolveCrossWaterPillarCastDistance(),
            crossWaterPillarWallLayers.value);

        if (hit.collider != null)
            return Mathf.Max(0.1f, hit.distance - crossWaterPillarWallStopPadding);

        return ResolveCrossWaterPillarCastDistance();
    }

    private float ResolveCrossWaterPillarCastDistance()
    {
        return Mathf.Max(MinCrossWaterPillarCastDistance, crossWaterPillarFallbackDistance);
    }

    /// <summary>물기둥 발생 지점 이펙트를 생성합니다.</summary>
    private void SpawnWaterPillarBlastEffect(Vector3 blastPosition, GameObject blastEffectPrefab)
    {
        if (blastEffectPrefab == null)
            return;

        GameObject effect = Instantiate(blastEffectPrefab, blastPosition, Quaternion.identity);
        if (effect == null)
            return;

        crossWaterPillarBlastEffects.Add(effect);
    }

    private void ClearCrossWaterPillarBlastEffects()
    {
        for (int i = crossWaterPillarBlastEffects.Count - 1; i >= 0; i--)
        {
            GameObject effect = crossWaterPillarBlastEffects[i];
            if (effect != null)
                Destroy(effect);
        }

        crossWaterPillarBlastEffects.Clear();
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
        if (direction.sqrMagnitude <= 0.0001f)
        {
            line = new WaterCannonLine();
            return false;
        }

        Vector2 safeDirection = direction.normalized;
        Vector2 start = ResolveWaterCannonOrigin(safeDirection);
        float distance = GetWaterCannonFixedDistance();
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

    /// <summary>현재 조준 방향을 목표 방향으로 제한 각도만큼 회전합니다.</summary>
    private static Vector2 RotateWaterCannonAimToward(
        Vector2 currentDirection,
        Vector2 targetDirection,
        float maxTurnDegrees,
        float minTurnDegrees,
        float snapAngleDegrees)
    {
        Vector2 safeCurrent = currentDirection.sqrMagnitude > 0.0001f ? currentDirection.normalized : Vector2.right;
        Vector2 safeTarget = targetDirection.sqrMagnitude > 0.0001f ? targetDirection.normalized : safeCurrent;
        float signedAngle = Vector2.SignedAngle(safeCurrent, safeTarget);
        float absAngle = Mathf.Abs(signedAngle);
        float maxTurn = Mathf.Max(0f, maxTurnDegrees);
        float minTurn = Mathf.Clamp(minTurnDegrees, 0f, maxTurn);
        float snapAngle = Mathf.Max(0f, snapAngleDegrees);
        float turnAngle = absAngle <= snapAngle
            ? signedAngle
            : Mathf.Sign(signedAngle) * Mathf.Min(maxTurn, Mathf.Max(minTurn, absAngle));
        float clampedAngle = Mathf.Clamp(turnAngle, -maxTurn, maxTurn);
        return (Quaternion.Euler(0f, 0f, clampedAngle) * safeCurrent).normalized;
    }

    /// <summary>물대포가 향할 현재 플레이어 방향을 계산합니다.</summary>
    private Vector2 ResolveWaterCannonDirection(GameObject explicitTarget)
    {
        Vector2 center = ResolveWaterCannonAimCenter();
        Transform targetTransform = explicitTarget != null ? explicitTarget.transform : CurrentTarget;
        if (targetTransform != null)
        {
            Vector2 toTarget = (Vector2)targetTransform.position - center;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    /// <summary>
    /// 책임:
    /// - 물대포 경고/레이저/벽 cast가 모두 같은 발사 시작점에서 시작되도록 월드 좌표를 해석한다.
    /// - 중앙 소켓에서 조준 방향으로 보정한 위치를 사용하고, 소켓 authoring이 없으면 기존 기준점으로 fallback한다.
    /// </summary>
    private Vector2 ResolveWaterCannonOrigin(Vector2 direction)
    {
        Vector2 center = ResolveWaterCannonAimCenter();
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (sprite != null && sprite.flipX ? Vector2.left : Vector2.right);

        return center + safeDirection * Mathf.Max(0f, waterCannonStartForwardOffset);
    }

    /// <summary>물대포 방향 계산과 발사 시작점 보정에 사용할 보스 중앙 기준점을 해석합니다.</summary>
    private Vector2 ResolveWaterCannonAimCenter()
    {
        if (waterCannonCenterSocket != null)
            return waterCannonCenterSocket.position;

        return waterCannonMuzzleSocket != null
            ? waterCannonMuzzleSocket.position
            : transform.position;
    }

    /// <summary>물대포가 벽 검사 없이 사용할 고정 사거리를 계산합니다.</summary>
    private float GetWaterCannonFixedDistance()
    {
        return Mathf.Max(0.1f, waterCannonFixedDistance);
    }

    /// <summary>물대포 피해 판정 폭과 시각 배율을 기준으로 경고선 폭을 계산합니다.</summary>
    private float GetWaterCannonWarningWidth()
    {
        return Mathf.Max(0.05f, waterCannonHitWidth * waterCannonWarningWidthMultiplier);
    }

    /// <summary>물대포 레이저 VFX의 로컬 Y scale에 사용할 표시 폭을 계산합니다.</summary>
    private float GetWaterCannonVfxWidth()
    {
        return Mathf.Max(0.01f, waterCannonVfxWidth);
    }

    /// <summary>물대포 고정 선분을 선형 텔레그래프 사양으로 변환합니다.</summary>
    private static AttackTelegraphSpec CreateWaterCannonLineSpec(
        WaterCannonLine line,
        float width,
        float duration,
        AttackTelegraphStyle style)
    {
        return AttackTelegraphSpec.CreateLine(
            line.Start,
            line.End,
            Mathf.Max(0.05f, width),
            duration,
            style);
    }

    /// <summary>물총 전용 레이저 VFX를 우선 사용하고 없으면 기존 막대기 비주얼로 물대포 샷을 표시합니다.</summary>
    private WaterZetLaserVfx SpawnWaterCannonShotVisual(WaterCannonLine line)
    {
        WaterZetLaserVfx laserPrefab = ResolveWaterCannonLaserVfxPrefab();
        if (laserPrefab != null)
        {
            WaterZetLaserVfx laserVfx = Instantiate(laserPrefab);
            if (laserVfx != null)
            {
                laserVfx.Play(
                    line.Start,
                    line.Direction,
                    line.Length,
                    GetWaterCannonVfxWidth(),
                    WaterCannonShotActiveSeconds,
                    waterCannonLaserBodyLengthMultiplier);
                waterCannonLaserVfxViews.Add(laserVfx);
                return laserVfx;
            }
        }

        ShowLegacyWaterCannonShotVisual(line);
        return null;
    }

    /// <summary>물대포 고정 사거리 안에서 벽을 만나면 벽 법선 방향으로 물튀김 이펙트를 재생합니다.</summary>
    private void SpawnWaterCannonWallHitEffect(WaterCannonLine line)
    {
        if (!line.IsValid || waterCannonHitEffectWallLayers.value == 0)
            return;

        GameObject effectPrefab = ResolveWaterCannonWallHitEffectPrefab();
        if (effectPrefab == null)
            return;

        RaycastHit2D hit = Physics2D.Raycast(
            line.Start,
            line.Direction,
            line.Length,
            waterCannonHitEffectWallLayers.value);

        if (hit.collider == null)
            return;

        Vector2 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : -line.Direction;
        Vector2 spawnPoint = hit.point + normal * waterCannonHitEffectSurfaceOffset;
        GameObject effect = Instantiate(effectPrefab, spawnPoint, Quaternion.identity);
        if (effect == null)
            return;

        WaterJetWallHitParticleEffect hitEffect = effect.GetComponent<WaterJetWallHitParticleEffect>();
        if (hitEffect == null)
            hitEffect = effect.AddComponent<WaterJetWallHitParticleEffect>();

        hitEffect.Play(spawnPoint, normal);
        waterCannonWallHitEffectViews.Add(effect);
    }

    /// <summary>물대포 벽 히트 이펙트 프리팹을 인스펙터 참조 또는 Resources fallback으로 해결합니다.</summary>
    private GameObject ResolveWaterCannonWallHitEffectPrefab()
    {
        if (waterCannonWallHitEffectPrefab != null)
            return waterCannonWallHitEffectPrefab;

        string resourcePath = string.IsNullOrWhiteSpace(waterCannonWallHitEffectResourcePath)
            ? DefaultWaterCannonHitEffectResourcePath
            : waterCannonWallHitEffectResourcePath;
        waterCannonWallHitEffectPrefab = Resources.Load<GameObject>(resourcePath);
        return waterCannonWallHitEffectPrefab;
    }

    /// <summary>물총 전용 레이저 VFX 프리팹을 인스펙터 참조 또는 Resources fallback으로 해결합니다.</summary>
    private WaterZetLaserVfx ResolveWaterCannonLaserVfxPrefab()
    {
        if (waterCannonLaserVfxPrefab != null)
            return waterCannonLaserVfxPrefab;

        if (waterCannonBeamVisualPrefab != null &&
            waterCannonBeamVisualPrefab.TryGetComponent(out WaterZetLaserVfx assignedLaserVfx))
        {
            waterCannonLaserVfxPrefab = assignedLaserVfx;
            return waterCannonLaserVfxPrefab;
        }

        string resourcePath = string.IsNullOrWhiteSpace(waterCannonLaserVfxResourcePath)
            ? DefaultWaterCannonLaserVfxResourcePath
            : waterCannonLaserVfxResourcePath;
        GameObject prefabObject = Resources.Load<GameObject>(resourcePath);
        if (prefabObject != null)
            waterCannonLaserVfxPrefab = prefabObject.GetComponent<WaterZetLaserVfx>();

        return waterCannonLaserVfxPrefab;
    }

    /// <summary>물총 전용 레이저 VFX가 없을 때 기존 물대포 막대기 비주얼을 현재 선분에 맞춰 표시합니다.</summary>
    private void ShowLegacyWaterCannonShotVisual(WaterCannonLine line)
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

        waterCannonBeamVisual.Show(
            line.Start,
            line.Direction,
            line.Length,
            GetWaterCannonVfxWidth(),
            1f);
    }

    /// <summary>물대포 샷 판정 박스 안에 있는 플레이어에게 샷당 한 번 피해를 적용합니다.</summary>
    public bool TryDamagePlayerInWaterCannonShot(AbilitySystem sourceSystem, AbilitySpec sourceSpec, WaterCannonLine line)
    {
        if (waterCannonDamage <= 0f || waterCannonDamageEffect == null)
            return false;

        float visualWidth = GetWaterCannonWarningWidth();
        float hitWidth = Mathf.Min(Mathf.Max(0.02f, waterCannonHitWidth), visualWidth);
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            line.Center,
            new Vector2(line.Length, hitWidth),
            line.RotationDegrees);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i];
            GameObject damageTarget = CombatTargetResolver2D.ResolveDamageTarget(hitCollider);
            if (damageTarget == null || !damageTarget.CompareTag("Player"))
                continue;

            if (!IsPlayerHurtboxCollider(hitCollider, damageTarget))
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

            return true;
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// - 물대포 피해 판정이 플레이어 하위 공격 이펙트/장식 collider를 타고 플레이어를 오인하지 않도록 필터링한다.
    /// - 실제 피해 수신용 CombatHurtbox2D가 소유한 collider만 플레이어 피격 collider로 인정한다.
    /// </summary>
    private static bool IsPlayerHurtboxCollider(Collider2D hitCollider, GameObject damageTarget)
    {
        if (hitCollider == null || damageTarget == null)
            return false;

        CombatHurtbox2D hurtbox = hitCollider.GetComponent<CombatHurtbox2D>();
        return hurtbox != null &&
               hurtbox.OwnsCollider(hitCollider) &&
               hurtbox.ResolveTargetRoot() == damageTarget;
    }

    /// <summary>랜덤 이동 바운더리 참조를 인스펙터 또는 씬 자동 탐색으로 해결합니다.</summary>
    private SlimeQueenRandomMoveBounds ResolveRandomMoveBounds()
    {
        if (randomMoveBounds == null)
            randomMoveBounds = FindAnyObjectByType<SlimeQueenRandomMoveBounds>();

        return randomMoveBounds;
    }

    /// <summary>생성된 물대포 막대기 비주얼을 제거합니다.</summary>
    private void ClearWaterCannonBeamVisual(bool resetAim = true)
    {
        if (waterCannonBeamVisual != null)
            Destroy(waterCannonBeamVisual.gameObject);

        for (int i = 0; i < waterCannonLaserVfxViews.Count; i++)
        {
            WaterZetLaserVfx laserVfx = waterCannonLaserVfxViews[i];
            if (laserVfx != null)
                Destroy(laserVfx.gameObject);
        }

        for (int i = 0; i < waterCannonWallHitEffectViews.Count; i++)
        {
            GameObject effect = waterCannonWallHitEffectViews[i];
            if (effect != null)
                Destroy(effect);
        }

        waterCannonLaserVfxViews.Clear();
        waterCannonWallHitEffectViews.Clear();
        waterCannonBeamVisual = null;
        if (resetAim)
        {
            waterCannonLockedBeamDirection = Vector2.zero;
            waterCannonHasLockedBeamDirection = false;
        }
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
