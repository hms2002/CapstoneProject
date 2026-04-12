using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class EnemyChaseIntent2D : MonoBehaviour, IIntentMovementSource2D
{
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

    private IntentMovementData lastIntent;
    private bool ignoreDetectionRange;

    public float DetectionRange => detectionRange;
    public float StopRange => stopRange;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();
    }

    public IntentMovementData GetIntent()
    {
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

    /// <summary>감지 범위 무시 여부를 바꿉니다.</summary>
    public void SetIgnoreDetectionRange(bool value)
    {
        ignoreDetectionRange = value;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopRange);
    }
}
