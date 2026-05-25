using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Rook이 만든 돌진 문맥을 받아 경고 표시, 돌진 이동, 충돌/피해 판정을 실행한다.
/// - 돌진 중단, 경고 정리, 패턴 runner 생명주기를 한곳에서 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rook))]
public class RookChargeRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    private const string StaggerImmuneTagResourcePath = "Tags/State.Status.StaggerImmune";

    [SerializeField] private Rook owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;
    [SerializeField] private GameplayTag staggerImmuneTag;

    [Header("Dash VFX")]
    [SerializeField] private GameObject dashDustEffectPrefab;
    [SerializeField] private Transform dashDustAnchor;
    [SerializeField] private Vector2 dashDustLocalOffset;
    [SerializeField] private float dashDustRotationOffsetDegrees;
    [SerializeField, Min(0.1f)] private float dashDustFallbackLifetime = 1.5f;

    [Header("Impact Feedback")]
    [SerializeField] private CameraShakeHook wallImpactCameraShake = CameraShakeHook.Create(
        amplitude: 1f,
        amplitudeMultiplier: 1f,
        maxAmplitude: 1f,
        minIntervalSeconds: 0.08f);

    private AbilityMotionController2D motionController;
    private AttackTelegraphStyle warningStyle;
    private AttackTelegraphStyle centerLineStyle;
    private AttackTelegraphStyle railStyle;
    private AttackTelegraphStyle impactTipStyle;
    private readonly List<AttackTelegraphView> accentTelegraphViews = new();
    private Rook.ChargeContext currentContext;
    private bool isRunning;
    private bool isDashing;
    private bool cancelRequested;
    private bool hitWall;
    private bool fellIntoHole;
    private bool hitPlayer;
    private TagSystem tagSystem;
    private bool staggerImmuneApplied;
    private float dashEndTime;
    private const float ImpactTipLength = 0.26f;

    public bool IsRunning => isRunning;

    /// <summary>
    /// 책임:
    /// - 룩 돌진 경고의 중심, 방향, 끝점, 회전값을 한 번만 계산해 모든 경고 레이어가 같은 기준을 쓰게 한다.
    /// - 기본 위험 박스와 보조선이 서로 다른 방향/크기 계산을 하면서 어긋나는 문제를 막는다.
    /// </summary>
    private readonly struct WarningGeometry
    {
        public readonly Vector3 Start;
        public readonly Vector3 Direction;
        public readonly Vector3 Perpendicular;
        public readonly Vector3 SegmentCenter;
        public readonly Vector3 SegmentEnd;
        public readonly Vector3 ImpactCenter;
        public readonly float AngleDeg;
        public readonly float SegmentLength;
        public readonly float Width;
        public readonly float Duration;

        public WarningGeometry(Rook.ChargeContext context, float duration)
        {
            Start = context.StartPos;
            Direction = ResolveSafeDirection(context.Direction);
            Perpendicular = new Vector3(-Direction.y, Direction.x, 0f);
            Width = Mathf.Max(0.01f, context.WarningWidth);
            Duration = duration;
            SegmentLength = Mathf.Max(0.01f, context.DashDistance);
            SegmentEnd = Start + Direction * SegmentLength;
            ImpactCenter = SegmentEnd - Direction * (ImpactTipLength * 0.5f);
            SegmentCenter = Vector3.Lerp(Start, SegmentEnd, 0.5f);
            AngleDeg = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        }

        private static Vector3 ResolveSafeDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector3.right;

            Vector2 normalized = direction.normalized;
            return new Vector3(normalized.x, normalized.y, 0f);
        }
    }

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<Rook>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        tagSystem = GetComponent<TagSystem>();
        if (staggerImmuneTag == null)
            staggerImmuneTag = Resources.Load<GameplayTag>(StaggerImmuneTagResourcePath);

        motionController = GetComponent<AbilityMotionController2D>();
        warningStyle = MakeWarningStyle();
        centerLineStyle = MakeCenterLineStyle();
        railStyle = MakeRailStyle();
        impactTipStyle = MakeImpactTipStyle();
        EnsureContactTriggerCollider();
    }

    private void OnDestroy()
    {
        RemoveChargeStaggerImmunity();

        if (warningStyle != null)
            Destroy(warningStyle);

        if (centerLineStyle != null)
            Destroy(centerLineStyle);

        if (railStyle != null)
            Destroy(railStyle);

        if (impactTipStyle != null)
            Destroy(impactTipStyle);
    }

    private void OnDisable()
    {
        HideWarning();
        StopDash();
    }

    /// <summary>룩 돌진 패턴의 전체 시퀀스를 실행합니다.</summary>
    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;
        if (!owner.TryBuildChargeContext(system, spec, initialTarget, out currentContext)) yield break;

        isRunning = true;
        cancelRequested = false;
        hitWall = false;
        fellIntoHole = false;
        hitPlayer = false;

        try
        {
            float warningSeconds = CombatTimingService.ScaleSeconds(system, currentContext.WarningTime, CombatTimingSlot.AttackWarning);
            ShowWarning(currentContext, warningSeconds);
            owner.PlayChargePrepareAnimation();
            owner.SetFacingLocked(true);

            if (warningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, warningSeconds);

            if (cancelRequested || owner.IsDead)
                yield break;

            HideWarning();
            BeginDash(currentContext);

            while (!cancelRequested &&
                   !owner.IsDead &&
                   !hitWall &&
                   !fellIntoHole &&
                   Time.time < dashEndTime)
            {
                yield return null;
            }

            PlayWallImpactCameraShakeIfDashReachedDestination();

        }
        finally
        {
            HideWarning();
            StopDash();
            currentContext = default;
            cancelRequested = false;
            hitWall = false;
            fellIntoHole = false;
            hitPlayer = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    /// <summary>룩 돌진 실행을 취소 상태로 전환합니다.</summary>
    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
        StopDash();
    }

    /// <summary>룩 돌진이 돌진 차단물과 충돌했는지 확인합니다.</summary>
    public void HandleBodyCollision(Collision2D collision)
    {
        if (!isDashing) return;
        if (collision == null) return;

        TryHitCollisionPlayer(collision);

        if (!HasChargeBlocker(collision)) return;

        hitWall = true;
        PlayWallImpactCameraShake();
        StopDash();
    }

    /// <summary>룩 돌진 중 플레이어와 구덩이 트리거를 처리합니다.</summary>
    public void HandleTrigger(Collider2D other)
    {
        if (!isDashing) return;
        if (other == null) return;

        if (IsHole(other))
        {
            fellIntoHole = true;
            StopDash();
            return;
        }

        TryHitTriggerPlayer(other);
    }

    /// <summary>남아 있는 룩 경고를 정리합니다.</summary>
    public void CleanupPresentation()
    {
        HideWarning();
    }

    /// <summary>룩의 경고 직사각형을 화면에 표시합니다.</summary>
    private void ShowWarning(Rook.ChargeContext context, float duration)
    {
        if (telegraphService == null) return;

        HideWarning();

        WarningGeometry geometry = new(context, duration);
        SpawnWarningTelegraph(AttackTelegraphSpec.CreateRectangle(
            geometry.SegmentCenter,
            new Vector2(geometry.SegmentLength, geometry.Width),
            geometry.AngleDeg,
            geometry.Duration,
            warningStyle));
        ShowWarningAccents(geometry);
    }

    /// <summary>현재 표시 중인 룩 경고를 숨깁니다.</summary>
    private void HideWarning()
    {
        telegraphService?.HideCurrent();
        HideWarningTelegraphs();
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 경고 위에 방향성과 끝점을 강조하는 보조 텔레그래프들을 겹쳐 표시한다.
    /// - 실제 위험 영역은 기본 사각형 spec이 유지하고, 이 레이어는 읽기 쉬운 연출만 담당한다.
    /// </summary>
    private void ShowWarningAccents(WarningGeometry geometry)
    {
        if (telegraphService == null)
            return;

        SpawnWarningTelegraph(AttackTelegraphSpec.CreateRectangle(
            geometry.SegmentCenter,
            new Vector2(geometry.SegmentLength, 0.08f),
            geometry.AngleDeg,
            geometry.Duration,
            centerLineStyle));

        float railOffset = geometry.Width * 0.42f;
        Vector2 railSize = new Vector2(geometry.SegmentLength, 0.06f);
        SpawnWarningTelegraph(AttackTelegraphSpec.CreateRectangle(
            geometry.SegmentCenter + geometry.Perpendicular * railOffset,
            railSize,
            geometry.AngleDeg,
            geometry.Duration,
            railStyle));
        SpawnWarningTelegraph(AttackTelegraphSpec.CreateRectangle(
            geometry.SegmentCenter - geometry.Perpendicular * railOffset,
            railSize,
            geometry.AngleDeg,
            geometry.Duration,
            railStyle));

        SpawnWarningTelegraph(AttackTelegraphSpec.CreateRectangle(
            geometry.ImpactCenter,
            new Vector2(ImpactTipLength, geometry.Width * 1.15f),
            geometry.AngleDeg,
            geometry.Duration,
            impactTipStyle));
    }

    /// <summary>룩 경고를 구성하는 모든 텔레그래프 조각을 같은 생성 경로와 cleanup 목록으로 관리합니다.</summary>
    private void SpawnWarningTelegraph(AttackTelegraphSpec spec)
    {
        AttackTelegraphView view = telegraphService.SpawnDetachedView(spec);
        if (view != null)
            accentTelegraphViews.Add(view);
    }

    /// <summary>룩 경고를 구성하는 모든 텔레그래프 조각을 즉시 숨기고 목록을 정리합니다.</summary>
    private void HideWarningTelegraphs()
    {
        for (int i = accentTelegraphViews.Count - 1; i >= 0; i--)
        {
            AttackTelegraphView view = accentTelegraphViews[i];
            if (view != null)
                view.HideImmediate();
        }

        accentTelegraphViews.Clear();
    }

    /// <summary>룩이 고정 방향으로 돌진을 시작합니다.</summary>
    private void BeginDash(Rook.ChargeContext context)
    {
        if (motionController == null) return;

        float dashTime = owner.GetDashTime(context.DashSpeed, context.DashDistance);
        if (dashTime <= 0f) return;

        isDashing = true;
        dashEndTime = Time.time + dashTime;
        ApplyChargeStaggerImmunity();
        owner.PlayChargeAnimation();
        owner.SetChargeAnimationActive(true);
        motionController.StartDash(context.Direction, context.DashSpeed, dashTime);
        SpawnDashDustEffect(context.Direction);
    }

    /// <summary>룩의 현재 돌진을 강제로 멈춥니다.</summary>
    private void StopDash()
    {
        RemoveChargeStaggerImmunity();
        isDashing = false;
        if (owner != null)
        {
            owner.SetChargeAnimationActive(false);
            owner.SetFacingLocked(false);
        }

        if (motionController != null)
            motionController.CancelMotion();
    }

    /// <summary>
    /// 책임:
    /// - 룩이 실제 돌진 중일 때 스태거 누적과 그로기 효과 발동을 막는 상태 태그를 부여한다.
    /// - 전투 피해 파이프라인의 공통 StaggerImmune 판정을 재사용해 별도 예외 로직을 만들지 않는다.
    /// </summary>
    private void ApplyChargeStaggerImmunity()
    {
        if (staggerImmuneApplied)
            return;

        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();

        if (staggerImmuneTag == null)
            staggerImmuneTag = Resources.Load<GameplayTag>(StaggerImmuneTagResourcePath);

        if (tagSystem == null || staggerImmuneTag == null)
            return;

        tagSystem.AddTag(staggerImmuneTag);
        staggerImmuneApplied = true;
    }

    /// <summary>
    /// 책임:
    /// - 돌진 종료/취소/비활성화 등 모든 종료 경로에서 돌진 중 부여한 스태거 면역 태그만 회수한다.
    /// - 다른 시스템이 같은 태그를 별도로 부여했더라도 count 기반 태그 시스템이 남은 스택을 유지하게 한다.
    /// </summary>
    private void RemoveChargeStaggerImmunity()
    {
        if (!staggerImmuneApplied)
            return;

        if (tagSystem != null && staggerImmuneTag != null)
            tagSystem.RemoveTag(staggerImmuneTag);

        staggerImmuneApplied = false;
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진이 벽/문 같은 차단물에 부딪힌 순간의 짧은 카메라 반응만 담당한다.
    /// - 이동 정지/피해 판정과 분리해 충돌 피드백 세기를 인스펙터에서 조절할 수 있게 한다.
    /// </summary>
    private void PlayWallImpactCameraShake()
    {
        Vector3 impactDirection = currentContext.Direction.sqrMagnitude > 0.0001f
            ? -(Vector3)currentContext.Direction.normalized
            : Vector3.up;

        wallImpactCameraShake.TryPlay(
            gameObject,
            impactDirection,
            debugReason: "Rook.ChargeWallImpact");
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진이 물리 충돌 이벤트 없이 사전 계산된 벽 목적지에 도착해 끝난 경우에도 충돌 피드백을 보장한다.
    /// - cast 기반 dash와 collision 기반 dash가 서로 다른 종료 경로를 타더라도 연출 결과가 같게 만든다.
    /// </summary>
    private void PlayWallImpactCameraShakeIfDashReachedDestination()
    {
        if (!isDashing)
            return;

        if (cancelRequested || owner == null || owner.IsDead || hitWall || fellIntoHole)
            return;

        if (Time.time < dashEndTime)
            return;

        PlayWallImpactCameraShake();
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 시작 순간의 먼지 VFX를 생성하고 돌진 방향에 맞춰 회전시킨다.
    /// - 플레이어 대시 먼지 프리팹을 재사용할 수 있도록 VFX 자산 의존성을 인스펙터 슬롯으로 분리한다.
    /// </summary>
    private void SpawnDashDustEffect(Vector2 direction)
    {
        if (dashDustEffectPrefab == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;

        Transform anchor = dashDustAnchor != null ? dashDustAnchor : transform;
        Vector3 spawnPosition = ResolveDashDustPosition(anchor, safeDirection);
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, ResolveDashDustAngle(safeDirection));
        GameObject instance = Instantiate(dashDustEffectPrefab, spawnPosition, spawnRotation);
        PlayDashDustParticles(instance);
        Destroy(instance, ResolveDashDustLifetime(instance));
    }

    /// <summary>먼지 VFX의 기준 위치에 인스펙터 offset을 돌진 방향 기준으로 보정해 더합니다.</summary>
    private Vector3 ResolveDashDustPosition(Transform anchor, Vector2 direction)
    {
        Vector2 right = new(direction.y, -direction.x);
        Vector3 worldOffset =
            (Vector3)(direction * dashDustLocalOffset.x) +
            (Vector3)(right * dashDustLocalOffset.y);

        return anchor.position + worldOffset;
    }

    /// <summary>먼지 VFX가 돌진 방향을 바라보도록 z 회전각을 계산합니다.</summary>
    private float ResolveDashDustAngle(Vector2 direction)
    {
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + dashDustRotationOffsetDegrees;
    }

    /// <summary>프리팹 안의 파티클 시스템이 비활성 생성되어도 즉시 재생되도록 보장합니다.</summary>
    private static void PlayDashDustParticles(GameObject instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.gameObject.SetActive(true);
            particleSystem.Play(withChildren: true);
        }
    }

    /// <summary>파티클 duration/startLifetime을 읽어 자동 파괴 시간을 계산하고, 없으면 fallback을 사용합니다.</summary>
    private float ResolveDashDustLifetime(GameObject instance)
    {
        if (instance == null)
            return dashDustFallbackLifetime;

        float lifetime = 0f;
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
        }

        return Mathf.Max(dashDustFallbackLifetime, lifetime);
    }

    /// <summary>충돌 정보 안에 룩 돌진 차단물이 있는지 확인합니다.</summary>
    private bool HasChargeBlocker(Collision2D collision)
    {
        if (owner == null)
            return false;

        if (owner.IsChargeBlocker(collision.collider) || owner.IsChargeBlocker(collision.otherCollider))
            return true;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (owner.IsChargeBlocker(contact.collider))
                return true;

            if (owner.IsChargeBlocker(contact.otherCollider))
                return true;
        }

        return false;
    }

    /// <summary>룩 돌진 중 trigger로 닿은 플레이어에게 1회 피해를 적용합니다.</summary>
    private bool TryHitTriggerPlayer(Collider2D other)
    {
        if (hitPlayer || other == null)
            return false;

        GameObject targetObject = ResolveChargeTriggerDamageTarget(other);
        if (targetObject == null || !targetObject.CompareTag("Player"))
            return false;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        return TryApplyPlayerHit(targetObject, hitPoint);
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 트리거가 닿은 collider에서 피해 대상 플레이어를 찾는다.
    /// - CombatHurtbox2D가 있는 정식 경로를 우선 사용하되, 플레이어 물리/하위 collider authoring 누락은 안전한 fallback으로 보완한다.
    /// - 공격 이펙트/히트박스 콜라이더가 부모의 Player 태그로 역참조되는 기존 오탐은 AttackBase 차단으로 막는다.
    /// </summary>
    private static GameObject ResolveChargeTriggerDamageTarget(Collider2D other)
    {
        GameObject targetObject = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (targetObject != null)
            return targetObject;

        if (other == null || other.GetComponentInParent<AttackBase>() != null)
            return null;

        targetObject = ResolvePlayerObject(other);
        if (targetObject != null)
            return targetObject;

        Rigidbody2D attachedBody = other.attachedRigidbody;
        return attachedBody != null
            ? ResolvePlayerObject(attachedBody.gameObject)
            : null;
    }

    /// <summary>룩 돌진 중 non-trigger 물리 충돌로 닿은 플레이어에게 1회 피해를 적용합니다.</summary>
    private bool TryHitCollisionPlayer(Collision2D collision)
    {
        if (hitPlayer || collision == null)
            return false;

        if (!TryResolvePlayerFromCollision(collision, out GameObject targetObject))
            return false;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        return TryApplyPlayerHit(targetObject, hitPoint);
    }

    /// <summary>룩과 충돌한 collider들 중 플레이어 루트 오브젝트를 찾습니다.</summary>
    private bool TryResolvePlayerFromCollision(Collision2D collision, out GameObject targetObject)
    {
        targetObject = ResolvePlayerObject(collision.collider);
        if (targetObject != null)
            return true;

        targetObject = ResolvePlayerObject(collision.otherCollider);
        if (targetObject != null)
            return true;

        targetObject = ResolvePlayerObject(collision.transform != null ? collision.transform.gameObject : null);
        return targetObject != null;
    }

    /// <summary>충돌 collider 또는 GameObject에서 Player 태그를 가진 루트를 찾습니다.</summary>
    private static GameObject ResolvePlayerObject(Collider2D collider)
    {
        return collider != null ? ResolvePlayerObject(collider.gameObject) : null;
    }

    /// <summary>GameObject의 부모 체인을 따라 Player 태그를 가진 전투 루트를 찾습니다.</summary>
    private static GameObject ResolvePlayerObject(GameObject candidate)
    {
        Transform current = candidate != null ? candidate.transform : null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    /// <summary>룩 돌진 피해 payload를 플레이어에게 적용하고 중복 적중을 막습니다.</summary>
    private bool TryApplyPlayerHit(GameObject targetObject, Vector3 hitPoint)
    {
        if (hitPlayer || targetObject == null)
            return false;

        if (currentContext.HitPayload == null || !currentContext.HitPayload.IsValid())
            return false;

        hitPlayer = CombatHitPayloadApplier.Apply(
            targetObject,
            currentContext.HitPayload,
            hitPoint);

        return hitPlayer;
    }

    /// <summary>현재 트리거가 구덩이 기믹인지 확인합니다.</summary>
    private bool IsHole(Collider2D other)
    {
        return other.GetComponent<HoleTrap>() != null ||
               other.GetComponentInParent<HoleTrap>() != null;
    }

    /// <summary>룩이 사용할 붉은 돌진 경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(0.85f, 0.03f, 0.02f, 0.08f);
        style.fillColorEnd = new Color(1f, 0.05f, 0.02f, 0.24f);
        style.borderColorStart = new Color(1f, 0.35f, 0.12f, 0.72f);
        style.borderColorEnd = new Color(1f, 0.08f, 0.02f, 1f);
        style.progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.68f;
        style.blinkFrequency = 7f;
        style.blinkAlphaMin = 0.52f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>룩 돌진 중심선을 강조하는 경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeCenterLineStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0.92f, 0.45f, 0.2f);
        style.fillColorEnd = new Color(1f, 0.98f, 0.72f, 0.72f);
        style.borderColorStart = new Color(1f, 0.8f, 0.32f, 0.25f);
        style.borderColorEnd = new Color(1f, 0.95f, 0.55f, 0.85f);
        style.progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.62f;
        style.blinkFrequency = 9f;
        style.blinkAlphaMin = 0.35f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>룩 돌진 경고의 양쪽 레일 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeRailStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0.18f, 0.04f, 0.1f);
        style.fillColorEnd = new Color(1f, 0.12f, 0.02f, 0.55f);
        style.borderColorStart = new Color(1f, 0.42f, 0.16f, 0.18f);
        style.borderColorEnd = new Color(1f, 0.2f, 0.05f, 0.85f);
        style.progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.72f;
        style.blinkFrequency = 8f;
        style.blinkAlphaMin = 0.4f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>룩 돌진 도착점의 충돌감을 강조하는 경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeImpactTipStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0.72f, 0.18f, 0.08f);
        style.fillColorEnd = new Color(1f, 0.16f, 0.04f, 0.68f);
        style.borderColorStart = new Color(1f, 0.75f, 0.32f, 0.25f);
        style.borderColorEnd = new Color(1f, 0.95f, 0.62f, 1f);
        style.progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.55f;
        style.blinkFrequency = 10f;
        style.blinkAlphaMin = 0.35f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>플레이어 감지용 트리거 콜라이더를 보장합니다.</summary>
    private void EnsureContactTriggerCollider()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D existingCollider = colliders[i];
            if (existingCollider != null && existingCollider.isTrigger)
                return;
        }

        BoxCollider2D bodyCollider = GetComponentInChildren<BoxCollider2D>(true);
        if (bodyCollider == null) return;

        BoxCollider2D triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.offset = bodyCollider.offset;
        triggerCollider.size = bodyCollider.size;
        triggerCollider.edgeRadius = bodyCollider.edgeRadius;
    }
}
