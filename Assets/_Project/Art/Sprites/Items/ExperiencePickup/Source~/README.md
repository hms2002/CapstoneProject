# Experience pickup color cycle

Approved Lumen Orb artwork, integrated on 2026-11-03.

- `ExperiencePickupColorCycle.aseprite`: editable 16×16, two-layer, 40-frame animation; 100 ms per frame, four-second loop.
- `author-color-cycle.lua`: original Aseprite authoring source, including palette interpolation.
- `aseprite-frames.json`: original export metadata for the 640×16 sheet.
- `../ExperiencePickupColorCycle.png`: runtime sheet, identical to the approved revision 6 export.

Unity slices each frame at `(16 * frame + 4, 4, 8, 8)`, with centered pivots and 8 pixels per unit. All 40 frames share the same silhouette. Keep these rectangles and existing sprite IDs when re-exporting. Import uses Point filtering, no mipmaps, no compression, and Clamp wrapping.

`Assets/_Project/Art/Animations/VFX/ExperiencePickup/ExperiencePickupColorCycle.anim` contains 40 sprite keys at 0.0 through 3.9 seconds, sampled at 10 FPS, with a four-second loop. Its controller is assigned to the existing `Assets/_Project/Prefabs/Loot/ExperiencePickup_Square.prefab`. The legacy prefab name/GUID is retained to preserve references. The renderer tint is white; root scale remains 0.18. No transform animation or bobbing is authored.

This `Source~` folder is intentionally excluded from Unity import. The PNG, clip, and controller are the runtime assets. Gameplay homing, EXP awards, and gain-particle playback remain owned by the existing ExperiencePickup2D component.
