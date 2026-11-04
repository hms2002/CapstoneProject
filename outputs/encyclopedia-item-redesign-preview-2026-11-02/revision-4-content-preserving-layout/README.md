# Item encyclopedia — revision 4, preserve information and improve layout

Mode: Implementation of Aseprite approval mockups only.
Authority: the user now explicitly permits layout changes while retaining the required information. This supersedes revision 3's fixed-placement constraint. The existing text-color roles and pale panel direction remain.

Allowed: mockup composition, text wrapping, spacing, panel design, PNG/Aseprite exports and narrow documentation.
Excluded: Unity integration, gameplay/content changes, production asset edits and tests.

## Deliverables

- [Weapon layout](weapon-proposal.png) / [layered Aseprite](weapon-proposal.aseprite)
- [Complex relic layout](relic-proposal.png) / [layered Aseprite](relic-proposal.aseprite)
- [Text/content specification](content-spec.json), [measured text layout](layout.json)
- [Aseprite package](encyclopedia-layout-v4-aseprite.zip)

Both preview PNGs are 1260×760. These are artwork proposals, not current Unity screenshots.

## Layout

### Weapon

Use the left page's free space for selected-item information: four-column collection above, then name, stats and story. The illustrated five current weapon definitions fit in two collection rows. A proposed page can hold up to eight weapon slots; further weapons would use the existing pagination concept.

The right page is reserved for skill descriptions. Each skill is one full-width panel with an icon, gray name, input and cooldown across its header, followed by the white/colored body. Skill1's two variants remain separate. The existing detail-mode shortcut remains at the bottom.

The Lightning Spear example retains:
- Name: 뇌창 갈라론
- Electric stat +5
- Complete story
- Three separate skill entries, their icons and their existing simple-mode descriptions
- Mouse1/Mouse1/Q input hints and 3s/3s/10s cooldowns
- Detail-mode control and collection pagination

Only line breaks are changed in the descriptions. This is a simple-mode view; detailed-mode strings are not illustrated in this revision. Any supplied extra metadata must retain a line below the skill header. Longer/detailed skill bodies can increase panel height and scroll within the right page; the footer remains outside that viewport. The illustrated three entries fit without scrolling, not a claim that every weapon or locale does.

### Relic

Retain a four-column, four-row collection on the left. Place selected relic icon/name, Q/E level preview and a full-width effect body on the right.

The real 쇳덩이의 예비 탄창 example shows level 2/3, all six authored condition/effect paragraphs and the runtime-appended remaining-reloads line. Paragraph spacing and hanging bullet indents separate conditions without a specialized large-number infographic. The body height follows its text; longer cases can scroll within the right page. The illustrated seven paragraphs fit without scrolling.

The collection montage/page counts are illustrative, not the user's saved acquisition state. The selected gun icon is the actual OddIronMagazine icon reference. Production discovery/silhouette behavior is unchanged and must be retained if this proposal is integrated.

## Readability and authoring

Keep Galmuri9 and the existing text-color roles: white body, blue story, gray skill names/cooldowns, brown item names and colored semantic emphasis. Thin opaque warm-dark outlines retain contrast on the pale background. No dark-brown body-text conversion is proposed.

Font sizes in the exported artwork: 36 px category headings, 27 px item/skill headings, 18 px body/metadata, 27 px body line step. The body is rasterized at a crisp pixel-font scale. Native Aseprite layers separate original book, reflowed panels, existing game icons and typography. Galmuri9 text is rasterized using Windows single-bit rendering, then imported as a pixel layer; it is not live editable type in Aseprite. content-spec.json preserves wording and semantic color markup. This is not an exact TMP rendering or TMP material-parameter result.

Sources include the original encyclopedia book capture, existing game sprite PNGs, current Lightning Spear item/ability definitions, LightningSpearSkill1Data tooltip variants, InputBindingDefaultsSO, RD_OddIronMagazine and its runtime tooltip. The original native reference remains under ../../encyclopedia-layout-2026-11-01/native/.

## Review and handoff

Aseprite exports completed; both final PNGs were visually reviewed. Header controls, all illustrated paragraphs, fixed footers and the left-page information fit. Manual line breaks were refined to avoid isolated final words. The measured last skill panel ends at Y=809 in the full source canvas, above the footer at Y=826.

No automated tests, Unity play checks, compilation or imports were performed. No production Assets or ProjectSettings were changed. Temporary PowerShell/Lua authoring files were removed; no disposable executable tool is retained in the repository.

Remaining: user design approval, then separately authorized Unity implementation and real TMP/localization/detail-mode review.

Doc Impact: [SessionLog](../../../Docs/SessionLogs/2026-11-02.md) only. ErrorLog, DecisionLog, StructureMemory and Presentation HTML are unchanged by this preview; no HTML stale candidate.

