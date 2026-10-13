---
status: complete
authority: active-task
mode: Implementation
last_reviewed: 2026-10-11
---

# Encyclopedia Monster / Boss / NPC Tabs

User approved the iterated in-chat layout for in-game implementation. The latest instruction excludes all Weapon, Relic and Consumable work, including their description hint placement.

## Allowed

- Add Monster, Boss and NPC tab presenters and authored encyclopedia prefab content.
- Extend the display catalog additively; preserve existing Item definitions and enum values.
- Use existing game portraits, Monster Idle frames and affection heart sprites.
- Add profile-owned species kill totals and run pending deltas with old saves defaulting to zero.
- Author stable species identities on registered Monster prefabs, including spawned variants.
- Add a fourth main-tab button and routing without changing Item-tab behavior.
- Add focused tests, SessionLog, StructureMemory and the existing deferred-category backlog update.

## Layout / Done Criteria

- Monster: selection, profile cumulative kills, description and an Idle preview that runs while the game is paused.
- Boss/NPC: 2x2 bottom-aligned cropped portraits; lower black-to-transparent overlay with name and actual affection hearts; no list dots.
- Detail: bottom-aligned portrait fitted inside its frame to keep faces visible (2026-10-11 follow-up), upper black-to-transparent name overlay, middle left/right arrows hidden with `SetActive(false)` whenever their direction cannot move, maximum three dots, no internal portrait labels, full-image button anchored 1 UI unit inside the illustration's lower-right corner.
- Dot window: first `● ○ ○`, middle `○ ● ○`, last `○ ○ ●`; no wrap. One-image entries display one centered dot.
- Rewards and story beneath the illustration read existing data only; missing content stays explicitly unregistered.
- Creature cards and preview frames use the sample's brown borders and section rules. Character rewards/story show a vertical scrollbar only on overflow; long reward descriptions expand their authored rows.
- Creature typography uses shared content insets, vertically centered affection/header labels and padded reward rows. The species-count footer is hidden; pagination remains. Monster kill counts have clearance beneath the preview frame.
- Full view displays the current uncropped portrait and closes before the encyclopedia on Escape.
- Existing Item source scripts/layout/data remain unchanged; shared routing changes are necessary integration only.

## Verification / Risks

Unity compile, focused save/dot tests, authored-reference checks, Item preservation comparison and actual UI rendering/input checks. No compile or Play Mode success claim without execution. Preserve existing uncommitted edits. Profile delta commit follows the existing run-end policy; previous unrecorded kills cannot be reconstructed. Registered Monster death events do not include damage attribution, so gameplay deaths are counted irrespective of the final damage source. No new Manager, Singleton, DDOL, package, bootstrap, scene or original art changes.

Architecture/Contracts and Presentation HTML are outside the approved write scope.

## Outcome And Verification

Implemented and automatically verified on 2026-10-11. The authored catalog contains 14 monsters, 4 bosses and 5 NPCs, with 27 existing game portrait references. Dedicated creature pages, actual resting-animation previews, registered affection/reward/story projection, full illustration view and profile kill progress are connected.

- Unity 6000.4.2f1 compile/import and final prefab authoring/polish completed. Final Editor MSBuild exit0 and [final import validation](../../outputs/encyclopedia-implementation-2026-10-11/final-import-validation.log) exit0 with no C# errors/exceptions.
- Focused [EditMode results](../../outputs/encyclopedia-implementation-2026-10-11/editmode-results.xml): 8/8 passed.
- Final [PlayMode results](../../outputs/encyclopedia-implementation-2026-10-11/playmode-final-results.xml): 19/19 passed in 7.895 seconds, including real paused book-Animator Boss → Monster → NPC transitions, hidden Item page roots and restored interaction.
- Three native 1920x1080 [Monster](../../outputs/encyclopedia-implementation-2026-10-11/actual-monster-1920x1080.png), [Boss](../../outputs/encyclopedia-implementation-2026-10-11/actual-boss-1920x1080.png) and [NPC](../../outputs/encyclopedia-implementation-2026-10-11/actual-npc-1920x1080.png) captures were visually checked after scoped name/reward-layout polish.
- [Item preservation](../../outputs/encyclopedia-implementation-2026-10-11/item-preservation-report.json) preserves all three Item presenter source files and GlobalUIRoot byte-for-byte. [Serialized preservation](../../outputs/encyclopedia-implementation-2026-10-11/serialized-preservation-report.json) retains original prefab document IDs/values except nine approved shared integration records and added creature content; fourteen monster prefabs retain original records plus species identity.
- [Localization check](../../outputs/encyclopedia-implementation-2026-10-11/creature-localization-report.txt): 13 new keys/65 localized values and placeholders verified; original CSV bytes remain the prefix.

Limits: full hub manual gameplay and Player build were not executed. Native captures verify the isolated base-font UI projection; the fixture removes localized font events and produced missing-CJK-glyph warnings, so the captures do not verify production CJK font rendering or a complete localization flow. Original runtime font bindings remain authored. Missing NPC story/reward content stays unregistered, and original one-frame Idle clips remain static previews.

The ten protected save files were restored byte-for-byte after testing. All fifteen staged prefab assets remained byte-identical through final Unity import; the earlier scope-aware serialized comparison records the approved integration differences from the task baseline. Exact results and limits are recorded in the [SessionLog](../SessionLogs/2026-10-11.md). [Creature structure](../StructureMemory/EncyclopediaCreatureTabs.md) records ownership and extension points.

The interrupted control-polish follow-up was completed in the resumed chat: species-count label hidden, full view at the illustration's lower-right corner with 1 UI-unit inset, unavailable portrait arrows inactive. Unity authoring/import, Editor and final PlayMode-project MSBuild passed; [PlayMode21/21](../../outputs/encyclopedia-controls-polish-2026-10-11/playmode-results.xml) and three native captures were checked. Only two of 2,583 existing prefab records changed; all six protected files and ten save files retain their baseline hashes. [Evidence](../../outputs/encyclopedia-controls-polish-2026-10-11/README.md).
