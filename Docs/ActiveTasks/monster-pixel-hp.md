---
status: completed
authority: active-task
category: implementation
last_reviewed: 2026-10-30
---

# Monster pixel HP assets

- Mode: Implementation. User approved the Aseprite six-color HP concept with `응 이렇게 해줘`.
- Goal: Use the approved frame/track/health/trail pixel artwork in the common monster HP bar, including both shared mesh rendering and overlap UI rendering.
- Allowed: New artwork/source/sheet and Unity-generated import metadata; existing HP shader/material/shared MonsterWorldHud prefab; focused HP authoring/validation/test code; task/session/feature memory records.
- Preserve: Current75/100/140 UI-unit widths,10-unit height,world scale. Health and damage-trail ownership and .3s hold/.5s interpolation; existing shared-mesh batching and overlap routing; status/groggy hierarchy, fonts, references, existing GUIDs and lifecycle.
- Forbidden: Combat/balance changes, boss-specific HP UI, other HUD art, scene edits, new managers, asmdef/settings/input/save changes. No broad rebuild of the HUD.
- Plan: Pack the four approved PNG parts into one point-filtered sheet. Author four sprites and one shared texture material. Sample that artwork in the existing instanced quad shader and assign the same sprites to existing fallback Images. Preserve the frame's end pixels while allowing the center to resize horizontally.
- Risk: Shared prefab changes can drift TMP styles; capture original serialized data and verify non-HP objects. Mesh and UI sampling/transparent order must match. One shared sheet avoids adding a different UI texture for every part; no performance gain is assumed.
- Done: Native rendering matches the approved source at its pixel scale; all three monster sizes and overlap use the same design; health/trail/status/groggy/lifecycle regressions pass; compilation/import and generated-project builds pass; changed assets remain scoped.
- Verification: Aseprite source/hash/palette checks, native prefab capture, existing MonsterHealthBarMeshPlayModeTests and MonsterWorldHudStatusTests with graphics enabled, MSBuild after Unity exits, serialized diff/link checks. Manual whole-combat/Player performance is reported separately if not executed.
- Reference: `Docs/StructureMemory/MonsterWorldHudAndStatus.md`; original six-color concept is retained with the new sprite source.

## Outcome

Unity compile/import and native source comparison passed: four states in both render paths,2560 compared pixels, maximum RGB difference0. Graphics-enabled Play Mode30/30 passed with0 skipped; Editor and ProceduralPlayModeTests MSBuild passed. Shared prefab has9 HP-only semantic component changes, no added/removed blocks, and unchanged fonts/status/groggy references. Manual whole-combat, standalone build and Player performance measurement were not executed. [Session](../SessionLogs/2026-10-30.md), [native comparison](../SessionLogs/2026-10-30-monster-pixel-hp/native-comparison.png).
