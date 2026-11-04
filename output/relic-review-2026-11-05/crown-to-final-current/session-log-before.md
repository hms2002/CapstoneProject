# Session Log — 2026-11-05

## 리자드 전사 광폭 추격 / 리자드 마법사 이동 단발 추적탄

- Mode: Implementation. Scope: user's approved frenzy pursuit and moving single homing shot, including existing monster prefab/AL schema/animation changes. Initial balance is provisional; no automated tests were authorized.
- Warrior: trigger range 2.2; red preparation 0.8s with 0.75x player-base-speed approach; frenzy 2.2s at 1.3x player base speed; steering cap 270 degrees/s. Any valid target contact consumes one common-damage attempt even if dash invulnerability prevents damage. Timeout/contact restores original tint, stops owned movement, then stationary recovery >=1.1s and another 1s before rearm. Warning minimum is 0.6s after attack-speed scaling. The preparation approach prevents a stationary long warning from making backward walking sufficient at the default starting distance.
- Mage: moving preparation 1.2s (minimum 0.8s), final aim-direction lock 0.2s; exactly one projectile emitted from the current aim origin; post-shot moving rest 1.5s independent of attack speed. Existing recovery/think timing makes the baseline shot interval roughly 3.2s. Retreat starts below distance 3 and ends at 4; approach above 5; otherwise strafe. Approach/retreat speed 0.5x and strafe speed 0.25x player base speed.
- Projectile: speed 1.3x player base speed; homing <=0.8s at <=75 degrees/s within +/-60 degrees of current heading; leaving the cone or losing the target permanently releases tracking; total life 2.6s. Common projectiles remain straight unless explicitly configured by a mage launch.
- Movement remains owned by existing actor/runner/ExternalMovementController2D. The existing body sweep is reused for walls/holes; mage tries safe tangents. No new MonoBehaviour, manager, singleton, runtime UI hierarchy, scene, GUID or asmdef was introduced.

### Changed files and purpose

| File | Purpose |
| --- | --- |
| [LizardWarrior.cs](../../Assets/_Project/Runtime/Features/Monsters/Common/CommonCorridor/LizardWarrior.cs) | Replace two fixed dashes with finite steering-limited frenzy; contact consumption, stationary recovery/rearm, captured tint and movement cleanup. |
| [AbilityLogic_LizardWarriorCharge.cs](../../Assets/_Project/Runtime/Features/Monsters/Common/Abilities/AbilityLogic_LizardWarriorCharge.cs) | Replace retired two-step schema with frenzy timing, speed, steering, tint and contact settings. |
| [AL_LizardWarriorCharge.asset](../../Assets/_Project/Data/Abilities/Strategies/Monsters/CommonCorridor/AL_LizardWarriorCharge.asset) | Apply the initial warrior balance to the existing strategy asset. |
| [AD_LizardWarriorCharge.asset](../../Assets/_Project/Data/Abilities/Definitions/Monsters/CommonCorridor/AD_LizardWarriorCharge.asset) | Describe the new frenzy behavior using the existing ability identity. |
| [LizardWarrior.prefab](../../Assets/_Project/Prefabs/Monsters/CommonCorridor/LizardWarrior.prefab) | Disable shared recovery retreat and remove retired runner telegraph settings. |
| [AC_LizardWarrior.controller](../../Assets/_Project/Art/Sprites/Monsters/CommonMonster/AC_LizardWarrior.controller) | Hold preparation; hold an accelerated looping Walk clip as the frenzy Attack state; retain death priority. |
| [LizardMage.cs](../../Assets/_Project/Runtime/Features/Monsters/Common/CommonCorridor/LizardMage.cs) | Single-shot deadline, moving preparation/repositioning, distance hysteresis, fresh launch origin and cancellation cleanup. |
| [AbilityLogic_LizardMageBurst.cs](../../Assets/_Project/Runtime/Features/Monsters/Common/Abilities/AbilityLogic_LizardMageBurst.cs) | Replace burst-count/interval schema with homing, deterministic shot rest and moving-cast settings. |
| [AL_LizardMageBurst.asset](../../Assets/_Project/Data/Abilities/Strategies/Monsters/CommonCorridor/AL_LizardMageBurst.asset) | Apply the initial mage movement/shot/homing balance. |
| [AD_LizardMageBurst.asset](../../Assets/_Project/Data/Abilities/Definitions/Monsters/CommonCorridor/AD_LizardMageBurst.asset) | Describe moving single homing shot using the existing ability identity. |
| [LizardMage.prefab](../../Assets/_Project/Prefabs/Monsters/CommonCorridor/LizardMage.prefab) | Disable shared recovery retreat so it cannot compete with runner-owned spacing. |
| [LightBeadProjectile2D.cs](../../Assets/_Project/Runtime/Features/Monsters/Shadow/StrangeCandlestick/LightBeadProjectile2D.cs) | Add per-launch opt-in limited homing; retain straight defaults and shared damage/lifetime/wall behavior. |
| [Mob.cs](../../Assets/_Project/Runtime/Features/Monsters/Common/Mob.cs) | Expose the existing body-sweep method to subclasses without changing the shared algorithm. |
| [CommonMonsterAuthoringGenerator.cs](../../Assets/_Project/Editor/Tools/Monsters/CommonMonsterAuthoringGenerator.cs) | Keep regenerated descriptions and recovery-retreat flags consistent with both new attacks. |
| [CommonMonsterAnimatorAuthoringGenerator.cs](../../Assets/_Project/Editor/Tools/Monsters/CommonMonsterAnimatorAuthoringGenerator.cs) | Preserve the held, accelerated warrior run state when regenerating controllers. |
| [CommonMonsterAuthoringValidator.cs](../../Assets/_Project/Editor/Tools/Monsters/CommonMonsterAuthoringValidator.cs) | Align existing Animator expectations with the held warrior Walk-based Attack state; validator was not executed. |
| [MonsterProjectileCadence.md](../StructureMemory/MonsterProjectileCadence.md) | Remove mage from the random burst budget map and identify retired regression expectations. |
| [BossAndMobEncounterStructure.md](../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#lizard-frenzy-and-moving-homing-shot-2026-11-05) | Record current ownership, flow, tuning entry points, lifecycle and pitfalls. |
| [Compile evidence](../../outputs/lizard-combat-2026-11-05/README.md) | Retain the initial balance and native/MSBuild compilation evidence. |

### Verification and remaining work

- Unity 6000.4.2f1 batch-mode compile/import completed with exit 0, without entering Play Mode or running tests. Both changed monster prefabs, strategy assets and the warrior AnimatorController imported. No C# compiler errors were reported; existing API/unused-field warnings and non-compilation license-token/shutdown network messages remain in the log.
- Editor.csproj MSBuild completed with exit 0 after Unity refreshed the generated project files. It includes all changed Gameplay/Editor source files. The initial dotnet MSBuild attempt stopped on an existing stale Core.csproj missing EncyclopediaDiscoveryProgress.cs; no generated project file was edited manually.
- Source/reference/diff inspection: all 16 code/asset metadata contents and GUIDs unchanged (line endings normalized for comparison); both prefab object-ID lists and script bindings preserved; changed local Markdown links resolve; scoped git diff --check passed.
- Automated tests, existing authoring-validator execution, manual combat Play Mode, low-frame-rate acceptance and Player build were not executed. No new tests or disposable validation tools were added.
- Suggested Later: update the old LizardMage random-budget/three-shot regression expectations when tests are authorized. Manually check dash contact consumption, corner/wall behavior, full-sprite moving-cast readability and combined warrior/mage pressure before treating the provisional balance as final.
- Doc Impact Check: SessionLog + existing StructureMemory. ErrorLog/DecisionLog unchanged; no new durable architecture policy or recurring-error entry. No Architecture/Contracts promotion needed. No Presentation HTML stale candidate identified; Presentation files unchanged.


## 최종 승인 유물 이미지 5종 적용

- 모드: Micro-fix. 첨부 순서대로 회복 펜던트·쇳덩이의 예비 탄약·뇌창의 세 번째 번개·뇌창의 추적 낙뢰·견습 용사의 연계 증표에 사용자 수정 PNG를 바이트 그대로 적용.
- 기존 포션/무기 공용 그림을 보존하고 전용 PNG/.meta 5쌍 추가, 각 유물 정의 icon만 연결. Point/32 PPU/단일 스프라이트/밉맵·압축 없음. 원본 픽셀·색·알파·여백 유지.
- 이름 변경 요청은 없어 현재 회복의 팬턴트·쇳덩이의 예비 탄창 표기도 유지. 이름·번역·효과·설명·logic·레벨·relicId·정의 GUID·저장/DB/제외 정책 보존. C#·씬·프리팹·스키마·asmdef 변경 없음.
- 확인: PNG 5/5 원본 일치, 정의 icon 외 바이트 보존, 새 GUID 5개 유일성, 보호 파일 해시 및 대상 정의 git diff --check. Unity import/컴파일/실제 화면/Play Mode와 자동화 테스트 미실행.
- [변경 파일 및 확인 기록](../../output/relic-review-2026-11-05/five-final-approved-icons/README.md).
- Doc Impact Check: SessionLog. ErrorLog/DecisionLog/StructureMemory/Presentation HTML 변경 및 새 HTML stale candidate 없음.
