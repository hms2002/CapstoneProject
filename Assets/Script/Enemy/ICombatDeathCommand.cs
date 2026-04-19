using UnityEngine;

/// <summary>
/// 이 인터페이스의 책임:
/// 외부 시스템이 몬스터/보스 같은 전투 엔티티에게 안전한 사망 명령을 요청할 수 있는 공용 진입점을 제공한다.
/// 직접 Destroy 하지 않고 각 엔티티의 Die 경로를 타게 만들어 드롭, 연출, 정리 후처리를 보장한다.
/// </summary>
public interface ICombatDeathCommand
{
    void RequestDeath(GameObject killer = null);
}
