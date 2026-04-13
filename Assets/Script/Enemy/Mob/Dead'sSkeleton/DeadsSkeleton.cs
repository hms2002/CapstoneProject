using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임: 
/// 플레이어 추적 중 자폭 조건을 판단하고, 자폭 모드에서만 폭발/광원 사망 규칙이 적용되며 일반 피해는 무시하는 해골 몬스터의 전투 흐름을 관리한다.
/// </summary>
public class DeadsSkeleton : Mob, IDamageReceiver
{
    [Header("자폭 모드")]
    [Tooltip("자폭 경고와 실제 폭발 범위의 지름입니다.")]
    [SerializeField] private float explosionDiameter = 5f;

    [Tooltip("자폭 모드에 들어간 뒤 폭발까지 걸리는 시간입니다.")]
    [SerializeField] private float explodeDelay = 3f;

    [Tooltip("폭발에 사용할 데미지 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec explosionDamageEffect;

    [Tooltip("폭발 피해량입니다.")]
    [SerializeField] private float explosionDamage = 1f;

    private readonly HashSet<GameObject> damagedTargets = new();

    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle warningStyle;
    private SpriteMask sightMask;
    private Transform sightMaskTransform;
    private Vector3 defaultSightMaskScale = Vector3.one;
    private Tween sightMaskScaleTween;
    private bool isSelfDestruct;
    private bool canCancelSelfDestruct = true;
    private float explodeTime;
    private float selfDestructIntroEndTime;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = MakeWarningStyle();
        sightMask = GetComponentInChildren<SpriteMask>(true);
        sightMaskTransform = sightMask != null ? sightMask.transform : null;

        if (sightMaskTransform != null)
            defaultSightMaskScale = sightMaskTransform.localScale;

        EnsureContactTriggerCollider();
    }

    protected override void UpdateAttack()
    {
        if (isSelfDestruct)
        {
            if (ShouldCancelSelfDestruct())
            {
                CancelSelfDestruct();
                return;
            }

            TickSelfDestruct();
            return;
        }

        if (!CanStartSelfDestruct()) return;

        StartSelfDestruct();
    }

    /// <summary>자폭 전환 애니메이션 중에는 추적 이동을 멈춥니다.</summary>
    public override bool CanUseChaseMovement()
    {
        return !IsPlayingSelfDestructIntro();
    }

    /// <summary>해골은 체력 Attribute 변화로 사망하지 않도록 공통 체력 처리 훅을 비워 둡니다.</summary>
    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
    }

    protected override void OnDeathStarted()
    {
        HideWarning();
        base.OnDeathStarted();
    }

    protected override bool CanDrawStopRangeGizmo()
    {
        return false;
    }

    /// <summary>해골 스프라이트 기준에 맞춰 반대 방향으로 뒤집습니다.</summary>
    protected override void UpdateFacing()
    {
        if (Target == null || sprite == null) return;

        if (transform.position.x > Target.position.x) sprite.flipX = false;
        else if (transform.position.x < Target.position.x) sprite.flipX = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || other == null) return;

        GameObject contactObject = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (TryExplodeOnPlayerContact(contactObject))
            return;

        CandlestickLightZone lightZone = other.GetComponent<CandlestickLightZone>();
        if (lightZone == null)
            lightZone = other.GetComponentInParent<CandlestickLightZone>();

        if (lightZone == null || !isSelfDestruct) return;

        DieFromLight();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead || other == null) return;

        GameObject contactObject = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        TryExplodeOnPlayerContact(contactObject);
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
        selfDestructIntroEndTime = Time.time + GetSelfDestructIntroDuration();

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        if (animator != null)
            animator.SetBool("selfDestructionMode", true);

        PlaySightMaskExpand();
        ShowWarning();

        if (IsInsideCandlestickLight())
            DieFromLight();
    }

    /// <summary>자폭 유지 조건이 깨졌는지 확인합니다.</summary>
    private bool ShouldCancelSelfDestruct()
    {
        if (!isSelfDestruct)
            return false;

        if (!canCancelSelfDestruct)
            return false;

        if (ChaseIntent == null)
            return false;

        return !ChaseIntent.IsTargetWithinDetectionRange();
    }

    /// <summary>자폭 모드를 해제하고 일반 추적 상태로 되돌립니다.</summary>
    private void CancelSelfDestruct()
    {
        isSelfDestruct = false;
        explodeTime = 0f;
        selfDestructIntroEndTime = 0f;

        if (animator != null)
            animator.SetBool("selfDestructionMode", false);

        HideWarning();
        PlaySightMaskReset();
    }

    /// <summary>자폭 대기 시간을 갱신합니다.</summary>
    private void TickSelfDestruct()
    {
        if (IsInsideCandlestickLight())
        {
            DieFromLight();
            return;
        }

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

    /// <summary>자폭 전환과 동시에 시야 마스크를 폭발 지름만큼 확장합니다.</summary>
    private void PlaySightMaskExpand()
    {
        if (sightMask == null || sightMaskTransform == null || sightMask.sprite == null)
            return;

        float introDuration = GetSelfDestructIntroDuration();
        if (introDuration <= 0f)
        {
            sightMaskTransform.localScale = GetExplosionSightMaskScale();
            return;
        }

        if (sightMaskScaleTween != null && sightMaskScaleTween.IsActive())
            sightMaskScaleTween.Kill();

        sightMaskTransform.localScale = defaultSightMaskScale;
        sightMaskScaleTween = sightMaskTransform
            .DOScale(GetExplosionSightMaskScale(), introDuration)
            .SetEase(Ease.Linear);
    }

    /// <summary>시야 마스크 크기를 기본값으로 되돌립니다.</summary>
    private void ResetSightMaskScale()
    {
        if (sightMaskScaleTween != null && sightMaskScaleTween.IsActive())
            sightMaskScaleTween.Kill();

        if (sightMaskTransform != null)
            sightMaskTransform.localScale = defaultSightMaskScale;
    }

    /// <summary>시야 마스크 크기를 기본값으로 부드럽게 되돌립니다.</summary>
    private void PlaySightMaskReset()
    {
        if (sightMaskTransform == null)
            return;

        float introDuration = GetSelfDestructIntroDuration();
        float resetDuration = introDuration > 0f ? introDuration : 0.15f;

        if (sightMaskScaleTween != null && sightMaskScaleTween.IsActive())
            sightMaskScaleTween.Kill();

        sightMaskScaleTween = sightMaskTransform
            .DOScale(defaultSightMaskScale, resetDuration)
            .SetEase(Ease.Linear);
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

    /// <summary>일반 공격 피해를 무시하고 0 데미지 팝업만 표시합니다.</summary>
    public bool TryApplyDamage(DamageRequest request)
    {
        if (isDead)
            return false;

        DamagePopupService.ShowText("0", transform.position);
        return true;
    }

    /// <summary>플레이어와 접촉했을 때 즉시 폭발을 시도합니다.</summary>
    private bool TryExplodeOnPlayerContact(GameObject contactObject)
    {
        if (!isSelfDestruct)
            return false;

        if (contactObject == null || !contactObject.CompareTag("Player"))
            return false;

        Explode(contactObject);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 해골이 현재 촛대 광원 트리거 안에 이미 들어와 있는지 즉시 판정한다.
    /// - 자폭 모드 진입 전에 이미 빛 안에 있던 경우도 놓치지 않게 한다.
    /// </summary>
    private bool IsInsideCandlestickLight()
    {
        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;

        Collider2D[] overlaps = new Collider2D[16];
        int overlapCount = ownCollider.Overlap(filter, overlaps);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = overlaps[i];
            if (overlap == null)
                continue;

            if (overlap.GetComponent<CandlestickLightZone>() != null ||
                overlap.GetComponentInParent<CandlestickLightZone>() != null)
            {
                return true;
            }
        }

        return false;
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

            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(transform.position));
        }
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
        return GetExplosionRadius();
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
        canCancelSelfDestruct = !ignoreRange;

        if (ChaseIntent == null) return;

        ChaseIntent.SetSpeedScale(boostedSpeedScale);
        ChaseIntent.SetIgnoreDetectionRange(ignoreRange);
    }

    /// <summary>자폭 전환 애니메이션이 아직 재생 중인지 확인합니다.</summary>
    private bool IsPlayingSelfDestructIntro()
    {
        return isSelfDestruct && Time.time < selfDestructIntroEndTime;
    }

    /// <summary>자폭 전환 애니메이션 길이를 반환합니다.</summary>
    private float GetSelfDestructIntroDuration()
    {
        return Mathf.Max(0f, FindAnimationClipLength("DeadsSkeleton_BeSelfDestructionMode"));
    }

    /// <summary>플레이어 접촉 감지용 트리거 콜라이더를 보장합니다.</summary>
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
        if (bodyCollider == null)
            return;

        BoxCollider2D triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.offset = bodyCollider.offset;
        triggerCollider.size = bodyCollider.size;
        triggerCollider.edgeRadius = bodyCollider.edgeRadius;
    }

    /// <summary>폭발 지름에 맞는 시야 마스크 스케일을 계산합니다.</summary>
    private Vector3 GetExplosionSightMaskScale()
    {
        if (sightMask == null || sightMask.sprite == null || sightMaskTransform == null)
            return defaultSightMaskScale;

        Vector2 spriteSize = sightMask.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return defaultSightMaskScale;

        float explosionWorldDiameter = explosionDiameter;
        return new Vector3(
            explosionWorldDiameter / spriteSize.x,
            explosionWorldDiameter / spriteSize.y,
            defaultSightMaskScale.z);
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

        if (sightMaskScaleTween != null && sightMaskScaleTween.IsActive())
            sightMaskScaleTween.Kill();

        if (warningStyle != null)
            Destroy(warningStyle);
    }
}
