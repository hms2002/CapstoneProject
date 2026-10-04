# Full game localization

Mode: Implementation. 2026-10-03.

User approved extending the existing 5-language, key-based translation system to
the whole game, then explicitly approved Unity API edits for used scenes/prefabs
and TMP CJK font assets/references. User also agreed to save and close the open
Unity Editor for batch authoring; inspect process state before launching a batch.

Goal: Korean, English, Japanese, Simplified and Traditional Chinese for active
player-facing UI, item names/descriptions/abilities, relic effects, progression,
NPC/Ink text and localized font support. Preserve gameplay state, numeric tokens,
Ink branches/effects, item IDs and persisted language enum values.

Allowed: runtime display projections, explicit fixed keys/CSV translations,
package tables/assets, editor authoring/validation tools, approved scene/prefab
text bindings and font references, narrow project-memory documents.

Forbidden: changing balance or gameplay behavior, whole-sentence runtime lookup,
new locale state/service, archived unused item migration, developer-log translation,
manual YAML authoring, unrelated scene/prefab cleanup, Architecture/Contracts or
Presentation HTML edits. Preserve all pre-existing user edits.

Success: complete fixed-key coverage inventory, five populated language columns,
translated active Ink runtime JSON with unchanged branch/side-effect semantics,
value-token parity, correctly bound fixed labels and localized TMP fonts, Unity
compile and relevant validation/MSBuild. Report manual/environment checks honestly.

Current routing: `Docs/StructureMemory/LocalizationFlow.md`,
`DataSheets/Localization/README.md`, `Docs/Contracts/PresentationAuthoringContract.md`.

Implementation applied 2026-10-03; screenshot review confirmed functional omissions
in world nameplate/count/sold text. Full localization completion is not established.
Follow-up Implementation authorized: fix localized font Outline/style preservation,
missing weapon variant/override and dynamic stat captions, locale refresh of NPC names,
stats and encyclopedia, and verify layout behavior. Preserve gameplay IDs/numbers,
preview/selection state and authored outer layouts. Approved UI/font asset scope remains.
Screenshot follow-up: investigate untranslated world NPC nameplates and merchant refresh
count separately from dialogue UI. User explicitly authorized Korean-equivalent font
settings/Outline repair. Restore original material presets and CJK default material
settings; do not treat equality against an already-localized material as proof.
User authorized Chinese pixel-font application. Fusion Pixel 10px was evaluated;
one current Simplified Chinese glyph was missing. Apply Fusion Pixel 12px regional
zh-Hans/zh-Hant fonts, retaining authored TMP sizes/material styles. SOLD is intentionally
excluded from translation. Current slice fixes the confirmed dynamic display and tutorial omissions.
- 1,706 fixed keys × five languages; 604 asset fields, 492 fixed UI paths,
  162 Ink text/choice annotations across 16 active sources.
- Approved scene/prefab text and regional TMP font bindings authored through Unity APIs.
- Unity compilation, 8,591 checks and 1,500 dynamic display projections, 492 caption/1,897 font binding readbacks,
  CJK glyph coverage, MSBuild and Addressables content build passed.
- Ink source matches Git baseline after removing only loc annotations.
- Not executed: actual Play Mode layout/overflow/style, preference relaunch,
  rapid language switching, full Player build, actual Steam App ID validation.
- Foreign sentences are drafts pending language review. No gameplay balance/ID/schema changes.
- See `Docs/SessionLogs/2026-10-03.md` for changed systems, recovery and validation.

Confirmed omission slice completed: weapon-exclusive description, 7 relic trigger
fields, 12 relic fallback paths, inventory action hints, NPC/world fallback names,
merchant remaining count, dummy damage, reward rerolls, fire-puddle cause, and
4 tutorial types/8 info pages. Language refresh preserves page/hold state. SOLD
remains English. Actual Play Mode visual acceptance remains outstanding.

2026-10-03 continuation: approved title/profile/marker implementation completed;
user additionally requested a whole game-flow omission investigation. That broader
slice is Investigation/reporting, not automatic implementation of every new finding.
See Docs/StructureMemory/LocalizationCoverage.md: 11 additional raw display paths,
56 non-Ink speech lines, intro/outro 4 slides each. Do not claim full localization done.
Current 1,713 keys/604 fields/468 fixed captions/1,897 font bindings. Title fixed/dynamic
ownership separated; 684 title probes plus prior 1,500 projections/8,626 checks pass.
Manual startup, failure branches, rapid switching, visual layouts and Player checks remain.

User authorized Implementation of the 11 game-flow omissions on 2026-10-03. Add reviewed nested Speech/Intro/Outro/Route keys, repair actual raw output paths, refresh visible projections, and verify all lines/branches without rerolling or changing gameplay/save schemas. Existing approved font/prefab scope applies to used chest presenters. SOLD remains excluded.

2026-10-04 authorized 11-flow implementation complete: +90 keys, 1,803 total/676 asset fields; 56 Speech lines, 8 slides, 8 route names. Added four existing chest font bindings, runtime text refresh and stable death-message identity. Unity compile, 9,015 values/glyph coverage, 468 captions/1,901 fonts, 1,500 dynamic + 684 title + 504 flow projections, 9,076 base checks and MSBuild exit0 passed. Actual full-flow Play Mode, rapid switching/relaunch, foreign-language review and Player/Steam verification remain. See Docs/SessionLogs/2026-10-04.md.

2026-10-04 user authorized Japanese pixel-font replacement and actual Play Mode screen review. Approved Implementation: fresh Japanese Galmuri9 dynamic TMP font/ja table references, preserve Korean authored material/size/hierarchy; capture actual title/profile/settings, hub/detail/encyclopedia and tutorial screens using existing controllers. No new packages/runtime managers/UI objects. Preserve and restore review-touched saves. Full-game acceptance remains separate.

During actual review, repair verified presentation issues within the approved font/UI scope: one-line fitting for skill/stat/slot titles and description hints, parent-bounded stat-row widths, and stale CanvasRenderer atlas override on the timer during localized font replacement. Numeric formatting, timer/gameplay state, hierarchy, save schemas and bootstrap stay unchanged. User additionally asked why the timer is corrupted; retain the diagnosis and before/after screen evidence.
