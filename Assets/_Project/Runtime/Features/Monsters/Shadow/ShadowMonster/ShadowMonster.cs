using UnityEngine;

public class ShadowMonster : Mob
{
    // 이 클래스의 책임:
    // ShadowMonster 프리팹의 몬스터 본체 역할을 담당한다.
    // 실제 공격 판단/문맥 생성은 같은 오브젝트의 TackleAttack helper가 맡고,
    // 본 클래스는 공통 Mob FSM과 몬스터 기본 속성 문맥을 제공하는 셸로 남는다.
}
