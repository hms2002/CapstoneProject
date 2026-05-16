using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(BishopLineBlastRunner))]
public class Bishop : Slime
{
    private const int WallLayer = 30;
    private const float AttackRange = 7f;
    private const float WarningTime = 1.6f;
    private const float WarningWidth = 0.35f;
    private const float MaxHealth = 10f;
    private const float VisualScale = 1.2f;
    private const float ChaseSpeedMultiplier = 0.5f;
    private const float DamageAmount = 1.5f;
    private const float SplitSpread = 0.55f;
    private const float AttackRecoverSeconds = 0.25f;
    private const float RaycastDistance = 64f;
    private const float FallbackHalfLength = 8f;
    private const float BlastInterval = 1.2f;
    private const float BlastDiameter = 1.25f;
    private const float BlastViewTime = 0.2f;

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private AbilityDefinition attackAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0)] private int splitCount = 2;

    private BishopLineBlastRunner lineBlastRunner;
    private bool hasLoggedInvalidConfig;

    public readonly struct LineBlastContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Center;
        public readonly Vector2 Direction;
        public readonly float HalfLength;
        public readonly float WarningTime;
        public readonly float WarningWidth;
        public readonly float BlastInterval;
        public readonly float BlastDiameter;
        public readonly float BlastViewTime;

        public LineBlastContext(
            GameObject target,
            Vector2 center,
            Vector2 direction,
            float halfLength,
            float warningTime,
            float warningWidth,
            float blastInterval,
            float blastDiameter,
            float blastViewTime)
        {
            Target = target;
            Center = center;
            Direction = direction;
            HalfLength = halfLength;
            WarningTime = warningTime;
            WarningWidth = warningWidth;
            BlastInterval = blastInterval;
            BlastDiameter = blastDiameter;
            BlastViewTime = blastViewTime;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();

        lineBlastRunner = GetComponent<BishopLineBlastRunner>();
        if (lineBlastRunner == null)
            lineBlastRunner = gameObject.AddComponent<BishopLineBlastRunner>();

        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        GiveAbility(attackAbility);
    }

    public override bool CanUseChaseMovement()
    {
        UpdateSpeed(ChaseSpeedMultiplier);

        if (!CanMove()) return false;

        return lineBlastRunner == null || !lineBlastRunner.IsRunning;
    }

    protected override void OnDeathStarted()
    {
        CancelAbility();
        SpawnSplit<Wizard>(splitPrefab, splitCount, SplitSpread);
        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
    }

    protected override void DrawAttackGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }

    /// <summary>비숍의 원거리 공격 요청을 만듭니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        if (!CanAct()) return false;
        if (!HasAttackData()) return false;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!InRange(targetObject, AttackRange)) return false;

        request = new MobAttackRequest(attackAbility, targetObject, AttackRecoverSeconds);
        return request.IsValid;
    }

    /// <summary>비숍의 직선 폭발 공격 정보를 만듭니다.</summary>
    public bool TryBuildLineContext(GameObject explicitTarget, out LineBlastContext context)
    {
        context = default;

        if (!CanAct()) return false;
        if (!HasAttackData()) return false;

        GameObject targetObject = GetTarget(explicitTarget);
        if (!InRange(targetObject, AttackRange)) return false;

        Vector2 center = transform.position;
        Vector2 direction = GetDirection(targetObject);
        context = new LineBlastContext(
            targetObject,
            center,
            direction,
            GetLineHalfLength(center, direction),
            WarningTime,
            WarningWidth,
            BlastInterval,
            BlastDiameter,
            BlastViewTime);
        return true;
    }

    /// <summary>경고선 안에 폭발 위치들을 채웁니다.</summary>
    public void FillBlastPoints(LineBlastContext context, List<Vector3> points)
    {
        if (points == null) return;

        points.Clear();

        float interval = Mathf.Max(0.1f, context.BlastInterval);
        float start = -context.HalfLength;
        float end = context.HalfLength;

        for (float offset = start; offset <= end + 0.001f; offset += interval)
            points.Add(context.Center + context.Direction * offset);

        if (points.Count == 0)
            points.Add(context.Center);
    }

    /// <summary>폭발 위치 안에 대상이 있으면 피해를 적용합니다.</summary>
    public bool TryHitBlasts(AbilitySystem system, AbilitySpec spec, LineBlastContext context, List<Vector3> points)
    {
        if (!HasAttackData()) return false;
        if (context.Target == null) return false;
        if (points == null || points.Count == 0) return false;

        float radius = context.BlastDiameter * 0.5f;
        float sqrRadius = radius * radius;
        Vector2 targetPos = context.Target.transform.position;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 toTarget = targetPos - (Vector2)points[i];
            if (toTarget.sqrMagnitude > sqrRadius)
                continue;

            CombatDamageAction.ApplyDamageAndEmitHit(
                system: system != null ? system : abilitySystem,
                spec: spec,
                damageEffect: damageEffect,
                knockbackEffect: null,
                target: context.Target,
                finalHpDamage: DamageAmount,
                finalStaggerBuildUp: 0f,
                finalKnockbackImpulse: 0f,
                hitConfirmedTag: null,
                hitWorldPosition: points[i],
                causer: gameObject);
            return true;
        }

        return false;
    }

    /// <summary>비숍의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Bishop", MaxHealth, VisualScale);
    }

    /// <summary>비숍 중심 기준으로 벽까지 이어지는 경고선 반쪽 길이를 구합니다.</summary>
    private float GetLineHalfLength(Vector2 center, Vector2 direction)
    {
        float forward = GetWallDistance(center, direction);
        float backward = GetWallDistance(center, -direction);

        bool hasForward = forward > 0f;
        bool hasBackward = backward > 0f;

        if (hasForward && hasBackward)
            return Mathf.Max(BlastDiameter * 0.5f, Mathf.Min(forward, backward));

        if (hasForward)
            return Mathf.Max(BlastDiameter * 0.5f, Mathf.Min(forward, FallbackHalfLength));

        if (hasBackward)
            return Mathf.Max(BlastDiameter * 0.5f, Mathf.Min(backward, FallbackHalfLength));

        return FallbackHalfLength;
    }

    /// <summary>지정 방향으로 벽까지의 거리를 구합니다.</summary>
    private static float GetWallDistance(Vector2 center, Vector2 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(center, direction, RaycastDistance, 1 << WallLayer);
        return hit.collider != null ? hit.distance : 0f;
    }

    /// <summary>비숍 공격 설정이 모두 연결되어 있는지 확인합니다.</summary>
    private bool HasAttackData()
    {
        bool isValid = attackAbility != null &&
                       damageEffect != null &&
                       abilitySystem != null &&
                       lineBlastRunner != null;

        if (isValid) return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(Bishop)}] 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}
