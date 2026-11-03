# Encyclopedia preview revision 5 — black gradients without nameplates

Scope: approval preview only. Combine the flat pixel frames and cleaned book from [revision 4](../revision-4-clean-book/README.md) with the original-style translucent black fades. No game assets, scripts, scenes, prefabs, metadata or save data were changed.

## Review

- [Monster preview](monster-preview.png): all twelve card nameplates removed; names sit directly over a bottom black fade.
- [Boss preview](boss-preview.png): four card nameplates and the large portrait's title nameplate removed. Restore bottom card fades and top/bottom portrait fades. Ivory illustration indicators remain readable over the dark fade.
- Flat frames, cleaned inner book corners, undiscovered silhouettes, four-arrow full-view icon and maximum five indicators remain.

These are native Aseprite compositions of existing art, not Unity captures. Original character artwork, positions and example discovery states are retained. The previous previews remain available for comparison.

## Files and reproduction

- [make_skin.lua](make_skin.lua): native Aseprite artwork/composition script, now using black alpha overlays instead of nameplate draw calls.
- [render_text.ps1](render_text.ps1): separate Galmuri9 label layers; updates the preview footer.
- [monster-preview.aseprite](monster-preview.aseprite), [boss-preview.aseprite](boss-preview.aseprite): editable composite and label layers.
- [Bottom fade](overlays/BlackFadeBottom.png), [top fade](overlays/BlackFadeTop.png): separate 8×64 black alpha strips with native `.aseprite` sources. Bottom alpha 0→242; top alpha 204→0. The preview computes the same linear fade at each display height.
- [Empty light indicator](overlays/DotEmptyLight.png), [selected light indicator](overlays/DotSelectedLight.png): ivory color variants, unchanged pixel shapes.
- [Base sheet](EncyclopediaPixelSkin.png) and all 22 `assets/` PNGs remain identical to revision 4: six opaque colors, alpha 0/255. Gradients are separate transparency assets, not extra shaded colors in this sheet. The generic Nameplate sprite remains in the pack but is unused by both previews.
- `sources.json`, `sources.lua`, `before-hashes.json`: unchanged reference metadata copied from revision 4.

Run `render_text.ps1 -OutputDir <this-directory> -ProjectDir <project-root>`, then Aseprite `-b --script-param outputDir=<this-directory> --script-param projectDir=<project-root> --script <this-directory>/make_skin.lua`. Create `assets/` and `overlays/` first. No Python image editing is used.

## Verification and limits

Native Aseprite exited 0 with empty stderr. Both 1180×740 compositions were visually inspected. The [native report](aseprite-validation.txt), [gradient checks](gradient-validation.txt), [label bounds](text-validation.txt), and [artifact/hash report](artifact-validation.txt) passed. All 22 base sprite PNGs, the sheet PNG and slice metadata match revision 4; four protected project files and all twenty original sprite references retain their hashes.

Unity import, compilation, Play Mode, navigation and Player build were not executed. Actual game integration and TMP/localization rendering remain pending.

Doc Impact: SessionLog only. No ErrorLog, DecisionLog, StructureMemory, Architecture/Contracts or Presentation HTML update/staleness.
