# Item encyclopedia — four integrated alternatives, revision 5

## Scope and intent

Mode: Implementation of Aseprite approval artwork only. The user requested **four additional alternatives**, more UI detail and less fragmented information. Required fields and the preceding text-color roles remain. No Unity integration is included.

All four options put the selected item's name, stats/level, story and effects/skills on one continuous reading surface. Separate ability boxes and individual metadata panels are removed. Grouping uses spacing, fine rules and headers. Each option has both a weapon and a complex relic example, for eight book views total.

## Alternatives

| Option | Design | Navigation and reading layout | Tradeoff |
|---|---|---|---|
| A | 정통 도감 | Four-column catalog left; one complete item record right. Featured weapon art fills the spare catalog space. | Retains the original two-page book rhythm; narrow body wraps sooner. |
| B | 세로 색인 | Icon/name rows left; one continuous record right. Selected row and stitched margin reinforce the ledger style. | Easy name scanning; fewer relics per catalog page. |
| C | 펼침 기록지 | A full-width inserted sheet, item header at top, borderless skill columns or full-width relic text, selection dock below. | Broad overview; the inserted sheet intentionally covers the book's center seam. |
| D | 넓은 본문 | Narrow three-column catalog and a broad continuous record sheet. | Most room for long text; catalog thumbnails are smaller and the sheet crosses the original center seam. |

Frames have layered copper edges, fine highlights, restrained corner joinery, ornamental separator endcaps, selection marks and binding/fold details. These belong to the new inserted UI panels; the original outer book artwork is retained. Reading areas are kept clear of decorative texture.

## Previews and Aseprite files

- **A:** [paired board](A-pair.png), [weapon](A-weapon.png), [relic](A-relic.png); [weapon source](A-weapon.aseprite), [relic source](A-relic.aseprite), [board source](A-pair.aseprite).
- **B:** [paired board](B-pair.png), [weapon](B-weapon.png), [relic](B-relic.png); [weapon source](B-weapon.aseprite), [relic source](B-relic.aseprite), [board source](B-pair.aseprite).
- **C:** [paired board](C-pair.png), [weapon](C-weapon.png), [relic](C-relic.png); [weapon source](C-weapon.aseprite), [relic source](C-relic.aseprite), [board source](C-pair.aseprite).
- **D:** [paired board](D-pair.png), [weapon](D-weapon.png), [relic](D-relic.png); [weapon source](D-weapon.aseprite), [relic source](D-relic.aseprite), [board source](D-pair.aseprite).
- [Complete Aseprite/PNG package](encyclopedia-four-options-aseprite.zip).
- [Content specification](content-spec.json), [option geometry/palettes](options.json), [measured text layout](layout.json), [catalog names](list-names.json).

Individual book PNGs are 1260×760. Paired boards are 2520×850, with weapon on the left and relic on the right at full preview size. The native individual images are preferable when comparing small text.

## Information retained

Weapon example: 뇌창 갈라론; Electric +5; full story; all three existing simple-mode skill descriptions; separate sweep/rush variants; each icon, name, input and cooldown; detail-mode shortcut; category, selection and pagination.

Relic example: 쇳덩이의 예비 탄창 at level 2/3; Q/E preview controls; all six authored effect/condition paragraphs and the appended remaining-reloads line. Long effects stay as continuous paragraphs rather than a specialized number infographic.

Catalog order, page counts and acquisition states are illustrative, not taken from the user's save. Discovery gating and silhouettes have not been changed. Extra skill metadata, detailed-mode descriptions and longer localization still need retained overflow behavior in a later implementation; the illustrated simple-mode data fits these mockups.

## Typography and source

Galmuri9 remains the pixel font. Preserve white body text, blue story text, gray skill names/cooldowns, brown item names and semantic emphasis colors. Thin opaque outlines retain contrast. Main body text is 18 px; layout differences are not achieved by shrinking the body between alternatives.

The original book and item/skill sprites come from the project. Content is reused from the verified Lightning Spear and OddIronMagazine sources described in [revision 4](../revision-4-content-preserving-layout/README.md). Text is rasterized from the project's Galmuri9 TTF with Windows single-bit rendering, then imported as a separate pixel layer. Native Aseprite composes and exports the panels, icons, decoration and boards. Text is not live type in the Aseprite files; JSON preserves the wording and semantic tags.

## Review and limits

Native Aseprite export completed. All four paired boards were visually reviewed; C's detail-shortcut position was adjusted to keep it inside the frame. The illustrated information is retained across all four options. This is artwork review, not Unity/TMP verification.

No production Assets or ProjectSettings were edited. No automated tests, Unity play checks, compilation or imports were run. Temporary PowerShell/Lua authoring files were removed; no one-off executable tool is added to the repository.

Remaining: select an approach, refine its artwork if needed, and separately authorize game integration. Actual TMP rendering, long localized strings, detailed-mode content and input rebinding require in-game review after integration.

Doc Impact: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) only. ErrorLog, DecisionLog, StructureMemory and Presentation HTML are unchanged by this preview; no HTML stale candidate.

