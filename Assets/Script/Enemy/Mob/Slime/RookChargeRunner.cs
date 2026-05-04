using System.Collections;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rook))]
public class RookChargeRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    private const int WallLayer = 30;

    [SerializeField] private Rook owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private AbilityMotionController2D motionController;
    private AttackTelegraphStyle warningStyle;
    private Rook.ChargeContext currentContext;
    private bool isRunning;
    private bool isDashing;
    private bool cancelRequested;
    private bool hitWall;
    private bool fellIntoHole;
    private bool hitPlayer;
    private float dashEndTime;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<Rook>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        motionController = GetComponent<AbilityMotionController2D>();
        warningStyle = MakeWarningStyle();
        EnsureContactTriggerCollider();
    }

    private void OnDestroy()
    {
        if (warningStyle != null)
            Destroy(warningStyle);
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
            ShowWarning(currentContext);

            if (currentContext.WarningTime > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, currentContext.WarningTime);

            if (cancelRequested || owner.IsDead)
                yield break;

            BeginDash(currentContext);

            while (!cancelRequested &&
                   !owner.IsDead &&
                   !hitWall &&
                   !fellIntoHole &&
                   Time.time < dashEndTime)
            {
                yield return null;
            }
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

    /// <summary>룩 돌진이 벽과 충돌했는지 확인합니다.</summary>
    public void HandleBodyCollision(Collision2D collision)
    {
        if (!isDashing) return;
        if (collision == null) return;
        if (!HasWall(collision)) return;

        hitWall = true;
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
            owner.FallIntoHole();
            return;
        }

        if (hitPlayer) return;

        GameObject targetObject = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (targetObject == null || !targetObject.CompareTag("Player")) return;
        if (currentContext.HitPayload == null || !currentContext.HitPayload.IsValid()) return;

        hitPlayer = CombatHitPayloadApplier.Apply(
            targetObject,
            currentContext.HitPayload,
            other.ClosestPoint(transform.position));
    }

    /// <summary>남아 있는 룩 경고를 정리합니다.</summary>
    public void CleanupPresentation()
    {
        HideWarning();
    }

    /// <summary>룩의 경고 직사각형을 화면에 표시합니다.</summary>
    private void ShowWarning(Rook.ChargeContext context)
    {
        if (telegraphService == null) return;

        Vector3 center = context.StartPos + context.Direction * (context.DashDistance * 0.5f);
        float angleDeg = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(context.DashDistance, context.WarningWidth),
            angleDeg,
            context.WarningTime,
            warningStyle);

        telegraphService.Show(spec);
    }

    /// <summary>현재 표시 중인 룩 경고를 숨깁니다.</summary>
    private void HideWarning()
    {
        if (telegraphService == null) return;

        telegraphService.HideCurrent();
    }

    /// <summary>룩이 고정 방향으로 돌진을 시작합니다.</summary>
    private void BeginDash(Rook.ChargeContext context)
    {
        if (motionController == null) return;

        float dashTime = owner.GetDashTime(context.DashSpeed);
        if (dashTime <= 0f) return;

        isDashing = true;
        dashEndTime = Time.time + dashTime;
        motionController.StartDash(context.Direction, context.DashSpeed, dashTime);
    }

    /// <summary>룩의 현재 돌진을 강제로 멈춥니다.</summary>
    private void StopDash()
    {
        isDashing = false;

        if (motionController != null)
            motionController.CancelMotion();
    }

    /// <summary>충돌 정보 안에 벽 레이어가 있는지 확인합니다.</summary>
    private bool HasWall(Collision2D collision)
    {
        if (collision.gameObject.layer == WallLayer)
            return true;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.collider != null && contact.collider.gameObject.layer == WallLayer)
                return true;

            if (contact.otherCollider != null && contact.otherCollider.gameObject.layer == WallLayer)
                return true;
        }

        return false;
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
        style.fillColorStart = new Color(1f, 0f, 0f, 0.16f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.28f);
        style.borderColorStart = new Color(1f, 0.25f, 0.25f, 0.95f);
        style.borderColorEnd = new Color(1f, 0.25f, 0.25f, 0.95f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>플레이어 감지용 트리거 콜라이더를 보장합니다.</summary>
    private void EnsureContactTriggerCollider()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D existingCollider = colliders[i];
            if (existingCollider != null && existingCollider.isTrigger)
                return;
        }

        BoxCollider2D bodyCollider = GetComponent<BoxCollider2D>();
        if (bodyCollider == null) return;

        BoxCollider2D triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.offset = bodyCollider.offset;
        triggerCollider.size = bodyCollider.size;
        triggerCollider.edgeRadius = bodyCollider.edgeRadius;
    }
}
