---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-15
---

# Loading Presentation Structure

## Purpose

Map loading, presentation runtime, global UI, camera, audio, input binding, settings, speech bubble, and related presentation support scripts.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Global UI | 36 | UIManager, popup stack, global UI root, flow blockers, raycast gates, pause/settings/reward/title panels, cursor and warning services. |
| Loading | 18 | Load manifests, asset providers, preload service, loading overlay/controller/debug/trace runtime. |
| Presentation Runtime | 10 | Cue catalog/service, presentation references, world presentation runtime, spawn/routine services. |
| Combat UI | 10 | Damage popup and monster element gauge UI. |
| Audio | 8 | Audio runtime services, sound playback context, sound cues, music/ambience support. |
| Input Bindings | 7 | Input action IDs, glyph database, binding service, shortcut defaults. |
| Speech Bubble | 6 | Player/boss speech data, controllers, component, theme settings. |
| Camera | 6 | Camera bootstrap, camera presentation director, shake service/hooks/drivers. |
| Settings | 5 | Game settings service, presentation scale/viewport/canvas adapters. |
| UI Utilities | 4 | UI particles, control lock bridge, UI debug probe, sprite hit flash control. |
| Lighting | 1 | Sprite glow pulse presentation helper. |

### Global UI Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Settings / Keybinding / Cursor UI | 7 | Settings panel, key binding rows/panel, cursor service/theme, and cursor domain source. |
| UI Root / Interfaces | 6 | UIManager, GlobalUIRoot/layers, and stackable/view/close handler interfaces. |
| Popup / Pause Stack | 6 | Warning popup, popup stack state, pause menu, and popup raycast gate. |
| Reward UI | 4 | Reward display UI/service, effect slot, and reward canvas raycast gate. |
| Raycast / Input Blocking | 3 | Canvas raycast gate base, dialogue gate, and game-flow input blocker. |
| UI Presentation Effects | 3 | UI slide/fade, chain drop, and button highlight presentation helpers. |
| Tooltip / Prompt UI | 3 | Hover UI controller, tooltip interface, and world prompt coordinator. |
| Currency / Unlock UI | 2 | Currency UI and unlock slot UI. |
| Title Profile UI | 2 | Title profile slot panel/card UI. |

### Loading Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Asset Provider / Addressables | 8 | Asset provider interfaces/operations, addressable provider, registry, debug info, and presentation asset provider/probe. |
| Bootstrap / Scope / Prewarm | 4 | Bootstrap config/resolver, load scope kind, and prewarm trace runtime. |
| Loading Overlay / Debug UI | 3 | Loading overlay view/controller and loading debug view. |
| Load Manifest | 2 | Load manifest and route-set load manifest ScriptableObjects. |
| Loading Runtime Other | 1 | Presentation preload service. |

## Key Files

- `Assets/LeeJunMo/Script/UIStructure/UIManager.cs`
- `Assets/LeeJunMo/Script/SceneManagement/TitleMenuController.cs`
- `Assets/LeeJunMo/Script/Camera/CameraBootstrap.cs`
- `Assets/LeeJunMo/Script/UIStructure/GameFlowInputBlocker.cs`
- `Assets/LeeJunMo/Script/Loading/Runtime/LoadingOverlayController.cs`
- `Assets/LeeJunMo/Script/Loading/Runtime/PresentationPreloadService.cs`

## Ownership And Lifecycle

- `UIManager` remains the central stack UI policy owner.
- `GameFlowInputBlocker` owns temporary flow blocks and should release from completion and disable/destroy cleanup paths.
- Title-local UI, canvas, and camera presentation should follow `Docs/Architecture/SceneDomainBootstrapArchitecture.md`: title scene authoring must not be replaced by gameplay runtime roots or camera rigs.
- Loading assets/providers should not own gameplay state; they prepare presentation/runtime dependencies.
- Camera/audio/settings/speech bubble scripts are presentation support and should not own progression state.

## Boundary Review

| Boundary | Current read |
| --- | --- |
| Global UI policy | `UIManager` correctly owns stack UI policy and external flow blockers. Return-to-title remains a UI entry point, but `TitleReturnService` now owns the run-end and scene-transition execution handoff. |
| Title-local vs global runtime UI | Title menu/panels are scene-local authored UI. Gameplay `GlobalUIRoot`, runtime camera rig, and stack UI services are cleaned or avoided on title through the scene-domain bootstrap policy; title-side persistent UI/camera cleanup now executes through `SceneDomainTitleCleanupScope`, and camera title guards now route through `CameraBootstrapScenePolicy`. |
| Loading / preload | `PresentationPreloadService` follows `LoadingScopes.md`: it reads the active route load window and delegates manifest preload/release to asset providers. |
| Presentation runtime | `WorldPresentationRuntime` and `PresentationSpawnService` execute sound, shake, visual spawn, pooling, and cleanup. They are runtime consumers, not authoring owners. |
| Runtime-created UI / overlay | Loading overlay fallback, cursor canvas, cinematic letterbox, and display letterbox create UI hierarchy at runtime. This is acceptable for prototype/debug/fallback, but production-facing UI should be authored in scenes or prefabs. |
| Camera / audio route support | Camera and route BGM bridge scene, boss, and route context for presentation. This is acceptable as support code, but new progression rules should stay outside these services. |

## Refactor Candidates

- `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md` tracks runtime-created UI and presentation fallbacks that should be promoted to authored scene/prefab objects before build-facing use.
- `Docs/RefactorBacklog/SceneDomainBootstrapBoundarySplit.md` records the resolved title/game bootstrap boundary split for title-local UI, gameplay global UI cleanup, camera title guards, and return-to-title flow.
- `Docs/RefactorBacklog/SceneRunStateBoundarySplit.md` remains related because `UIManager.ReturnToTitleScreen()` connects pause/title UI to run end and scene transition.

## Extension Entry Points

- Add global UI flow policy through UIManager and related stack/blocker interfaces.
- Add loading behavior through manifests, bootstrap config, asset providers, and overlay/controller scripts.
- Add presentation support through dedicated presentation services rather than gameplay owners.

## Known Pitfalls

- Runtime UI object creation can be useful for first-pass feel checks, debug fallback, or emergency fallback, but it should not silently become the build-facing structure.
- Title-local presentation should stay scene-authored; adding runtime fallback UI or camera objects for title needs explicit owner, cleanup, and migration notes.
- Production-facing global UI and presentation overlays should be scene- or prefab-authored where possible, then driven through serialized references or `GlobalUIRoot` layers.
- Runtime-created fallback paths need explicit owner, cleanup, and a migration follow-up before they are treated as final UI.
- Presentation authoring should follow `Docs/Contracts/PresentationAuthoringContract.md`.
- Loading/addressable behavior can be scene and asset-reference sensitive; verify paths and asset references before changing.

## Promotion Candidate

Loading scope policy already has `Docs/Architecture/LoadingScopes.md`, and title/game bootstrap policy has `Docs/Architecture/SceneDomainBootstrapArchitecture.md`. Keep broader presentation topology here until another stable rule needs Architecture/Contract promotion.
