# 번개창 생성 위치 구조 분리 검증

범위는 [ActiveTask](../../ActiveTasks/lightning-spear-placement-structure.md), 소유권 지도는 [StructureMemory](../../StructureMemory/LightningSpearPlacement.md)에 있다. 기존 최적화 알고리즘을 유지한 채 위치 계획과 맵·물리 조회를 분리했다.

## 비교 기준

- 분리 직전의 이미 최적화된 RuntimeState를 별도 Editor 전용 `LightningSpearPlacementBeforeRefactor`로 보존했다. 추출한 메서드 26개 본문이 원본과 일치한다. 허용한 차이는 요청 값 타입 이름, `UnityEngine.Object` 명시와 줄바꿈뿐이다. 현재 Planner/WorldQuery 호출은 없다.
- 원본 RuntimeState SHA256: `5912d9607a47e8ba9ba8dbb59df6cdd93b96823a8a1da2a66a566ec892d32aa5`. PlacementSearch 파일도 분리 전후 동일하다.
- 이전 전체 정렬 방식의 legacy 경로는 기존 측정기에 남겼다. **이번 구조 분리의 시간 기준은 그 legacy가 아니라 분리 직전 최적화 구현**이다.
- Unity 6000.4.2f1, Windows Editor Play Mode의 동기식 계획만 측정했다. DX11 및 `-nographics`의 Null graphics CPU 비교를 각각 보존했다. 열려 있는 Editor와 다른 배치 실행이 끝난 뒤 직렬로 실행했다. 운영 씬을 저작하지 않고 저장하지 않는 빈 씬에 일시적인 fixture를 생성·정리했다.

## 생성 결과와 작업량

빈 바닥, 벽/구멍, 중첩 바닥, 회전/비균등 축척 Grid, 기존 창 밀집, 바닥 없음의 무작위/표적 모드 12개와 간격 0 표적 모드 1개를 비교했다.

- 496개 seed 쌍에서 위치·개수·순서·실제 방 참조, 실행 후 Unity RNG 상태가 정확히 같았다. 독립 legacy 판정으로 유효성도 확인했다.
- 후보 검사·수집, 방문 셀, 전체 정렬, heap 생성, 기하 캐시 적중, 대상 overlap/대상 수, 경로/착지 물리 조회의 10개 카운터가 모두 같았다.
- 마지막 DX11/CPU 실행 각각 새 managed 버퍼 생성과 첫 요청의 312쌍도 같은 결과·RNG·작업량 검사를 통과했다. 반복 496쌍과 합쳐 실행별 측정/검증 쌍은 808개다.
- 별도 사전 워밍업 전 요청 13쌍도 같은 검사를 통과했다. 동률 거리의 X/Y 순서, 간격 0, 80개 초과 collider 결과 버퍼/대상 중복 제거 검사도 통과했다.
- 처음 별도 실행 전후의 496개 최적화 결과/작업량 비교와 첫 같은 프로세스 단일 호출 비교도 통과했다. 별도 실행 시간은 환경 편차가 커 성능 보존 근거로 사용하지 않았다.

## 시간 비교

[CPU raw report](paired/), [DX11 raw report](paired-dx11/), [요약 JSON](summary.json), [CPU 시간 CSV](timing.csv)에 같은 프로세스에서 번갈아 실행한 최적화 전후 값을 보존했다. 아래 표는 최종 Null graphics CPU 실행이다. 각 seed를 9회 동일하게 실행한 평균 계획 시간을 한 표본으로 삼았다. 난수 초기화, 진단 객체 할당, 결과 복사와 JSON 작성은 측정에서 제외했다. 매번 맵 캐시를 무효화하여 원래의 요청당 Tilemap 검색 비용은 양쪽에 포함했다. 카운터·결과는 마지막 동일 요청의 값이다.

| 사례 | 분리 전 중앙값 ms | 분리 후 중앙값 ms | 같은 seed 시간비의 중앙값 변화 |
| --- | ---: | ---: | ---: |
| 열린 바닥 무작위 | 0.165 | 0.166 | +1.4% |
| 열린 바닥 표적 | 0.659 | 0.628 | −3.8% |
| 벽/구멍 무작위 | 0.208 | 0.202 | +1.1% |
| 벽/구멍 표적 | 0.991 | 1.057 | +3.7% |
| 중첩 바닥 무작위 | 0.212 | 0.227 | −0.7% |
| 중첩 바닥 표적 | 1.287 | 1.223 | −5.8% |
| 변형 Grid 무작위 | 0.246 | 0.265 | +2.7% |
| 변형 Grid 표적 | 1.187 | 1.304 | +2.8% |
| 창 밀집 무작위 | 1.160 | 1.213 | −3.9% |
| 창 밀집 표적 | 2.228 | 2.192 | −1.6% |
| 바닥 없음 무작위 | 0.495 | 0.570 | +4.6% |
| 바닥 없음 표적 | 0.765 | 0.771 | +3.3% |
| 간격 0 표적 | 0.374 | 0.408 | +1.5% |

각 열의 독립 중앙값 비율과 같은 seed의 시간비 중앙값은 서로 다른 통계다. 이 CPU 측정의 사례별 시간비 중앙값은 −5.8%~+4.6%였다. DX11 최종 반복 요청은 −7.6%~+11.5%였고, p95와 개별 값은 더 크게 흔들렸다. 예를 들어 DX11 열린 바닥 표적의 새 버퍼 p95는 2.171→10.185ms였고 CPU 실행의 같은 값은 1.003→0.701ms였다. 이를 지속적인 속도 향상이나 꼬리 지연 동일성의 증거로 해석하지 않는다.

최종 간격 검사는 owner의 List를 읽기 전용 용도로 직접 참조하여 기존 조회 방식과 맞췄다. actor 등록/정리는 여전히 RuntimeState만 수행한다. Editor 진단 카운터도 기존처럼 필드로 접근하여 새 getter 호출이 측정에 섞이지 않게 했다. 별도 모듈 호출의 작은 상수 비용 가능성은 남지만 알고리즘·물리 작업량은 동일하다.

## 새 버퍼와 첫 요청

양쪽 코드를 미리 실행한 뒤 매 쌍에서 새로운 기존 구현 또는 새로운 WorldQuery/Planner를 만든다. managed 협력 객체 구성과 첫 Generate를 함께 재며, 각 사례 24쌍의 AB/BA 순서를 교대로 실행했다. 카운터/RNG 준비와 검증은 시간 밖이다. 이 초기 비용의 CPU 시간비 중앙값은 사례별 −15.0%~+3.6%였다. [새 버퍼 CSV](fresh-buffer-timing.csv)에 중앙값·p95를 보존했다. 큰 비용 증가를 관찰하지 않았지만 24쌍으로 시작 지연의 모든 특성을 증명하지 않는다.

이 비교에는 MonoBehaviour/AddComponent, 씬 로딩, 프로세스/JIT 시작 비용이 포함되지 않는다. fresh GC 횟수는 결과 복사·검증까지 포함한 루프 전체이며 객체 구성 단독 GC 지표가 아니다.

`firstRequest`는 한 번의 사전 워밍업 전 탐침이며 기존 Runtime 재사용, 실행 순서와 JIT 편향이 있다. 반복 가능한 cold 비용 증거로 해석하지 않는다. 이번 시간 결과는 prefab 생성·VFX·렌더링·FPS, Player/IL2CPP 성능을 포함하지 않는다.

## 할당과 수명

4096-byte 할당 canary가 관측되지 않아 byte API 결과는 **unavailable / −1**로 보존했다. 측정 중 GC collection은 모든 사례에서 0이었지만 이를 무할당으로 해석하지 않는다. 기존 List/heap 용량과 O(K·T) 저장 형태를 유지하며, WorldQuery와 lazy Planner의 객체 두 개가 owner마다 추가된다. 객체 구성의 시간은 위 첫 요청 비교에 포함했지만 실제 byte/peak memory는 별도의 정상 Profiler 검증이 남는다.

Unity 컴파일과 A/B suite 성공 증거는 [발췌 로그](validation-evidence.txt)에 있다.

## 회귀 검사와 컴파일

- 실제 Unity PlayMode: [NUnit XML](tests.xml)의 선택 11개 중 10개 통과, 1개 실패. 새 구조 검사 **5/5**는 계획 단계 미생성/actor 비소유, 독립 Planner 격리, 실패 후 바닥 복구 재시도, 실제 E 지연 중 scratch 재사용, 실제 E 취소/disable 창 정리를 확인했다.
- List/진단 접근 조정 후 최종 구조 검사 [5/5](placement-tests.xml)도 Unity CLI exit0으로 다시 통과했다.
- 기존 LightningFeedback 4개와 저장 슬롯별 첫 돌진 안내 1개도 통과했다.
- **Suggested Later:** 기존 `WeaponFeedback_MarkOutlineAndKeyFollowAvailabilityAndRebinding`의 134행 색상 정확 동등 단언은 실패했다. 실패 메시지는 예상/실제 모두 `RGBA(0.000, 0.627, 0.902, 1.000)`로 표시한다. 해당 테스트는 MarkActor를 직접 생성하며 새 Planner/WorldQuery를 호출하지 않는다. MarkActor 소스와 prefab은 이번에 바꾸지 않았고 테스트의 이 단언도 이전과 같다. 부동소수점/MaterialPropertyBlock 색상 반환의 정확한 차이와 허용 오차 검토는 이 위치 구조 분리 범위 밖에 남긴다. 이 확장 run을 전체 통과로 보고하지 않는다.
- Unity CLI는 실패 run을 exit2/TESTS_FAILED로 보고했다. 초기 licensing handshake 경고 뒤 실제 테스트는 실행되고 XML이 작성됐다.
- MSBuild `Editor.csproj`, `ProceduralPlayModeTests.csproj` 각각 exit0. Unity가 새 source를 생성 project에 포함한 뒤 실행했으며 csproj는 수동 편집하지 않았다. 관련 Gameplay와 Editor, 새 PlayMode source가 포함됐다. 기존 deprecated API 경고는 남아 있다.

## 재실행

같은 프로젝트의 Unity를 닫고 아래 명령을 PowerShell에서 실행한다. `<report-dir>`과 `<log-file>`에는 이 실행용 출력 경로를 지정한다. 완료는 `LIGHTNING_STRUCTURE_VALIDATION_PASS`와 13개 report의 성공 필드를 확인한다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Users\nadom\Desktop\졸업작품\CapstoneProject' -executeMethod LightningSpearStructureValidation.Run -uiBatchValidationTests -lightningCompareStructure -lightningStructureReport '<report-dir>' -logFile '<log-file>'
```

CPU 비교는 같은 명령에 `-nographics`를 추가한다. 해당 report의 `graphicsDevice`는 `Null`이다. 두 모드의 시간 분포를 하나의 절대 시간으로 합치지 않는다.

이름 기반 Wall 분류, 가벽 돌진 정책, 표식 registry/돌진/회수 발사 구조 변경은 이번 작업에서 진행하지 않았다. 기존 번개창 data/prefab/메타 및 TestScene은 task 시작 hash와 동일하다. 보호 목록 62개 중 GlobalUIRoot 하나는 병행 현지화 작업이 바꿨으며 이 작업이 수정하거나 복원하지 않았다.
