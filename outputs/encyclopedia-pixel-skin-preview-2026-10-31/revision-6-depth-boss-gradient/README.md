# Encyclopedia preview revision 6 — depth restored, boss-only gradients

Scope: Micro-fix limited to approval assets/compositions. Restore the earlier beveled UI, retain black translucent gradients only on the Boss page, and align the large boss illustration border with the gradient bounds while removing its corner motifs. Game integration is not part of this revision.

## Review images

- [Boss preview](boss-preview.png): beveled cards, buttons, reward panel, dividers and scrollbar; bottom card fades and top/bottom portrait fades. The large portrait now uses a clean rectangular bevel without corner motifs.
- [Monster preview](monster-preview.png): beveled UI with no black gradients. Names use dark ink directly on the card background; nameplates remain absent.
- The cleaned inner book corners, black undiscovered silhouettes, four-arrow full-view icon and maximum five portrait indicators remain. The Monster illustration's separate small corner motifs remain, since this request targeted the Boss illustration.

## Changed artifacts

- [make_skin.lua](make_skin.lua): restores all 22 original [revision 2](../revision-2/README.md) sprite definitions; adds `BossIllustrationFrame`, removes Monster fade calls, and shares the boss portrait/frame/fade rectangle `(1008,290,450,304)`. The frame overlays fade edges without an exposed paper gap. Source character art and fitting stay unchanged.
- [render_text.ps1](render_text.ps1): dark Monster card labels and updated preview footers; separate Galmuri9 text layers.
- [EncyclopediaPixelSkin.aseprite](EncyclopediaPixelSkin.aseprite), [sheet PNG](EncyclopediaPixelSkin.png), [slice metadata](slices.json), `assets/`: 23 sprites in 256×144, seven used opaque colors and alpha 0/255. [BossIllustrationFrame.png](assets/BossIllustrationFrame.png) is a 48×24 rectangular bevel, transparent inside and without corner motifs. The unused generic Nameplate sprite remains in the pack.
- [Bottom fade](overlays/BlackFadeBottom.png), [top fade](overlays/BlackFadeTop.png), [empty indicator](overlays/DotEmptyLight.png), [selected indicator](overlays/DotSelectedLight.png): unchanged from revision 5. Black gradients are separate alpha assets; ivory hollow/filled indicators remain distinct over the dark portrait bottom.
- [Monster Aseprite source](monster-preview.aseprite), [Boss Aseprite source](boss-preview.aseprite): editable composite and text layers.
- `sources.lua`, `sources.json`, `before-hashes.json`: unchanged source reference/hash metadata.

Previous revisions remain unchanged. Native Aseprite exports these assets and composites; Python is used only for text/metadata and hash checks, not image editing. These previews are not Unity screenshots.

## Reproduce and verify

Create `assets/` and `overlays/`. Run `render_text.ps1 -OutputDir <this-directory> -ProjectDir <project-root>`, then native Aseprite `-b --script-param outputDir=<this-directory> --script-param projectDir=<project-root> --script <this-directory>/make_skin.lua`.

- Native Aseprite exit 0, empty stderr; [native report](aseprite-validation.txt), [gradient checks](gradient-validation.txt), [label checks](text-validation.txt).
- All 22 restored sprite PNGs match revision 2 byte-for-byte. All 23 slices are in bounds, nonoverlapping and match individual PNG sizes; [artifact report](artifact-validation.txt).
- Both 1180×740 previews visually inspected for depth, boss-only fades, aligned clean portrait frame and name readability.
- Four protected project files and all twenty original sprite references retained their hashes. No Assets, prefabs, scenes, runtime scripts or metadata were edited.
- Unity import, compilation, Play Mode, navigation, TMP/localization rendering and Player build were not executed. Actual integration remains pending.

Doc Impact: SessionLog only. ErrorLog, DecisionLog, StructureMemory, Architecture/Contracts and Presentation HTML unchanged; no Presentation HTML stale candidate.
