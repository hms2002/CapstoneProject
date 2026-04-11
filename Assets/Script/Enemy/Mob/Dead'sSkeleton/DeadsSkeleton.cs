using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class DeadsSkeleton : Mob
{
    private float explosionDiameter = 5f;
    private const string DeathTriggerName = "isDead";

    [Header("자폭")]
    [Tooltip("자폭 모드에 들어가는 거리의 지름입니다.")]
    [SerializeField] private float selfDestructRangeDiameter = 6f;

    [Tooltip("자폭 모드에 들어간 뒤 폭발까지 걸리는 시간입니다.")]
    [SerializeField] private float explodeDelay = 3f;

    [Tooltip("폭발에 사용할 데미지 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec explosionDamageEffect;

    [Tooltip("폭발 피해량입니다.")]
    [SerializeField] private float explosionDamage = 1f;

    private readonly HashSet<GameObject> damagedTargets = new();

    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle warningStyle;
    private bool isSelfDestruct;
    private float explodeTime;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = MakeWarningStyle();
    }

    protected override void UpdateAttack()
    {
        if (isSelfDestruct)
        {
            TickSelfDestruct();
            return;
        }

        if (!CanStartSelfDestruct()) return;

        StartSelfDestruct();
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
    }

    protected override void OnDeathStarted()
    {
        HideWarning();
        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
        if (animator != null)
            animator.SetTrigger(DeathTriggerName);
    }

    protected override bool CanDrawStopRangeGizmo()
    {
        return false;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (!isSelfDestruct || isDead || other == null) return;

        if (!other.gameObject.CompareTag("Player")) return;

        Explode(other.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isSelfDestruct || isDead || other == null) return;

        CandlestickLightZone lightZone = other.GetComponent<CandlestickLightZone>();
        if (lightZone == null)
            lightZone = other.GetComponentInParent<CandlestickLightZone>();

        if (lightZone == null) return;

        DieFromLight();
    }

    protected override void DrawAttackGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, GetSelfDestructRadius());
    }

    /// <summary>지금 자폭 모드에 들어갈 수 있는지 확인합니다.</summary>
    private bool CanStartSelfDestruct()
    {
        if (isDead || isSelfDestruct)
            return false;

        if (!HasExplodeData())
            return false;

        return IsTargetInRange();
    }

    /// <summary>폭발에 필요한 참조가 있는지 확인합니다.</summary>
    private bool HasExplodeData()
    {
        bool isValid = explosionDamageEffect != null &&
                       abilitySystem != null &&
                       target != null;

        if (isValid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError(
                $"[{nameof(DeadsSkeleton)}] 자폭 설정이 비어 있습니다.",
                this);

            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>플레이어가 자폭 발동 범위 안에 있는지 확인합니다.</summary>
    private bool IsTargetInRange()
    {
        if (target == null)
            return false;

        float radius = GetSelfDestructRadius();
        if (radius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        return toTarget.sqrMagnitude <= radius * radius;
    }

    /// <summary>자폭 모드를 시작하고 경고를 띄웁니다.</summary>
    private void StartSelfDestruct()
    {
        isSelfDestruct = true;
        explodeTime = Time.time + Mathf.Max(0f, explodeDelay);
        ShowWarning();
    }

    /// <summary>자폭 대기 시간을 갱신합니다.</summary>
    private void TickSelfDestruct()
    {
        if (Time.time < explodeTime) return;

        Explode(target != null ? target.gameObject : null);
    }

    /// <summary>자폭 경고를 표시합니다.</summary>
    private void ShowWarning()
    {
        if (telegraphService == null) return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            transform.position,
            explosionDiameter,
            Mathf.Max(0f, explodeDelay),
            warningStyle);

        telegraphService.Show(spec);
    }

    /// <summary>자폭 경고를 숨깁니다.</summary>
    private void HideWarning()
    {
        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    /// <summary>플레이어와 닿았을 때 폭발을 처리합니다.</summary>
    private void Explode(GameObject hitTarget)
    {
        CombatHitPayload payload = MakeHitPayload();
        if (payload != null)
            DamageTargets(payload, hitTarget);

        Die();
    }

    /// <summary>광원에 닿았을 때 일반 사망을 처리합니다.</summary>
    private void DieFromLight()
    {
        Die();
    }

    /// <summary>폭발 범위 안의 타겟에게 피해를 적용합니다.</summary>
    private void DamageTargets(CombatHitPayload payload, GameObject hitTarget)
    {
        LayerMask damageMask = GetDamageMask(hitTarget);
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            GetExplosionRadius(),
            damageMask);

        damagedTargets.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);

            if (targetRoot == null || targetRoot == gameObject)
                continue;

            if (!damagedTargets.Add(targetRoot))
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.transform.position);
        }

        if (damagedTargets.Count == 0 && hitTarget != null)
            CombatHitPayloadApplier.Apply(hitTarget, payload, hitTarget.transform.position);
    }

    /// <summary>폭발이 맞을 레이어를 구합니다.</summary>
    private LayerMask GetDamageMask(GameObject hitTarget)
    {
        if (target != null)
            return 1 << target.gameObject.layer;

        if (hitTarget != null)
            return 1 << hitTarget.layer;

        return 0;
    }

    /// <summary>폭발에 사용할 피격 정보를 만듭니다.</summary>
    private CombatHitPayload MakeHitPayload()
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: explosionDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: explosionDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    /// <summary>자폭 발동 반경을 구합니다.</summary>
    private float GetSelfDestructRadius()
    {
        return Mathf.Max(0f, selfDestructRangeDiameter * 0.5f);
    }

    /// <summary>폭발 반경을 돌려줍니다.</summary>
    private float GetExplosionRadius()
    {
        return Mathf.Max(0f, explosionDiameter * 0.5f);
    }

    /// <summary>패턴용 강화 수치를 적용합니다.</summary>
    public void SetBoost(
        Transform combatTarget,
        float boostedExplosionDiameter,
        float boostedSpeedScale,
        bool ignoreRange)
    {
        if (combatTarget != null)
            SetTarget(combatTarget);

        explosionDiameter = Mathf.Max(0f, boostedExplosionDiameter);

        if (ChaseIntent == null) return;

        ChaseIntent.SetSpeedScale(boostedSpeedScale);
        ChaseIntent.SetIgnoreDetectionRange(ignoreRange);
    }

    /// <summary>해골 전용 경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.45f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.45f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = true;
        style.fillScaleStart = 0f;
        style.fillScaleEnd = 1f;
        return style;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (warningStyle != null)
            Destroy(warningStyle);
    }
}
