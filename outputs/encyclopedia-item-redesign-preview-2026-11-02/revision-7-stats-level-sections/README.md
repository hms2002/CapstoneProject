# Item encyclopedia — separate stat and level sections, revision 7

Mode: Implementation of approval mockups only. All four equal-page E–H designs are revised following the user's preference to separate weapon stats and relic level from other information.

## Changes

- Weapon: name → a labeled **능력치** strip → story → three skill entries → detail shortcut.
- Relic: name → a labeled **레벨** strip containing Q / Lv. 2 / 3 / E → **효과** and the complete effect text.
- Section labels, thin rules and spacing distinguish these areas within one reading surface. Existing content, text-color roles, original book and catalog styles remain.
- Both facing pages stay exactly 460×576 in the 1920×1080 authoring canvas, separated by the original 72-pixel gutter. No new content crosses the gutter.

## Deliverables

Each board shows the weapon book and relic book side by side. Each book has two equal facing pages.

| Option | Paired board | Individual previews | Native Aseprite |
|---|---|---|---|
| E — 고전 장정 | [Board](E-pair.png) | [Weapon](E-weapon.png), [Relic](E-relic.png) | [Weapon](E-weapon.aseprite), [Relic](E-relic.aseprite), [Board](E-pair.aseprite) |
| F — 장서 색인 | [Board](F-pair.png) | [Weapon](F-weapon.png), [Relic](F-relic.png) | [Weapon](F-weapon.aseprite), [Relic](F-relic.aseprite), [Board](F-pair.aseprite) |
| G — 표본 도감 | [Board](G-pair.png) | [Weapon](G-weapon.png), [Relic](G-relic.png) | [Weapon](G-weapon.aseprite), [Relic](G-relic.aseprite), [Board](G-pair.aseprite) |
| H — 제본 기록부 | [Board](H-pair.png) | [Weapon](H-weapon.png), [Relic](H-relic.png) | [Weapon](H-weapon.aseprite), [Relic](H-relic.aseprite), [Board](H-pair.aseprite) |

- [PNG / Aseprite package](encyclopedia-stats-level-sections.zip)
- [Section geometry](section-layout.json), [option definitions](options.json), [complete sample content](content-spec.json)
- [Prior equal-page revision](../revision-6-equal-page-options/README.md)

Individual PNGs are 1260×760; paired boards are 2520×850. Native Aseprite files have separate pixel artwork and typography layers. Text is rasterized from the project's Galmuri9 font, not live editable type.

## Review and limits

All four final paired boards were visually inspected for section separation, overlaps and page balance. Native Aseprite export completed. Existing sample content is preserved, including weapon input/cooldown/variant details and all seven relic effect paragraphs. Catalog order, page counts and acquisition state are illustrative.

No production Assets, Unity scenes/prefabs or settings were edited. No automated tests, Unity play checks, compilation or import were executed. Actual TMP appearance, longer/localized/detailed descriptions and scrolling remain integration work after design approval. Unknown-entry silhouettes and hidden details remain required during integration.

Temporary authoring scripts were removed after export; no one-off executable tool is retained.

Doc Impact: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) + [DecisionLog](../../../Docs/DecisionLog.md) for the continuing section preference. ErrorLog, StructureMemory and Presentation HTML unchanged; no HTML stale candidate.

