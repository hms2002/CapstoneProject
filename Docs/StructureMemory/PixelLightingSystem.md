---
status: active
authority: structure-memory
category: rendering
last_reviewed: 2026-06-01
---

# Pixel Lighting System

## Purpose

Track the project's URP 2D pixel-lighting baseline so later work on Light2D, Sprite secondary textures, Sprite-Lit emission shaders, ShadowCaster2D, and Bloom starts from the same rendering assumptions.

## Current Baseline

- Unity version: `6000.4.2f1`.
- URP package: `com.unity.render-pipelines.universal` `17.4.0`.
- Active URP pipeline asset: `Assets/Settings/UniversalRP.asset`.
- Active 2D renderer data: `Assets/Settings/Renderer2D.asset`.
- Graphics Settings uses `Assets/Settings/UniversalRP.asset`.
- Quality Settings explicitly uses the same URP asset for all current quality levels.
- Transparency sorting is custom axis `{ x: 0, y: 1, z: 0 }`, matching top-down Y sorting.

## Renderer2D Light Blend Styles

`Assets/Settings/Renderer2D.asset` owns the current 4-slot blend-style contract:

- Slot 0: `Multiply`
- Slot 1: `Additive`
- Slot 2: `Multiply with Mask`
- Slot 3: `Additive with Mask`

Mask slots use the mask texture red channel. Keep slot order stable because existing `Light2D` components serialize their blend-style index.

## Sorting Layer Read

Current sorting layers include:

- `Background`
- `Wall`
- `GroundAOE`
- `AttackTelegraph`
- `Entity`
- `FloatingAOE`
- `Projectile`
- `MaskRender`
- `ForeGround`
- `UI`

The first lighting pass should target world gameplay layers first: `Background`, `Wall`, `Entity`, `Projectile`, and `ForeGround`. `AttackTelegraph`, `GroundAOE`, `FloatingAOE`, `MaskRender`, and `UI` need explicit policy per effect because many of them are authored as gameplay readability overlays rather than world-lit objects.

## Current Usage Notes

- Build scenes mainly author TilemapRenderer and SpriteRenderer objects directly; most gameplay cameras appear to come from bootstrap/prefab paths rather than scene-local Camera objects.
- Existing serialized `Light2D` use was found on:
  - `Assets/HeoMinSeok/_Project/Prefabs/Gameplay/Player/PF Player.prefab`
  - `Assets/Prefabs/Enemies/Mobs/ShadowCorridor/StrangeCandlestick/LightBead.prefab`
- Camera Sorting Layer Texture is currently off. Leave it off until a shader actually samples `_CameraSortingLayerTexture`, because enabling it adds render cost and is not required for ordinary Light2D, Sprite-Lit materials, ShadowCaster2D, or Bloom.

## PixelLightTest Camera Baseline

`Assets/Scenes/PixelLightTest.unity` uses the existing `Assets/LeeJunMo/Prefab/Camera/Main Camera.prefab` as a scene instance. Apply scene-local camera overrides through the Editor tool at `Tools/Rendering/Pixel Lighting/Apply PixelLightTest Camera Baseline`; do not hand-edit the scene YAML.

The current baseline values are:

- `PixelPerfectCamera` type: URP `UnityEngine.Rendering.Universal.PixelPerfectCamera`.
- Assets PPU: `16`, matching the dominant world/tile pixel-art import baseline.
- Reference Resolution: `1280 x 720`, matching `ProjectSettings/ProjectSettings.asset` and `GameSettingsService` defaults.
- Crop Frame: `None`, so the test scene does not introduce letterbox/pillarbox bars by default.
- Grid Snapping: `UpscaleRenderTexture`.
- Filter Mode: `Point`.
- Camera: Orthographic, HDR enabled, MSAA disabled.
- `UniversalAdditionalCameraData`: Post Processing enabled, HDR Output enabled, Anti-aliasing `None`.

## PixelLightTest Global Light Baseline

`Assets/Scenes/PixelLightTest.unity` uses a scene-local `Global Light 2D` for baseline darkness instead of the ShadowServant restricted-vision mask prefab.

Current values:

- Light type: `Global`.
- Blend Style index: `0` / `Multiply`.
- Color: white.
- Intensity: `0.35`.
- Target Sorting Layers: all currently authored sorting layers.

The former `GlobalVisionMaskRoot` scene instance was removed only from `PixelLightTest`. The source prefab remains at `Assets/Prefabs/LevelGimmikManagement/Witch/GlobalVisionMaskRoot.prefab` for restricted-vision gameplay scenes, where it owns the dark overlay, player vision mask, and restricted-vision status application.

## PixelLightTest Beating Spot Light

`BeatingSpotLight 2D` uses `Assets/Script/Rendering/ScaleWave.cs` instead of the former Visual Scripting `Assets/Graphs/Scale Wave.asset` graph.

The preserved runtime formula is:

```txt
initialScale = transform.localScale on Awake
localScale = initialScale + initialScale * (Sin(Time.time * 5) * 0.02)
```

The scene component serializes this as `speed: 5` and `amplitude: 0.02`. The component captures its initial local scale from the authored Transform at runtime, so there is no separate serialized base-scale parameter. The replacement removes the per-object `ScriptMachine` and object `Variables` components from `BeatingSpotLight 2D`; the graph asset remains in the project because other prototype assets may still reference Visual Scripting graphs.

## Known Pitfalls

- Do not reorder the four Renderer2D blend-style slots after authored Light2D components exist.
- Do not globally light telegraph/UI layers without reviewing readability; warning overlays often need to stay unlit.
- Do not replace existing URP assets when adding pixel lighting. Extend the current `Assets/Settings/UniversalRP.asset` and `Assets/Settings/Renderer2D.asset` baseline.
- `PixelLightTest` camera setup is scene-instance authoring. If the shared camera prefab changes later, rerun the Pixel Lighting camera baseline tool and verify the scene override still wins.
- Do not remove `GlobalVisionMaskRoot` from restricted-vision gameplay scenes as a lighting cleanup. It is a gameplay/status/mask authoring root, not just a black overlay.
- `BeatingSpotLight 2D` no longer uses Visual Scripting. Tune its pulse through the `ScaleWave` component fields instead of editing `Assets/Graphs/Scale Wave.asset`.

## Promotion Candidate

This is still StructureMemory, not a formal contract. Promote to `Docs/Architecture/` or `Docs/Contracts/` only after the later shader, secondary-texture, shadow, and Bloom passes settle into a stable authoring policy.
