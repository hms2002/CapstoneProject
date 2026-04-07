using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("Mob's Settings")]
    [Tooltip("접촉 피해 및 기본 공격 시도 후 다시 공격할 수 있을 때까지의 시간입니다.")]
    [SerializeField] private float damageInterval = 1.0f;

    [Header("Mob's Ability")]
    [Tooltip("잡몹 기본 공격으로 사용할 GAS Ability입니다.")]
    [SerializeField] private AbilityDefinition tackleAbility;

    [Header("Referenses")]
    [Tooltip("플레이어 추적 의도를 제공하는 컴포넌트입니다.")]
    [SerializeField] private EnemyChaseIntent2D chaseIntent;
    //[SerializeField] private TagSystem tagSystem;

    [Header("Tackle Range")]
    [Tooltip("Tackle 공격 시도를 시작하는 원형 범위의 지름입니다.")]
    [SerializeField] private float tackleAttackRangeDiameter = 6.0f;

    [Tooltip("Tackle 실행 시 플레이어 방향으로 튀어나가는 고정 거리입니다.")]
    [SerializeField] private float tackleLungeDistance = 5.0f;

    [Tooltip("코킹 중 표시할 직사각형 돌진 예고 범위의 폭입니다.")]
    [SerializeField] private float tackleTelegraphWidth = 1.0f;

    [Header("Movement Tags")]
    [Tooltip("공격 후 쿨타임 동안 추적 의도 이동을 막는 태그")]
    [SerializeField] private GameplayTag blockIntentMoveTag;

    [Tooltip("사망/완전 정지 상태에서 모든 이동을 막는 태그")]
    [SerializeField] private GameplayTag freezeAllMovementTag;

    private float currentCooltime;

    private bool intentMoveTagApplied;
    private bool freezeMoveTagApplied;

    private bool hasPendingPreparedTackle;
    private PreparedTackleContext pendingPreparedTackle;
    private bool isTackleTelegraphVisible;
    private PreparedTackleContext activeTackleTelegraph;

    public float TackleAttackRangeRadius => Mathf.Max(0f, tackleAttackRangeDiameter * 0.5f);
    public bool IsPreparingTackle => isTackleTelegraphVisible;
    public bool HasTackleHitCooldown => currentCooltime > 0f;

    public struct PreparedTackleContext
    {
        public GameObject Target;
        public Vector2 StartPosition;
        public Vector2 Direction;
        public Vector2 ImpactPosition;
        public float LungeDistance;
        public float TelegraphWidth;
    }

    protected override void Awake()
    {
        base.Awake();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();

        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();
    }

    private void Update()
    {
        if (isDead) return;

        UpdateCooldown();
        SyncMovementTags();
        TryStartPreparedTackle();

        if      (transform.position.x > Target.position.x) sprite.flipX = true;
        else if (transform.position.x < Target.position.x) sprite.flipX = false;

        if (animator != null && movementMotor != null)
            animator.SetBool("isMoving", movementMotor.IsMoving);

    }

    /// <summary>Tackle 접촉 피해 쿨타임을 갱신합니다.</summary>
    private void UpdateCooldown()
    {
        if (currentCooltime <= 0f) return;

        currentCooltime -= Time.deltaTime;
        if (currentCooltime < 0f) currentCooltime = 0f;
    }

    /// <summary>현재 쿨타임과 Ability 실행 상태에 맞춰 이동 차단 태그를 동기화합니다.</summary>
    private void SyncMovementTags()
    {
        // 쿨타임/공격 실행 중에는 추적 의도 이동 금지
        bool shouldBlockIntentMove = currentCooltime > 0f || (abilitySystem != null && abilitySystem.IsBusy);
        SetTagActive(blockIntentMoveTag, shouldBlockIntentMove, ref intentMoveTagApplied);
    }

    /// <summary>공격 가능 범위에 들어온 타겟을 향해 준비 Tackle을 시도합니다.</summary>
    private void TryStartPreparedTackle()
    {
        if (target == null)                                         return;
        if (abilitySystem == null || tackleAbility == null)         return;
        if (currentCooltime > 0f)                                   return;
        if (abilitySystem.IsBusy)                                   return;
        if (abilitySystem.GetCooldownRemaining(tackleAbility) > 0f) return;
        if (!IsTargetInTackleAttackRange())                         return;

        PrepareTackleContext(target.gameObject);
        bool activated = abilitySystem.TryActivateAbility(tackleAbility, target.gameObject);

        if (!activated)
        {
            ClearPreparedTackleContext();
            return;
        }

        SyncMovementTags(); // 즉시 반영
    }

    /// <summary>현재 타겟이 Tackle 공격 가능 범위 안에 있는지 확인합니다.</summary>
    private bool IsTargetInTackleAttackRange()
    {
        if (target == null) return false;

        float attackRangeRadius = TackleAttackRangeRadius;
        if (attackRangeRadius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        return toTarget.sqrMagnitude <= attackRangeRadius * attackRangeRadius;
    }

    /// <summary>Tackle 실행에 사용할 고정 방향과 예고 범위 정보를 준비합니다.</summary>
    private void PrepareTackleContext(GameObject targetObject)
    {
        Vector2 startPosition = transform.position;
        Vector2 targetPosition = targetObject != null ? targetObject.transform.position : transform.position;
        Vector2 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        float lungeDistance = Mathf.Max(0f, tackleLungeDistance);

        if (distance <= 0.0001f)
        {
            direction = Vector2.right;
        }
        else
        {
            direction /= distance;
        }

        pendingPreparedTackle = new PreparedTackleContext
        {
            Target = targetObject,
            StartPosition = startPosition,
            Direction = direction,
            ImpactPosition = startPosition + direction * lungeDistance,
            LungeDistance = lungeDistance,
            TelegraphWidth = tackleTelegraphWidth
        };

        hasPendingPreparedTackle = true;
        ShowPreparedTackleTelegraph(pendingPreparedTackle);
    }

    /// <summary>준비된 Tackle 정보를 Ability Logic에서 사용할 수 있도록 꺼냅니다.</summary>
    public bool TryConsumePreparedTackleContext(out PreparedTackleContext context)
    {
        context = pendingPreparedTackle;

        if (!hasPendingPreparedTackle) return false;

        ClearPreparedTackleContext(false);
        return true;
    }

    /// <summary>현재 표시 중인 Tackle 예고 기즈모를 숨깁니다.</summary>
    public void HidePreparedTackleTelegraph()
    {
        isTackleTelegraphVisible = false;
        activeTackleTelegraph = default(PreparedTackleContext);
    }

    /// <summary>준비된 Tackle 정보를 기준으로 예고 기즈모를 표시합니다.</summary>
    private void ShowPreparedTackleTelegraph(PreparedTackleContext context)
    {
        activeTackleTelegraph = context;
        isTackleTelegraphVisible = true;
    }

    /// <summary>대기 중인 Tackle 정보와 필요 시 예고 기즈모를 정리합니다.</summary>
    private void ClearPreparedTackleContext(bool hideTelegraph = true)
    {
        hasPendingPreparedTackle = false;
        pendingPreparedTackle = default(PreparedTackleContext);

        if (hideTelegraph)
            HidePreparedTackleTelegraph();
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        if (isDead) return;
        if (!other.gameObject.CompareTag("Player")) return;
        if (currentCooltime > 0f) return;

        GameObject contactTarget = other.gameObject;
        bool damageApplied = TryApplyContactTackleDamage(contactTarget);

        if (damageApplied)
            StartTackleHitCooldown();
    }

    /// <summary>플레이어와 접촉 중일 때 Tackle 접촉 피해를 즉시 적용합니다.</summary>
    private bool TryApplyContactTackleDamage(GameObject contactTarget)
    {
        if (abilitySystem == null || tackleAbility == null || contactTarget == null)
            return false;

        AL_Tackle tackleLogic = tackleAbility.logic as AL_Tackle;
        if (tackleLogic == null)
        {
            if (abilitySystem.IsBusy)
                return false;

            return abilitySystem.TryActivateAbility(tackleAbility, contactTarget);
        }

        AbilitySpec tackleSpec = abilitySystem.FindSpec(tackleAbility);
        return tackleLogic.TryApplyContactDamage(abilitySystem, tackleSpec, contactTarget);
    }

    /// <summary>Tackle 접촉 피해 이후 Mob과 GAS 쿨타임을 함께 시작합니다.</summary>
    public void StartTackleHitCooldown()
    {
        currentCooltime = damageInterval;

        if (abilitySystem != null && tackleAbility != null)
            abilitySystem.TrySetCooldownRemaining(tackleAbility, damageInterval);

        SyncMovementTags(); // 즉시 반영
    }

    /// <summary>체력 Attribute가 0 이하가 되면 사망 처리를 실행합니다.</summary>
    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (attribute == healthDef && newValue <= 0f && !isDead)
            Die();
    }

    /// <summary>causer와 impulse를 받아 KnockbackReceiver2D로 넉백을 위임합니다.</summary>
    public void ApplyKnockbackFrom(GameObject causer, float impulse)
    {
        if (isDead) return;
        if (knockbackReceiver == null) return;

        knockbackReceiver.ApplyKnockback(causer, impulse);
    }

    /// <summary>Mob 사망 시 태그와 Tackle 상태를 정리하고 공통 사망 처리를 실행합니다.</summary>
    protected override void OnDeathStarted()
    {
        // 쿨타임 기반 이동 차단은 정리
        ClearPreparedTackleContext();
        SetTagActive(blockIntentMoveTag, false, ref intentMoveTagApplied);

        // 사망 중에는 완전 정지
        SetTagActive(freezeAllMovementTag, true, ref freezeMoveTagApplied);

        LootManager.Instance?.SpawnMonsterLoot(transform.position);
    }

    private void OnDisable()
    {
        // 풀링/비정상 종료에도 태그 잔류 방지
        ClearPreparedTackleContext();
        SetTagActive(blockIntentMoveTag, false, ref intentMoveTagApplied);
        SetTagActive(freezeAllMovementTag, false, ref freezeMoveTagApplied);
    }

    /// <summary>지정한 GameplayTag의 적용 상태를 요청 값에 맞게 변경합니다.</summary>
    private void SetTagActive(GameplayTag tag, bool shouldBeActive, ref bool appliedFlag)
    {
        if (tagSystem == null || tag == null)
            return;

        if (shouldBeActive)
        {
            if (appliedFlag)
                return;

            tagSystem.AddTag(tag);
            appliedFlag = true;
        }
        else
        {
            if (!appliedFlag)
                return;

            tagSystem.RemoveTag(tag);
            appliedFlag = false;
        }
    }

    private void OnDrawGizmos()
    {
        EnemyChaseIntent2D gizmoChaseIntent = chaseIntent != null
            ? chaseIntent
            : GetComponent<EnemyChaseIntent2D>();

        if (gizmoChaseIntent != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.DetectionRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.StopRange);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, TackleAttackRangeRadius);

        DrawTackleGizmo();
    }

    /// <summary>코킹 중인 Tackle의 직사각형 예고 범위를 기즈모로 그립니다.</summary>
    private void DrawTackleGizmo()
    {
        if (!isTackleTelegraphVisible) return;

        float length    = Mathf.Max(0.01f, activeTackleTelegraph.LungeDistance);
        float width     = Mathf.Max(0.01f, activeTackleTelegraph.TelegraphWidth);

        Vector2 direction = activeTackleTelegraph.Direction.sqrMagnitude > 0.0001f
            ? activeTackleTelegraph.Direction.normalized
            : Vector2.right;

        Vector3     center          = activeTackleTelegraph.StartPosition + direction * (length * 0.5f);
        Quaternion  rotation        = Quaternion.FromToRotation(Vector3.right, direction);
        Matrix4x4   previousMatrix  = Gizmos.matrix;

        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, width, 0f));
        Gizmos.matrix = previousMatrix;
    }
}
