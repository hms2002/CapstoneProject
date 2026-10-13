# Encyclopedia control polish — resumed 2026-10-11

Continues the interrupted final request in the 「도감 UI 확장」 chat. Implementation is limited to hiding the creature species-count footer, placing FullView at the illustration's lower-right corner with a 1 UI-unit inset, and deactivating unavailable portrait arrows.

## Changes

- `EncyclopediaUI.prefab`: Count GameObject inactive; FullView RectTransform anchor/pivot `(1,0)`, position `(-1,1)`, existing size `160×42` retained. The inset scales with the existing Canvas.
- `EncyclopediaCreatureDetailView.cs`: each arrow's active state follows its existing interactable condition. Lock/unlock and portrait changes restore only available directions; existing input guards remain.
- `EncyclopediaCreatureUIAuthoring.cs`: targeted `PolishCreatureControls()` updates the persistent asset through Unity APIs and runs after `AlignTextAndPadding()`, preserving the result through Build/Polish.
- `EncyclopediaCreaturePlayModeTests.cs`: checks added before the old chat stopped were retained and executed. They cover the hidden footer, first/last portraits, missing/single portraits, lock/unlock and arrow restoration.

## Verification

- Unity 6000.4.2f1 native authoring/import exit0: [log](authoring-unity.log).
- Editor MSBuild exit0: [console](msbuild-editor-console.log).
- Final PlayMode-project MSBuild exit0: [console](msbuild-playmode-final-console.log). The first attempt encountered another task's newly introduced electric-relic type; Unity refreshed the generated project and the final attempt passed. No generated project was edited manually.
- Unity PlayMode **21/21 passed**, 9.4354539 seconds: [NUnit XML](playmode-results.xml), [log](playmode-unity.log).
- [Serialized/protected-file report](serialized-controls-report.json): 14 checks passed; all 2,583 record IDs/order/classes and hierarchy retained; only Count GameObject and FullView RectTransform changed. Original fonts, sprites, materials, image fit and all six protected file hashes match the baseline.
- [Save restoration](save-restoration-report.json): all ten protected files match the interrupted-task baseline, which was rechecked before this test run. Earlier implementation captures were restored after the new captures were copied here.
- Visually reviewed native 1920×1080 [Boss](actual-boss-1920x1080.png), [NPC](actual-npc-1920x1080.png), and [Monster](actual-monster-1920x1080.png) captures. Footer count is absent, FullView is at the lower-right edge, and the single-portrait NPC has no arrows.

## Limits and documentation

Full hub manual gameplay and Player build were not executed. These isolated native fixtures use the existing base-font capture setup; their CJK glyph warnings do not establish production localization/font quality. No scene, Item presenter, save schema, original art, serialized field name, GUID, assembly or bootstrap changes were made in this follow-up.

SessionLog, matching ActiveTask and existing creature StructureMemory were updated. ErrorLog and DecisionLog were not changed by this follow-up. Doc Impact Check: SessionLog + StructureMemory + ActiveTask; no Presentation HTML stale candidate.
