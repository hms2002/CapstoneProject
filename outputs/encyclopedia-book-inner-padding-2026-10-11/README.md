# Encyclopedia book inner padding — 2026-10-11

Aligns headings and illustrations to the flattened book art. The page-level top/side inset is25 UI units, approximately20 pixels in the existing1920×1080 capture setup.

## Changes

- `EncyclopediaUI.prefab`: only five RectTransform records changed. Creature left and Monster headings use550×52 boxes at centerY−51, giving top25 and clearance above the unchanged heading rule. Boss/NPC Illustration uses550×371 at topY−25, retaining bottom−396. Monster preview width matches550. Item left TitleGroup moves down10 units toY322.5; its layout-managed child geometry is untouched.
- `EncyclopediaCreatureUIAuthoring.cs`: added targeted persistent-asset `AlignBookContentInsets()` and applies it after existing text/padding/control authoring, so Build/Polish retains the layout.
- Original book and portrait art, font fields, FitInParent, runtime source, grids, name overlay, affection, reward/story scroll and controls are unchanged. FullView retains its1-unit inset relative to the resized frame; unavailable arrows remain inactive.

## Verification

- Unity6000.4.2f1 native authoring/import exit0: [log](authoring-unity.log).
- Editor MSBuild exit0: [console](msbuild-editor-console.log). The modified editor source is included in the generated Editor.csproj; generated projects were not edited.
- Existing [PlayMode21/21](playmode-results.xml) passed in8.3708577seconds: [log](playmode-unity.log). Covers paused native book tab animation, portrait/arrow controls, rendering and long/short scroll behavior. No new test cases were added for this reversible layout adjustment.
- [Preservation23/23](serialized-preservation-report.json): 2583record IDs/order/classes/hierarchy retained, only five authorized RectTransform records changed and2578other records identical, all2964font/sprite/material/Fit fields unchanged. Ten protected source/art/prefab/test hashes match the baseline.
- Visually reviewed1920×1080 [Boss](actual-boss-1920x1080.png), [Monster](actual-monster-1920x1080.png) and [NPC](actual-npc-1920x1080.png) captures. The title areas and portrait/preview frame edges have consistent page insets without moving the lower content.
- [Save restoration](save-restoration-report.json): all ten protected files match fresh baseline hashes; no test-created files remain. Earlier renderer captures were restored after copying new captures here.

## Limits and memory

The Item title-group inset was verified from native authored geometry; a full Item-tab render/manual gameplay flow was not executed. Full-hub manual play, Player build and production locale-font visual verification remain unexecuted. Existing base-font fixture CJK warnings are not localized-font quality evidence.

SessionLog, existing creature StructureMemory, ActiveTask and TaskIndex updated. ErrorLog and DecisionLog not changed by this task. Doc Impact: SessionLog + StructureMemory + ActiveTask/TaskIndex. No Presentation HTML stale candidate; Architecture/Contracts/Guides/HTML were not edited.
