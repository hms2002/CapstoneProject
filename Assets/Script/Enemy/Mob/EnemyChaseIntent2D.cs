using UnityEngine;
using UnityGAS;

public interface IEnemyChaseIntent
{
    // 이 인터페이스의 책임:
    // 일반 몬스터 FSM이 추적 상태의 생명주기만 제어할 수 있게 최소 추적 제어 표면을 제공한다.
    // 구체 추적 구현 세부는 숨기고, 상태 기계가 Start/Stop/Detection 판단만 의존하게 만든다.

    void StartChase();
    void StopChase();
    bool IsTargetWithinDetectionRange();
}

[DisallowMultipleComponent]
public sealed class EnemyChaseIntent2D : MonoBehaviour, IIntentMovementSource2D, IEnemyChaseIntent
{
    // 이 클래스의 책임:
    // 평소에는 플레이어 추적 의도 이동을 만들고, 복귀 상태일 때는 집으로 돌아가는 의도 이동을 우선 제공한다.
    // 일반 몬스터 FSM이 Chase 상태 생명주기에 맞춰 추적 시작/정지를 명시적으로 제어할 수 있는 창구를 제공한다.

    [Header("Refs")]
    [Tooltip("추적 대상 정보를 제공하는 Enemy 컴포넌트입니다.")]
    [SerializeField] private Enemy enemy;

    [Header("Chase")]
    [Tooltip("유저를 감지하고 추적을 시작하는 원형 범위의 반지름입니다. 지름 10타일이면 5를 입력합니다.")]
    [SerializeField] private float detectionRange = 5f;

    [Tooltip("이 거리 안에서는 추적 의도 이동을 멈춥니다.")]
    [SerializeField] private float stopRange = 0.8f;

    [Tooltip("현재 이동속도에 곱할 추적 속도 배율입니다.")]
    [SerializeField] private float speedScale = 1f;

    [Header("Return")]
    [Tooltip("집으로 돌아갈 때 사용할 속도 배율입니다.")]
    [SerializeField] private float returnSpeedScale = 0.9f;

    private IntentMovementData lastIntent;
    private bool ignoreDetectionRange;
    private bool chaseEnabled = true;
    private MonsterReturnHome2D returnHome;

    public float DetectionRange => detectionRange;
    public float StopRange => stopRange;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        RefreshReturnHomeReference();
    }

    public IntentMovementData GetIntent()
    {
        RefreshReturnHomeReference();

        if (returnHome != null && returnHome.TryGetReturnDirection(out Vector2 returnDirection))
        {
            lastIntent = IntentMovementData.FromDirection(returnDirection, returnSpeedScale);
            return lastIntent;
        }

        if (enemy == null || enemy.Target == null)
        {
            lastIntent = IntentMovementData.None;
            return lastIntent;
        }

        Mob mob = enemy as Mob;
        if (mob != null && !mob.CanUseChaseMovement())
        {
            lastIntent = IntentMovementData.None;
            return lastIntent;
        }

        if (!chaseEnabled)
        {
            lastIntent = IntentMovementData.None;
            return lastIntent;
        }

        Vector2 toTarget = (Vector2)(enemy.Target.position - transform.position);
        float sqrDistance = toTarget.sqrMagnitude;

        if (!ignoreDetectionRange && sqrDistance > detectionRange * detectionRange)
        {
            lastIntent = IntentMovementData.None;
            return lastIntent;
        }

        if (sqrDistance <= stopRange * stopRange)
        {
            lastIntent = IntentMovementData.None;
            return lastIntent;
        }

        Vector2 dir = toTarget.normalized;
        lastIntent = IntentMovementData.FromDirection(dir, speedScale);
        return lastIntent;
    }

    /// <summary>추적 속도 배율을 바꿉니다.</summary>
    public void SetSpeedScale(float value)
    {
        speedScale = Mathf.Max(0f, value);
    }

    /// <summary>추적 감지 범위를 런타임에 바꿉니다.</summary>
    public void SetDetectionRange(float value)
    {
        detectionRange = Mathf.Max(0f, value);
    }

    /// <summary>감지 범위 무시 여부를 바꿉니다.</summary>
    public void SetIgnoreDetectionRange(bool value)
    {
        ignoreDetectionRange = value;
    }

    /// <summary>FSM Chase 상태 진입 시 추적 의도 이동을 다시 허용합니다.</summary>
    public void StartChase()
    {
        chaseEnabled = true;
    }

    /// <summary>FSM이 Chase 상태를 벗어날 때 추적 의도 이동을 즉시 멈춥니다.</summary>
    public void StopChase()
    {
        chaseEnabled = false;
        lastIntent = IntentMovementData.None;
    }

    /// <summary>현재 플레이어 추적 조건이 유효한지 반환합니다.</summary>
    public bool IsTargetWithinDetectionRange()
    {
        if (enemy == null || enemy.Target == null)
            return false;

        Vector2 toTarget = (Vector2)(enemy.Target.position - transform.position);
        float sqrDistance = toTarget.sqrMagnitude;
        if (!ignoreDetectionRange && sqrDistance > detectionRange * detectionRange)
            return false;

        return true;
    }

    /// <summary>런타임에 뒤늦게 추가된 복귀 컴포넌트 참조를 안전하게 다시 잡습니다.</summary>
    private void RefreshReturnHomeReference()
    {
        if (returnHome != null)
            return;

        returnHome = GetComponent<MonsterReturnHome2D>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopRange);
    }
}
