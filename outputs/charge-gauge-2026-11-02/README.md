# 차징 게이지 연결과 Aseprite 견본

## A안 적용 완료 (2026-11-02 후속 승인)

사용자가 A안을 선택해 두 무기에 적용했다. `ChargeGauge.prefab`과 `CrimsonChargePresentation.prefab`의 게이지를24×4픽셀,20PPU(월드1.2×0.2), 머리 위 offset1.4로 변경했다. 원본 Aseprite처럼1픽셀 위/아래 테두리와2픽셀 채움, 완충 시 금색 윗선·밝은 끝점을 사용한다. 불필요한 기존 장식과 그림자 띠는 비활성화했으며 모든 객체 ID·컴포넌트 참조를 보존했다. C#·차징 시간·피해·해제 조건은 이 후속 작업에서 변경하지 않았다.

- 변경 파일: 위 두 프리팹(승인된 크기·배치·색상 적용), 이 기록 및 SessionLog/DecisionLog/관련 StructureMemory(선택된 디자인을 현재 상태로 기록).
- 소스 검토: 프리팹의 사각형 배치를0/30/70/100% 상태로 대조했을 때 승인된 Aseprite A안과 픽셀 차이0. 두 무기의 배치가 같으며 로컬 참조 누락0, 홍련의 게이지 외 블록 변경0.
- [A안 적용 미리보기](charge-gauge-a-applied-preview.png)는 승인된 원본의30/70/100% 확대이며 실제 Unity 화면 캡처는 아니다. [대조 결과](apply-a-review.json), 변경 전 두 프리팹은 `apply-a-before/`에 보관했다.
- Unity import/게임 화면 검증과 자동 테스트는 실행하지 않았다. C# 변경이 없어 추가 컴파일도 실행하지 않았다. Editor에서 실제 머리 위 표시 확인이 남아 있다.
- Doc Impact: SessionLog + StructureMemory + DecisionLog. ErrorLog·Architecture/Contracts·Presentation HTML 변경 없음, Presentation HTML stale 후보 없음.

## 최초 게이지 연결 기록 (A안 승인 전)

- 견습용사의 검 우클릭은 기존 `chargeElapsed / MaxChargeSeconds` 값을 머리 위 게이지에 표시한다.
- `ChargeGaugePresentation`은 홍련과 견습용사의 검이 공유한다. 세 채움 띠는 동일한 `Image.fillAmount`를 사용한다. 50% 미만 빨강, 50% 이상 주황, 100% 금색 테두리.
- 견습 차징 연출 runtime이 authored `ChargeGauge.prefab`을 플레이어 아래에 생성하고, 정상 해제·취소·씬 전환 정리에서 숨기고 제거한다. 부모 비활성화 때 게이지 자체를 숨기고 채움을 초기화하며, 비활성 AbilitySystem도 차징 루프를 종료한다.
- 홍련의 기존 `HeadChargeGauge` 도형·객체 ID·색상·크기는 보존했다. 기존 직렬화 게이지 필드를 같은 자식의 공용 컴포넌트로 이관했다. 마법진·운석·피해·차징 시간·발사 조건은 변경하지 않았다.
- `AGENTS.md`와 `Docs/DecisionLog.md`에 “차징 스킬은 차징 게이지로 차징 정도를 표시한다”를 기록했다.

## 견본 (A안 적용, B~D 미적용)

| 견본 | 게이지 픽셀 | 특징 | 파일 |
|---|---|---|---|
| A | 24×4 | 얇은 연속 막대, 잔량을 가장 쉽게 읽을 수 있음 | [01-slim.aseprite](01-slim.aseprite) |
| B | 20×5 | 가장 짧은 캡슐, 머리 주변 점유 면적 감소 | [02-capsule.aseprite](02-capsule.aseprite) |
| C | 24×4 | 네 칸으로 나눠 대략적인 진행을 읽는 막대 | [03-segment.aseprite](03-segment.aseprite) |
| D | 26×5 | 18픽셀 채움과 작은 양끝 장식 | [04-wing.aseprite](04-wing.aseprite) |

각 파일은 64×64 캔버스, 배경·플레이어·테두리·채움의 4레이어와 14프레임(80ms/프레임)으로 구성된다. 0~100% 11단계와 완충 유지 3프레임이며 스킬 실제 충전 시간은 변경하지 않는다. 플레이어는 원본 Idle 스프라이트의 15×22픽셀을 사용했다. 비교용 배경과 플레이어는 레이어를 숨겨 분리할 수 있다.

[전체 비교 Aseprite](charge-gauge-four-options.aseprite)는 위에서부터 30%, 70%, 100%이고, 왼쪽부터 A~D다. [4배 확대 PNG](charge-gauge-four-options-4x.png)는 nearest-neighbor로 확대했다. 견본은 캐릭터와 동일한 20PPU를 기준으로 게이지 중심을 발 기준 약1.4단위에 둔다. 종전 게임 게이지32×10/16PPU(2×0.625단위)에서 선택된 A안은24×4/20PPU(1.2×0.2단위)로 적용했다.

제작은 설치된 Aseprite1.3.18.6의 [공식 Lua API](https://www.aseprite.org/api/sprite)로 픽셀과 레이어를 직접 작성했다. `author-gauge-samples.lua`는 편집 가능한 원본 제작 스크립트다. AI 생성 이미지를 Aseprite 확장자로 바꾼 파일이 아니다.

## 검증과 한계

- Gameplay / Presentation 전체 소스는 Unity6000.4.2f1 내장 Roslyn과 현재 Bee Editor 응답 파일을 사용해 별도 출력 폴더에서 컴파일했다. 새 파일을 응답 파일 사본에 포함했으며 원본 `.csproj`, Bee 응답 파일, Unity DLL은 수정하지 않았다. 컴파일 로그는 `compile/`에 있다.
- 기존 생성 `.csproj`에는 새 파일이 아직 없어 MSBuild 포함 검증은 수행하지 않았다.
- 프리팹 로컬 참조 누락 없음. 홍련의 기존 객체 ID와 게이지 도형/이미지/캔버스 블록 보존을 원본과 비교했다. Aseprite4종을 다시 열어 레이어·프레임 메타데이터와 PNG 시트로 내보냈다.
- 자동 테스트·PlayMode 진입·게임 화면 검증은 실행하지 않았다. 사용자의 진행 중인 Unity 플레이를 중단하거나 새로고침하지 않았다. 플레이 종료 후 Editor의 스크립트/프리팹 import 및 실제 우클릭·취소·무기교체·비활성화 확인이 남아 있다.
- 기존 Editor 로그에서 `PrewarmTraceRuntime.Flush`의 IO1224 오류가 보였다. 이번 변경과 별개이며 수정하지 않았다.

## 변경 파일

- `Assets/_Project/Runtime/Features/Items/Weapons/Abilities/IChargeGaugePresentation.cs` (+meta): Gameplay→Presentation 인터페이스.
- `Assets/_Project/Runtime/Presentation/VFX/Presentation/ChargeGaugePresentation.cs` (+meta): 공용 게이지 표시와 비활성화 초기화.
- `Assets/_Project/Runtime/Presentation/VFX/Presentation/CrimsonBoundaryChargePresentation.cs`: 공용 표시기로 위임.
- `Assets/_Project/Runtime/Features/Items/Weapons/ApprenticeHeroSword/AbilityLogic_ApprenticeHeroSwordChargeSpin.cs`: 차징 비율과 게이지 수명 연결.
- `Assets/_Project/Runtime/Features/Items/Weapons/ApprenticeHeroSword/ApprenticeHeroSwordChargeSpinData.cs`: authored 게이지 참조.
- `Assets/_Project/Prefabs/VFX/ChargeGauge.prefab` (+meta): 기존 홍련 게이지 도형 복사본.
- `Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab`: 기존 게이지 참조의 공용 컴포넌트 이관.
- `Assets/_Project/Data/Items/Weapons/LogicData/ALData_ApprenticeHeroSwordChargeSpin.asset`: 새 게이지 프리팹 연결.
- `AGENTS.md`, `Docs/DecisionLog.md`, `Docs/SessionLogs/2026-11-02.md`, `Docs/StructureMemory/ScriptSystems/WeaponAndGASStructure.md`, `Docs/StructureMemory/ScriptSystems/BurnStatusAndCrimsonBoundaryWeapon.md`: 규칙·구조·작업 기록.

Doc Impact: SessionLog + StructureMemory + DecisionLog. ErrorLog 미변경. Architecture/Contracts 및 Presentation HTML 변경·stale 후보 없음.
