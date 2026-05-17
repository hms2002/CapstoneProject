---
status: completed
authority: current-task
category: editor-tooling
last_reviewed: 2026-05-17
---

# Current Task

## Goal

Improve polygon vertex selection and deletion while editing Level Design Editor battle-room grids.

## Requested Work

- Allow polygon room vertices to be clicked and selected during room grid edit mode.
- Visualize the selected vertex clearly in SceneView.
- Make Delete/Backspace delete the selected vertex instead of relying on last-vertex deletion.

## Scope Notes

- Keep the changes editor-only.
- Do not modify prefabs, scenes, serialized runtime schemas, or runtime managers.
- Preserve existing polygon vertex move and edge insertion behavior.
- Continue mutating the selected `PolygonCollider2D` referenced by `MonsterRoomArea2D.areaCollider`.
- Do not modify Presentation HTML.

## Done Criteria

- Clicking a polygon room vertex selects it.
- The selected vertex has a distinct SceneView visualization.
- Delete/Backspace deletes the selected vertex when the resulting polygon remains valid.
- The Rooms tab exposes selected-vertex delete feedback/action.
- Static analysis is run after code changes.

## Verification Plan

- Static/source checks for changed editor paths, helper references, and generated project membership.
- Compile with Visual Studio MSBuild only when generated project files include the changed editor source.
- Do not run Unity batchmode while Unity Editor processes are open.

## Outcome

- Added selected polygon vertex state to the editor window.
- Clicking near a polygon room vertex selects it while preserving existing drag behavior.
- Selected vertices draw a stronger SceneView ring and label so the delete target is visible.
- Delete/Backspace and the Rooms tab action now delete the selected vertex instead of the last vertex.
- The existing polygon validity checks still reject deletion results that would leave fewer than 3 points, duplicate points, or self-intersection.
- Updated session and structure memory documentation.

## Verification

- Static/source checks were run for the selected-vertex editing helpers and generated project membership.
- `git diff --check` passed for the changed editor and current task files.
- `Assembly-CSharp-Editor.csproj` includes `LevelDesignEditorWindow.cs`.
- Visual Studio MSBuild passed for `Assembly-CSharp-Editor.csproj` with existing project warnings.
- Unity batchmode was not run because Unity Editor processes were open.

## Remaining Risks

- Unity Editor SceneView click behavior will not be directly observed by Codex.
- Polygon room editing supports a single collider path only.
- Deleting a selected vertex can still be rejected if the resulting polygon would self-intersect.
