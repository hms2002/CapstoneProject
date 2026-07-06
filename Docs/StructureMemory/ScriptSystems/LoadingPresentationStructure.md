---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-07-05
---

# Loading Presentation Structure

## Purpose

Map loading, presentation runtime, global UI, camera, audio, input binding, settings, speech bubble, and related presentation support scripts.

## Current Structure

| Area | Count | Responsibility |
| --- | ---: | --- |
| Global UI | 36 | UIManager, popup stack, global UI root, flow blockers, raycast gates, pause/settings/reward/title panels, cursor and warning services. |
| Loading | 18 | Load manifests, asset providers, preload service, loading overlay/controller/debug/trace runtime. |
| Presentation Runtime | 11 | Cue catalog/service, presentation references, world presentation runtime, spawn/routine services, and visual-only helper components. |
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
- `Assets/_Project/Editor/Build/Loading/PrewarmTraceRuntime.cs`
- `Assets/LeeJunMo/Script/Presentation/Runtime/TopDownDebrisBounceEmitter2D.cs`
- `Assets/Editor/ExplosionDebrisBouncePrefabBuilder.cs`
- `Assets/Editor/ExplosionDebrisBouncePreviewWindow.cs`
- `Assets/_Project/Editor/Build/Loading/RouteSetLoadManifestBuilderWindow.cs`
- `Assets/_Project/Editor/Build/Loading/PrewarmRecommendationWindow.cs`

## Ownership And Lifecycle

- `UIManager` remains the central stack UI policy owner.
- `GameFlowInputBlocker` owns temporary flow blocks and should release from completion and disable/destroy cleanup paths.
- Title-local UI, canvas, and camera presentation should follow `Docs/Architecture/SceneDomainBootstrapArchitecture.md`: title scene authoring must not be replaced by gameplay runtime roots or camera rigs.
- Loading assets/providers should not own gameplay state; they prepare presentation/runtime dependencies.
- `FirstRunIntro` is a one-time profile loading scope for `TitleScene -> TutorialCorridor -> DarkLord_Tutorial -> first ProtoTypeHub intro`. It is configured on `LoadingBootstrapConfigSO`, retained by `PresentationPreloadService` until the configured tutorial completion id defaults to `hub_intro_after_darklord_seen`, and then released.
- `RouteSetLoadManifestBuilderWindow` should write root-loadable manifest entries only. Dependency-only assets such as sprites, textures, materials, animation clips, animator controllers, and tile assets should be pulled by their owning prefab or ScriptableObject instead of being listed directly.
- `RouteSetLoadManifestBuilderWindow` provides a release loading set button for `Boot -> FirstRunIntro -> all RouteSets -> Addressable Registry`. Addressables content build remains an explicit separate release step.
- Loading cleanup paths must not recreate runtime provider services. `PresentationPreloadService` release-on-destroy uses non-creating provider lookup and clears active manifest references even when the provider is already gone.
- `SceneFadeTransitionService` may create a runtime fallback overlay when a transition begins from a scene without an authored fade service, but title-origin transitions should prefer a scene-root authored `SceneFadeTransitionService` so fade timing is Inspector-tuned. If the loaded scene brings in an authored service during any active transition, replacement is deferred until `EndTransitionSession()` so the same overlay that faded to black can fade the next scene back in. The deferred authored overlay is reset transparent/inactive while pending.
- `PrewarmTraceRuntime` is editor-only trace capture in the Editor assembly. Runtime presentation spawn paths record through Core `PresentationPrewarmTracePlayback`, and the Editor backend writes tester/machine-specific `PrewarmTrace_*.json` files under `Assets/_Project/Data/SceneFlow/LoadingManifests/` while keeping the older legacy `PrewarmTrace.json` source readable for recommendations. Player builds must not create the trace service or write `PrewarmTrace.json` under `Application.persistentDataPath`.
- Camera/audio/settings/speech bubble scripts are presentation support and should not own progression state.
- `TopDownDebrisBounceEmitter2D` is visual-only. It simulates debris ground XY plus virtual height, supports circular or rotated-ellipse ground spread through prefab-facing fields, updates child ParticleSystems, and emits contact puffs on bounce; it must not own damage, hit timing, gameplay tags, or flow blocking.

## Boundary Review

| Boundary | Current read |
| --- | --- |
| Global UI policy | `UIManager` correctly owns stack UI policy and external flow blockers. Return-to-title remains a UI entry point, but `TitleReturnService` now owns the run-end and scene-transition execution handoff. |
| Title-local vs global runtime UI | Title menu/panels are scene-local authored UI. Gameplay `GlobalUIRoot`, runtime camera rig, and stack UI services are cleaned or avoided on title through the scene-domain bootstrap policy; title-side persistent UI/camera cleanup now executes through `SceneDomainTitleCleanupScope`, and camera title guards now route through `CameraBootstrapScenePolicy`. |
| Loading / preload | `PresentationPreloadService` keeps Boot, FirstRunIntro, RunCommon, Current, and Next scopes independently and delegates manifest preload/release to asset providers. FirstRunIntro is profile-progress-gated; RunCommon/Current/Next still come from the active route load window. |
| Presentation runtime | `WorldPresentationRuntime` and `PresentationSpawnService` execute sound, shake, visual spawn, pooling, and cleanup. They are runtime consumers, not authoring owners. `TopDownDebrisBounceEmitter2D` is an authored prefab helper consumed by those spawn paths. |
| Runtime-created UI / overlay | Loading overlay fallback, cursor canvas, cinematic letterbox, status HUD entry/tooltip fallback, and Boss HUD dual/split fallback can create UI hierarchy at runtime and report through `RuntimePresentationFallbackAudit` in editor/development builds. `MouseCursorService` now prefers serialized cursor canvas/image references before fallback creation. `GamePresentationController` intentionally keeps the display letterbox runtime-generated because it follows window/resolution policy. Scene Setup Validator validates the representative `GlobalUIRoot.prefab` and provides an auto-fix path for cursor authoring. |
| Camera / audio route support | Camera and route BGM bridge scene, boss, and route context for presentation. This is acceptable as support code, but new progression rules should stay outside these services. |

## Refactor Candidates

- `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md` tracks runtime-created UI and presentation fallbacks that should be promoted to authored scene/prefab objects before build-facing use. `RuntimePresentationFallbackAudit`, representative `GlobalUIRoot.prefab` validation, and the Scene Setup Validator auto-fix path make the current fallback paths visible during editor/development testing but do not replace final visual review.
- `Docs/RefactorBacklog/SceneDomainBootstrapBoundarySplit.md` records the resolved title/game bootstrap boundary split for title-local UI, gameplay global UI cleanup, camera title guards, and return-to-title flow.
- `Docs/RefactorBacklog/SceneRunStateBoundarySplit.md` remains related because `UIManager.ReturnToTitleScreen()` connects pause/title UI to run end and scene transition.

## Extension Entry Points

- Add global UI flow policy through UIManager and related stack/blocker interfaces.
- Add loading behavior through manifests, bootstrap config, asset providers, and overlay/controller scripts.
- Add presentation support through dedicated presentation services rather than gameplay owners.
- Add visual-only helper prefabs through UnityEditor builders or Inspector authoring, then spawn them through existing presentation hooks.

## Known Pitfalls

- Runtime UI object creation can be useful for first-pass feel checks, debug fallback, or emergency fallback, but it should not silently become the build-facing structure. New runtime hierarchy fallback paths should call `RuntimePresentationFallbackAudit.Record(...)`.
- Run `Tools/Validation/Scene Setup Validator` after global UI/presentation edits to catch missing loading/cursor/status/Boss HUD authored references before play verification. If the representative prefab is missing cursor authoring, run `Auto Fix GlobalUIRoot Prefab` from the same window and review the generated objects.
- Title-local presentation should stay scene-authored; adding runtime fallback UI or camera objects for title needs explicit owner, cleanup, and migration notes.
- Production-facing global UI and presentation overlays should be scene- or prefab-authored where possible, then driven through serialized references or `GlobalUIRoot` layers.
- Runtime-created fallback paths need explicit owner, cleanup, and a migration follow-up before they are treated as final UI.
- Do not destroy or replace the active fade service while `IsTransitionActive` is true. Scene loads can awaken authored `GlobalUIRoot` fade services before the transition owner has called `FadeInAsync()`, so replacement must be deferred until the current fade session ends. Also hide the deferred authored overlay immediately because prefab-authored fade images may start active with alpha 1. For title-origin transitions, keep the title authored fade service as a scene-root object rather than a title-canvas child so it survives the load long enough to complete fade-in.
- Service properties that call `EnsureInstance()` are unsafe in destruction cleanup. Use non-creating lookups when releasing loading manifests during `OnDestroy` or scene teardown.
- Do not place once-per-save tutorial/intro assets in Boot just because they happen before RouteSet creation. Use the FirstRunIntro manifest and regenerate the Addressables registry after changing it.
- If Boot grows unexpectedly, inspect the builder output before accepting the asset. Scene dependency collection is broad by nature, so route/boss/monster/weapon data and dependency-only art assets should normally be filtered or moved to FirstRunIntro/RouteSet scopes.
- The release loading set button does not replace Addressables content build or final count review. Run it before content build, then inspect manifest/registry counts and only then build Addressables content.
- Keep prewarm trace capture editor-only and per-tester. Shared trace writes create source-control conflicts when several testers run sessions, and player builds should not emit persistent trace data.
- Presentation authoring should follow `Docs/Contracts/PresentationAuthoringContract.md`.
- Loading/addressable behavior can be scene and asset-reference sensitive; verify paths and asset references before changing.
- Top-down airborne debris should keep ground contact points explicit. Avoid ParticleSystem gravity/collision as the source of truth when the visual needs to look like it bounced on the map plane. Use ground spread scale/rotation for ellipse-shaped explosion silhouettes instead of faking direction through screen-space height offset.
- Manually driven ParticleSystems that write particles through code need a dedicated Editor preview path; the built-in ParticleSystem preview does not step custom virtual-height simulation.
- VFX prefab builders may auto-create missing scaffold assets, but should not auto-repair existing prefab assets on Play/domain reload because that overwrites Inspector tuning. Keep destructive regeneration behind an explicit menu command.

## Promotion Candidate

Loading scope policy already has `Docs/Architecture/LoadingScopes.md`, and title/game bootstrap policy has `Docs/Architecture/SceneDomainBootstrapArchitecture.md`. The new FirstRunIntro scope should be promoted into `LoadingScopes.md` after Architecture-doc approval because the current source-of-truth scope list still only names Boot, RunCommon, and RouteSet.
