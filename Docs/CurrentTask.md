---
status: active
authority: current-task
category: runtime-gameplay
last_reviewed: 2026-05-19
---

# Current Task

## Goal

Close three runtime gameplay gaps: BossEncounter ESC input blocking, scene-consistent first chest reveal presentation, and closed-door monster perception.

## Requested Work

- Treat the ESC issue as a boss encounter presentation ownership problem, not a `DialogueService` or `UIManager` special case.
- Have `BossEncounterDirector` own a `GameFlowInputBlocker` from sequence start through boss combat handoff.
- Apply the same blocker ownership to legacy `BossTalkManager`.
- Align chest reveal timing, shake, and layout scalar values to `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab`.
- Update only the relevant UI root variants and scene chest reveal overrides.
- Prevent enemies from perceiving the player through closed `DoorObject` line of sight.
- Apply the same closed-door perception rule to target acquisition, chase intent, common attack continuation, and Dead's Skeleton self-destruct detection/cancel paths.

## Scope Notes

- Do not change `DialogueService` ESC blocking.
- Do not add BossEncounter-specific branches to `UIManager.HandleEscapeInput()`.
- Do not add asmdefs, managers, singletons, serialized fields, or prefab-facing schema.
- Do not touch chest hierarchy, references, serialized field names, or `TreasureChest` world-open settings.
- Door perception blocking assumes closed `DoorObject` colliders are present on the enemy-target linecast path.
- Unity batchmode must not run while Unity Editor processes are open.

## Done Criteria

- Boss intro camera focus, boss dialogue, and return-to-player presentation block ESC until combat handoff.
- Encounter input blocker releases on normal completion, setup error, `OnDisable`, and coroutine stop paths.
- First chest open reveal scalar values are consistent across the relevant UI root prefabs and scene overrides.
- Monsters fail target acquisition/chase/attack/self-destruct checks when a closed door is between enemy and player, and can perceive again after the door is open.
- Static analysis runs after code changes.

## Verification Plan

- Run `git diff --check` for changed source, prefab, scene, and docs.
- Search for BossEncounter blocker acquire/release paths and closed-door perception helper call sites.
- Confirm changed C# files are present in the generated `.csproj`.
- Run MSBuild if the generated project includes the changed C# files.
- Do not run Unity batchmode while Unity Editor processes are open.

## Outcome

- `BossEncounterDirector` now acquires a private `GameFlowInputBlocker` at sequence start and releases it on normal combat handoff, missing-reference exits, and `OnDisable`.
- Legacy `BossTalkManager` now uses the same encounter blocker ownership pattern.
- `DialogueService` and `UIManager.HandleEscapeInput()` were not changed.
- `GlobalUIRoot_DialogueUpdate.prefab`, `GlobalUIRoot_Deafiso.prefab`, and `GlobalUIRoot_Water.prefab` chest reveal scalar values now match the canonical `GlobalUIRoot.prefab` values.
- `DemonkingCorridor`, `SangHyup_Hallway`, and `SpiliCorridor` chest reveal scene overrides were aligned to the same scalar values while preserving references.
- `Enemy.CanPerceiveTarget(...)` now blocks line of sight when a closed `DoorObject` collider is between enemy and target.
- Target acquisition, chase intent, shared mob attack continuation, and Dead's Skeleton self-destruct range checks now use the same closed-door perception policy.

## Verification

- `git diff --check -- <changed source/prefab/scene/docs>` passed with line-ending normalization warnings only.
- `rg` confirmed BossEncounter blocker acquire/release paths in `BossEncounterDirector` and `BossTalkManager`.
- `rg` confirmed closed-door perception helper usage in `Enemy`, `EnemyChaseIntent2D`, `MobAttackState`, and `DeadsSkeleton`.
- `rg` confirmed the three UI root prefab chest reveal scalar values match the canonical values.
- A read-only PowerShell YAML check confirmed the three scene chest reveal override scalar values match the canonical values.
- `Select-String -Path Assembly-CSharp.csproj -Pattern 'BossEncounterDirector.cs|BossTalkManager.cs|Enemy.cs|EnemyChaseIntent2D.cs|MobAttackState.cs|DeadsSkeleton.cs'` confirmed all changed C# files are included in the generated project.
- `& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' Assembly-CSharp.csproj /p:RunAnalyzers=true /v:minimal /nologo` passed with existing project warnings.
- `Get-Process -Name Unity -ErrorAction SilentlyContinue` showed Unity Editor processes are open, so Unity batchmode was not run.

## Remaining Risks

- Unity Editor import/compile and play-mode validation were not run by Codex because Unity Editor processes are open.
- BossEncounter blocker release should be play-tested through skip/error-like scene unload paths to confirm ESC never remains permanently blocked.
- Prefab and scene YAML changes should be opened in Unity for import/normalization and visual confirmation.
- Closed-door perception depends on closed `DoorObject` colliders being hit by the linecast and not authored as ignored trigger-only colliders.
- Door perception should be manually checked in scenes with one-way, animated, or composite door colliders.
