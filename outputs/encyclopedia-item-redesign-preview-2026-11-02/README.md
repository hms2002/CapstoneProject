# Item encyclopedia redesign approval mockups — 2026-11-02

Applied after explicit user approval: weapon revision 11, consumable revision 13 and relic revision 14. [Unity integration record and verification limits](../encyclopedia-item-skin-integration-2026-11-02/README.md). Earlier preview records below describe their historical preview-only scope.

Latest relic preview: [revision 14 — centered level below the icon](revision-14-relic-level-below-icon/README.md). Removed the level heading and centered Q/E plus the level value between the icon and effects. Consumables retain [revision 13's centered icon and effects](revision-13-centered-icons/README.md). Left icon grids, equal pages and gutter remain.

Weapon and shared catalog baseline: [revision 11 — option 28 with an icon-only grid](revision-11-option28-icon-grid/README.md). The user retained option 28's weapon information layout and requested a four-column icon-only catalog on the left, without entry names or numbers. Equal pages, the gutter and distinct stat/level sections remain. Revision-9 options 11–20 are excluded; earlier conflicting layouts are historical proposals, not approval candidates.

Original mode: Implementation of preview artwork in Aseprite. The subsequent integration is documented separately above.

## Deliverables

- [Weapon proposal](weapon-proposal.png), [layered Aseprite source](weapon-proposal.aseprite): named two-column collection cards, a clear selection band, a centered right-page header, grouped conditional skill summaries and a fixed detail shortcut.
- [Relic proposal](relic-proposal.png), [layered Aseprite source](relic-proposal.aseprite): named three-column cards, a prominent effect summary and a separate level-preview area.
- [Typography comparison](typography-comparison.png), [Aseprite source](typography-comparison.aseprite): the same Galmuri9 font with a reference outlined style versus opaque dark text. The outlined sample is a style reconstruction, not a captured Unity TMP render.
- [Before/after comparison](weapon-before-after.png), [Aseprite source](weapon-before-after.aseprite): left is the previous standalone authored UI capture, right is the proposed design. This is a design comparison, not a new runtime verification.
- [Source asset inventory](source-assets.json), [rasterized label specification](labels.json).

## Design choices

- Preserve the original book and existing item/skill artwork. Frames use the current parchment/brown family with restrained pixel bevels; body sections have consistent insets and aligned headings.
- Retain Galmuri9. Titles use 36 px, primary body 27 px, secondary labels 18 px, and primary body line steps of 35–38 px in the exported image. Body text is opaque #39291F without an outline; dark boss panels use opaque #FFF0D4. Selection labels use the same ivory on a dark brown band. Limited muted teal distinguishes skill conditions.
- The existing shared black-outline material has alpha 0.59607846. This supports the user's concern, but it does not establish that every encyclopedia text uses that material. The proposal removes translucent outline styling from body text rather than changing fonts.
- Weapon summaries group the existing no-mark/mark branches under the same input. Full descriptions remain a separate proposed detail state; the sample is not a complete rewrite of game descriptions or binding behavior.
- Discovery counts, page counts and locked silhouettes are illustrative. Unknown entries show no detail. The relic example uses the existing RecoveryPendant effect, 1 → 2 potion recovery. Sample counts are not read from the user's save.
- Named cards trade some list density for recognizability: six weapon cards or nine relic cards per page in this proposal. Pagination and long localized text still need review before integration.

## Authoring and review

Generated and exported through native Aseprite batch scripting. Original book, pixel frame shapes, item icon composition and image comparisons were composed in Aseprite. Galmuri9 glyph layers were rasterized from the existing project TTF with Windows single-bit rendering, then imported as separate Aseprite layers. Aseprite's text layers contain editable pixels, not live font objects; labels.json retains wording, positions, sizes and colors.

Preview images are 1260×760, the typography board 1260×550, and the side-by-side comparison 2520×760. The layered files were saved natively. The temporary authoring script was removed; no one-off executable tool was added to the repository.

All final PNGs were visually inspected. A sprite-sheet sampling issue in the initial staff icon was corrected to use the first 32×32 sprite, and captions were synchronized with the real item definitions. No automated tests, Unity play checks, compilation or imports were executed. No files in Assets or ProjectSettings were edited for this preview.

Doc Impact: [SessionLog](../../Docs/SessionLogs/2026-11-02.md) only. StructureMemory, ErrorLog and DecisionLog are unchanged by this design-only work; no Presentation HTML impact.
