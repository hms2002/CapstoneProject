# Item encyclopedia — four equal-page alternatives, revision 6

## Scope

Mode: Implementation of approval artwork only. The user made **two equally sized facing pages mandatory** and requested four additional mockups. This supersedes the revision-5 full-width/asymmetric C/D proposals. Selected-item information, existing text-color roles and the book atmosphere remain.

No Unity implementation is included. Allowed changes are preview artwork, artifact metadata and narrow project memory.

## Fixed page geometry

Every E–H view uses exactly the same authored page rectangles in its 1920×1080 source canvas:

| Region | X | Y | Width | Height |
|---|---:|---:|---:|---:|
| Left page | 464 | 286 | 460 | 576 |
| Right page | 996 | 286 | 460 | 576 |
| Preserved center gutter | 924 | 286 | 72 | 576 |

The new panel, icon and typography layers are clipped to the two page rectangles. No inserted sheet, text, icon or shadow crosses the center gutter. The original outer book and external bookmarks remain. These are artwork coordinates, not a change to Unity RectTransforms.

The corresponding [durable decision](../../../Docs/DecisionLog.md#2026-11-02---encyclopedia-keeps-two-equal-facing-pages) records the user's constraint for later related work.

## Four alternatives

| Option | Catalog layout | Detail treatment |
|---|---|---|
| E — 고전 장정 | Four-column icons; a larger selected-weapon illustration uses spare space. | Warm layered frame, continuous record with header and fine separators. |
| F — 장서 색인 | Two columns of icon/name tiles; long selected relic name uses two lines. | Small margin index marks and one continuous reading area. |
| G — 표본 도감 | Enlarged selected icon above four columns of smaller thumbnails. | Compact header, beveled corners and continuous body. |
| H — 제본 기록부 | Named vertical rows with a narrow bookmark selection marker. | Stitched edges and an icon/name portrait header above the continuous record. |

All required sample information remains on the right page. No separate boxes are introduced for each stat, story paragraph or skill body. List slots remain selection controls.

## Files

Each paired board contains weapon on the left and relic on the right. Each book in that board independently retains equal left/right pages.

- **E:** [paired board](E-pair.png), [weapon](E-weapon.png), [relic](E-relic.png), [weapon Aseprite](E-weapon.aseprite), [relic Aseprite](E-relic.aseprite), [board Aseprite](E-pair.aseprite).
- **F:** [paired board](F-pair.png), [weapon](F-weapon.png), [relic](F-relic.png), [weapon Aseprite](F-weapon.aseprite), [relic Aseprite](F-relic.aseprite), [board Aseprite](F-pair.aseprite).
- **G:** [paired board](G-pair.png), [weapon](G-weapon.png), [relic](G-relic.png), [weapon Aseprite](G-weapon.aseprite), [relic Aseprite](G-relic.aseprite), [board Aseprite](G-pair.aseprite).
- **H:** [paired board](H-pair.png), [weapon](H-weapon.png), [relic](H-relic.png), [weapon Aseprite](H-weapon.aseprite), [relic Aseprite](H-relic.aseprite), [board Aseprite](H-pair.aseprite).
- [PNG and Aseprite package](encyclopedia-equal-pages-four-options.zip).
- [Exact page geometry](page-geometry.json), [option palette/layout choices](options.json), [complete sample content](content-spec.json).

Individual previews are 1260×760. Paired boards are 2520×850; use individual PNGs to inspect small text.

## Preserved information and color

Weapon: name, Electric +5, complete story, three separate simple-mode skill entries, icons, names, Mouse1/Mouse1/Q, 3s/3s/10s, full description text, detail-mode shortcut and pagination.

Relic: actual OddIronMagazine name/icon, level 2/3 and Q/E controls, six authored effect paragraphs and the runtime-appended remaining-reloads line. No specialized number infographic replaces the text.

Galmuri9, white body text, blue story text, gray skill names/cooldowns, brown item names and semantic colors remain. Body typography is reused from the prior verified raster mockup; F's new catalog labels and H's header are rasterized from the same project TTF. Native Aseprite composes the frames, icons, text and boards as separate pixel layers. They are editable pixels, not live font objects.

Catalog order/acquisition state/page counts are illustrative. F/H show eight relic rows/tiles per example page, while E/G show sixteen. No user save or discovery state was read or changed. Unknown-entry silhouettes and information hiding remain required in any later implementation.

## Review and remaining work

All four paired boards were visually reviewed. Source authoring uses identical rectangles and preserves the center gutter by clipping. No automated tests, Unity play checks, compilation or import were performed. No production Assets, ProjectSettings or Unity UI files were changed.

Longer localized/detailed-mode descriptions and optional skill metadata still need the existing scroll behavior when integrated. The preview does not establish final TMP material parameters. Selection/refinement and separately authorized Unity integration remain.

Temporary PowerShell/Lua authoring files were removed after export; no one-off executable tool was retained.

Doc Impact: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) + DecisionLog for the explicit equal-page constraint. ErrorLog, StructureMemory and Presentation HTML are unchanged; no HTML stale candidate.

