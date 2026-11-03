# 2026-11-02 작업 기록

## 서기관 카메라 표식 노출 및 blocking 대사 보존

- 범위: 서기관 카메라 연출과 문 개방 조건은 유지하면서 내부 `[camera cue]`가 표시되지 않도록 수정한다. 호감도/실패 연출 뒤 실제 대사가 누락되지 않는 기존 동작은 보존한다. 씬·프리팹·NPC 데이터·저장 스키마·카메라 소유권은 변경하지 않는다.
- `DialogueController.cs`: 현재 Ink 줄의 `presentation_only` 태그를 콜백에 전달한다. 표시 전용 효과만 있는 줄은 효과 종료 후 다음으로 진행하고, 태그가 없는 실제 대사는 기존처럼 같은 줄을 표시한다. blocking feature가 없는 표시 전용 줄도 내부 글자를 노출하지 않는다. 종료된 세션의 늦은 콜백 무시 조건은 보존했다.
- `GrandHallScribe.ink` 및 컴파일 JSON: 기존 `feature: scribe_cue` 6개 줄에만 `presentation_only`를 추가했다. 실제 대사·카메라 큐 순서·진행 조건은 그대로다. 서기관 카메라/입장 허용 소유자인 `GrandHallScribeSequence.cs`와 `Grand Hall.unity`의 SHA-256은 작업 전후 동일하다.
- 기존 `SceneSmokePlayModeTests.cs`에 별도 `DialogueBlockingPresentationPlayModeTests` fixture를 추가했다. 실제 Ink/TagHandler/DialogueController 경로에서 카메라 콜백 대기와 표식 미표시, 언어 변경, 실제 대사를 동반한 feature/호감도/실패 콜백, feature 없는 표시 전용 줄, 세션 종료 후 콜백, 서기관 intro/audience/repeat 큐 수를 검증하는 9개 사례다. 테스트용 inactive 객체를 사용하고 사용자 씬/저장은 로드하거나 저장하지 않는다.
- 실행한 검증: Ink 컴파일과 소스/JSON 일치. intro 대사 9/큐 4, audience 대사 2/큐 2, repeat 대사 1/큐 0. 모든 카메라 큐에 표시 전용 태그가 있고 실제 대사에는 없음을 확인했다. UI.csproj 및 PlayModeTests.csproj MSBuild 모두 종료 코드 0. 테스트 fixture의 컴파일 성공은 PlayMode 테스트 실행 성공과 구분한다.
- Unity PlayMode 테스트는 실행 완료하지 못했다. 사용자의 Editor가 이미 열린 상태라 별도 배치는 즉시 종료됐고, 열린 Editor는 JSON import 중 Win32 1224 파일 잠금 오류와 Importing assets 대기 창을 표시했다. Editor를 종료/재시작하거나 씬을 저장하지 않았다. 컴파일 결과와 실제 PlayMode 결과를 구분하며, 수동 서기관 연출/문 개방 및 기존 보스 호감도 연출 재확인은 남아 있다. 검증 로그: `Library/CodexScribeCue20261102/`.
- Doc Impact Check: SessionLog + ErrorLog + StructureMemory(GrandHallScribeSequence). DecisionLog/Architecture/Contracts/Guides/Presentation HTML 변경 및 HTML stale 후보 없음.

### 가져오기 대기 복구 확인

- 사용자가 약 10분 뒤 Importing assets 창이 해제됐다고 확인했다. 이어 열린 Unity의 정상 에디터 화면과 UI/PlayModeTests 컴파일 산출물 생성을 확인했다. 강제 종료나 재시작은 하지 않았다.
- Win32 1224 예외와 Ink 플러그인의 예외 시 StopAssetEditing 미실행 가능성은 확인했지만, 이번 대기가 영구 교착이거나 재시작이 필수라고 단정할 근거는 없었다. 앞선 사용자 안내의 재시작 필요 단정을 정정했다.
- 열린 에디터의 테스트 러너 접근을 시도했으나 사용자 입력이 감지돼 추가 UI 조작을 중단했다. PlayMode 회귀 테스트 실행 및 실제 서기관/보스 대화 검증은 여전히 미완료다.
