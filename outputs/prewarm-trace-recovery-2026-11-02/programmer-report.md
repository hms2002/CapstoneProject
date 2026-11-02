프로그래머 전달 요약

PrewarmTraceRuntime이 Assets의 JSON을 직접 덮어쓰다 Windows 1224가 발생했고, 실패 시 재시도 시간 갱신이 누락돼 Editor update마다 오류가 반복됐습니다.
Library 임시 JSON을 이용한 교체 저장, 실패 시 5초 재시도, 장애 구간당 경고 1회로 수정했습니다. 기존 JSON 형식·경로·20세션 보관과 게임 데이터는 유지했습니다.
격리 Unity 6000.4.2f1 검사 40/40 및 Editor MSBuild가 통과했고 보호 파일 42/42가 유지됐습니다. 실제 Editor의 일반 Play Mode 확인은 남아 있습니다. 잠금이 지속된 채 종료하면 최신 미저장 측정 일부는 자동 반영되지 않을 수 있습니다.
