# 설정 번역·제목 너비 검수 — 2026-10-08

Unity 실제 Play Mode에서 Settings / Key Settings를 각각 ko/en/ja/zh-Hans/zh-Hant로 캡처했다. 10 PNG와 같은 이름 JSON, 2400×1359, 종료0. FullLocalizationValidation.ReviewSettingsScreensBatch는 기존 컨트롤러의 OpenUI/OpenFromSettings를 사용하고 검수 후 저장6개와 언어 preference를 복원한다.

영어 Settings는 한 줄을 유지하면서 상단 패널 폭이 늘어난다. 키 설정 행의 조작 이름을 덮어쓰던 Screen Mode 고정 writer를 제거했고, Reset와 입력 대기는 키 기반 번역으로 연결했다. 실제 화면에서 일본어/중국어 조작 이름과 Reset을 확인했다. 전체 행의 사용자 입력·스크롤·재할당·오류/교환 확인 분기까지 플레이한 검수는 아니다.

남은 레이아웃: ja-keybindings.png의 하단 설정 초기화 버튼은 긴 번역문이 옆 버튼에 닿는다. 상단 제목 수정과 구분한 후속 검수 대상이다. 기존 PrewarmTraceRuntime 파일 쓰기 오류도 별도 항목이다.

[변경 파일·검증 기록](../../Docs/SessionLogs/2026-10-08.md).
