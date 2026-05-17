---
status: completed
authority: current-task
category: runtime-presentation
last_reviewed: 2026-05-17
---

# Current Task

## Goal

Add monster direction guidance for KillLock chests when only a small number of linked monsters remain.

## Requested Work

- Keep the existing `SceneMonsterSpawnDirector -> ChestMonsterKillLock.RegisterMonster(...)` registration flow.
- Add a safe API for copying alive monsters from `ChestMonsterKillLock`.
- Add a world-prefab based navigation view that shows one arrow per remaining monster when the remaining count is 1-4.
- Wire the representative KillLock chest prefab to the navigation view and arrow prefab.

## Scope Notes

- Do not add a new manager, singleton, or global service.
- Do not change monster spawn ownership or KillLock unlock semantics.
- Prefer prefab-authored world presentation over runtime-created UI.
- Do not modify Presentation HTML.

## Done Criteria

- KillLock chest arrows are hidden at 0 monsters, when unlocked, or when more than 4 linked monsters remain.
- KillLock chest arrows appear for 1-4 alive linked monsters.
- Each arrow is offset from the chest and rotates toward its linked monster.
- Missing arrow prefab is warned once and hides the feature instead of failing silently.
- Static analysis is run after code changes.

## Verification Plan

- Static/source checks for changed files and relevant generated project membership.
- Compile with Visual Studio MSBuild when generated project files include the relevant source files.
- If new files are not yet in generated `.csproj`, do not manually edit generated project files and report Unity import/compile as not observed.
- Do not run Unity batchmode while Unity Editor processes are open.

## Outcome

- Added `ChestMonsterKillLock.GetAliveMonstersNonAlloc(...)`.
- Added `ChestMonsterKillLockNavigationView`.
- Added `KillLockMonsterNavigationArrow.prefab` as a SpriteRenderer-only authored arrow prefab.
- Wired `KillLockTresureChest.prefab` to the navigation view and arrow prefab.
- Updated session and structure memory documentation.

## Verification

- Visual Studio MSBuild passed for generated `Assembly-CSharp.csproj` contents after the generated project refreshed away the deleted arrow helper script.
- Static searches confirmed the navigation view GUID, arrow prefab reference, and SpriteRenderer-only arrow presentation are present.
- `git diff --check` passed for tracked changed files; untracked new files were checked for trailing whitespace with `rg`.
- The new arrow prefab passed a duplicate local fileID scan.
- Unity batchmode was not run because Unity Editor processes were open.

## Remaining Risks

- Unity Editor SceneView/playmode behavior has not been directly observed for the updated arrow prefab and navigation gizmos.
- Existing scene instances not linked to the representative KillLock chest prefab may need manual authoring.
- Arrow visibility follows existing registered-root lifetime semantics, so delayed root destruction keeps the arrow visible.
