# 언어 비동기 준비 후 적용 — 2026-10-10

Unity 실제 Play Mode의 Settings/Key Settings×5언어=10 PNG/JSON. ReviewLanguageCommitBatch는 초기 저장 언어 적용 완료 후 en→Hans→ja 연속 요청, 이후 ko/en/Hans/Hant 전환을 검수한다. Canvas 렌더 직전 활성 GameText 고정 문구/GameFonts 폰트 일치 검사2,971회 통과, batch exit0. 콜백 수는 고유 프레임 수나 프로파일링 결과가 아니다.

준비 중 기존 화면 유지, 자료 준비 후 locale 이벤트에서 문구/폰트/레이아웃 적용. glyph prewarming은 추가하지 않았다. 최종10파일은 초기 startup mismatch 실패를 수정한 재검수 결과다. 저장6개 byte parity와 Editor language preference 복원 확인.

남은 검수: 모든 화면의 전환, Player build, 실패 주입, CPU/글리프 비용 및 메모리 profiling. 이전에 발견한 하단 일본어 Reset 버튼 레이아웃은 이번 범위 밖이다.

[변경 파일·검증 기록](../../Docs/SessionLogs/2026-10-10.md).
