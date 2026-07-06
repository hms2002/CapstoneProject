using UnityEngine;
using UnityGAS;
using CapstoneAudio;

/// <summary>
/// 책임:
/// - 태클 발동 가능 여부를 판단하고, 태클 경고/돌진에 필요한 문맥을 준비한다.
/// - bridge를 통한 태클 실행 요청과 태클 중 이동 차단 상태, 적중 후 재공격 지연을 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Mob))]
public class TackleAttack : MonoBehaviour, IMobAttackDecisionSource, IMobPresentationCleanup
{
    private const int WallLayer = 30;
    private static readonly SoundRef ShadowMonsterAttackSound = SoundRef.FromKey("sound_shadowMonster_dash");
    private static readonly SoundRef TreasureMonsterAttackSound = SoundRef.FromKey("sound_treasureMonster_dash");

    [Header("태클")]
    [Tooltip("태클에 사용할 GAS 어빌리티입니다.")]
    [SerializeField] private AbilityDefinition tackleAbility;

    [Tooltip("태클 피해 후 다시 공격하기까지의 시간입니다.")]
    [SerializeField] private float hitDelay = 1f;

    [Tooltip("태클을 시작하는 원형 범위의 지름입니다.")]
    [SerializeField] private float attackRangeDiameter = 6f;

    [Tooltip("태클 돌진 거리입니다.")]
    [SerializeField] private float lungeDistance = 5f;

    [Tooltip("태클 경고 범위의 폭입니다.")]
    [SerializeField] private float telegraphWidth = 1f;

    [Tooltip("태클 중 추적 이동을 막는 태그입니다.")]
    [SerializeField] private GameplayTag blockMoveTag;

    [Tooltip("태클 경고를 표시할 presenter 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour telegraph;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;

    [Header("Sound")]
    [Tooltip("태클 공격 시작 타이밍에 재생할 사운드입니다. 비워두면 일부 몬스터 전용 기본 사운드를 사용합니다.")]
    [SerializeField] private SoundRef attackSound;

    [Header("Legacy")]
    [Tooltip("켜면 FSM을 거치지 않고 TackleAttack.Update에서 직접 태클을 시작합니다. 일반 몬스터 FSM 사용 대상은 꺼두는 것이 기본입니다.")]
    [SerializeField] private bool allowLegacyUpdateActivation;

    [Header("Animation")]
    [Tooltip("태클 준비/공격 트리거를 받을 Animator입니다. 비워두면 자식까지 포함해 자동 탐색합니다.")]
    [SerializeField] private Animator animator;

    [Tooltip("태클 경고가 시작될 때 호출할 Animator Trigger입니다. 비워두면 호출하지 않습니다.")]
    [SerializeField] private string attackReadyTriggerName = "attackReady";

    [Tooltip("태클 돌진이 시작될 때 호출할 Animator Trigger입니다. 비워두면 호출하지 않습니다.")]
    [SerializeField] private string attackTriggerName = "attack";

    private Mob mob;
    private IMobAbilityHelperAccess helperAccess;
    private float delayTime;
    private bool blockMoveApplied;
    private bool attackPreparationMoveBlocked;
    private bool hasContext;
    private TackleContext tackleContext;
    private IAttackTelegraphPresenter telegraphPresenter;
    private int attackReadyTriggerHash;
    private int attackTriggerHash;
    private bool hasAttackReadyTrigger;
    private bool hasAttackTrigger;

    public float RangeRadius => Mathf.Max(0f, attackRangeDiameter * 0.5f);
    public bool IsPreparing => telegraphPresenter != null && telegraphPresenter.HasActiveTelegraph;
    public bool HasDelay => delayTime > 0f;

    // 책임: 태클 공격의 대상, 시작점, 방향, 돌진 거리와 경고 폭을 보관한다.
    public struct TackleContext
    {
        public GameObject Target;
        public Vector2 StartPos;
        public Vector2 Direction;
        public float LungeDistance;
        public float TelegraphWidth;
    }

    private void Awake()
    {
        mob = GetComponent<Mob>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator == null)
            abilityCoordinator = gameObject.AddComponent<MobAbilityCoordinator>();
        helperAccess = abilityCoordinator as IMobAbilityHelperAccess;

        telegraphPresenter = AttackTelegraphPresenterResolver.Resolve(telegraph, this);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        CacheAnimatorTriggers();
        EnsureContactTriggerCollider();
    }

    private void Update()
    {
        if (mob == null) return;

        if (mob.IsDead)
        {
            ClearContext();
            SetAttackPreparationMoveBlocked(false);
            return;
        }

        if (abilityCoordinator != null && abilityCoordinator.IsAbilityExecutionSuppressed)
        {
            ClearContext();
            HideTelegraph();
            SetAttackPreparationMoveBlocked(false);
            return;
        }

        TickDelay();
        UpdateTags();

        if (allowLegacyUpdateActivation)
            TryRequestTackle();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (mob == null || mob.IsDead)
            return;

        if (abilityCoordinator != null && abilityCoordinator.IsAbilityExecutionSuppressed)
            return;

        if (other == null)
            return;

        GameObject contactObject = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (contactObject == null || !contactObject.CompareTag("Player"))
            return;

        if (HasDelay) return;
        if (!HasClearPathTo(contactObject)) return;

        if (HitPlayer(contactObject))
            StartDelay();
    }

    private void OnDisable()
    {
        ClearContext();
        HideTelegraph();
        attackPreparationMoveBlocked = false;
        SetTag(blockMoveTag, false, ref blockMoveApplied);
    }

    private void OnDrawGizmos()
    {
        DrawRange();
        DrawBox();
    }

    /// <summary>태클 딜레이를 줄입니다.</summary>
    private void TickDelay()
    {
        if (delayTime <= 0f) return;

        delayTime -= Time.deltaTime;
        if (delayTime < 0f)
            delayTime = 0f;
    }

    /// <summary>태클 상태에 맞춰 이동 차단 태그를 맞춥니다.</summary>
    private void UpdateTags()
    {
        bool shouldBlock = attackPreparationMoveBlocked ||
                           HasDelay ||
                           (abilityCoordinator != null && abilityCoordinator.IsAbilityExecutionBusy);
        SetTag(blockMoveTag, shouldBlock, ref blockMoveApplied);
    }

    /// <summary>태클 실행 가능 여부를 평가하고, 가능하면 bridge를 통해 실행을 요청합니다.</summary>
    public bool TryRequestTackle()
    {
        if (!CanTryTackle())
            return false;

        if (!TryBuildTackleContext(out _))
            return false;

        bool isActivated = abilityCoordinator != null &&
                           abilityCoordinator.TryStartAbility(tackleAbility, mob.Target.gameObject);
        if (!isActivated)
        {
            ClearContext();
            return false;
        }

        UpdateTags();
        return true;
    }

    /// <summary>FSM AttackState가 사용할 태클 요청을 구성합니다.</summary>
    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        if (!TryBuildTackleContext(out TackleContext context))
            return false;

        request = new MobAttackRequest(tackleAbility, context.Target);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 태클 helper가 추가로 처리할 것이 없어 비워 둡니다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
    }

    /// <summary>공격 상태 종료 시 남은 태클 문맥과 경고를 정리합니다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        ClearContext();
        HideTelegraph();
        SetAttackPreparationMoveBlocked(false);
    }

    /// <summary>지금 태클을 시작할 수 있는지 확인합니다.</summary>
    public bool CanTryTackle()
    {
        if (mob == null || mob.Target == null)
            return false;

        if (abilityCoordinator == null || tackleAbility == null)
            return false;

        if (abilityCoordinator.IsAbilityExecutionSuppressed)
            return false;

        if (HasDelay || abilityCoordinator.IsAbilityExecutionBusy)
            return false;

        if (helperAccess != null && helperAccess.GetCooldownRemaining(tackleAbility) > 0f)
            return false;

        return InRange() && HasClearPathToTarget();
    }

    /// <summary>플레이어가 태클 범위 안에 있는지 확인합니다.</summary>
    private bool InRange()
    {
        if (mob == null || mob.Target == null)
            return false;

        float radius = RangeRadius;
        if (radius <= 0f) return false;

        Vector2 toTarget = (Vector2)(mob.Target.position - transform.position);
        return toTarget.sqrMagnitude <= radius * radius;
    }

    /// <summary>태클 시작점과 타깃 사이에 벽이 있는지 확인합니다.</summary>
    private bool HasClearPathToTarget()
    {
        if (mob == null || mob.Target == null)
            return false;

        return HasClearPathTo(mob.Target.gameObject);
    }

    /// <summary>현재 위치에서 지정한 대상까지 벽이 없는지 확인합니다.</summary>
    public bool HasClearPathTo(GameObject target)
    {
        if (target == null)
            return false;

        Vector2 start = transform.position;
        Vector2 end = target.transform.position;
        Vector2 toTarget = end - start;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(start, toTarget / distance, distance, 1 << WallLayer);
        return hit.collider == null;
    }

    /// <summary>태클 실행에 필요한 문맥을 만들고 내부 저장소에 보관합니다.</summary>
    public bool TryBuildTackleContext(out TackleContext context)
    {
        context = default;

        if (mob == null || mob.Target == null)
            return false;

        Vector2 startPos = transform.position;
        Vector2 targetPos = mob.Target != null ? (Vector2)mob.Target.position : startPos;
        Vector2 direction = targetPos - startPos;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;
        else
            direction.Normalize();

        tackleContext = new TackleContext
        {
            Target = mob.Target != null ? mob.Target.gameObject : null,
            StartPos = startPos,
            Direction = direction,
            LungeDistance = Mathf.Max(0f, lungeDistance),
            TelegraphWidth = telegraphWidth
        };

        hasContext = true;
        context = tackleContext;
        return true;
    }

    /// <summary>저장한 태클 정보를 꺼내고 비웁니다.</summary>
    public bool TryGetContext(out TackleContext context)
    {
        context = tackleContext;

        if (!hasContext) return false;

        hasContext = false;
        tackleContext = default;
        return true;
    }

    /// <summary>현재 태클 경고를 숨깁니다.</summary>
    public void HideTelegraph()
    {
        telegraphPresenter?.HideCurrent();
    }

    /// <summary>태클 준비 구간 동안 추적 이동을 막을지 설정합니다.</summary>
    public void SetAttackPreparationMoveBlocked(bool blocked)
    {
        attackPreparationMoveBlocked = blocked;
        UpdateTags();
    }

    /// <summary>
    /// 책임 :
    /// - 태클 helper가 생성한 telegraph를 suppression / death / disable 같은 전역 종료 경로에서 정리한다.
    /// - 전투 객체가 helper 구현 세부를 몰라도 presentation cleanup을 공통 계약으로 호출하게 한다.
    /// </summary>
    public void CleanupPresentation()
    {
        HideTelegraph();
        SetAttackPreparationMoveBlocked(false);
    }

    /// <summary>태클 경고를 화면에 표시합니다.</summary>
    public void ShowTelegraph(TackleContext context, float duration, AttackTelegraphStyle style = null)
    {
        if (telegraphPresenter == null) return;

        float length = Mathf.Max(0f, context.LungeDistance);
        Vector3 center = context.StartPos + context.Direction * (length * 0.5f);
        float angle = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, Mathf.Max(0.01f, context.TelegraphWidth)),
            angle,
            duration,
            style);

        spec = ApplyMonsterSpecificTelegraphPresentation(spec);
        telegraphPresenter.Show(spec);
    }

    /// <summary>몬스터별 경고선 표시 정책을 적용합니다.</summary>
    private AttackTelegraphSpec ApplyMonsterSpecificTelegraphPresentation(AttackTelegraphSpec spec)
    {
        if (GetComponent<TreasureMonster>() != null || GetComponent<ShadowMonster>() != null || GetComponent<CorridorCandlestickMonster>() != null)
            return AttackTelegraphSpecUtility.WithThinWarningOutlineOnly(spec);

        return spec;
    }

    /// <summary>태클 준비 애니메이션 트리거를 선택적으로 호출합니다.</summary>
    public void PlayAttackReadyAnimation()
    {
        SetAnimatorTriggerIfAvailable(attackReadyTriggerHash, hasAttackReadyTrigger);
    }

    /// <summary>태클 공격 애니메이션 트리거를 선택적으로 호출합니다.</summary>
    public void PlayAttackAnimation()
    {
        SetAnimatorTriggerIfAvailable(attackTriggerHash, hasAttackTrigger);
    }

    /// <summary>태클 공격 시작 타이밍에 몬스터별 사운드를 재생합니다.</summary>
    public void PlayAttackSound()
    {
        SoundRef sound = attackSound.IsSet ? attackSound : ResolveFallbackAttackSound();
        if (!sound.IsSet)
            return;

        SoundPlaybackUtility.Play(
            sound,
            instigator: gameObject,
            causer: gameObject,
            target: mob != null && mob.Target != null ? mob.Target.gameObject : null,
            position: transform.position,
            sourceObject: this);
    }

    /// <summary>태클 딜레이를 시작합니다.</summary>
    public void StartDelay()
    {
        delayTime = CombatTimingService.ScaleSeconds(
            GetComponent<AbilitySystem>(),
            hitDelay,
            CombatTimingSlot.AttackInterval);

        if (helperAccess != null && tackleAbility != null)
            helperAccess.TrySetCooldownRemaining(tackleAbility, delayTime);

        UpdateTags();
    }

    /// <summary>플레이어 접촉 시 태클 피해를 적용합니다.</summary>
    private bool HitPlayer(GameObject target)
    {
        if (abilityCoordinator == null || tackleAbility == null || target == null)
            return false;

        if (abilityCoordinator.IsAbilityExecutionSuppressed)
            return false;

        AL_Tackle logic = tackleAbility.logic as AL_Tackle;
        if (logic == null)
        {
            if (abilityCoordinator.IsAbilityExecutionBusy)
                return false;

            return abilityCoordinator.TryStartAbility(tackleAbility, target);
        }

        if (helperAccess == null ||
            !helperAccess.TryGetAbilityExecutionContext(tackleAbility, out AbilitySystem system, out AbilitySpec spec))
            return false;

        return logic.TryApplyContactDamage(system, spec, target);
    }

    /// <summary>저장한 태클 정보를 비웁니다.</summary>
    private void ClearContext()
    {
        hasContext = false;
        tackleContext = default;
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

    /// <summary>태그를 켜거나 끕니다.</summary>
    private void SetTag(GameplayTag tag, bool active, ref bool applied)
    {
        if (helperAccess == null || tag == null) return;

        if (active)
        {
            if (applied) return;

            applied = helperAccess.TryAddStateTag(tag);
            return;
        }

        if (!applied) return;

        if (helperAccess.TryRemoveStateTag(tag))
            applied = false;
    }

    /// <summary>Animator 파라미터 목록을 확인해 실제 존재하는 트리거만 캐시합니다.</summary>
    private void CacheAnimatorTriggers()
    {
        attackReadyTriggerHash = 0;
        attackTriggerHash = 0;
        hasAttackReadyTrigger = false;
        hasAttackTrigger = false;

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        attackReadyTriggerHash = string.IsNullOrWhiteSpace(attackReadyTriggerName)
            ? 0
            : Animator.StringToHash(attackReadyTriggerName);
        attackTriggerHash = string.IsNullOrWhiteSpace(attackTriggerName)
            ? 0
            : Animator.StringToHash(attackTriggerName);

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != AnimatorControllerParameterType.Trigger)
                continue;

            if (attackReadyTriggerHash != 0 && parameter.nameHash == attackReadyTriggerHash)
                hasAttackReadyTrigger = true;

            if (attackTriggerHash != 0 && parameter.nameHash == attackTriggerHash)
                hasAttackTrigger = true;
        }
    }

    /// <summary>Animator와 트리거가 준비된 경우에만 안전하게 트리거를 재시작합니다.</summary>
    private void SetAnimatorTriggerIfAvailable(int triggerHash, bool hasTrigger)
    {
        if (!hasTrigger || triggerHash == 0 || animator == null || !animator.isActiveAndEnabled)
            return;

        animator.ResetTrigger(triggerHash);
        animator.SetTrigger(triggerHash);
    }

    /// <summary>직렬화 사운드가 비어 있을 때 기존 몬스터 타입별 기본 태클 사운드를 선택합니다.</summary>
    private SoundRef ResolveFallbackAttackSound()
    {
        if (GetComponent<ShadowMonster>() != null)
            return ShadowMonsterAttackSound;

        if (GetComponent<TreasureMonster>() != null)
            return TreasureMonsterAttackSound;

        return default;
    }

    /// <summary>태클 원형 범위를 그립니다.</summary>
    private void DrawRange()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, RangeRadius);
    }

    /// <summary>준비된 태클 경고 범위를 그립니다.</summary>
    private void DrawBox()
    {
        if (!hasContext) return;

        float length = Mathf.Max(0.01f, tackleContext.LungeDistance);
        float width = Mathf.Max(0.01f, tackleContext.TelegraphWidth);
        Vector2 direction = tackleContext.Direction.sqrMagnitude > 0.0001f
            ? tackleContext.Direction.normalized
            : Vector2.right;

        Vector3 center = tackleContext.StartPos + direction * (length * 0.5f);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, direction);
        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, width, 0f));
        Gizmos.matrix = oldMatrix;
    }
}
