# Encyclopedia preview — clean book corners, revision 4

Removed the printed geometric ornaments at all four inner-border corners of each original book page (eight positions total). Adjacent clean paper and existing border samples reconnect the thin page outlines. The outer gold binding ornaments and the separate illustration-frame marks remain unchanged.

- [Boss preview](boss-preview.png) / [editable Aseprite composition](boss-preview.aseprite).
- [Monster preview](monster-preview.png) / [editable Aseprite composition](monster-preview.aseprite).
- [Reproduction script](make_skin.lua): eight localized patches to the reused book capture before composing the existing flat UI.
- The22 UI sprites, [PNG sheet](EncyclopediaPixelSkin.png) and [slice metadata](slices.json) are byte-identical to [revision3](../revision-3-flat/README.md). The new composition still includes boss silhouettes, the four-arrow full-view icon and maximum-five markers.

Native Aseprite exit0/stderr0; both final pages visually inspected. [Pixel comparison](corner-validation.txt) confirms5,095 changed pixels in each preview, confined to the eight corner patches; every pixel outside those patches matches revision3. [Artifact/source checks](artifact-validation.txt) confirm the unchanged UI pack, four protected project files and20 original sprite references.

This changes the approval compositions only. The production book sprite sheet/animation art and game files are not edited; that work remains part of the later approved integration. No Unity import, compilation, Play Mode or Player build was run.

Doc Impact: SessionLog only. No ErrorLog, DecisionLog, StructureMemory or Presentation HTML changes/staleness.
