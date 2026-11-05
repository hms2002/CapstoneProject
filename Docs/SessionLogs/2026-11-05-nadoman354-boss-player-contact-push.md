# 플레이어 걷기·대시로 인한 보스 밀림 수정

플레이어가 몸으로 부딪혔을 때 보스가 밀린다는 보고를 받았다. 플레이어의 몸체는 `Player` 레이어에 있고, 드래곤과 지상 상태의 슬라임 여왕 등은 해당 레이어의 접촉 힘을 받는 동적 Rigidbody2D를 사용했다. 수정 범위는 몸체 간 충돌이다. 무기 피격으로 발생하는 넉백은 별도 전투 효과가 처리한다.

- [Enemy.cs](../../Assets/_Project/Runtime/Features/Enemies/Common/Enemy.cs)에 보스 본체의 Rigidbody2D에 연결된 비트리거 콜라이더에서 `forceReceiveLayers`의 `Player` 비트만 제거하는 메서드를 추가했다. 접촉 이벤트와 보스의 이동 방식은 기존 설정을 사용한다.
- [BossControllerBase.cs](../../Assets/_Project/Runtime/Features/Bosses/Common/FSM/Core/BossControllerBase.cs)와 개구리 보스가 사용하는 [Boss.cs](../../Assets/_Project/Runtime/Features/Bosses/Common/FSM/Boss.cs)가 초기화 시 이 메서드를 호출한다.

플레이어 몸체 레이어, 보스 프리팹과 상속 경로, 슬라임 여왕의 착지 시 충돌 모드 전환을 소스에서 확인했다. Gameplay.csproj MSBuild가 성공했고 코드 diff의 공백 검사를 통과했다. 자동 테스트와 Unity 플레이 모드는 실행하지 않았다. 수동 확인 시 정지한 드래곤이나 착지한 슬라임 여왕을 향해 계속 걷고 한 번 대시해, 접촉 중 보스 위치가 밀리는지 확인한다.

문서 영향은 SessionLog다. ErrorLog, DecisionLog 및 Presentation HTML 갱신은 필요 없다.
