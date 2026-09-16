---
status: partially-refactored
authority: planning-aid
category: rendering
last_reviewed: 2026-09-16
---

# Shadow Lighting Mask Migration

## Current Problem

ShadowCorridorForLight now renders darkness with Light2D and reveals ShadowMonster through explicit circular sources. Other scenes still use SpriteMask. GlobalVisionMaskController therefore remains a named legacy facade, forwarding through IGlobalVisionPresentation only when its scene explicitly configures a lighting backend.

Some source prefabs retain non-rendering SpriteMask components in the lighting scene because existing gameplay code, particularly Dead'sSkeleton, references their transforms and sprites for radius animation. LightRevealSource consumes the authored transform and sprite bounds rather than rendered mask pixels. Shared prefabs carry dormant opt-in adapters and disabled light children for compatibility with unmigrated scenes.

## Why It Remains

The approved migration targets one test scene. Removing masks or renaming serialized fields globally would alter other corridors, fog recipients, player binding and reward-reveal isolation. Skeleton attack geometry and light-zone damage logic are outside this rendering migration.

## Target Shape

- A scene vision controller with presentation-independent naming after every consumer has migrated.
- Explicit radius-animation data independent of SpriteMask authoring.
- Dedicated authored scene/prefab content once the mask-based scenes can be retired.
- A larger or tiled reveal representation only if profiling/content proves the per-monster 32-overlapping-source budget insufficient.

## Risks And Trigger

Trigger the next slice when expanding real lighting beyond ShadowCorridorForLight or changing skeleton light-expansion authoring. Audit player respawn/scene transitions, fog ownership, candle seals, moving projectiles, source overlap, gauges, hit flashes and reward portal masks. Decide wall occlusion separately: the current circular predicate intentionally does not sample URP shadow textures.

Related map: [PixelLightingSystem](../StructureMemory/PixelLightingSystem.md). No Architecture/Contracts change is authorized by this backlog entry.
