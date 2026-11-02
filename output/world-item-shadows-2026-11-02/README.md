# Approved field item shadow application — 2026-11-02

Scope: Implementation, explicitly authorized by the user after reviewing the original asset comparison and spacing tester. This approves the generic field relic/consumable pickup prefab and its presentation support. No scenes, existing sprite import settings, gameplay item schemas, save data or bootstrap behavior were changed.

## Accepted appearance

- Existing `Shadow_0`, GUID `66a4ec10a50936e449f2c1fd782999b2`, sprite fileID `4093480436322176185`; original RGB.
- Width 0.75 and height 0.25 relative to the largest normalized display dimension; renderer alpha 0.34.
- Visible-contour spacing **-4 px at a 32 px preview size**. The user described this as -4%, but the selected tester state was gap=-4 px; the implementation follows the selected appearance as explained before editing.
- Each icon's point-sampled alpha contour is matched to the 24 × 8 preview shadow, excluding transparent padding. The first non-overlapping contact offset is found, then four rows of overlap are applied. Runtime `centerFromTop` includes the 4-row half-height, so `(contact - 4 + 4) / 32 = contact / 32`.

## Changed implementation files

- `Assets/_Project/Prefabs/Items/Pickups/PF_WorldItemPickup2D.prefab`: authored Shadow child, sprite/material/color, explicit source/shadow references, and 74 baked sprite contacts. All original YAML blocks are preserved except the required component/child list additions.
- `Assets/_Project/Runtime/Presentation/Loot/WorldDrops/WorldItemShadowPresenter2D.cs` and new `.meta`: local shadow display owner; no runtime texture reads or hierarchy creation; hides during travel, restores after landing, excludes itself from icon outline/spin ownership.
- `Assets/_Project/Editor/Tools/Validation/WorldItemShadowAuthoring.cs` and new `.meta`: repeatable targeted bake menu and native validation/capture menu. The native validation has not been run.
- `Docs/SessionLogs/2026-11-02.md` and `Docs/StructureMemory/WorldItemShadows.md`: outcome, verification limits and future icon rebake route.

## Executed verification

- `baked-contacts.json`: active definition coverage 75/75 and 74 distinct sprite references. PNG source maximum dimension is 287, below the imported size limits. Separate original PNG and `.meta` files were not modified.
- 148 independent alpha collision assertions: every computed zero-contact position has no overlapping opaque pixels; moving the shadow upward by one row creates overlap. The approved -4 row offset is then serialized.
- Serialized prefab audit: all original YAML blocks preserved, no duplicate fileIDs, every local file reference resolves, and all 74 serialized contacts match the bake report.
- `protected-before.json`: 1,941 pre-existing files under relevant prefabs/scenes/item data/runtime and ProjectSettings retained their SHA-256 hashes.
- New script GUIDs occur in exactly their two new `.meta` files. Namespace and asmdef boundaries remain Presentation -> Gameplay and Editor -> Presentation. No asmdefs or existing GUIDs changed.
- Existing `Presentation.csproj` MSBuild passed, but it has **not refreshed to include the new source file**, so this result does not cover the new code.
- `ShadowRuntimeCheck.rsp` / `ShadowEditorCheck.rsp`: the two new C# files were separately compiled using Roslyn with the generated project's actual Unity/project references; both returned exit code 0. Only ordinary unassigned-serialized-field warnings occurred in the runtime source. This is source compilation, not Unity import or Play Mode verification.
- `git diff --check` passed for the prefab and changed documentation.

## Not executed and remaining check

- The already-open Unity Editor displayed `Importing assets` for over 27 minutes during inspection. Separate batch Unity attempts returned code 1 because this project is open (`unity-stdout.log`). No Editor was closed, restarted, unlocked or forced to save.
- Unity import/compilation of the new source, **native validation/capture**, actual animated drop and pickup Play Mode testing, scene lighting and Player build were not executed. No native capture is claimed.
- After the Editor becomes responsive, allow import/compile, run the validation menu, and confirm an actual relic/consumable drop lands with the shadow, has no shadow outline while highlighted, and disappears normally when collected. Later icon art changes require the bake menu.

`apply-offline.py` records the one-time fallback used while Unity was unavailable. For routine future updates use the Unity bake menu.

Doc Impact Check: SessionLog + StructureMemory. ErrorLog/DecisionLog unchanged. No Architecture/Contracts/Guides or Presentation HTML update/stale candidate.
