# Item encyclopedia preview — revision 2

Preview-only Aseprite artwork, revised at the user's request. Bright backgrounds are retained; weapon details follow the existing layout and relic effects use variable-height text instead of a specialized numeric infographic. No Unity integration is included.

## Files

- [Weapon, initial position](weapon-proposal.png) / [Aseprite](weapon-proposal.aseprite)
- [Weapon, scrolled to the bottom](weapon-scrolled.png) / [Aseprite](weapon-scrolled.aseprite)
- [Relic, initial position](relic-proposal.png) / [Aseprite](relic-proposal.aseprite)
- [Relic, scrolled to the bottom](relic-scrolled.png) / [Aseprite](relic-scrolled.aseprite)
- [Full weapon detail](weapon-content.png) / [Aseprite](weapon-content.aseprite)
- [Full relic detail](relic-content.png) / [Aseprite](relic-content.aseprite)
- [Editable wording specification](content-spec.json), [page labels](page-labels.json), [measured content layout](layout.json)
- [Original source-art inventory](../source-assets.json)

## Revised design

Weapon details retain name → stats → story → separate skill entries. Every skill header includes icon, name, input and cooldown, followed by its description. The two Skill1 variants remain separate entries. This replaces revision 1's merged conditional summary; wording is not shortened just to fit the page.

Relic details retain name → level preview → effect body. The sample is the actual complex relic **쇳덩이의 예비 탄창**, shown at level 2 of 3. All six description paragraphs and the runtime-appended remaining-reloads line are included. Paragraph spacing and a hanging bullet indent keep conditions readable. The body panel grows with the text; the right page scrolls independently. The loss-at-zero condition has a restrained dark-red emphasis.

The full weapon content is 430×938, relic content 430×636. Both use a 430×522 viewport in the book, with the detail shortcut or level hint outside the viewport. Top/bottom snapshots intentionally show scroll clipping; the full-content files preserve all text. Whole-book previews are 1260×760.

Galmuri9 remains the font. Primary text uses 27 px, secondary metadata 18 px. Main ink is opaque #39291F without translucent outlines; pale panels use #F8D9AF. Frame and icon placement is pixel artwork in native Aseprite. Text was rasterized with Windows single-bit rendering from the project's Galmuri9.ttf and imported as separate layers. Text layers contain editable pixels rather than live text objects; JSON preserves their wording/layout.

## Data references

- Assets/_Project/Data/Items/Weapons/Definitions/WD_LightningSpear.asset
- Assets/_Project/Runtime/Features/Items/Weapons/Data/LightningSpearSkill1Data.cs
- Assets/_Project/Data/Abilities/Definitions/AD_LightningSpearSkill1.asset
- Assets/_Project/Data/Abilities/Definitions/AD_LightningSpearSkill2.asset
- Assets/_Project/Runtime/Infrastructure/Input/InputBindingDefaultsSO.cs
- Assets/_Project/Data/Items/Relics/Definitions/RD_OddIronMagazine.asset
- Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_WeaponExclusive.cs
- Additional rush icon: Assets/_Project/Art/Sprites/ThirdParty/WeaponAndSandBack/WeaponSkill/LightningSpear/LightningSkill1MarkRushIcon.png

Discovery/page counts and locked examples remain illustrative; they are not taken from the user's save. The gun icon matches the relic's authored icon reference. Body colors and layout are proposals, not runtime changes.

## Review and handoff

Aseprite batch exports completed. All four book PNGs were visually inspected for spacing, text contrast, control alignment and complete content across the scroll positions. No automated tests, Unity play checks, compilation or import were run. No production Assets or ProjectSettings were modified by this preview revision. No executable authoring tool was added to the repository; the temporary Lua authoring file was removed.

User design approval and eventual game integration remain. Other languages, actual TMP rendering and user-rebound keys still require in-game review if this design is approved.

Doc Impact: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) only. ErrorLog, DecisionLog and StructureMemory unchanged by this revision; no Presentation HTML stale candidate.

