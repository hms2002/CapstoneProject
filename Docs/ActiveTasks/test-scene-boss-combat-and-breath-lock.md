---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-07
---

# TestScene Boss Combat and Breath Lock

- Mode: Implementation, explicitly requested by the user.
- Goal: TestScene boss generation always creates one boss, bypasses dialogue/cinematics and permits immediate combat. Dragon/Spiri's tracking breath warning commits its aim when blinking begins.
- Scope authority: Current user instruction expands the previous TestScene task to the production Dragon breath logic.
- Allowed: Test-only control/grid source, existing Editor validation, Dragon breath AbilityLogic source, the confirmed blank-list CombatHurtbox2D startup defect and focused Play Mode regressions; focused project memory.
- Preserve: Production encounter dialogue/death flow, boss prefab/SO authoring, serialized field names, timings/damage/repetition, bootstrap/save formats and unrelated changes. No Architecture/Contracts/Presentation HTML edits.
- Boss policy: Ignore monster quantity for boss generation; replace existing test bosses and their detached companions when generating another. Natural gameplay phase splitting remains owned by the original boss logic.
- Verified causes: Prefab BossEncounterDirector.OnEnable disables combat and acquires player protection/input/state locks; existing test spawn did not disable it. The open catalog also held attack input. User Editor logs show Dragon entering intro and failing to find a DialogueController in TestScene.
- Real damage verification additionally confirmed DragonBoss.prefab owns a CombatHurtbox2D list containing only a null reference. Awake treated the nonempty list as configured and skipped own-collider fallback, so CombatTargetResolver2D rejected every contact. The narrow Core fix treats entirely blank lists as empty while preserving lists containing an actual collider; no prefab edit is required.
- Implementation: Same-frame disable of test instance intro/death controllers, FinishEncounterIntro before Start/FSM, BeginCombatEncounter with current player, close catalog/release test input after successful actor spawn. Monster quantity stays available only for monsters.
- Breath boundary: Default assigned style begins blinking at 80% of 1.8-second preparation (1.44 seconds), earlier than previous aim commit. Match the style's Time.time progress; keep the last warning/fire snapshot through blinking and the original 0.4-second delay, with balanced facing-lock cleanup.
- Success criteria: Six selected boss entries start direct combat, real physical hurtbox resolution/GAS damage reduces HP, no dialogue/intro lock remains; repeated generation with quantity 99 still yields one active test boss; warning tracks before blink and stays fixed afterward/fire, including cancellation cleanup.
- Verification: Unity 6000.4.2f1 compilation/import and TestScene Play Mode passed 149 checks with zero collected errors, including actual HP reduction for all six boss entries. Six focused Unity Play Mode regressions passed (two breath cases, four hurtbox cases). Editor.csproj and ProceduralPlayModeTests.csproj MSBuild passed; generated projects were not manually edited. Reports/capture are retained in `Docs/SessionLogs/2026-10-07-boss-combat-and-breath/`.
- Documentation impact: SessionLog, existing TestScene and BossAndMobEncounterStructure memory; narrow ErrorLog entry for the confirmed test-prefab startup trap. No new durable contract is needed.
