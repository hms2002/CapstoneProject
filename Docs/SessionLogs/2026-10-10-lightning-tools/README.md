# 번개창 공통 도구 조합 검증

[승인 범위](../../ActiveTasks/lightning-spear-tool-composition.md)에 따라 번개창의 세 위치 계획을 [공통 도구](../../StructureMemory/PlacementTools.md)로 구성했다. 콘텐츠 연결은 [번개창 구조 지도](../../StructureMemory/LightningSpearPlacement.md)에 있다.

## 구현과 재사용 경계

- Gameplay assembly의 `Features/Common/Placement`에 MapQuery2D, PhysicsQuery2D, CandidateSources, NearestPositionSearch, PlacementSession, PlacementFlow를 추가했다. 공통 6개 source에는 Lightning/Spear/Enemy/TrainingDummy/유물/GAS 참조가 없다.
- Planner는 수치 설정 변환, 대상 포함과 live 좌표 reader 연결, 공통 Flow 조합을 담당한다. 일반 비는 bounded random fill, 표적 비는 round robin 뒤 같은 fill, 3타는 nearest-first 뒤 고정점/난수 first-success를 사용한다. 공통 Flow에 무기 모드 switch가 없다.
- MapQuery 입력의 기존 레이어/이름 convention은 연결부에 남았다. 다른 production 소비자 마이그레이션과 새로운 맵 정책은 이번에 하지 않았다.
- Actor 생성·등록·GAS·소리·E 지연·취소·disable/destroy/무기 교체 본문은 요청 값 타입 이름을 제외하면 이전과 같다. E의 첫 yield 전 결과 복사를 유지한다. 3타 연결은 값 하나를 반환한 뒤 borrowed 결과를 비워 기존 scratch 예약0을 보존한다.
- 공통 reader는 값 타입 + generic 제약으로 기존 좌표를 live로 읽으며 후보마다 boxing/actor snapshot/새 registry를 만들지 않는다. 대상 resolver와 sort comparer는 재사용한다. heap과 컬렉션 용량도 유지한다.

## 독립 비교 기준

구조 변경 전 source를 먼저 보존했다. `LightningSpearPlacementBeforeTools`와 내부 World snapshot은 공통 도구를 호출하지 않는다. 기존 Planner의 braced 7개 주요 본문과 World의 15개 본문이 값 타입 이름/줄바꿈을 제외하고 원본과 같다. Rain에 옮긴 null actor compaction은 원래 Runtime wrapper의 작업이다. 이전 Search는 Editor-only로 보존하고, wrapper를 제거한 본문이 source snapshot과 같다.

원본 Planner SHA256: `16299d7f32811b84eb0c6d63cdea25681a91fc32e7b123735f4485e4fbfb45bc`.

새 최근접 힙의 Reset/Peek/Reject/SiftDown/Compare 5개와 후보 수집 본문도 기존 알고리즘과 일치한다. 새 후보와 정책을 명시적으로 조합했으며 알고리즘을 다른 탐색으로 바꾸지 않았다.

## 실제 Unity 결과

Unity6000.4.2f1, Windows Editor PlayMode, Null graphics CPU 비교를 수행했다. 같은 프로젝트의 열린 Editor와 다른 배치가 종료한 뒤 직렬로 실행했다. 저장하지 않는 빈 씬의 fixture를 사용했다.

최종 [19개 raw report](paired/)의 결과:

- 일반/표적 비 반복496쌍, 3타240쌍, 일반/표적 비 새 managed 버퍼 구성+첫 요청312쌍, **총1,048쌍**의 위치·개수·순서·실제 방 참조·RNG·10개 작업 카운터가 일치했다.
- 열린 바닥, 벽/구멍, 중첩 바닥, 회전/비균등 Grid, 기존 창 밀집, 바닥 없음에서 비교했다. 간격0, X/Y 동률, 대상의 다수 collider 성장/중복 제거도 검사했다. 부수 first-use13쌍도 통과했다.
- 3타의 1.5 범위는 대상 조회/마지막 무작위 공급에만 사용하고, nearest 타일 후보는 player origin과 생성 반경을 유지했다. preferred 재검사, 동일 고정점, 고정점까지 포함하는 fallback 예산, 표적 비의 부족분 전체 예산을 보존했다.
- [실제 NUnit XML](tests.xml): 선택한 **17/17** 통과. 비무기 공통 도구 구성7, 기존 번개창 구조/E 취소·정리5, 기존 feedback4, 슬롯별 첫 돌진 안내1이다. Unity CLI exit0. 기존 별도 MarkOutline 색상 정확 비교 실패는 [이전 보고서](../2026-10-10-lightning-structure/README.md)에 기록돼 있으며 이번 선택 목록에 포함하지 않았다.
- 비무기 구성 검사는 좌표 List와 수치 설정으로 bounded fill/round robin/first-success를 실행한다. 무기 data/actor를 만들지 않고 계획만 생성한다. fallback 예산1/2/3의 RNG0/0/1, 간격0 반복, 예약 이후 거절/동률, 요청 재사용과 live 좌표 변경을 확인한다.

## 비용과 한계

[요약 JSON](summary.json), [반복 요청 CSV](timing.csv), [새 버퍼 CSV](fresh-buffer-timing.csv)에 전체 중앙값·p95를 보존했다. 같은 프로세스에서 AB/BA 순서를 교대하고 각 seed의9회 평균 요청 시간을 한 표본으로 썼다. 난수 초기화·진단 객체 구성·결과 복사/JSON은 시간 밖이고 요청당 맵 검색은 포함했다. fresh-buffer는 각 쌍마다 협력 객체를 새로 만든24표본이며 constructor+첫 요청을 잰다.

| 유형 | 사례별 같은 seed 시간비 중앙값 변화 |
| --- | ---: |
| 반복 일반/표적 비 | −6.5%~+6.8% |
| 반복 3타 | −4.6%~+8.9% |
| 새 버퍼 + 첫 일반/표적 요청 | −5.2%~+13.3% |

계산·물리 작업량은 같지만 모든 비용이 완전히 같지는 않다. 공통 도구·요청 설정 구성에는 상수 비용이 추가될 수 있다. 최종 새 버퍼의 가장 큰 증가 사례는 결과가 없는 no-ground random이며 이 값도 숨기지 않는다. 3타 open의 독립 중앙값은 0.218→0.265ms다. 각 경로의 독립 중앙값 비율과 같은 seed 시간비의 중앙값은 서로 다른 통계다. 첫 pass의 변형 Grid 3타 +16.1%는 최종 −4.6%로 바뀌었고 일부 p95도 흔들렸으므로 지속적인 속도 향상/꼬리 지연 동일성을 주장하지 않는다.

4096-byte canary를 관측하지 못해 allocation-byte API는 unavailable/−1다. 일반/표적 비교의 GC collection0은 무할당 증거가 아니다. 3타 별도 probe는 byte/GC 횟수를 측정하지 않았다. Actor prefab 생성·VFX·렌더링/FPS, Player/IL2CPP, 실제 byte/peak memory, 프로세스/JIT/MonoBehaviour/씬 시작 비용은 미검증이다. 게임 전체 성능 보존이나 FPS 증가로 해석하지 않는다.

## 컴파일·소유권·문서

실제 Unity compile/import를 거쳐 새 공통 source와 테스트가 생성 csproj에 포함됐다. 최종 MSBuild Editor.csproj와 ProceduralPlayModeTests.csproj 각각 exit0이다. csproj는 수동 수정하지 않았다. 기존 deprecated API 경고는 남아 있다.

보호한 번개창 data/prefab/meta와 TestScene은 시작 hash와 같다. 62개 중 GlobalUIRoot 하나는 병행 UI 작업이 수정했고 이 작업에서 변경하거나 복원하지 않았다. 새 .meta는 Unity import가 생성했다. 새 MonoBehaviour/Manager/Singleton/DDOL, 씬 저작, 직렬화 schema/설정 변경은 추가하지 않았다.

Doc Impact: SessionLog + StructureMemory + 기존 RefactorBacklog context + 공통 도구/요청 소유권 DecisionLog. ErrorLog의 새 문제 기록은 필요하지 않았다. Architecture/Contracts 승격은 여러 production 소비자 검증 뒤 검토할 후보이며 source-of-truth/Presentation HTML 변경 및 HTML stale 후보는 없다.

## 재실행

같은 프로젝트의 Unity를 닫고, 이 실행 전용 report/log 경로를 지정한다. 성공은 `LIGHTNING_STRUCTURE_VALIDATION_PASS`,19개 report의 성공 필드와 실제 종료를 함께 확인한다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\nadom\Desktop\졸업작품\CapstoneProject' -executeMethod LightningSpearStructureValidation.Run -uiBatchValidationTests -lightningCompareStructure -lightningStructureReport '<report-dir>' -logFile '<log-file>'
```

[증거 발췌](validation-evidence.txt)와 원본 결과는 이 폴더에 보존했다.
