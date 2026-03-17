using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class EnemyChaseIntent2D : MonoBehaviour, IIntentMovementSource2D
{
    [Header("Refs")]
    [SerializeField] private Enemy enemy;

    [Header("Chase")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float stopRange = 0.8f;
    [SerializeField] private float speedScale = 1f;

    private IntentMovementData lastIntent;

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

        Vector2 toTarget = (Vector2)(enemy.Target.position - transform.position);
        float sqrDistance = toTarget.sqrMagnitude;

        if (sqrDistance > detectionRange * detectionRange)
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stopRange);
    }
}