# 그림자 잔향 폭발음 / 클로에의 등불 발사음

- 작성일: 2026-11-05. 작성자 GitHub 로그인: `CubeHood` (동일 사용자가 호감도 보상 채팅에서 제공한 ID 확인).
- Mode: Micro-fix. 사용자 요청은 두 효과에 고블린 거너와 동일한 발사음을 추가하는 것이다. 기존 전투 수치/표적/판정/타이밍/아이콘과 다른 유물은 범위 밖이다.

## 변경과 이유

- [RelicLogic_ChloeLantern.cs](../../Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_ChloeLantern.cs), [RelicLogic_ShadowEcho.cs](../../Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_ShadowEcho.cs): 각 유물 로직에 비직렬화 static readonly SoundRef를 정의했다. 고블린 거너가 사용하는 `sound_goblinGunner_GunShot` 키를 그대로 사용하여 기존 카탈로그의 클립/음량/피치/공간 설정을 공유한다. 새 직렬화 필드/스키마/에셋은 없다.
- [ChloeLanternOrbitController.cs](../../Assets/_Project/Runtime/Features/Items/Relics/ChloeLanternOrbitController.cs): 실제 탄막 Launch 뒤에 소환수의 발사 위치에서 원샷을 한 번 요청한다. 표적/능력치가 없어 발사를 생략하면 호출하지 않는다. 공용 탄막 스크립트에 넣지 않아 그림자 혼불에는 전파되지 않는다.
- [ShadowEchoActor.cs](../../Assets/_Project/Runtime/Features/Items/Relics/ShadowEchoActor.cs): 지연/정상 대시 완료 확인 이후 exploded 단발 가드를 세운 즉시 잔향의 고정 위치에서 원샷을 한 번 요청한다. 적이 없는 폭발도 재생하며, 폭발 전 취소 경로는 호출하지 않는다. 소리의 일회 재생 수명은 기존 오디오 백엔드가 소유하고 새 루프/AudioSource 생성/핸들 관리는 추가하지 않았다.
- [RelicRuntimeStructure.md](../StructureMemory/ScriptSystems/RelicRuntimeStructure.md): 사운드 선택/요청 위치와 그림자 혼불에 영향이 없음을 기존 두 유물 항목에 기록했다.

## 검증과 남은 확인

- 고블린 거너의 기존 호출 키와 두 유물 키가 같고, DefaultAudioCatalog는 대소문자를 구분하지 않아 소문자 엔트리로 해석되는 것을 소스로 확인했다. 기존 엔트리는 volume 0.45, pitch 1, loop 0, cooldown 0이며 기존 MP3 GUID를 참조한다.
- 실제 발사 뒤 호출, 폭발 단발 가드/확인/취소 경로, 위치 전달 및 기존 SoundPlaybackUtility 백엔드 사용을 소스/작업 직전 스냅샷 diff로 확인했다. 정적 필드만 추가하여 직렬화 필드/참조/namespace/asmdef/Unity 생명주기 변경은 없다. 네 C# 파일 모두 기존 Gameplay.csproj에 포함된다.
- Gameplay.csproj MSBuild exit 0. 기존 obsolete/unused 경고가 있다. 기록: `Library/CodexRelicAudio20261105/msbuild.log`, 작업 직전 스냅샷 `before/`.
- 자동 테스트, Unity import/Editor 컴파일, Play Mode, 실제 청취 및 Player build는 실행하지 않았다. 게임 내 두 소리의 크기/중첩/공간감과 취소 시 무음은 수동 확인 대상이다.
- Doc Impact Check: SessionLog + 기존 StructureMemory. ErrorLog/DecisionLog 추가 변경 없음. Architecture/Contracts/Presentation HTML 미수정, 새 HTML stale candidate 없음.
