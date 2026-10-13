# 설정 제목·언어 이름 수정 검수

Unity 실제 TitleScene Play Mode에서 Settings/Key Settings×5언어 10 PNG/JSON을 캡처했다. ReviewLanguageButtonBatch는 실제 SettingsPanelUI 좌우 버튼의 onClick listener를 호출한다. 번역 테이블을 테스트에서 미리 동기로 로드하지 않는다. 사용자 키보드/마우스 이벤트 자체를 자동 재현한 검수와는 구분한다.

최종 batch exit0. 적용 완료 언어에 대한 고정 문구·폰트·동적 언어 이름·네모 대체 글리프의 렌더 직전 검사2,768회 통과(고유 프레임 수는 아님). 최종 로그 caption-title-button-review-complete-2026-10-10.log에는 invalid handle/Mixed font·caption/글리프 대체 경고가 없다. 이전 실패 run의 PNG는 최종 캡처로 대체했다.

제목은 기존 y=-40을 복원하고 가로 preferred size/최소400을 유지한다. 설정 서비스는 CurrentLanguage(요청값)와 DisplayedLanguage(적용 완료값)를 구분한다. 언어 이름은 준비 중 기존 값을 유지하며 commit에서만 갱신한다. 저장6개 및 Editor 언어 설정 복원 확인.

GlobalUIRoot/TitleScene geometry API 검사는 통과했지만 ProtoTypeHub의 실제 플레이 캡처, Editor의 직접 Locale dropdown 전환, 전체 게임/Player 및 CPU profiling은 이번 run 범위 밖이다. 글리프 사전 생성은 추가하지 않았다.

[변경 파일·검증 기록](../../Docs/SessionLogs/2026-10-10.md).
