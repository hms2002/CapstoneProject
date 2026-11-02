# Spiri treasure storeroom: sparse presentation mockup

- Mode: Implementation, image only.
- Tool: built-in image_gen, two edit passes.
- Input: user-provided 2304x1296 gameplay screenshot.
- Final: [spiri-treasure-vault-2304x1296.png](./spiri-treasure-vault-2304x1296.png).
- Export: generated 1672x941 image exported at 2304x1296 using nearest-neighbor resampling, no crop or padding; exact original 16:9 ratio.
- Scope: sparse small treasure chests, coins/gems and liquor barrels at the visible room perimeter; central combat space kept clear.
- Verification: visual inspection and file-dimension check. This is an AI-edited presentation concept; source pixels/UI are not guaranteed byte-identical. No Unity assets/code changed, no Unity tests run.

## Initial edit prompt

```text
Use case: precise-object-edit.
Asset type: presentation example screenshot for an existing indie 2D top-down pixel-art roguelike game.
Input image 1 is the EDIT TARGET. Edit this exact screenshot conservatively.

Primary request: Make the existing room subtly read as "Tipsy Dragon Spiri / the Demon King's castle treasure storeroom" by adding ONLY a few tiny environmental pixel-art props at the existing room perimeter. It must remain convincing as the same actual playable game screenshot.

MANDATORY CANVAS: Same exact 16:9 aspect ratio and framing as the source, preferably exactly 2304 x 1296 pixels. Preserve all four image edges; no crop, no zoom, no border, no letterboxing, no reframing.

STYLE IS CRITICAL: Match the source's coarse, low-resolution sprite art EXACTLY: visibly large square pixel clusters, hard stepped outlines, limited flat colors, simple 2-3 shade ramps, near-black sprite outlines, nearest-neighbor appearance. New props should look like assets from the exact same game's tileset, with the SAME apparent pixel size as the existing characters, coins and walls. NO fine detailed pixel painting, no smooth contours, no antialiasing, no gradients, no bloom, no painterly or realistic rendering.

Only add approximately FIVE small, widely separated prop groupings hugging the INSIDE edges of the visible room: a couple of modest wood-and-gold treasure chests, low handfuls of gold coins with one or two colored gems, and two small wooden liquor barrels as a very restrained nod to Spiri. One grouping may be a tiny chest and a few spilled coins. Use restrained warm gold, brown wood and a very occasional ruby/emerald accent. Do not fill the black off-map void. Do not obstruct doors.

Scale and placement: Each individual chest or barrel should be only around half the visible player character's body height or smaller, and a coin pile much lower. Think tiny 16-bit game props, NOT giant treasure. Place them at a few spots along the left inner wall and lower inner wall, tucked in the peripheral strips of floor. Separate groupings with large stretches of untouched floor. At least 90% of the existing open playable floor must remain visually untouched and clear, including the entire central combat zone.

STRICT INVARIANTS: Keep the original floor color and simple floor markings, map geometry, wall layout, camera viewpoint, perspective and original brightness. Preserve every player/enemy/boss sprite, all their poses and positions, weapon, projectiles, damage numbers, status effects and health bars. Preserve the HUD exactly: hearts, top-left slots, minimap, timer 14:43, Korean labels, bottom skill icons and currency counts. Do not redraw or reinterpret the characters or UI. Preserve the original Korean text. NO new captions, title, watermark, logo, extra UI, new characters, large dragon, throne, giant mound of gold, carpet, elaborate floor decoration or wall redesign.

The finished image should look almost identical to the original at first glance, with only sparse, carefully scaled treasure-storehouse dressing at the margins.
```

## Targeted refinement prompt

```text
Use case: precise-object-edit.
Two inputs: IMAGE 1 is the ORIGINAL screenshot and authoritative edit target. IMAGE 2 is a proposed sparse treasure-prop placement reference ONLY.
Produce the final edited IMAGE 1, using only the small peripheral treasure props seen in IMAGE 2.

Single targeted correction: eliminate the new smooth shading/vignette and preserve the original screenshot's exact flat colors and coarse pixel rendering. The original floor is flat solid gray (#53565f approximately), the dark off-map void is flat dark purple, not textured or softly shaded. Restore those original flat colors. New props must use simple hard-edged limited palettes and large square pixels matching the source, with simple hard pixel shadows, no soft shadow or gloss gradients.

Keep the sparse modest chest / coin / liquor barrel groupings tucked along the left and bottom inside edges as in IMAGE 2. Leave all central playable floor empty of decoration. Props no taller than half the player body. Keep the source's exact wall geometry, scene crop, player, enemies, attacks, damage numbers, health bars, hearts, inventory, minimap, Korean text, timer and bottom HUD unchanged from IMAGE 1. No new text or characters.

Output canvas MUST have exact 16:9 aspect ratio, at 2304 x 1296 pixels, identical to IMAGE 1. No crop, no scaling of scene contents relative to canvas, no border, no letterbox.
Final result is a game screenshot with extremely restrained treasure storeroom dressing, not concept art. Preserve image 1 pixel art.
```

