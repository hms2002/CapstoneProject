# Encyclopedia skin preview — revision 2

Requested changes, limited to the approval artwork:

- Undiscovered bosses use their original portrait alpha as a black silhouette. The two examples are Spiri and the Dark Lord; names remain `???`, and affection information remains hidden.
- The full-illustration button is now a square button with a **16×16 four-corner outward-arrow icon**, displayed at2× in the composition. Its former text label is removed.
- The illustration indicators show at most **five** markers. Melta has seven source portraits, so this preview shows five. Intended integration behavior: show the actual count below five; above five, shift the visible window as the selected portrait moves. No portrait is removed or limited to five.

## Files

- [Revised boss preview](boss-preview.png), [editable composition](boss-preview.aseprite).
- [FullViewExpand.png](assets/FullViewExpand.png): transparent pixel icon.
- [Native skin source](EncyclopediaPixelSkin.aseprite), [skin PNG](EncyclopediaPixelSkin.png), [enlarged sheet](skin-sheet-preview.png), [slice metadata](slices.json):22 components, seven used opaque colors, alpha0/255. The previous21 PNG components remain byte-identical. The generic question-mark components remain available in the pack but are no longer used on the boss page.
- [Monster preview](monster-preview.png): byte-identical to the previous revision.
- [make_skin.lua](make_skin.lua), [render_text.ps1](render_text.ps1), [sources.json](sources.json), [sources.lua](sources.lua): native Aseprite reproduction and original-art references. Reuse the parent folder's source-resolution script if the catalog changes.

The [previous previews and comparisons](../README.md) are retained as revision1; their boss question marks and three markers are superseded by this revision.

## Verification and limits

Native Aseprite exit0, stderr empty. [Native export checks](aseprite-validation.txt) and [artifact/source preservation checks](artifact-validation.txt) passed. The final composition was visually inspected for the two silhouettes, four outward arrows, five centered indicators and removal of the full-view text. The prior21 PNGs, Monster composition, four protected project files and20 original source references are unchanged.

This is an Aseprite approval composition, not a new Unity render. Runtime full-view interaction and five-marker navigation have not been implemented or tested. No Unity import, compilation, Play Mode or Player build was run; no game assets, prefab, scripts, save data or metadata changed.

Doc Impact: SessionLog only; no ErrorLog, DecisionLog, StructureMemory or Presentation HTML change/staleness.
