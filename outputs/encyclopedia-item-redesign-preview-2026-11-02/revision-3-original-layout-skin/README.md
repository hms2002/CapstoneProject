# Item encyclopedia — revision 3, original layout and text colors

## Scope

Mode: Implementation of approval artwork only. The user explicitly corrected the scope: preserve the existing layout and text face colors; adjust text outlines/readability and the UI backgrounds/frame design. This supersedes the layout and dark-body-text proposals in revisions 1 and 2.

Allowed: Aseprite/PNG mockups, reference comparisons, artifact notes and SessionLog.
Excluded: Unity implementation, font-family or text-size changes, panel/slot/control movement, new collection labels, rewritten descriptions, gameplay, tests and production assets.

## Deliverables

- [Weapon proposal](weapon-proposal.png) / [Aseprite](weapon-proposal.aseprite)
- [Relic proposal](relic-proposal.png) / [Aseprite](relic-proposal.aseprite)
- [Weapon scrolled detail](weapon-bottom-proposal.png) / [Aseprite](weapon-bottom-proposal.aseprite)
- Original/proposal comparisons, left original and right proposed: [weapon](weapon-comparison.png), [relic](relic-comparison.png), [weapon scrolled](weapon-bottom-comparison.png). Each also has an Aseprite counterpart.
- [Original weapon crop](weapon-original.png), [original relic crop](relic-original.png), [original scrolled weapon crop](weapon-bottom-original.png).

## Preserved

The existing native reference captures supply the actual item selection, four-column collection layout, icons, control positions, text coordinates, wording, line breaks, scroll position and page counts. There are no newly arranged cards or effect infographics.

White body text, blue story text, colored gameplay emphasis, dark-brown names and gray skill/cooldown labels retain their intended face colors. Galmuri9 glyph shapes and captured text positions are reused; no new font or typesetting layout is introduced.

## Skin and readability changes

- Pale parchment #F8D9AF behind the existing detail panels, restrained copper borders and a fine top highlight.
- Collection-slot backgrounds are lightened in place; the selected border has clearer contrast. Original icon artwork and slot ornaments remain.
- Text faces are extracted from the existing capture at their original coordinates. Blurry outline pixels are replaced by a thin opaque pixel outline: dark warm brown for bright body text and a light edge for dark names.
- Gray skill/cooldown text is reconstructed at its existing coordinates using its authored #505050 face color, with no extra outline. This removes the old background blend while preserving the text's intended gray.
- Category title/page-number outlines receive the same thin treatment. Right bookmark and detail-shortcut styling is left as captured.

This is a raster Aseprite approximation of the intended text treatment, not a Unity TMP rendering or a proven TMP parameter mapping. Captured text edges contain prior antialiasing; the proposed outlines and gray coverage are manually authored presentation choices. Actual TMP appearance needs review during a separately approved integration.

## Source and authoring

Existing native references:
- ../../encyclopedia-layout-2026-11-01/native/weapon-ko.png
- ../../encyclopedia-layout-2026-11-01/native/relic-ko.png
- ../../encyclopedia-layout-2026-11-01/native/weapon-detailed-bottom-ko.png

The shared Galmuri9 black-outline material currently has outline alpha 0.59607846 and width 0.165; it was inspected only. This does not establish that every label uses that material. Skill-name/cooldown authored text color in Panel_AbilityBlock_Encyclopedia.prefab is approximately #505050.

Native Aseprite batch composition separates the original capture, panel skin, outline and glyph-face layers. Whole-book crops are 1260×760; comparisons are 2520×760. Temporary authoring Lua is removed after export; no one-off executable tool is retained in the repository.

## Review and limits

The three proposal views were visually reviewed; the small gray labels were refined to avoid missing strokes. Existing wrapping/clipping is intentionally represented by the captured layout. No automated tests, Unity play checks, compilation or imports were executed. No production Assets or ProjectSettings were changed. These are approval mockups, not installed game UI.

Doc Impact: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) only. ErrorLog, DecisionLog and StructureMemory unchanged by this revision; no Presentation HTML impact.

