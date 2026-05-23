---
status: active
authority: current-task
category: run-special-npc
last_reviewed: 2026-05-23
---

# Current Task

## Goal

Stabilize the run-internal special NPC interaction pipeline for construction and same-scene teleport NPCs.

## Requested Work

- Keep run-special NPC dialogue separate from the normal `DialogueController` / Ink / portrait dialogue stack.
- Use `SpeechBubbleComponent` for NPC lines and the authored `RunSpecialNpcChoicePanel` under `GlobalUIRoot > DialogueCanvas` for user choices.
- During NPC lines, allow the camera to focus on the NPC/speech-bubble target.
- Fade HUD/prompt presentation out and back in like Merchant cinematic presentation, while keeping the run-special choice panel usable.
- Before user choices appear, return the camera to the player, then show the choice panel.
- Support the authored three-button choice panel while hiding unused button slots when fewer choices are available.
- In construction-pending dialogue, replace the authored `N일` text token with the remaining run completion count.
- When construction is already pending, skip the normal opening lines and immediately play the construction-pending status lines.
- When construction has not started but the player lacks enough magic stones, use a separate insufficient-funds line branch.
- Move run-special dialogue data into `RunSpecialNpcDialogueSetSO`, with feature-specific branch keys returned by the primary feature.
- Treat line breaks inside a `RunSpecialNpcLine` text field as separate speech-bubble lines at playback time, so SO authors can paste multi-line dialogue without adding array elements for every line.
- Provide an Editor migration tool that creates dialogue set assets from existing Interactor/provider-authored lines and choices without direct YAML editing.
- Keep run timer and `Time.timeScale` paused during the interaction, while speech, camera waits, letterbox, and choice input use unscaled time.
- For same-scene teleport, close the run-special dialogue presentation and restore gameplay time before executing the teleport feature.
- Keep scene/prefab authoring explicit. Source code may drive serialized references, but should not create runtime UI hierarchy.

## Scope Notes

- `ConstructionNpc` in `SlimeCorridor` is the current primary validation target.
- Construction shortcut state, tilemap block authoring, Door/Shortcut, and save integration remain part of the same RunSpecialNpc pipeline.
- Current work may update `RunSpecialNpc` scripts and project memory docs.
- Do not directly edit Unity scenes, prefabs, serialized assets, or ScriptableObject schemas unless the user explicitly asks for that authoring step.
- Existing unrelated worktree changes are not part of this task and must not be reverted.
- Unity batchmode must not run while Unity Editor processes are open.

## Done Criteria

- Opening NPC lines can play with camera focused on the NPC target.
- HUD/prompt layers fade out during the run-special presentation and fade back in during cleanup.
- User choice UI appears only after the camera has returned to the player.
- Choice buttons beyond the visible choice count are inactive, so a three-button panel can safely show one, two, or three choices.
- The SlimeCorridor pending-construction line `앞으로 N일 정도 남았어.` displays the current remaining run completion count in place of `N`.
- Pending construction re-interaction goes directly to the construction status line set instead of replaying first-time opening lines.
- Construction insufficient-funds re-interaction goes to the construction insufficient-funds line set instead of showing the payment choice.
- A single SO line text containing multiple newline-separated sentences plays as multiple speech-bubble lines, including choice response lines.
- `RunSpecialNpcInteractor` reads `RunSpecialNpcDialogueSetSO` and executes the branch selected by `primaryFeature.GetDialogueBranchKey(...)`.
- Same-scene teleport choice closes speech/letterbox/HUD presentation, restores `Time.timeScale`, then runs fade out -> warp -> fade in without hanging behind `FixedUpdate`.
- Existing Interactor/provider line and choice data can be migrated through `Tools/RunSpecialNpc/Create Dialogue Set Asset From Selected Interactors`.
- Choice input still supports mouse click and number keys for active choices only.
- Cleanup hides choices, clears choice follow target, restores camera state, releases input/timer/time-scale locks, and restores player state.
- Static analysis runs after C# changes when the generated project file includes the touched scripts.

## Verification Plan

- Run `git diff --check` for changed source/docs.
- Run a trailing-whitespace scan for touched files.
- Confirm changed C# files are present in generated `.csproj` files.
- Run MSBuild analyzers for `Assembly-CSharp.csproj` when the changed files are included.
- Check `Get-Process -Name Unity -ErrorAction SilentlyContinue`; do not run Unity batchmode if the Editor is open.

## Remaining Risks

- Manual Unity play validation is required for SlimeCorridor camera framing, choice-panel positioning, and button layout.
- Existing scene instances should be reviewed in Inspector after script import because `RunSpecialNpcInteractor` has recently gained camera focus serialized fields.
- Existing scene/prefab line and choice data still needs Unity Editor migration through `Tools/RunSpecialNpc/Create Dialogue Set Asset From Selected Interactors`; legacy interactor fields and provider components should remain only until that migration is inspected and saved.
- Camera feel depends on the current `PlayerCam` Cinemachine damping and may need scene-side tuning.
