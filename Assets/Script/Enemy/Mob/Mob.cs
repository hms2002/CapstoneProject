using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    [Header("참조")]
    [Tooltip("플레이어 추적 범위를 가진 컴포넌트입니다.")]
    [SerializeField] private EnemyChaseIntent2D chaseIntent;

    private bool hasMoveBool;

    protected EnemyChaseIntent2D ChaseIntent => chaseIntent;

    protected override void Awake()
    {
        base.Awake();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();

        hasMoveBool = CheckMoveBool();
    }

    private void Update()
    {
        if (isDead) return;

        UpdateAttack();
        UpdateAnimation();
    }

    /// <summary>필요한 공격 로직을 갱신합니다.</summary>
    protected virtual void UpdateAttack()
    {
    }

    /// <summary>이 몬스터가 추적 이동을 사용할지 정합니다.</summary>
    public virtual bool CanUseChaseMovement()
    {
        return true;
    }

    /// <summary>이동과 방향 애니메이션을 갱신합니다.</summary>
    protected virtual void UpdateAnimation()
    {
        if (animator != null && movementMotor != null && hasMoveBool)
            animator.SetBool(IsMovingHash, movementMotor.IsMoving);

        if (Target == null || sprite == null) return;

        if      (transform.position.x > Target.position.x) sprite.flipX = true;
        else if (transform.position.x < Target.position.x) sprite.flipX = false;
    }

    /// <summary></summary>
    private bool CheckMoveBool()
    {
        if (animator == null) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.nameHash == IsMovingHash)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (attribute == healthDef && newValue <= 0f && !isDead)
            Die();
    }

    /// <summary>넉백 요청을 KnockbackReceiver2D에 넘깁니다.</summary>
    public void ApplyKnockbackFrom(GameObject causer, float impulse)
    {
        if (isDead || knockbackReceiver == null) return;

        knockbackReceiver.ApplyKnockback(causer, impulse);
    }

    protected override void OnDeathStarted()
    {
        LootManager.Instance?.SpawnMonsterLoot(transform.position);
    }

    private void OnDrawGizmos()
    {
        DrawChaseGizmos();
        DrawAttackGizmos();
    }

    /// <summary>추적 범위를 기즈모로 그립니다.</summary>
    private void DrawChaseGizmos()
    {
        EnemyChaseIntent2D gizmoChaseIntent = chaseIntent != null
            ? chaseIntent
            : GetComponent<EnemyChaseIntent2D>();

        if (gizmoChaseIntent == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.DetectionRange);

        if (!CanDrawStopRangeGizmo()) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.StopRange);
    }

    /// <summary>정지 범위 기즈모를 그릴지 정합니다.</summary>
    protected virtual bool CanDrawStopRangeGizmo()
    {
        return true;
    }

    /// <summary>추가 공격 기즈모를 그립니다.</summary>
    protected virtual void DrawAttackGizmos()
    {
    }
}
