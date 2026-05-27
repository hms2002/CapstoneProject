---
status: active
authority: current-task
category: shortcut-cinematic
last_reviewed: 2026-05-26
---

# Current Task

## Goal

Add a lever-triggered door reveal cinematic so the player can see which door opened.

## Requested Work

- When a lever is activated, apply the lever activated visual and success presentation immediately.
- Delay the linked door opening until the camera has moved to the door.
- Reuse the existing cinematic presentation pattern: letterbox, HUD fade, player control lock, external UI input block, gameplay camera focus, and camera restore.
- Keep the scope limited to `LeverShortcut`; statue and temporary shortcut behavior should remain unchanged.

## Scope Notes

- User current instruction supersedes the older Flowering weapon polish task.
- Do not pause `Time.timeScale`; the door opening animation/DOTween path should continue using the existing scaled-time behavior.
- Do not edit scene YAML or prefab YAML directly. Optional door focus target authoring can be reviewed in Unity Inspector.
- Unity batchmode must not run while Unity Editor processes are open.

## Done Criteria

- Lever interaction no longer calls `DoorObject.ForceOpen(...)` before the camera focus beat.
- Lever interaction is blocked while the reveal cinematic is already running.
- Cinematic cleanup restores camera follow/look-at/priority, legacy camera follow state, Cinemachine brain unscaled-time setting, player protection, UI input blocking, and letterbox overlay.
- The linked door opens with permanent save enabled after the camera focus wait.
- Already-unlocked permanent shortcuts still show the activated lever visual on startup.

## Verification Plan

- Run `rg` checks for lever cinematic state, focus target, door opening, letterbox, input blocking, and player protection references.
- Confirm generated `Assembly-CSharp.csproj` includes the changed runtime script.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` because the changed script is included by the generated project file.
- Run `git diff --check` for touched tracked files.
- Run a trailing-whitespace scan for touched source/docs.
- Check for Unity Editor processes and do not run Unity batchmode if the Editor is open.

## Remaining Risks

- Unity Editor import/compile must still confirm the new `LeverShortcut` serialized fields.
- Existing lever prefab and scene instances should be reviewed in Inspector for optional `doorFocusTarget` placement and timing values.
- Manual Play Mode validation is required for camera framing, door timing, input blocking, and saved shortcut startup visual state.
