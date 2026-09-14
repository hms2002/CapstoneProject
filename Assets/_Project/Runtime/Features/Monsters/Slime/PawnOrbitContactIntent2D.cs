using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Pawn이 플레이어에게 접근한 뒤 정면으로 밀지 않고 주변을 접선 방향으로 돌며 압박하는 이동 의도를 만든다.
/// - 일반 몬스터 FSM의 추적 생명주기와 MovementMotor2D 의도 이동 인터페이스를 함께 만족한다.
/// - 개체별로 엇갈리는 짧은 전진과 휴식을 만들어 기어가는 이동 리듬을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PawnOrbitContactIntent2D : MonoBehaviour, IIntentMovementSource2D, IEnemyChaseIntent
{
    [Header("Refs")]
    [SerializeField] private Enemy enemy;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRange = 7f;
    [SerializeField, Min(0f)] private float targetAcquireInterval = 0.25f;

    [Header("Approach")]
    [SerializeField, Min(0f)] private float approachRange = 1.15f;
    [SerializeField, Min(0f)] private float approachSpeedScale = 2f;

    [Header("Orbit")]
    [SerializeField, Min(0.01f)] private float idealOrbitRadius = 0.72f;
    [SerializeField, Min(0f)] private float orbitSpeedScale = 1.55f;
    [SerializeField, Min(0f)] private float inwardPressure = 0.42f;
    [SerializeField, Min(0f)] private float outwardPressure = 0.75f;
    [SerializeField, Min(0f)] private float orbitRadiusDeadZone = 0.12f;

    [Header("Return")]
    [SerializeField, Min(0f)] private float returnSpeedScale = 0.9f;

    [Header("Crawl Rhythm")]
    [SerializeField, Min(0.02f)] private float crawlMoveSeconds = 0.22f;
    [SerializeField, Min(0f)] private float crawlRestSeconds = 0.32f;
    [SerializeField, Range(0f, 1f)] private float crawlPeakSpeedScale = 0.55f;

    private MonsterReturnHome2D returnHome;
    private bool chaseEnabled = true;
    private int orbitSign = 1;
    private float nextTargetAcquireTime;
    private double crawlEpoch;
    private float crawlPhaseOffset;

    private void OnEnable()
    {
        // Hash the instance without consuming gameplay RNG; pooled reuse restarts its clock.
        uint hash = unchecked((uint)GetInstanceID() * 2654435761u);
        crawlPhaseOffset = (hash & 0x00ffffffu) / 16777216f;
        crawlEpoch = Time.timeAsDouble;
    }

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        returnHome = GetComponent<MonsterReturnHome2D>();
        orbitSign = GetInstanceID() % 2 == 0 ? 1 : -1;
    }

    public IntentMovementData GetIntent()
    {
        if (returnHome != null && returnHome.TryGetReturnDirection(out Vector2 returnDirection))
            return CreateCrawlIntent(returnDirection, returnSpeedScale);

        if (!EnsureTarget())
            return IntentMovementData.None;

        Mob mob = enemy as Mob;
        if (mob != null && !mob.CanUseChaseMovement())
            return IntentMovementData.None;

        if (!chaseEnabled || !enemy.CanPerceiveTarget(enemy.Target))
            return IntentMovementData.None;

        Vector2 toTarget = (Vector2)(enemy.Target.position - transform.position);
        float distance = toTarget.magnitude;
        if (distance > detectionRange)
            return IntentMovementData.None;

        if (distance > approachRange)
            return CreateCrawlIntent(toTarget.normalized, approachSpeedScale);

        Vector2 direction = ResolveOrbitDirection(toTarget, distance);
        return CreateCrawlIntent(direction, orbitSpeedScale);
    }

    private IntentMovementData CreateCrawlIntent(Vector2 direction, float speedScale)
    {
        float moveSeconds = Mathf.Max(0.02f, crawlMoveSeconds);
        float cycleSeconds = moveSeconds + Mathf.Max(0f, crawlRestSeconds);
        double elapsed = Time.timeAsDouble - crawlEpoch + crawlPhaseOffset * cycleSeconds;
        float phase = (float)(elapsed % cycleSeconds);
        if (phase >= moveSeconds)
            return IntentMovementData.None;

        // A smooth zero-to-peak-to-zero pulse leaves external knockback to the motor.
        float pulse = Mathf.Sin(Mathf.PI * phase / moveSeconds);
        float crawlScale = pulse * pulse * Mathf.Clamp01(crawlPeakSpeedScale);
        return IntentMovementData.FromDirection(direction, speedScale * crawlScale);
    }

    public void StartChase()
    {
        chaseEnabled = true;
    }

    public void StopChase()
    {
        chaseEnabled = false;
    }

    public bool IsTargetWithinDetectionRange()
    {
        if (!EnsureTarget())
            return false;

        if (!enemy.CanPerceiveTarget(enemy.Target))
            return false;

        Vector2 toTarget = enemy.Target.position - transform.position;
        return toTarget.sqrMagnitude <= detectionRange * detectionRange;
    }

    /// <summary>거리 오차를 보정하는 반경 성분과 접선 성분을 섞어 플레이어 주변을 미끄러지듯 돌게 한다.</summary>
    private Vector2 ResolveOrbitDirection(Vector2 toTarget, float distance)
    {
        if (distance <= 0.001f)
            toTarget = Vector2.right;

        Vector2 radialToPlayer = toTarget.normalized;
        Vector2 tangent = new Vector2(-radialToPlayer.y, radialToPlayer.x) * orbitSign;
        float radiusError = distance - idealOrbitRadius;
        Vector2 radialCorrection = Vector2.zero;

        if (radiusError > orbitRadiusDeadZone)
            radialCorrection = radialToPlayer * inwardPressure;
        else if (radiusError < -orbitRadiusDeadZone)
            radialCorrection = -radialToPlayer * outwardPressure;

        Vector2 result = tangent + radialCorrection;
        return result.sqrMagnitude > 0.0001f ? result.normalized : tangent;
    }

    /// <summary>타겟이 비어 있으면 낮은 빈도로 감지 범위 검색을 수행해 추적 대상을 회복한다.</summary>
    private bool EnsureTarget()
    {
        if (enemy == null)
            return false;

        if (enemy.Target != null)
            return true;

        if (Time.time < nextTargetAcquireTime)
            return false;

        nextTargetAcquireTime = Time.time + Mathf.Max(0.05f, targetAcquireInterval);
        return enemy.TryAcquireTargetInRange(detectionRange);
    }
}
