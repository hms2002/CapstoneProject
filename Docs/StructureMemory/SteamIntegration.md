# Steam integration

2026-10-03. 현재 구조 지도이며 공식 계약이 아님.

## 설치와 소유권

- 사용자 Downloads의 Steamworks.NET 2025.163.0 unitypackage에서 runtime/Plugins/라이선스와 패키지 메타데이터를 embedded UPM `Packages/com.rlabrecque.steamworks.net`으로 설치. 공급자 C#와 네이티브 라이브러리/메타는 원본 유지.
- Editor 폴더와 샘플 SteamManager는 제외. 기본 480 App ID 생성/ProjectSettings define 자동 변경과 중복 서비스 소유권을 피함. 설치 출처와 변경 경계는 패키지 `PROJECT-INTEGRATION.md`.
- `Assets/_Project/Runtime/Infrastructure/Steam/SteamPlatformService.cs`: App scope, 기존 `[RuntimeServices]` root 소유. BeforeSceneLoad 및 최초 언어 조회가 동일한 EnsureReady 경로 사용. Awake는 중복 component 제거, 기존 authored service 채택.
- UI 설정 서비스는 SDK 타입을 참조하지 않고 `TryGetGameLanguage` 결과만 사용. Infrastructure와 Editor 검증 asmdef에 SDK 참조.

## 초기화와 정리

- 서비스당 초기화 1회. 에디터에서 유효한 `steam_appid.txt`가 없으면 native Init 생략. Steam 실행된 플레이어는 Steam이 제공한 App identity 사용.
- Packsize/DllCheck 후 Init. 실패 또는 DLL 누락/형식/entry point 오류는 게임 실행을 유지. 자동 Steam 재실행이나 강제 종료를 하지 않음.
- 성공 시 Update에서 RunCallbacks. title 복귀나 run 종료에는 유지. OnApplicationQuit 후 신규 생성/조회 방지; owner OnDestroy에서 성공한 API만 Shutdown. 중복 component 파괴는 API를 종료하지 않음.
- SubsystemRegistration에서 정적 참조/종료 상태 리셋. Edit Mode 조회는 생성/초기화하지 않음.
- 초기화 실패 후 같은 실행에서 재시도하지 않음. App ID 또는 Steam 상태 변경 후 Play Mode/게임 재시작.

## 언어와 확장

- 기존 유효한 settings.language 우선. 없을 때만 Steam 게임 언어 koreana/english/japanese/schinese/tchinese를 지원 enum으로 매핑. Steam 미지원/사용 불가는 OS, 미지원 OS는 영어.
- 사용자 언어 저장과 Locale 선택은 기존 GameSettingsService 소유. [LocalizationFlow](LocalizationFlow.md).
- 로컬 개발 App ID 파일은 Git ignored. Steam 배포에 포함하지 않음. 실제 게임 App ID, 계정 사용 권한과 Steam client가 필요.
- 업적/통계/클라우드/멀티플레이는 현재 범위 밖. SDK를 추가 사용할 때 이 서비스 수명을 사용하고 별도 Init/Shutdown owner를 만들지 않음.

## 검증과 남은 확인

- `SteamIntegrationValidation.RunBatch`: 언어 우선순위/OS fallback, Edit Mode 무생성, 패키지 버전, native ABI/DLL 검사 후 기존 LocalizationValidation 호출.
- 실제 App ID/Steam 연결 성공, runtime callback/Shutdown 및 Play Mode 종료/재진입, Steam 배포 빌드는 별도 환경 검증 대상.
- 향후 App platform service 수명 계약 또는 Architecture 승격 후보. 현재 Architecture/Contracts/Presentation은 수정하지 않음.
