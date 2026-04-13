using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class ShadowServant : Mob
{
    private const float AttackDelay = 2f;

    [Header("Fog")]
    [Tooltip("폭발 뒤 생성할 안개 프리팹입니다.")]
    [SerializeField] private GameObject fog;

    [Tooltip("폭발에 사용할 데미지 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec explosionDamageEffect;

    [Tooltip("폭발 피해량입니다.")]
    [SerializeField] private float explosionDamage = 1f;

    private readonly HashSet<GameObject> damagedTargets = new();

    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle warningStyle;
    private Coroutine attackRoutine;
    private bool isAttacking;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = MakeWarningStyle();
    }

    public override bool CanUseChaseMovement()
    {
        return !isAttacking;
    }

    protected override void UpdateAttack()
    {
        if (attackRoutine != null) return;

        if (!CanAttack()) return;

        attackRoutine = StartCoroutine(RunAttack());
    }

    protected override void OnDeathStarted()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        ClearAttack();
        base.OnDeathStarted();
    }

    protected override bool CanDrawStopRangeGizmo()
    {
        return false;
    }

    protected override void DrawAttackGizmos()
    {
        float attackRadius = GetAttackRadius();
        if (attackRadius <= 0f) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (warningStyle != null) Destroy(warningStyle);
    }

    /// <summary>지금 공격을 시작할 수 있는지 확인합니다.</summary>
    private bool CanAttack()
    {
        if (isDead || isAttacking)
            return false;

        if (!HasAttackData())
            return false;

        if (target == null)
            return false;

        return IsTargetInRange();
    }

    /// <summary>공격에 필요한 참조가 있는지 확인합니다.</summary>
    private bool HasAttackData()
    {
        bool isValid = fog != null &&
                       explosionDamageEffect != null &&
                       abilitySystem != null &&
                       GetFogRadius() > 0f;

        if (isValid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError(
                $"[{nameof(ShadowServant)}] 공격 설정이 비어 있습니다.",
                this);

            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>플레이어가 공격 범위 안에 있는지 확인합니다.</summary>
    private bool IsTargetInRange()
    {
        if (target == null)
            return false;

        float attackRadius = GetAttackRadius();
        if (attackRadius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        return toTarget.sqrMagnitude <= attackRadius * attackRadius;
    }

    /// <summary>경고 후 폭발까지의 공격 순서를 처리합니다.</summary>
    private IEnumerator RunAttack()
    {
        isAttacking = true;

        Vector3 targetPoint = target != null ? target.position : transform.position;
        Vector3 hitPoint = GetHitPoint(targetPoint);
        ShowWarning(hitPoint);

        yield return new WaitForSeconds(AttackDelay);

        if (isDead)
        {
            ClearAttack();
            yield break;
        }

        PlayAttackAnimation();

        Explode(hitPoint);
        SpawnFog(targetPoint);
        ClearAttack();
    }

    /// <summary>원형 경고를 표시합니다.</summary>
    private void ShowWarning(Vector3 targetPoint)
    {
        if (telegraphService == null) return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            targetPoint,
            GetFogDiameter(),
            AttackDelay,
            warningStyle);

        telegraphService.Show(spec);
    }

    /// <summary>현재 공격 상태를 정리합니다.</summary>
    private void ClearAttack()
    {
        attackRoutine = null;
        isAttacking = false;

        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    /// <summary>지정 위치에서 폭발 피해를 적용합니다.</summary>
    private void Explode(Vector3 targetPoint)
    {
        CombatHitPayload payload = MakeHitPayload();
        if (payload == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            targetPoint,
            GetFogRadius(),
            GetDamageMask());

        damagedTargets.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            GameObject hitTarget = CombatTargetResolver2D.ResolveDamageTarget(hit);

            if (hitTarget == null || hitTarget == gameObject)
                continue;

            if (!damagedTargets.Add(hitTarget))
                continue;

            CombatHitPayloadApplier.Apply(hitTarget, payload, hit.ClosestPoint(targetPoint));
        }
    }

    /// <summary>폭발 뒤 안개를 생성합니다.</summary>
    private void SpawnFog(Vector3 targetPoint)
    {
        Instantiate(fog, new Vector3(targetPoint.x, targetPoint.y, 0f), Quaternion.identity);
    }

    /// <summary>플레이어 피해 레이어를 구합니다.</summary>
    private LayerMask GetDamageMask()
    {
        return target != null
            ? (LayerMask)(1 << target.gameObject.layer)
            : (LayerMask)0;
    }

    /// <summary>폭발용 공격 정보를 만듭니다.</summary>
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

    /// <summary>공격 범위를 구합니다.</summary>
    private float GetAttackRadius()
    {
        return ChaseIntent != null
            ? Mathf.Max(0f, ChaseIntent.StopRange)
            : 0f;
    }

    /// <summary>안개 반지름을 구합니다.</summary>
    private float GetFogRadius()
    {
        if (fog == null) return 0f;

        CircleCollider2D fogCollider = fog.GetComponent<CircleCollider2D>();
        if (fogCollider == null) return 0f;

        return Mathf.Max(0f, fogCollider.radius);
    }

    /// <summary>안개 지름을 구합니다.</summary>
    private float GetFogDiameter()
    {
        return GetFogRadius() * 2f;
    }

    /// <summary>안개 실제 중심을 구합니다.</summary>
    private Vector3 GetHitPoint(Vector3 targetPoint)
    {
        Vector2 fogOffset = GetFogOffset();
        return targetPoint + new Vector3(fogOffset.x, fogOffset.y, 0f);
    }

    /// <summary>안개 콜라이더 오프셋을 구합니다.</summary>
    private Vector2 GetFogOffset()
    {
        if (fog == null) return Vector2.zero;

        CircleCollider2D fogCollider = fog.GetComponent<CircleCollider2D>();
        if (fogCollider == null) return Vector2.zero;

        Vector3 scale = fog.transform.localScale;
        return new Vector2(
            fogCollider.offset.x * scale.x,
            fogCollider.offset.y * scale.y);
    }

    /// <summary>그림자 하수인 경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.35f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.35f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>폭발 직전 공격 애니메이션을 재생합니다.</summary>
    private void PlayAttackAnimation()
    {
        if (animator != null)
            animator.SetTrigger("attack");
    }
}
