# 필드 아이템 그림자 참조 복구

- 최초 작성: 2026-11-05. 작성자: 사용자가 확인한 GitHub 로그인 `CubeHood`. 모드: Implementation. 범위: 이 채팅에서 승인한 필드 유물·소모품 그림자의 누락 여부 재확인 및 재적용.
- 원인: `WorldItemShadowPresenter2D.cs`, 해당 MonoScript GUID, 프리팹의 Shadow 자식/컴포넌트/렌더러 연결, 회랑·허브의 드롭 프리팹 참조는 남아 있었다. 현재 아이콘으로 교체된 뒤 프리팹의 serialized `contacts` 목록이 갱신되지 않아 활성 정의 74개 중 37개가 목록과 일치하지 않았다. 런타임은 일치하는 접점이 없는 아이콘의 그림자를 숨긴다. Git 병합이 코드를 삭제했다는 근거는 확인하지 않았다.
- 변경: `Assets/_Project/Prefabs/Items/Pickups/PF_WorldItemPickup2D.prefab`의 `contacts`만 현재 PNG·스프라이트 rect·GUID/fileID를 기준으로 재계산했다. 현재 정의 74개/고유 아이콘 74종을 반영하고, 같은 참조라도 이미지 윤곽이 바뀐 경우에는 접점 값도 갱신했다.
- 모양: 기존 Shadow_0, 폭 75%·높이 25%·알파 34%, 32px 표시 기준 윤곽 간격 -4px을 유지했다. contacts 이전 프리팹 내용(표시 객체, collider, script/renderer 연결, sorting, sprite/color/transform 설정)은 작업 전과 동일하다. runtime/Editor 코드, 아이템 정의·효과, 씬, 기존 `.meta`는 변경하지 않았다.
- 확인: 소스·런타임 접점 조회/낙하 인터페이스·프리팹/씬 참조·diff를 검토했다. 현재 정의의 아이콘 참조 74개가 모두 새 contacts 목록과 일치하며 누락이 없다. `WorldItemShadowPresenter2D.cs`를 포함하는 `Presentation.csproj`의 MSBuild가 종료 코드 0으로 통과했다(기존 obsolete API 및 serialized-field 경고 있음). 빌드 로그: `Library/WorldItemShadowRestore20261105/msbuild.log`. 자동 테스트 및 Unity import/Play Mode/실제 게임 화면 확인은 실행하지 않았다.
- 문서: [WorldItemShadows](../StructureMemory/WorldItemShadows.md)의 현재 범위와 실패 조건을 갱신하고 ErrorLog에 아이콘 교체 후 접점 목록 미갱신 사례를 추가했다. DecisionLog는 미변경. Doc Impact Check: SessionLog + StructureMemory + ErrorLog. Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음.
