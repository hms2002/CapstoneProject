---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-06-09
---

# Runtime Service Ownership Architecture

This document defines how runtime services should be classified before refactors change bootstrap, singleton, or `DontDestroyOnLoad` behavior.

The goal is not to remove every singleton at once. The goal is to make lifecycle ownership explicit so future refactors can move one service at a time without changing gameplay behavior.

## Scope Model

| Scope | Lifetime | Typical Owner | Rule |
| --- | --- | --- | --- |
| App | Process/profile lifetime. | Service itself, `SceneDomainCoordinator`, or `[RuntimeServices]`. | May survive title, hub, run, and scene changes. Must be safe to create before title UI wakes. |
| Gameplay Session | Non-title gameplay session. | `SceneDomainCoordinator` gameplay-session scope. | Must not treat `TitleScene` as active gameplay. Must be cleared on title return. |
| Run | Run start to run end. | Run/session flow such as `GamePlayDataManager` and run services. | Must clear volatile state on run end/title return. Durable profile data is not owned here. |
| Scene | Current loaded scene only. | Scene-authored objects. | Must not become `DontDestroyOnLoad` unless explicitly promoted. |
| UI Root | Persistent global UI root. | `GlobalUIRoot` and its `Services` child. | UI presentation should be authored under the root when production-facing. |
| Fallback | Emergency or prototype runtime creation. | The specific fallback service. | Must be documented, validated, and reduced over time. |

## New Or Changed Service Checklist

Before adding or changing a runtime service, answer these:

- Which scope owns it: App, Gameplay Session, Run, Scene, UI Root, or Fallback?
- Who creates it?
- What happens if a scene-authored copy and runtime-created copy both exist?
- Does it survive return to `TitleScene`?
- Does it reset at run end?
- Does cleanup run without creating another runtime service?
- Which validator or play flow catches missing authoring?

If these answers are unclear, do not move lifecycle code in the same slice. Record the uncertainty as a refactor candidate.

## Inventory Format

Use this table shape when adding or updating service ownership entries.

| Service | Scope | Creation Path | Owner | Duplicate Policy | Cleanup Timing | Status | Refactor Trigger |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ServiceName | App / Gameplay Session / Run / Scene / UI Root / Fallback | AutoBootstrap / scene-authored / `[RuntimeServices]` / GlobalUIRoot | Owning service or root | destroy duplicate / adopt / unknown | app quit / title return / run end / scene unload / unknown | stable / compatibility / unknown | When to revisit |

## Current Runtime Ownership Inventory

This inventory records known runtime service patterns. It does not migrate them.

| Service | Scope | Creation Path | Owner | Duplicate Policy | Cleanup Timing | Status | Refactor Trigger |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SceneDomainCoordinator` | App | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | stable | New title/gameplay session boundary. |
| `SceneTransitionCoordinator` | App | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | stable | New transition owner or fade policy. |
| `SceneFadeTransitionService` | App / UI presentation | scene-authored or runtime fallback | Self | existing owner wins while transition active | app quit / title cleanup | compatibility | Fade service replacement or authored fade migration. |
| `SceneTransitionPolicyResolver` | App | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | Transition policy expansion. |
| `RunTransitionResolver` | App / Run policy | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | New run start/end rule. |
| `RunProgressCoordinator` | Run | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | run end / app quit | compatibility | New run progress commit rule. |
| `GameDataManager` | App durable profile | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit, except active-run skip | compatibility | Save owner or slot lifecycle change. |
| `GameDataSaveCoordinator` | App durable profile | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | Save request routing change. |
| `GamePlayDataManager` | Run | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | run end / title return | compatibility | Volatile run state ownership change. |
| `ShortcutProgressService` | App / Run commit support | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | Shortcut save ownership change. |
| `PortalRouteManager` | App / Run route support | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | title cleanup clears route plan | compatibility | Route plan lifecycle change. |
| `GameSettingsService` | App | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | stable | Settings persistence policy change. |
| `InputBindingService` | App | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | stable | Input rebinding storage change. |
| `SoundManager` | App | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | compatibility | Audio catalog or service-root migration. |
| `RunRouteBgmService` | Gameplay Session | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | title cleanup / app quit | compatibility | Route BGM lifecycle change. |
| `CameraBootstrap` | Gameplay Session presentation | `AfterSceneLoad` bootstrap | Self | destroy duplicate | title cleanup / app quit | compatibility | Camera rig ownership change. |
| `CameraShakeService` | App presentation support | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | stable | Shake policy or title-scene behavior change. |
| `GlobalUIRoot` | UI Root | scene-authored prefab, then `DontDestroyOnLoad` | Root instance | existing root wins | title cleanup only when policy says so | stable | Global UI prefab migration. |
| `UIManager` | UI Root | child of `GlobalUIRoot/Services` | `GlobalUIRoot` | unique under root | root cleanup | stable | Popup stack or control-lock ownership change. |
| `RewardDisplayService` | UI Root / App UI | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | Reward UI authoring migration. |
| `WorldInteractionPromptController` | UI Root | `GlobalUIRoot` adoption or persistent root | UI root | existing owner wins | root cleanup | compatibility | Prompt authoring migration. |
| `DamagePopupService` | UI Root / App UI | scene/root-authored or persistent root | UI root | unique service | root cleanup | compatibility | Damage popup pooling/presentation migration. |
| `StatusHudRuntimeBootstrap` | UI Root / Fallback | `AfterSceneLoad` bootstrap | Status HUD services | fallback creation | scene/root cleanup | compatibility | Status HUD prefab completion. |
| `LoadingOverlayController` | App loading presentation | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | stable | Loading overlay authoring change. |
| `PresentationAssetProvider` | App loading/presentation | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | stable | Asset provider replacement. |
| `PresentationPreloadService` | App loading/presentation | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | stable | Preload manifest lifecycle change. |
| `PresentationSpawnService` | App presentation pooling | `[RuntimeInitializeOnLoadMethod]`, `[RuntimeServices]` host | Self / `RuntimeServiceOwnership` | adopt/find existing | app quit | stable | Presentation pool ownership change. |
| `ItemManager` | App data/catalog | `[RuntimeInitializeOnLoadMethod]`, scene database adoption | Self | adopt incoming database if empty | app quit | compatibility | Item catalog/save readiness refactor. |
| `CurrencyManager` | App durable profile plus run pending deltas | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit / run commit | compatibility | Currency commit timing change. |
| `LootManager` | Unknown: Run or Scene reward facade | scene-authored or persistent singleton | Self | destroy duplicate | unknown | unknown | New loot source, loot table expansion, or reward lifecycle change. |
| `RunModifierService` | App / Run modifier derivation | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility facade | New modifier source or non-singleton boundary. |
| `UpgradeManager` | UI Root / App progression facade | `GlobalUIRoot` service adoption | `UpgradeManagerLifetimeService` / `GlobalUIRoot` | claim/release instance | root cleanup | compatibility facade | New effect ownership or planned prefab API migration. |
| `DialogueService` | App / UI dialogue support | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | Dialogue UI root migration or service scope change. |
| `NPCManager` | App data/service | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit | compatibility | NPC catalog/lifecycle change. |
| `AffectionManager` | App durable profile plus run pending deltas | `[RuntimeInitializeOnLoadMethod]` | Self | destroy duplicate | app quit / run commit | compatibility | Affection commit timing change. |
| `WorldItemDetailPresenter` | UI Root / Fallback | `[RuntimeInitializeOnLoadMethod]`, `GlobalUIRoot` adoption | UI root | existing owner wins | root cleanup | compatibility | Merchant/world item detail authoring migration. |
| `TimeScalePauseService` | App support | `[RuntimeInitializeOnLoadMethod]` runner object | Self | static runner | app quit | compatibility | Input-blocking/pause ownership change. |
| `DemoCheatHotkeyController` | Fallback / debug | `[RuntimeInitializeOnLoadMethod]` | Self | static host | app quit | debug | Promotion or removal of demo cheat system. |

## Refactor Rules

- Do not remove singletons or `DontDestroyOnLoad` services globally.
- Do not change service bootstrap and gameplay behavior in the same slice.
- Keep public facade classes when they are scene-facing, prefab-facing, or widely called.
- Move one service to `[RuntimeServices]`, `SceneDomainCoordinator`, `GlobalUIRoot`, or scene authoring only after its scope, duplicate policy, and cleanup timing are known.
- Runtime fallback creation should move toward editor validation or prefab/scene authoring when the feature is production-facing.

## Related Documents

- [Scene Domain Bootstrap Architecture](./SceneDomainBootstrapArchitecture.md)
- [Runtime Save Architecture](./RuntimeSaveArchitecture.md)
- [Presentation Authoring Contract](../Contracts/PresentationAuthoringContract.md)
- [Loading Scopes](./LoadingScopes.md)
- [Scene Runtime Save Structure](../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md)
- [Runtime Presentation Fallback Authoring Split](../RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md)
