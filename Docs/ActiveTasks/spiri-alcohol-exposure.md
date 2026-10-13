# Spiri affection reward and alcohol exposure

Mode: Implementation. User approved the investigated scope on 2026-10-12.
Risk: High (player prefab, input interpretation, gameplay status and affinity data).
Status: complete. Unity6000.4.2f1 import/authoring succeeded; 38 unique selected
PlayMode cases passed across the final runtime and repaired visual/contact runs.
Native authored mug capture, five-locale readback and preservation audit passed.
Editor and test generated-project MSBuild passed; full manual gameplay/Player build remain
unexecuted. Evidence: [validation report](../../outputs/spiri-alcohol-exposure-2026-10-12/README.md).

## Accepted behavior

- One recipient-owned mug fills over three game seconds across valid overlapping
  alcohol puddles. Overflow activates strictly after three seconds.
- Before Spiri affection 1, overflow causes confusion: reverse ordinary walking
  and directional dash in all four directions, preserving raw input, other
  skills and forced movement. The dash extension was explicitly requested in
  the 2026-10-12 follow-up; no-input aim fallback retains its direction.
- At Spiri affection >=1, overflow grants the existing +25% movement and +0.4
  attack-speed modifiers instead of confusion. Register the level-1 reward in
  the existing NPC reward/presentation path; query current run affection.
- Leaving the last valid puddle drains the current fill to zero over 1.5 seconds.
  Reentry continues from the remaining fill. Effects refresh during contact and
  drain; confusion ends at empty, while the speed buff starts its five-second
  lifetime after empty. Reentry also refreshes an already earned residual buff.
- Charge/drain follow scaled gameplay time. Disable/destroy clears recipient
  effects and visuals. Source disable, ignition and absorption release contact.

## Scope

Allowed: alcohol/player input/status scripts; authored player mug and references;
Spiri reward and confusion GE/GameplayTag/HUD data; existing alcohol GE duration;
narrow shader/materials, five-locale strings, native authoring, focused tests,
validation artifacts and task memory. Preserve existing modifier values,
asset identities, saved data and all unrelated working-tree changes.

Forbidden: unrelated fixes, new Managers/Singletons/DDOL, save schema, existing
enum IDs/order, scenes, ProjectSettings, Input Actions, asmdefs, architecture,
contracts and Presentation HTML edits.

## Verification and completion

- Unity import/compile, targeted PlayMode tests and generated-project MSBuild.
- Check actual GE and attribute lifetimes, threshold, drain, reentry, overlap,
  airborne landing, source/recipient cleanup and walking/directional-dash confusion.
- Native authored-reference/five-locale readback and representative mug renders.
- Review narrow prefab/data diffs, existing GUIDs, scenes/settings and saved
  runtime data. Report unexecuted manual gameplay and Player build honestly.
- Record SessionLog, feature StructureMemory and any retained structural debt.

## Dash follow-up

The current user requested directional dash to reverse together with walking.
Scope is a local input/AbilityLogic_Dash2D change and focused direction tests;
no additional prefab, effect, binding, save or skill changes. A shared confusion
direction function covers cached raw intent and the direct input fallback.
The earlier38 native cases describe the original walking-only implementation;
follow-up validation is reported separately in the SessionLog.
