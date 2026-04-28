using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임:
/// 취룡 복도용 촛대 몬스터의 공통 Mob FSM 본체 역할을 담당하고,
/// 사망 시 주변 술 장판을 불 장판으로 전환시키는 고유 기믹을 제공한다.
/// 추적/공격 판단은 같은 오브젝트의 helper/AD 구성에 위임한다.
/// </summary>
public class CorridorCandlestickMonster : Mob
{
    [Header("Ignition")]
    [Tooltip("사망 지점 기준으로 술 장판을 점화할 반지름입니다.")]
    [SerializeField, Min(0f)] private float deathIgnitionRadius = 1.5f;

    [Tooltip("켜두면 사망 시 주변 술 장판을 점화합니다.")]
    [SerializeField] private bool igniteAlcoholPuddlesOnDeath = true;

    [Tooltip("동시 사망 시 다른 몬스터가 장판을 생성할 시간을 주기 위한 첫 점화 검색 지연입니다.")]
    [SerializeField, Min(0f)] private float deathIgnitionStartDelay = 0.02f;

    [Tooltip("사망 후 뒤늦게 생성/등록되는 술 장판을 잡기 위해 반복 점화 검색을 유지할 시간입니다.")]
    [SerializeField, Min(0f)] private float deathIgnitionScanDuration = 0.2f;

    [Tooltip("사망 후 점화 검색 반복 간격입니다.")]
    [SerializeField, Min(0.01f)] private float deathIgnitionScanInterval = 0.03f;

    private Coroutine deathIgnitionRoutine;

    protected override void OnDeathStarted()
    {
        StartDeathIgnitionScan();
        base.OnDeathStarted();
    }

    protected override void DestroyAfterDelay()
    {
        if (!ShouldDelayDestroyForIgnition())
        {
            base.DestroyAfterDelay();
            return;
        }

        StartCoroutine(DestroyAfterIgnitionWindow());
    }

    /// <summary>
    /// 책임:
    /// 동시 사망으로 술 장판 생성 순서가 뒤로 밀려도 촛대가 일정 시간 주변 점화를 재시도하게 한다.
    /// </summary>
    private void StartDeathIgnitionScan()
    {
        if (!igniteAlcoholPuddlesOnDeath || deathIgnitionRoutine != null)
            return;

        deathIgnitionRoutine = StartCoroutine(DeathIgnitionScanRoutine());
    }

    /// <summary>
    /// 책임:
    /// 사망 후 짧은 시간 동안 주변 술 장판을 반복 검색해 늦게 등록된 장판도 점화한다.
    /// </summary>
    private IEnumerator DeathIgnitionScanRoutine()
    {
        if (deathIgnitionStartDelay > 0f)
            yield return new WaitForSeconds(deathIgnitionStartDelay);

        float elapsed = 0f;
        while (elapsed <= deathIgnitionScanDuration)
        {
            IgniteNearbyAlcoholPuddles();

            float interval = Mathf.Max(0.01f, deathIgnitionScanInterval);
            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }

        deathIgnitionRoutine = null;
    }

    /// <summary>
    /// 책임:
    /// 점화 감시 창이 필요한 경우 촛대 오브젝트 파괴를 잠깐 늦춰 coroutine이 끊기지 않게 한다.
    /// </summary>
    private bool ShouldDelayDestroyForIgnition()
    {
        return igniteAlcoholPuddlesOnDeath &&
               deathIgnitionScanDuration > 0f &&
               gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 책임:
    /// 점화 감시 창이 끝난 뒤 Enemy의 공통 사망 제거 흐름으로 복귀한다.
    /// </summary>
    private IEnumerator DestroyAfterIgnitionWindow()
    {
        float waitSeconds = Mathf.Max(0f, deathIgnitionStartDelay) +
                            Mathf.Max(0f, deathIgnitionScanDuration) +
                            Mathf.Max(0.01f, deathIgnitionScanInterval);

        yield return new WaitForSeconds(waitSeconds);
        base.DestroyAfterDelay();
    }

    /// <summary>
    /// 책임:
    /// 촛대 몬스터 사망 지점 주변에 있는 지면 상태의 술 장판을 찾아 점화를 요청한다.
    /// </summary>
    private void IgniteNearbyAlcoholPuddles()
    {
        if (!igniteAlcoholPuddlesOnDeath)
            return;

        PuddleManager manager = PuddleManager.ResolveForScene();
        if (manager == null)
            return;

        for (int i = 0; i < manager.Puddles.Count; i++)
        {
            if (manager.Puddles[i] is not AlcoholPuddleArea alcohol)
                continue;

            if (alcohol == null || alcohol.Mode != PuddleAreaMode.Ground)
                continue;

            float igniteDistance = deathIgnitionRadius + alcohol.GroundRadius;
            float sqrDistance = ((Vector2)(alcohol.transform.position - transform.position)).sqrMagnitude;
            if (sqrDistance > igniteDistance * igniteDistance)
                continue;

            alcohol.RequestIgnite();
        }
    }

    protected override void DrawAttackGizmos()
    {
        base.DrawAttackGizmos();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, deathIgnitionRadius);
    }
}
