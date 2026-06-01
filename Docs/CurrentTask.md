---
status: active
authority: current-task
category: pre-demo-polish
last_reviewed: 2026-06-01
---

# Current Task

## Goal

Apply the pre-demo polish fixes requested for cursor visibility, SFX overlap, DemonKing Debris High fade behavior, and upgrade metadata review.

## Requested Work

- Re-check and harden the custom cursor after resolution or fullscreen-mode changes.
- Suppress short one-shot SFX overlap only when the same sound key is replayed from the same source object.
- Keep the DemonKing `PF_ExplosionDebrisBounce_HighArc` debris visual-only, but make fragments fade out instead of reading as larger fading particles.
- Produce an upgrade node metadata audit table for missing or placeholder names, descriptions, icons, and effects.

## Scope Notes

- Do not directly edit Unity scene YAML.
- Do not add new managers, singletons, or `DontDestroyOnLoad` objects.
- Do not change asmdefs or serialized ScriptableObject schemas unless an implementation blocker is found.
- Unity Editor is open, so do not run Unity batchmode.
- `Assets/Resources/DemonKing/Vfx/PF_ExplosionDebrisBounce_HighArc.prefab` remains the authored runtime Debris High prefab; do not create a second runtime copy.
- Upgrade asset data corrections are out of scope for this task; report the audit findings for manual review.

## Done Criteria

- Cursor service detects display-state changes and reapplies cursor state without losing the visible software cursor clamp.
- Same-source one-shot duplicate suppression is scoped so different objects can still play the same key.
- Debris High fragments fade by alpha after their final bounce instead of being replaced by visually oversized final contact puffs.
- Upgrade metadata audit findings are reported.
- Static checks and touched-file diff checks are run.

## Verification Plan

- Run `rg` checks for cursor display-state tracking, scoped same-source SFX suppression, debris fade state, and upgrade audit fields.
- Confirm touched C# files are included in `Assembly-CSharp.csproj`.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` when the project file includes touched scripts.
- Do not run Unity batchmode while Unity Editor processes are open.
- Manual Play Mode still needs to verify resolution/fullscreen cursor visibility, same-source SFX suppression without muting separate sources, DemonKing Debris High fade behavior, and upgrade metadata corrections.

## Remaining Risks

- Cursor behavior after display changes is platform/window-mode sensitive and needs manual Game view or build review.
- Same-source SFX suppression may need per-key tuning if any intentional rapid same-source sound should stack.
- Debris High visual timing remains subjective and should be reviewed in the DemonKing explosion context.
- Upgrade audit reports data issues only; asset corrections require manual icon/text/effect decisions.
