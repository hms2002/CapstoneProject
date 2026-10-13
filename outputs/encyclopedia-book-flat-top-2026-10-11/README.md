# Encyclopedia book flat top — 2026-10-11

The user chose to flatten the book art rather than move UI content down. The existing book sprite sheet is the only production asset changed.

## Art and preservation

- Production file: `Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book SpriteSheet.png`.
- Existing canvas remains 7344×3600. Slice8 is the open book; stationary upper areas in slices9/10/11/12/14/15/16/17 share the edit.
- The visible upper edge is horizontal at local y181 across x89..727; only the small corner bevel remains. Both upper inner rules are horizontal at y196. Original lower book art from y230 downward is byte-identical in decoded RGBA.
- Moving-page columns in slices10/11/12 are excluded. All other25 slices, existing metadata, sprite IDs/rects/pivots, controller, clips, prefab and UI layout remain unchanged.
- [Art validation](book-art-validation.json): 60/60 checks, 45,649 changed pixels only in authorized upper regions, original29-color sheet palette retained with no new colors, nine protected file hashes match.

## Image workflow

Built-in ImageGen edited the existing [open-book crop](book-open-before.png). The generated [flat-top reference](generated-flat-top-reference.png) was normalized back to the native dimensions and original palette with nearest sampling. Only its upper band was composited; the lower area was restored from the original pixel data. The raw generated reference is not imported into Unity. [Processing parameters](generated-normalization.json), [frame manifest](sheet-edit-manifest.json), and the two Node processing scripts retain the reproducible integration steps.

Prompt: change only the domed upper page edges and their upper inner rules to a flat horizontal top, retain the central crease, original parchment palette/pixel style, transparency, dimensions, side edges and all lower book art; add no text, UI widgets, decorations, antialiasing or gradients. Aseprite was not used.

## Unity validation

- Unity6000.4.2f1 native texture import and [PlayMode21/21](playmode-results.xml) passed in 9.7277318 seconds; [log](playmode-unity.log).
- Existing tests execute actual paused book tab animation, native input/portrait controls, scroll shrink/expand and three tab renders. No C# source changed, so no new MSBuild run was required.
- [Protected saves](save-restoration-report.json): all ten fresh baseline hashes match after cleanup; no test-created save files remain. Earlier renderer captures were restored after copying the new captures here. Cleanup waited for a concurrent electric-relic Unity test run to leave this project.
- Actual1920×1080 [Boss](actual-boss-1920x1080.png), [Monster](actual-monster-1920x1080.png), [NPC](actual-npc-1920x1080.png) captures were visually reviewed. The native upper-band join is unobtrusive at game UI scale.
- Full-hub manual gameplay and Player build were not executed. The existing base-font fixture and its CJK glyph warnings do not verify production localized font quality. Opening/closing perspective frames were preserved; the automated native animation check covers tab changes, not a full frame-by-frame manual animation review.

Doc Impact: SessionLog + existing creature StructureMemory + ActiveTask/TaskIndex. ErrorLog and DecisionLog were not updated. No Architecture/Contracts/Guides or Presentation HTML changes; no HTML stale candidate.
