using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Mob))]
public class TackleAttack : MonoBehaviour
{
    // 이 클래스의 책임:
    // 태클 발동 가능 여부를 판단하고, 태클 경고/돌진에 필요한 문맥을 준비하며, 태클 중 이동 차단 상태를 관리한다.

    private const int WallLayer = 30;

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

    [Tooltip("태클 경고를 표시할 서비스입니다.")]
    [SerializeField] private AttackTelegraphService telegraph;

    private Mob mob;
    private AbilitySystem abilitySystem;
    private TagSystem tagSystem;
    private float delayTime;
    private bool blockMoveApplied;
    private bool hasContext;
    private TackleContext tackleContext;

    public float RangeRadius => Mathf.Max(0f, attackRangeDiameter * 0.5f);
    public bool IsPreparing => telegraph != null && telegraph.HasActiveTelegraph;
    public bool HasDelay => delayTime > 0f;

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
        abilitySystem = GetComponent<AbilitySystem>();
        tagSystem = GetComponent<TagSystem>();

        if (telegraph == null)
            telegraph = GetComponent<AttackTelegraphService>();

        EnsureContactTriggerCollider();
    }

    private void Update()
    {
        if (mob == null) return;

        if (mob.IsDead)
        {
            ClearContext();
            SetTag(blockMoveTag, false, ref blockMoveApplied);
            return;
        }

        TickDelay();
        UpdateTags();
        TryAttack();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (mob == null || mob.IsDead)
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
        bool shouldBlock = HasDelay || (abilitySystem != null && abilitySystem.IsBusy);
        SetTag(blockMoveTag, shouldBlock, ref blockMoveApplied);
    }

    /// <summary>태클 발동 조건을 확인하고 어빌리티를 실행합니다.</summary>
    private void TryAttack()
    {
        if (!CanAttack()) return;

        MakeContext();

        bool isActivated = abilitySystem.TryActivateAbility(tackleAbility, mob.Target.gameObject);
        if (!isActivated)
        {
            ClearContext();
            return;
        }

        UpdateTags();
    }

    /// <summary>지금 태클을 시작할 수 있는지 확인합니다.</summary>
    private bool CanAttack()
    {
        if (mob == null || mob.Target == null)
            return false;

        if (abilitySystem == null || tackleAbility == null)
            return false;

        if (HasDelay || abilitySystem.IsBusy)
            return false;

        if (abilitySystem.GetCooldownRemaining(tackleAbility) > 0f)
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

    /// <summary>태클에 쓸 방향과 범위를 저장합니다.</summary>
    private void MakeContext()
    {
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
        if (telegraph != null)
            telegraph.HideCurrent();
    }

    /// <summary>태클 경고를 화면에 표시합니다.</summary>
    public void ShowTelegraph(TackleContext context, float duration, AttackTelegraphStyle style = null)
    {
        if (telegraph == null) return;

        float length = Mathf.Max(0f, context.LungeDistance);
        Vector3 center = context.StartPos + context.Direction * (length * 0.5f);
        float angle = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, Mathf.Max(0.01f, context.TelegraphWidth)),
            angle,
            duration,
            style);

        telegraph.Show(spec);
    }

    /// <summary>태클 딜레이를 시작합니다.</summary>
    public void StartDelay()
    {
        delayTime = Mathf.Max(0f, hitDelay);

        if (abilitySystem != null && tackleAbility != null)
            abilitySystem.TrySetCooldownRemaining(tackleAbility, delayTime);

        UpdateTags();
    }

    /// <summary>플레이어 접촉 시 태클 피해를 적용합니다.</summary>
    private bool HitPlayer(GameObject target)
    {
        if (abilitySystem == null || tackleAbility == null || target == null)
            return false;

        AL_Tackle logic = tackleAbility.logic as AL_Tackle;
        if (logic == null)
        {
            if (abilitySystem.IsBusy)
                return false;

            return abilitySystem.TryActivateAbility(tackleAbility, target);
        }

        AbilitySpec spec = abilitySystem.FindSpec(tackleAbility);
        return logic.TryApplyContactDamage(abilitySystem, spec, target);
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
        if (tagSystem == null || tag == null) return;

        if (active)
        {
            if (applied) return;

            tagSystem.AddTag(tag);
            applied = true;
            return;
        }

        if (!applied) return;

        tagSystem.RemoveTag(tag);
        applied = false;
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
