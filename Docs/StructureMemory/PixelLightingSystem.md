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

`BeatingSpotLight 2D` uses [ScaleWave.cs](../../Assets/_Project/Runtime/Infrastructure/Rendering/Effects/ScaleWave.cs) instead of the former Visual Scripting `Assets/Graphs/Scale Wave.asset` graph. Despite its object name, this test light is a Sprite-type Light2D.

The preserved runtime formula is:

```txt
initialScale = transform.localScale on Awake
localScale = initialScale + initialScale * (Sin(Time.time * 5) * 0.02)
```

The scene component serializes this as `speed: 5` and `amplitude: 0.02`. The component captures its initial local scale from the authored Transform at runtime, so there is no separate serialized base-scale parameter. The replacement removes the per-object `ScriptMachine` and object `Variables` components from `BeatingSpotLight 2D`; the graph asset remains in the project because other prototype assets may still reference Visual Scripting graphs.

## Candlestick Spot Light Pulse

- [Candlestick 1.prefab](../../Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/Candlestick%201.prefab) has `ScaleWave` on its `Spot Light 2D` child, with `speed: 5` and `amplitude: 0.02` (approximately 1.26 seconds per cycle, +/-2%).
- URP Spot lights (`Light2D.LightType.Point`) ignore Transform scale for their light radius. `ScaleWave` captures their inner/outer radii in `Awake` and multiplies both by `Max(0, 1 + Sin(Time.time * speed) * amplitude)` in `Update`. Other objects retain the original Transform-scale path above.
- The pulse owns the Spot radii while enabled and restores their initial values in `OnDisable`. Initial values are not recaptured on re-enable. Light intensity, color, Transform, SpriteMasks and gameplay light-zone colliders are not driven by this branch.
- [Infrastructure.asmdef](../../Assets/_Project/Runtime/Infrastructure/Infrastructure.asmdef) references `Unity.RenderPipelines.Universal.2D.Runtime` for this presentation component; gameplay state ownership is unchanged.
- Tune `Speed` and `Amplitude` on the child component. The prefab's base radii are inner `2` and outer `3`; the default outer pulse range is `2.94` to `3.06`. Do not simultaneously animate these radii with another component.

## ShadowCorridor Ambient Pixel Dust

- Scene: [ShadowCorridorForLight](../../Assets/_Project/Scenes/Tests/ShadowCorridorForLight.unity), root `Ambient Pixel Dust`.
- Reusable prefab: [PF_PixelAmbientDust](../../Assets/_Project/Prefabs/VFX/Particle/PF_PixelAmbientDust.prefab). Its three native Particle Systems represent 1x1, 2x1 and 2x2 dots at 24 PPU. The default prefab emits inside a 12x8 box.
- The scene instance overrides the shape with `ShadowCorridorDustEmission.asset`, a mesh generated from the scene's `Ground` tile cells. The mesh is emission geometry only; it has no renderer or collider.
- [PixelAmbientDustAuthoringTool](../../Assets/_Project/Editor/Tools/Rendering/PixelAmbientDustAuthoringTool.cs) authors the texture, material, prefab and scene placement through Unity APIs. Menu: `Tools/Rendering/Pixel Lighting/Install ShadowCorridor Ambient Dust`. It requires the target scene in Edit Mode and refuses a duplicate installation.
- [PixelAmbientDust.shader](../../Assets/_Project/Art/Shaders/PixelAmbientDust.shader) snaps rendered world XY vertices to the pixel grid and consumes URP 2D shape-light textures plus particle vertex color/alpha. Native simulation positions remain continuous. It does not use Bloom, normals, additive blending or camera sorting-layer textures.
- Density starts at 0.4 live particles per square world unit. Lifetimes are 8-18 seconds, with low-speed XY drift, weak Noise and alpha fade. Loop, Prewarm and Play On Awake are enabled.
- The scene owns the authored instance and native particle lifetime. There is no runtime manager, camera-follow component, object factory or additional gameplay state. Scene unload owns cleanup.
- Renderer sorting is `Projectile`, order `-20`, with `SpriteMaskInteraction.None`. The initial placement was below the `MaskRender` overlay; the September 16 lighting scene now uses actual Light2D illumination without that overlay.
- Tune density with each child system's Emission rate, keeping the 72/20/8 size ratio. Keep Start Size and the material's Pixels Per Unit aligned. A floor-layout change requires reviewing the authored emission mesh; it does not regenerate at runtime.
- This effect does not establish a project-wide camera PPU baseline. The earlier PixelLightTest tool values above remain separate from the dust material's 24 PPU grid.

## ShadowCorridorForLight Scene Vision

- Scope is [ShadowCorridorForLight.unity](../../Assets/_Project/Scenes/Tests/ShadowCorridorForLight.unity). The user's existing Global Light stays at baseline intensity `0.2`; fog targets `0`, entering over `0.3` seconds and recovering over `0.2` seconds with unscaled fade time. No ProjectSettings, sorting layers or other scenes were changed.
- `Global Light 2D` owns an authored `Player Vision Light` child (radius `6.4`, player offset `(0, 0.5)`) plus the existing GlobalVisionMaskController, SceneRestrictedVisionController and RestrictedVisionVisualController. It has no dark overlay or PlayerVisionMask prefab reference.
- [IGlobalVisionPresentation](../../Assets/_Project/Runtime/Features/Monsters/Shadow/ShadowServant/IGlobalVisionPresentation.cs) is the gameplay-facing binding/fog contract. [SceneLightVisionPresentation](../../Assets/_Project/Runtime/Infrastructure/Rendering/Lighting/SceneLightVisionPresentation.cs) implements the actual Light2D output and owns a scene-local list of reveal sources, not a new singleton. GlobalVisionMaskController's explicit presentationOverride forwards to it; an unconfigured controller retains the legacy mask path.
- SceneRestrictedVisionController still owns the restricted-vision status handle. Player changes release the old player's fog request and light binding. Scene disable releases the status. Fog recipients release their requests on disable/destroy; repeated application extends the timer. Multiple request owners compose, and only releasing the final owner starts recovery.
- [LightRevealSource](../../Assets/_Project/Runtime/Infrastructure/Rendering/Lighting/LightRevealSource.cs) is authored on Candlestick, Candlestick 1, StrangeCandlestick, LightBead and Dead'sSkeleton. It opts in only when the same scene contains an enabled SceneLightVisionPresentation. Other scenes leave the original masks and light settings intact.
- Sources reuse existing animated transforms and sprite bounds for circular radius/position, but disable SpriteMask rendering in this scene. This preserves skeleton windup expansion without rewriting its gameplay owner. New Light2D children are authored in prefabs, disabled by default; no presentation GameObjects are created at runtime. The inactive LightBead SpotLight2D/collider branch is not enabled, so its previously inactive damage/light-zone trigger remains unchanged.
- CandlestickSeal remains the seal-state owner. Sources subscribe to SealChanged and gate both light emission and reveal availability. Candlestick 1 uses its existing Spot Light and a fixed reveal radius of `3`; ScaleWave's +/-2% visual pulse does not change gameplay visibility or the light-zone collider. A duplicate scene-level ScaleWave was removed because the prefab already contains it.
- [ShadowMonsterLightVisibility](../../Assets/_Project/Runtime/Infrastructure/Rendering/Lighting/ShadowMonsterLightVisibility.cs) opts in per scene, binds the body's and ground shadow's renderers to [M_ShadowMonsterRevealLit](../../Assets/_Project/Art/Materials/M_ShadowMonsterRevealLit.mat), and temporarily bypasses their SpriteMaskInteraction. It preserves other MaterialPropertyBlock properties, including hit flash, and restores materials/mask settings on disable.
- [ShadowMonsterRevealLit.shader](../../Assets/_Project/Art/Shaders/ShadowMonsterRevealLit.shader) clips pixels outside the union of accepted reveal circles before actual URP lighting and hit flash. Global Light never grants visibility. No valid source means no pixels and no gauge. The existing gauge filter delegates to the adapter, retaining its authored sample offset `(0, -0.5)`.
- Each monster receives up to 32 intersecting areas; body/shadow and gauge use the same selected set. Bounds include both renderers and the gauge sample. Exceeding this overlap budget is reported, rather than allowing the CPU to reveal something the shader cannot draw. Shadows/wall line-of-sight are not part of the reveal predicate in this migration.
- Validation lives in [ShadowLightVisionPlayModeTests](../../Assets/_Project/Tests/PlayMode/Procedural/ShadowLightVisionPlayModeTests.cs). Remaining mask-authoring dependencies and the legacy facade are tracked in [ShadowLightingMaskMigration](../RefactorBacklog/ShadowLightingMaskMigration.md).

## Known Pitfalls

- Do not reorder the four Renderer2D blend-style slots after authored Light2D components exist.
- Do not globally light telegraph/UI layers without reviewing readability; warning overlays often need to stay unlit.
- Do not replace existing URP assets when adding pixel lighting. Extend the current `Assets/Settings/UniversalRP.asset` and `Assets/Settings/Renderer2D.asset` baseline.
- `PixelLightTest` camera setup is scene-instance authoring. If the shared camera prefab changes later, rerun the Pixel Lighting camera baseline tool and verify the scene override still wins.
- Do not remove `GlobalVisionMaskRoot` from restricted-vision gameplay scenes as a lighting cleanup. It is a gameplay/status/mask authoring root, not just a black overlay.
- `BeatingSpotLight 2D` no longer uses Visual Scripting. Tune its pulse through the `ScaleWave` component fields instead of editing `Assets/Graphs/Scale Wave.asset`.

## Promotion Candidate

This is still StructureMemory, not a formal contract. Promote to `Docs/Architecture/` or `Docs/Contracts/` only after the later shader, secondary-texture, shadow, and Bloom passes settle into a stable authoring policy.
