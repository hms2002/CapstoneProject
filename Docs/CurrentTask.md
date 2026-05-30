---
status: active
authority: current-task
category: explosion-debris-bounce-vfx
last_reviewed: 2026-05-30
---

# Current Task

## Goal

Create reusable top-down explosion debris bounce VFX prefabs:

explosion origin -> debris spreads in a circle -> debris uses virtual height to read as airborne -> gravity brings it back to the same 2D ground plane -> contact puffs fire on each bounce.

## Requested Work

- Add a reusable `TopDownDebrisBounceEmitter2D` runtime presentation helper.
- Add an Editor builder for three explosion debris bounce prefab variants:
  - high arc
  - diagonal scatter
  - low skitter
- Keep this as reusable prefab content only; do not wire it into existing boss, mob, weapon, or ability data in this task.
- Record the top-down contact-point decision and verification status in project memory.

## Scope Notes

- The effect is visual-only and must not apply damage, gameplay tags, hit detection, input blocking, or scene progression.
- The helper may simulate individual debris pieces, but actual use remains prefab-authored and spawned through existing presentation paths.
- Do not hand-edit prefab YAML. Prefab generation must happen through UnityEditor APIs or Unity Inspector authoring.
- Unity Editor is open, so do not run Unity batchmode.

## Done Criteria

- Runtime source exists for virtual-height debris motion and contact puffs.
- Editor menu exists at `Tools/VFX/Rebuild Explosion Debris Bounce Prefabs`.
- Builder targets:
  - `Assets/LeeJunMo/Prefab/Effect/Particle/ExplosionDebrisBounce/PF_ExplosionDebrisBounce_HighArc.prefab`
  - `Assets/LeeJunMo/Prefab/Effect/Particle/ExplosionDebrisBounce/PF_ExplosionDebrisBounce_DiagonalScatter.prefab`
  - `Assets/LeeJunMo/Prefab/Effect/Particle/ExplosionDebrisBounce/PF_ExplosionDebrisBounce_LowSkitter.prefab`
- Static checks confirm the new helper, builder, output paths, and project-file inclusion state.
- Final report states whether Unity import/compile, prefab generation, and manual preview were actually run.

## Verification Plan

- Run `rg` checks for helper/builder/menu/output paths.
- Check generated `.csproj` inclusion for new scripts before choosing MSBuild coverage.
- Run `dotnet build` only if generated project files include the new scripts.
- Run `git diff --check` for touched source/docs.
- Run trailing-whitespace checks for touched source/docs.
- Check for Unity Editor processes and do not run Unity batchmode if the Editor is open.

## Remaining Risks

- New MonoBehaviour and generated prefab assets require Unity import/compile.
- The builder may auto-create prefabs only after Unity imports the new scripts; otherwise run the menu manually.
- Manual Scene/Game preview is required to confirm the virtual-height offset reads as airborne debris rather than northward ground movement.
