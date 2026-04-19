using UnityEngine;

/// <summary>
/// 이 클래스의 책임:
/// 패턴 정리나 강제 제거 시 대상이 전투 엔티티면 Die 경로를, 아니면 일반 Destroy 경로를 사용하도록 공통 제거 규칙을 제공한다.
/// 제거 정책을 한 곳으로 모아 패턴 코드가 몬스터/투사체 구분 세부를 직접 알지 않게 만든다.
/// </summary>
public static class CombatEntityCleanupUtil
{
    public static void Cleanup(GameObject target, GameObject killer = null)
    {
        if (target == null)
            return;

        if (target.TryGetComponent(out ICombatDeathCommand deathCommand))
        {
            deathCommand.RequestDeath(killer);
            return;
        }

        Object.Destroy(target);
    }
}
