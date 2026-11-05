# 그림자 잔향 분신 불투명도 조정

- 작성일: 2026-11-05. 작성자 GitHub 로그인: `CubeHood` (동일 사용자 제공 ID 확인). Mode: Micro-fix.
- 사용자 요청: 분신의 기본 불투명도를 약 50%로 높인다.
- 변경: [ShadowEchoActor.cs](../../Assets/_Project/Runtime/Features/Items/Relics/ShadowEchoActor.cs)의 대기 알파 기준을 0.35에서 0.5로 변경했다. 맥동 진폭 0.06을 유지하여 44~56%로 표시한다. 폭발 후 소멸 알파 기준도 0.38에서 0.5로 맞췄으며 소멸 시간은 기존 약 0.175초다. 범위 원/입자/색상/피해/음향과 직렬화 필드/프리팹/에셋은 변경하지 않았다.
- 검증: 해당 파일의 Gameplay.csproj 포함과 알파 계산/기존 타이밍을 소스로 확인했다. Gameplay.csproj MSBuild exit 0 (기존 경고 존재). 로그: `Library/CodexRelicAudio20261105/opacity-msbuild.log`. 자동 테스트, Unity import/Editor 컴파일, Play Mode/실제 시인성 확인은 실행하지 않았다.
- Doc Impact Check: SessionLog만 추가. StructureMemory/ErrorLog/DecisionLog 추가 변경 없음. Presentation HTML 영향 없음. 실제 게임에서 밝기 확인이 남는다.
